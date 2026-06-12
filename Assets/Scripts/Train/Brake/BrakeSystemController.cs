using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class BrakeSystemController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainController train;
    [SerializeField] private TrainSpec trainSpec;
    [SerializeField] private ConsistDefinition consistDefinition;

    private readonly BrakeControlUnit brakeControlUnit = new BrakeControlUnit();
    private readonly AirBrakeUnit airBrakeUnit = new AirBrakeUnit();

    private readonly List<VVVFController> vvvfControllers = new();

    private readonly List<CarBrakeState> carBrakeStates = new List<CarBrakeState>();
    public IReadOnlyList<CarBrakeState> CarBrakeStates => carBrakeStates;
    public ConsistDefinition ConsistDefinition => consistDefinition;

    [Header("Rolling Prevention")]
    [SerializeField] private bool isRollingPreventionActive = false;
    [SerializeField, Min(0f)] private float rollingPreventionEnterSpeedMS = 0.05f;
    [SerializeField, Min(0f)] private float rollingPreventionMinBCPressureKPa = 100f;
    public bool IsRollingPreventionActive => isRollingPreventionActive;

    [Header("Regen Release")]
    [SerializeField] private bool regenRelease = false;
    public bool IsRegenReleased => regenRelease;

    /// <summary>
    /// 役割: BC圧を決める候補を表します。
    /// </summary>
    private struct BCPressureCandidate
    {
        public bool isValid;
        public string sourceLabel;
        public float targetBCPressureKPa;
    }

    /// <summary>
    /// 役割: Awake の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Awake()
    {
        if (trainSpec == null)
        {
            Debug.LogError("TrainSpec is not assigned.", this);
        }

        ResolveVVVFControllers();
        InitializeCarBrakeStates();
    }

    /// <summary>
    /// 役割: OnValidate の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void OnValidate()
    {
        rollingPreventionEnterSpeedMS = Mathf.Max(0f, rollingPreventionEnterSpeedMS);
        rollingPreventionMinBCPressureKPa = Mathf.Max(0f, rollingPreventionMinBCPressureKPa);
        if (train == null)
        {
            train = GetComponent<TrainController>();
        }

        InitializeCarBrakeStates();
    }

    public float CurrentBCPressureKPa { get; private set; } = 0f;
    public float CurrentRegenForceN { get; private set; } = 0f;
    public float CurrentAirForceN { get; private set; } = 0f;
    public float TotalBrakeForceN { get; private set; } = 0f;
    public float CurrentRegenDecelMS2 { get; private set; } = 0f;
    public float CurrentAirDecelMS2 { get; private set; } = 0f;
    public float TotalBrakeDecelMS2 { get; private set; } = 0f;
    public float CurrentConsistMassKg { get; private set; } = 0f;

    /// <summary>
    /// 役割: UpdateBrake の処理を実行します。
    /// </summary>
    /// <param name="brakeNotch">brakeNotch を指定します。</param>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <param name="deltaTime">deltaTime を指定します。</param>
    /// <param name="isEmergency">isEmergency を指定します。</param>
    /// <param name="useTascBrakeStep">TASC の連続ブレーキ段を使う場合は true を指定します。</param>
    /// <param name="tascBrakeStep">TASC の連続ブレーキ段を指定します。</param>
    /// <param name="manualPowerNotch">運転士が入力している手動力行ノッチを指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void UpdateBrake(int brakeNotch, float speedMS, float deltaTime, bool isEmergency, bool useTascBrakeStep = false, int tascBrakeStep = 0, int manualPowerNotch = 0)
    {
        // エディタ上の編成変更や初期化漏れに備え、件数を毎フレーム同期する
        EnsureCarBrakeStateCount();
        ResolveVVVFControllers();

        if (trainSpec == null)
        {
            ResetOutputs();
            return;
        }

        // 制動目標力 F_target = a_target * M_total のために、編成質量を先に確定
        CurrentConsistMassKg = GetTotalConsistMassKg();
        if (CurrentConsistMassKg <= 0f)
        {
            ResetOutputs();
            return;
        }

        bool isEmergencyByNotch = brakeNotch >= trainSpec.GetEmergencyBrakeNotch();
        bool emergencyActive = isEmergency || isEmergencyByNotch;
        bool hasBrakeCommand = brakeNotch > 0 || (useTascBrakeStep && tascBrakeStep > 0);
        float targetTotalBrakeForceN = hasBrakeCommand
            ? GetTargetTotalBrakeForceN(brakeNotch, useTascBrakeStep, tascBrakeStep, CurrentConsistMassKg)
            : 0f;

        // 非常時は「回生OFF + 全車最大BC」の単純分岐
        if (emergencyActive)
        {
            ApplyEmergencyBrake(speedMS, deltaTime);
            RefreshOutputsFromStates(CurrentConsistMassKg);
            return;
        }

        UpdateRollingPreventionState(speedMS, manualPowerNotch);
        ApplyNormalBrake(speedMS, deltaTime, hasBrakeCommand, targetTotalBrakeForceN);
        RefreshOutputsFromStates(CurrentConsistMassKg);

    }

    private void ResolveVVVFControllers()
    {
        if (train == null)
        {
            train = GetComponent<TrainController>();
        }

        if (train == null)
        {
            train = GetComponentInParent<TrainController>();
        }

        vvvfControllers.Clear();
        VVVFController[] controllers = train != null ? train.VVVFControllers : null;
        if (controllers == null || controllers.Length == 0)
        {
            controllers = GetComponentsInChildren<VVVFController>(true);
        }

        if (controllers == null)
        {
            return;
        }

        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
            {
                vvvfControllers.Add(controllers[i]);
            }
        }
    }

    /// <summary>
    /// 役割: ブレーキノッチまたは TASC 連続段から編成全体の目標ブレーキ力を求めます。
    /// </summary>
    /// <param name="brakeNotch">整数ブレーキノッチを指定します。</param>
    /// <param name="useTascBrakeStep">TASC の連続ブレーキ段を使う場合は true を指定します。</param>
    /// <param name="tascBrakeStep">TASC の連続ブレーキ段を指定します。</param>
    /// <param name="massKg">編成質量[kg]を指定します。</param>
    /// <returns>編成全体の目標ブレーキ力[N]を返します。</returns>
    private float GetTargetTotalBrakeForceN(int brakeNotch, bool useTascBrakeStep, int tascBrakeStep, float massKg)
    {
        float targetDecelerationMS2 = useTascBrakeStep
            ? trainSpec.GetTascBrakeStepDeceleration(tascBrakeStep)
            : trainSpec.GetBrakeDeceleration(brakeNotch);

        return Mathf.Max(0f, targetDecelerationMS2) * Mathf.Max(1f, massKg);
    }

    /// <summary>
    /// 役割: ApplyEmergencyBrake の処理を実行します。
    /// </summary>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <param name="deltaTime">deltaTime を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ApplyEmergencyBrake(float speedMS, float deltaTime)
    {
        // 非常時は全車とも回生を使わず、BCを最大へ向けて込める
        for (int i = 0; i < carBrakeStates.Count; i++)
        {
            CarSpec carSpec = GetCarSpec(i);
            CarBrakeState state = carBrakeStates[i];
            if (state == null)
            {
                continue;
            }

            if (carSpec == null)
            {
                state.Reset();
                continue;
            }

            ResetRegenState(state);

            // 車両ごとの最大BC圧を目標に、遅れを通して実圧へ更新
            float targetBCPressureKPa = Mathf.Max(0f, carSpec.bcMaxPressureKPa);
            state.bcPressureKPa = airBrakeUnit.UpdateBCPressureKPa(trainSpec, carSpec, state.bcPressureKPa, targetBCPressureKPa, deltaTime);
            state.airForceN = airBrakeUnit.GetAirBrakeForceN(trainSpec, carSpec, state.bcPressureKPa, speedMS);
        }
    }

    /// <summary>
    /// 役割: 停止中に転動防止を有効化し、手動力行が入ったら解除します。
    /// </summary>
    /// <param name="speedMS">現在速度[m/s]を指定します。</param>
    /// <param name="manualPowerNotch">運転士が入力している手動力行ノッチを指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void UpdateRollingPreventionState(float speedMS, int manualPowerNotch)
    {
        if (manualPowerNotch > 0)
        {
            isRollingPreventionActive = false;
            return;
        }

        if (isRollingPreventionActive)
        {
            return;
        }

        if (Mathf.Abs(speedMS) <= rollingPreventionEnterSpeedMS)
        {
            isRollingPreventionActive = true;
        }
    }

    /// <summary>
    /// 役割: ApplyNormalBrake の処理を実行します。
    /// </summary>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <param name="deltaTime">deltaTime を指定します。</param>
    /// <param name="hasBrakeCommand">hasBrakeCommand を指定します。</param>
    /// <param name="targetTotalBrakeForceN">targetTotalBrakeForceN を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ApplyNormalBrake(float speedMS, float deltaTime, bool hasBrakeCommand, float targetTotalBrakeForceN)
    {
        int carCount = carBrakeStates.Count;

        // 最大空制力を求める
        float[] airCapsN = new float[carCount];
        for (int i = 0; i < carCount; i++)
        {
            CarSpec carSpec = GetCarSpec(i);
            CarBrakeState state = carBrakeStates[i];
            if (state == null)
            {
                continue;
            }

            ResetRegenState(state);
            if (carSpec == null)
            {
                state.Reset();
                continue;
            }

            airCapsN[i] = airBrakeUnit.GetAirBrakeCapForceN(trainSpec, carSpec, speedMS);
        }

        // 最大回生力を求める
        float[] regenCapsN = new float[carCount];

        foreach (VVVFController vvvf in vvvfControllers)
        {
            if (vvvf == null)
            {
                continue;
            }

            if(vvvf.AssignedCarIndex < 0 || vvvf.AssignedCarIndex >= carCount)
            {
                continue;
            }


            CarBrakeState state = carBrakeStates[vvvf.AssignedCarIndex];
            if (state == null)
            {
                continue;
            }
            

            regenCapsN[vvvf.AssignedCarIndex] = 
            regenRelease ? 0 : vvvf.GetRegenCapForceN(speedMS);
        }

        float actualTotalRegenForceN = 0f;

        // 回生目標力
        float[] targetRegenForceN = brakeControlUnit.AllocateEvenlyWithSaturation(regenCapsN, targetTotalBrakeForceN);

        foreach (VVVFController vvvf in vvvfControllers)
        {
            if (vvvf == null)
            {
                continue;
            }

            if(vvvf.AssignedCarIndex < 0 || vvvf.AssignedCarIndex >= carCount)
            {
                continue;
            }

            int carIndex = vvvf.AssignedCarIndex;


            if (targetRegenForceN[carIndex] < 0.01f)
            {
                continue;
            }

            CarBrakeState state = carBrakeStates[carIndex];
            if (state == null)
            {
                continue;
            }

            vvvf.SetTargetTractionForce(-targetRegenForceN[carIndex]);

            float actualRegenForceN = Mathf.Max(0f, -vvvf.TotalMotorTractionForceN);
            state.regenForceN = actualRegenForceN;

            actualTotalRegenForceN += actualRegenForceN;
        }

        targetTotalBrakeForceN = Mathf.Max(0f, targetTotalBrakeForceN - actualTotalRegenForceN);


        // 空制目標力
        float[] targetAirForcesN = brakeControlUnit.AllocateEvenlyWithSaturation(airCapsN, targetTotalBrakeForceN);
        for (int i = 0; i < carCount; i++)
        {
            CarSpec carSpec = GetCarSpec(i);
            CarBrakeState state = carBrakeStates[i];
            if (state == null || carSpec == null)
            {
                continue;
            }

            float targetAirForceN = i < targetAirForcesN.Length ? targetAirForcesN[i] : 0f;
            BCPressureCandidate normalCandidate = BuildNormalBCPressureCandidate(carSpec, targetAirForceN, speedMS, hasBrakeCommand);
            ApplyBCPressureCandidate(carSpec, state, normalCandidate, speedMS, deltaTime);
        }
    }

    private void ResetRegenState(CarBrakeState state)
    {
        if (state == null)
        {
            return;
        }

        state.regenForceN = 0f;
        state.regenBrakeApplicationActive = false;
        state.regenLatchedForCurrentBrake = false;
        state.regenNoiseTime = 0f;
    }

    /// <summary>
    /// 役割: 通常ブレーキ計算からBC圧候補を作ります。
    /// </summary>
    /// <param name="carSpec">対象車両の仕様を指定します。</param>
    /// <param name="targetAirForceN">対象車両に要求する空気ブレーキ力[N]を指定します。</param>
    /// <param name="speedMS">現在速度[m/s]を指定します。</param>
    /// <param name="hasBrakeCommand">通常ブレーキ指令がある場合は true を指定します。</param>
    /// <returns>通常ブレーキ由来のBC圧候補を返します。</returns>
    private BCPressureCandidate BuildNormalBCPressureCandidate(CarSpec carSpec, float targetAirForceN, float speedMS, bool hasBrakeCommand)
    {
        return new BCPressureCandidate
        {
            isValid = true,
            sourceLabel = "Normal",
            targetBCPressureKPa = airBrakeUnit.GetTargetBCPressureKPa(
                trainSpec,
                carSpec,
                targetAirForceN,
                speedMS,
                hasBrakeCommand
            )
        };
    }

    /// <summary>
    /// 役割: 転動防止ブレーキ由来のBC圧候補を作ります。
    /// </summary>
    /// <param name="carSpec">対象車両の仕様を指定します。</param>
    /// <returns>転動防止由来のBC圧候補を返します。</returns>
    private BCPressureCandidate BuildRollingPreventionBCPressureCandidate(CarSpec carSpec)
    {
        if (!isRollingPreventionActive || carSpec == null)
        {
            return new BCPressureCandidate
            {
                isValid = false,
                sourceLabel = "Rolling Prevention",
                targetBCPressureKPa = 0f
            };
        }

        return new BCPressureCandidate
        {
            isValid = true,
            sourceLabel = "Rolling Prevention",
            targetBCPressureKPa = Mathf.Clamp(rollingPreventionMinBCPressureKPa, 0f, carSpec.bcMaxPressureKPa)
        };
    }

    /// <summary>
    /// 役割: 2つのBC圧候補から高いBC圧を要求する候補を選びます。
    /// </summary>
    /// <param name="normalCandidate">通常ブレーキ由来のBC圧候補を指定します。</param>
    /// <param name="rollingPreventionCandidate">転動防止由来のBC圧候補を指定します。</param>
    /// <returns>採用するBC圧候補を返します。</returns>
    private BCPressureCandidate ChooseHigherBCPressureCandidate(
        BCPressureCandidate normalCandidate,
        BCPressureCandidate rollingPreventionCandidate
    )
    {
        if (!normalCandidate.isValid)
        {
            return rollingPreventionCandidate;
        }

        if (!rollingPreventionCandidate.isValid)
        {
            return normalCandidate;
        }

        return rollingPreventionCandidate.targetBCPressureKPa > normalCandidate.targetBCPressureKPa
            ? rollingPreventionCandidate
            : normalCandidate;
    }

    /// <summary>
    /// 役割: BC圧候補を選択し、実BC圧と空気ブレーキ力を更新します。
    /// </summary>
    /// <param name="carSpec">対象車両の仕様を指定します。</param>
    /// <param name="state">対象車両のブレーキ状態を指定します。</param>
    /// <param name="normalCandidate">通常ブレーキ由来のBC圧候補を指定します。</param>
    /// <param name="speedMS">現在速度[m/s]を指定します。</param>
    /// <param name="deltaTime">前フレームからの経過時間[秒]を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ApplyBCPressureCandidate(
        CarSpec carSpec,
        CarBrakeState state,
        BCPressureCandidate normalCandidate,
        float speedMS,
        float deltaTime
    )
    {
        BCPressureCandidate rollingCandidate = BuildRollingPreventionBCPressureCandidate(carSpec);
        BCPressureCandidate selectedCandidate = ChooseHigherBCPressureCandidate(normalCandidate, rollingCandidate);
        float targetBCPressureKPa = selectedCandidate.isValid ? selectedCandidate.targetBCPressureKPa : 0f;

        state.bcPressureKPa = airBrakeUnit.UpdateBCPressureKPa(
            trainSpec,
            carSpec,
            state.bcPressureKPa,
            targetBCPressureKPa,
            deltaTime
        );
        state.airForceN = airBrakeUnit.GetAirBrakeForceN(trainSpec, carSpec, state.bcPressureKPa, speedMS);
    }

    /// <summary>
    /// 役割: RefreshOutputsFromStates の処理を実行します。
    /// </summary>
    /// <param name="massKg">massKg を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void RefreshOutputsFromStates(float massKg)
    {
        // 各車の実状態を集約して、外部公開用の値へ反映する
        float totalRegenN = 0f;
        float totalAirN = 0f;
        float maxBCKPa = 0f;

        for (int i = 0; i < carBrakeStates.Count; i++)
        {
            CarBrakeState state = carBrakeStates[i];
            if (state == null)
            {
                continue;
            }

            totalRegenN += Mathf.Max(0f, state.regenForceN);
            totalAirN += Mathf.Max(0f, state.airForceN);
            float bc = Mathf.Max(0f, state.bcPressureKPa);
            if (bc > maxBCKPa)
            {
                maxBCKPa = bc;
            }
        }

        float safeMassKg = Mathf.Max(1f, massKg);
        CurrentRegenForceN = totalRegenN;
        CurrentAirForceN = totalAirN;
        TotalBrakeForceN = CurrentRegenForceN + CurrentAirForceN;
        CurrentRegenDecelMS2 = CurrentRegenForceN / safeMassKg;
        CurrentAirDecelMS2 = CurrentAirForceN / safeMassKg;
        TotalBrakeDecelMS2 = TotalBrakeForceN / safeMassKg;
        CurrentBCPressureKPa = maxBCKPa;
    }

    /// <summary>
    /// 役割: GetTotalConsistMassKg の処理を実行します。
    /// </summary>
    /// <returns>処理結果を返します。</returns>
    private float GetTotalConsistMassKg()
    {
        float fallbackMassKg = trainSpec != null ? trainSpec.massKg : 1f;
        if (consistDefinition == null)
        {
            return Mathf.Max(1f, fallbackMassKg);
        }

        return consistDefinition.GetTotalMassKgOrFallback(fallbackMassKg);
    }

    /// <summary>
    /// 役割: GetCarSpec の処理を実行します。
    /// </summary>
    /// <param name="index">index を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private CarSpec GetCarSpec(int index)
    {
        return consistDefinition != null ? consistDefinition.GetCar(index) : null;
    }

    /// <summary>
    /// 役割: InitializeCarBrakeStates の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void InitializeCarBrakeStates()
    {
        // 編成長に合わせて、車両ごとの実行時状態を初期生成
        carBrakeStates.Clear();
        int target = consistDefinition != null ? consistDefinition.CarCount : 0;
        for (int i = 0; i < target; i++)
        {
            carBrakeStates.Add(CreateBrakeState());
        }

        ResetNullCarStates();
    }

    /// <summary>
    /// 役割: EnsureCarBrakeStateCount の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void EnsureCarBrakeStateCount()
    {
        // 編成変更に追従して、状態リストの不足/過剰を調整
        int target = consistDefinition != null ? consistDefinition.CarCount : 0;

        while (carBrakeStates.Count < target)
        {
            carBrakeStates.Add(CreateBrakeState());
        }

        while (carBrakeStates.Count > target)
        {
            carBrakeStates.RemoveAt(carBrakeStates.Count - 1);
        }

        ResetNullCarStates();
    }

    /// <summary>
    /// 役割: ResetNullCarStates の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ResetNullCarStates()
    {
        // 編成内にnull車両があっても落ちないように、そのスロットだけ初期化して無効化
        if (consistDefinition == null || !consistDefinition.HasCars)
        {
            return;
        }

        int count = Mathf.Min(carBrakeStates.Count, consistDefinition.CarCount);
        for (int i = 0; i < count; i++)
        {
            if (GetCarSpec(i) == null)
            {
                carBrakeStates[i].Reset();
            }
        }
    }

    /// <summary>
    /// 役割: CreateBrakeState の処理を実行します。
    /// </summary>
    /// <returns>処理結果を返します。</returns>
    private CarBrakeState CreateBrakeState()
    {
        return new CarBrakeState();
    }

    /// <summary>
    /// 役割: ResetOutputs の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ResetOutputs()
    {
        // 外部公開値と内部状態を全クリア
        for (int i = 0; i < carBrakeStates.Count; i++)
        {
            CarBrakeState state = carBrakeStates[i];
            if (state == null)
            {
                continue;
            }
            state.Reset();
        }

        CurrentBCPressureKPa = 0f;
        CurrentRegenForceN = 0f;
        CurrentAirForceN = 0f;
        TotalBrakeForceN = 0f;
        CurrentRegenDecelMS2 = 0f;
        CurrentAirDecelMS2 = 0f;
        TotalBrakeDecelMS2 = 0f;
        CurrentConsistMassKg = 0f;
        isRollingPreventionActive = false;
    }
}
