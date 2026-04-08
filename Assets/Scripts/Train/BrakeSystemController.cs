using System.Collections.Generic;
using UnityEngine;

public class BrakeSystemController : MonoBehaviour
{
    [SerializeField] private TrainSpec trainSpec;
    [SerializeField] private ConsistDefinition consistDefinition;
    [SerializeField, Min(0f)] private float mtAirDistributionThresholdKmH = 120f; // この速度以下で「M回生 -> MT空気」に切替
    private readonly BrakeControlUnit brakeControlUnit = new BrakeControlUnit();
    private readonly RegenBrakeUnit regenBrakeUnit = new RegenBrakeUnit();
    private readonly AirBrakeUnit airBrakeUnit = new AirBrakeUnit();

    private readonly List<CarBrakeState> carBrakeStates = new List<CarBrakeState>();
    public IReadOnlyList<CarBrakeState> CarBrakeStates => carBrakeStates;
    public ConsistDefinition ConsistDefinition => consistDefinition;

    private void Awake()
    {
        if (trainSpec == null)
        {
            Debug.LogError("TrainSpec is not assigned.", this);
        }

        InitializeCarBrakeStates();
    }

    private void OnValidate()
    {
        InitializeCarBrakeStates();
    }

    public float currentBCPressureKPa { get; private set; } = 0f;
    public float currentRegenForceN { get; private set; } = 0f;
    public float currentAirForceN { get; private set; } = 0f;
    public float totalBrakeForceN { get; private set; } = 0f;
    public float currentRegenDecel { get; private set; } = 0f;
    public float currentAirDecel { get; private set; } = 0f;
    public float totalBrakeDecel { get; private set; } = 0f;
    public float CurrentConsistMassKg { get; private set; } = 0f;

    public void UpdateBrake(int brakeNotch, float speedMS, float deltaTime, bool isEmergency)
    {
        // エディタ上の編成変更や初期化漏れに備え、件数を毎フレーム同期する
        EnsureCarBrakeStateCount();

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
        bool hasBrakeCommand = brakeNotch > 0;
        float targetTotalBrakeForceN = hasBrakeCommand
            ? brakeControlUnit.GetTargetTotalBrakeForceN(trainSpec, brakeNotch, CurrentConsistMassKg)
            : 0f;

        // 非常時は「回生OFF + 全車最大BC」の単純分岐
        if (emergencyActive)
        {
            ApplyEmergencyBrake(speedMS, deltaTime);
            RefreshOutputsFromStates(CurrentConsistMassKg);
            return;
        }

        ApplyNormalBrake(speedMS, deltaTime, hasBrakeCommand, targetTotalBrakeForceN);
        RefreshOutputsFromStates(CurrentConsistMassKg);
    }

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

            regenBrakeUnit.ResetCarState(state);

            // 車両ごとの最大BC圧を目標に、遅れを通して実圧へ更新
            float targetBCPressureKPa = Mathf.Max(0f, carSpec.bcMaxPressureKPa);
            state.bcPressureKPa = airBrakeUnit.UpdateBCPressureKPa(trainSpec, carSpec, state.bcPressureKPa, targetBCPressureKPa, deltaTime);
            state.airForceN = airBrakeUnit.GetAirBrakeForceN(trainSpec, carSpec, state.bcPressureKPa, speedMS);
        }
    }

    private void ApplyNormalBrake(float speedMS, float deltaTime, bool hasBrakeCommand, float targetTotalBrakeForceN)
    {
        // ------------------------------------------------------------
        // 通常ブレーキ時の配分フロー
        // 1) M車回生を先に使う
        // 2) 残差をT車空気に配る
        // 3) さらに残差があればM車空気に配る
        // ------------------------------------------------------------

        // 編成車両数（carBrakeStatesとconsistDefinitionは同じ件数に同期済み）
        int carCount = carBrakeStates.Count;

        // ===== 1) M車回生の上限（cap）を車両ごとに作る =====
        // capは「その車両がこのフレームで理論上出せる最大回生力[N]」
        float[] regenCapsN = new float[carCount];
        for (int i = 0; i < carCount; i++)
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

            // 回生ノイズシードなど、車両ごとの回生状態を初期化
            regenBrakeUnit.InitializeCarState(state);

            // ブレーキ指令なし、またはT車なら回生無効
            bool disableRegen = !hasBrakeCommand || carSpec.carType != CarType.Motor;

            // 「今回のブレーキ操作で回生を使えるか」をラッチ更新
            regenBrakeUnit.UpdateBrakeApplicationState(trainSpec, carSpec, state, hasBrakeCommand, speedMS, disableRegen);

            // 現在速度・車両特性を踏まえた回生上限[N]
            regenCapsN[i] = regenBrakeUnit.GetRegenCapForceN(trainSpec, carSpec, state, speedMS);
        }

        // 目標制動力を、M回生capの範囲でなるべく均等配分（各車の回生目標力[N]）
        float[] regenTargetForcesN = brakeControlUnit.AllocateEvenlyWithSaturation(regenCapsN, targetTotalBrakeForceN);

        // ===== 1-b) 回生目標を「実回生力」に反映（立上/立下や揺らぎを通す） =====
        float totalRegenActualN = 0f;
        for (int i = 0; i < carCount; i++)
        {
            CarSpec carSpec = GetCarSpec(i);
            CarBrakeState state = carBrakeStates[i];
            if (state == null || carSpec == null)
            {
                continue;
            }

            // M車以外、またはブレーキ非指令時は回生を落とす
            bool disableRegen = !hasBrakeCommand || carSpec.carType != CarType.Motor;
            float targetRegenForceN = i < regenTargetForcesN.Length ? regenTargetForcesN[i] : 0f;

            // 回生の遅れ・揺らぎ・低速失効を含んだ実回生力[N]
            float actualRegenForceN = regenBrakeUnit.UpdateRegenForceN(
                trainSpec,
                carSpec,
                state,
                targetRegenForceN,
                speedMS,
                deltaTime,
                disableRegen
            );
            state.regenForceN = actualRegenForceN;
            totalRegenActualN += actualRegenForceN;
        }

        // 回生で賄えなかった不足分（次段の空気ブレーキに回す）
        float remainAfterRegenN = Mathf.Max(0f, targetTotalBrakeForceN - totalRegenActualN);

        // 低速域では、T優先ではなく「M回生 -> MT空気（全車）」に切り替える
        float thresholdMS = Mathf.Max(0f, mtAirDistributionThresholdKmH / 3.6f);
        bool useMTAirAllocation = speedMS <= thresholdMS;
        if (useMTAirAllocation)
        {
            float[] allAirCapsN = new float[carCount];
            for (int i = 0; i < carCount; i++)
            {
                CarSpec carSpec = GetCarSpec(i);
                if (carSpec == null)
                {
                    allAirCapsN[i] = 0f;
                    continue;
                }

                allAirCapsN[i] = airBrakeUnit.GetAirBrakeCapForceN(trainSpec, carSpec, speedMS);
            }

            float[] allTargetAirForcesN = brakeControlUnit.AllocateEvenlyWithSaturation(allAirCapsN, remainAfterRegenN);
            for (int i = 0; i < carCount; i++)
            {
                CarSpec carSpec = GetCarSpec(i);
                CarBrakeState state = carBrakeStates[i];
                if (state == null || carSpec == null)
                {
                    continue;
                }

                float targetAirForceN = i < allTargetAirForcesN.Length ? allTargetAirForcesN[i] : 0f;
                float targetBCPressureKPa = airBrakeUnit.GetTargetBCPressureKPa(trainSpec, carSpec, targetAirForceN, speedMS, hasBrakeCommand);
                state.bcPressureKPa = airBrakeUnit.UpdateBCPressureKPa(trainSpec, carSpec, state.bcPressureKPa, targetBCPressureKPa, deltaTime);
                state.airForceN = airBrakeUnit.GetAirBrakeForceN(trainSpec, carSpec, state.bcPressureKPa, speedMS);
            }

            return;
        }

        // ===== 2) T車空気ブレーキの上限capを作る =====
        float[] trailerAirCapsN = new float[carCount];
        for (int i = 0; i < carCount; i++)
        {
            CarSpec carSpec = GetCarSpec(i);
            if (carSpec == null || carSpec.carType != CarType.Trailer)
            {
                trailerAirCapsN[i] = 0f;
                continue;
            }

            // そのT車が現在速度で出せる空気ブレーキ上限[N]
            trailerAirCapsN[i] = airBrakeUnit.GetAirBrakeCapForceN(trainSpec, carSpec, speedMS);
        }

        // 不足分をT車空気capの範囲で、なるべく均等に配分（各T車の目標空気力[N]）
        float[] trailerTargetAirForcesN = brakeControlUnit.AllocateEvenlyWithSaturation(trailerAirCapsN, remainAfterRegenN);

        // ===== 2-b) T車の目標空気力 -> 目標BC圧 -> 遅れ後BC圧 -> 実空気力 =====
        float totalTrailerAirActualN = 0f;
        for (int i = 0; i < carCount; i++)
        {
            CarSpec carSpec = GetCarSpec(i);
            CarBrakeState state = carBrakeStates[i];
            if (state == null || carSpec == null || carSpec.carType != CarType.Trailer)
            {
                continue;
            }

            float targetAirForceN = i < trailerTargetAirForcesN.Length ? trailerTargetAirForcesN[i] : 0f;

            // 力[N]から必要BC圧[kPa]を逆算（協調時最低圧もここで反映）
            float targetBCPressureKPa = airBrakeUnit.GetTargetBCPressureKPa(trainSpec, carSpec, targetAirForceN, speedMS, hasBrakeCommand);

            // BC圧の応答遅れ（込め/抜き）を通した実BC圧
            state.bcPressureKPa = airBrakeUnit.UpdateBCPressureKPa(trainSpec, carSpec, state.bcPressureKPa, targetBCPressureKPa, deltaTime);

            // 実BC圧から実空気力[N]を算出
            state.airForceN = airBrakeUnit.GetAirBrakeForceN(trainSpec, carSpec, state.bcPressureKPa, speedMS);
            totalTrailerAirActualN += state.airForceN;
        }

        // T車空気まで使った後の残差（必要ならM車空気で補う）
        float remainAfterTrailerAirN = Mathf.Max(0f, targetTotalBrakeForceN - totalRegenActualN - totalTrailerAirActualN);

        // ===== 3) M車空気ブレーキの上限capを作る =====
        float[] motorAirCapsN = new float[carCount];
        for (int i = 0; i < carCount; i++)
        {
            CarSpec carSpec = GetCarSpec(i);
            if (carSpec == null || carSpec.carType != CarType.Motor)
            {
                motorAirCapsN[i] = 0f;
                continue;
            }

            // そのM車が現在速度で出せる空気ブレーキ上限[N]
            motorAirCapsN[i] = airBrakeUnit.GetAirBrakeCapForceN(trainSpec, carSpec, speedMS);
        }

        // 残差をM車空気capで配分（各M車の目標空気力[N]）
        float[] motorTargetAirForcesN = brakeControlUnit.AllocateWithSaturation(motorAirCapsN, remainAfterTrailerAirN);

        // ===== 3-b) M車の目標空気力 -> 目標BC圧 -> 遅れ後BC圧 -> 実空気力 =====
        for (int i = 0; i < carCount; i++)
        {
            CarSpec carSpec = GetCarSpec(i);
            CarBrakeState state = carBrakeStates[i];
            if (state == null || carSpec == null || carSpec.carType != CarType.Motor)
            {
                continue;
            }

            float targetAirForceN = i < motorTargetAirForcesN.Length ? motorTargetAirForcesN[i] : 0f;

            // 力[N]から必要BC圧[kPa]を逆算
            float targetBCPressureKPa = airBrakeUnit.GetTargetBCPressureKPa(trainSpec, carSpec, targetAirForceN, speedMS, hasBrakeCommand);

            // BC圧の遅れを反映
            state.bcPressureKPa = airBrakeUnit.UpdateBCPressureKPa(trainSpec, carSpec, state.bcPressureKPa, targetBCPressureKPa, deltaTime);

            // 実空気力[N]を更新
            state.airForceN = airBrakeUnit.GetAirBrakeForceN(trainSpec, carSpec, state.bcPressureKPa, speedMS);
        }
    }

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
        currentRegenForceN = totalRegenN;
        currentAirForceN = totalAirN;
        totalBrakeForceN = currentRegenForceN + currentAirForceN;
        currentRegenDecel = currentRegenForceN / safeMassKg;
        currentAirDecel = currentAirForceN / safeMassKg;
        totalBrakeDecel = totalBrakeForceN / safeMassKg;
        currentBCPressureKPa = maxBCKPa;
    }

    private float GetTotalConsistMassKg()
    {
        // 編成未設定時は既存TrainSpec質量をフォールバックとして使う
        if (consistDefinition == null || consistDefinition.cars == null || consistDefinition.cars.Count == 0)
        {
            return Mathf.Max(1f, trainSpec.massKg);
        }

        float totalMassKg = 0f;
        for (int i = 0; i < consistDefinition.cars.Count; i++)
        {
            CarSpec carSpec = consistDefinition.cars[i];
            if (carSpec == null)
            {
                continue;
            }

            totalMassKg += Mathf.Max(1f, carSpec.massKg);
        }

        if (totalMassKg <= 0f)
        {
            totalMassKg = Mathf.Max(1f, trainSpec.massKg);
        }

        return totalMassKg;
    }

    private CarSpec GetCarSpec(int index)
    {
        if (consistDefinition == null || consistDefinition.cars == null)
        {
            return null;
        }
        if (index < 0 || index >= consistDefinition.cars.Count)
        {
            return null;
        }

        return consistDefinition.cars[index];
    }

    private void InitializeCarBrakeStates()
    {
        // 編成長に合わせて、車両ごとの実行時状態を初期生成
        carBrakeStates.Clear();
        int target = consistDefinition?.cars?.Count ?? 0;
        for (int i = 0; i < target; i++)
        {
            CarBrakeState state = new CarBrakeState();
            regenBrakeUnit.InitializeCarState(state);
            carBrakeStates.Add(state);
        }

        ResetNullCarStates();
    }

    private void EnsureCarBrakeStateCount()
    {
        // 編成変更に追従して、状態リストの不足/過剰を調整
        int target = consistDefinition?.cars?.Count ?? 0;

        while (carBrakeStates.Count < target)
        {
            CarBrakeState state = new CarBrakeState();
            regenBrakeUnit.InitializeCarState(state);
            carBrakeStates.Add(state);
        }

        while (carBrakeStates.Count > target)
        {
            carBrakeStates.RemoveAt(carBrakeStates.Count - 1);
        }

        ResetNullCarStates();
    }

    private void ResetNullCarStates()
    {
        // 編成内にnull車両があっても落ちないように、そのスロットだけ初期化して無効化
        if (consistDefinition == null || consistDefinition.cars == null)
        {
            return;
        }

        int count = Mathf.Min(carBrakeStates.Count, consistDefinition.cars.Count);
        for (int i = 0; i < count; i++)
        {
            if (consistDefinition.cars[i] == null)
            {
                carBrakeStates[i].Reset();
            }
        }
    }

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

        currentBCPressureKPa = 0f;
        currentRegenForceN = 0f;
        currentAirForceN = 0f;
        totalBrakeForceN = 0f;
        currentRegenDecel = 0f;
        currentAirDecel = 0f;
        totalBrakeDecel = 0f;
        CurrentConsistMassKg = 0f;
    }
}
