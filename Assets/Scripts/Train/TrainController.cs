using UnityEngine;
using UnityEngine.Splines; // 線路（Spline）を扱うための準備

public class TrainController : MonoBehaviour
{
    [SerializeField] private TrainSpec trainSpec;
    [SerializeField] private NotchManager notchManager;
    [SerializeField] private BrakeSystemController brakeSystem;
    [SerializeField] private TractionSystemController tractionSystem;
    public SplineContainer spline; // どの線路を走るか
    [SerializeField] private float speedMS = 0f; // 現在速度 (m/s)
    private float distance = 0f;   // スタートからの累計走行距離 (m)

    private float currentAccelerationMS2 = 0f;

    public float SpeedKmH => speedMS * 3.6f;
    public float SpeedMS => speedMS;
    public float DistanceM => distance;
    public int PowerNotch => notchManager != null ? notchManager.ResolvedPowerNotch : 0;
    public int BrakeNotch => notchManager != null ? notchManager.ResolvedBrakeNotch : 0;
    public int ManualPowerNotch => notchManager != null ? notchManager.ManualPowerNotch : 0;
    public int ManualBrakeNotch => notchManager != null ? notchManager.ManualBrakeNotch : 0;
    public int ATCBrakeNotch => notchManager != null ? notchManager.ATCBrakeNotch : 0;
    public int EmergencyBrakeNotch => trainSpec != null ? trainSpec.GetEmergencyBrakeNotch() : 9;
    public bool IsEmergencyBrakeActive => BrakeNotch >= EmergencyBrakeNotch;
    public float CurrentBrakeDecelMS2 => brakeSystem != null ? brakeSystem.totalBrakeDecel : 0f;
    public float CurrentRegenBrakeDecelMS2 => brakeSystem != null ? brakeSystem.currentRegenDecel : 0f;
    public float CurrentAirBrakeDecelMS2 => brakeSystem != null ? brakeSystem.currentAirDecel : 0f;
    public float CurrentBrakeForceN => brakeSystem != null ? brakeSystem.totalBrakeForceN : 0f;
    public float CurrentRegenBrakeForceN => brakeSystem != null ? brakeSystem.currentRegenForceN : 0f;
    public float CurrentAirBrakeForceN => brakeSystem != null ? brakeSystem.currentAirForceN : 0f;
    public float CurrentTractionForceN => tractionSystem != null ? tractionSystem.CurrentTotalTractionForceN : 0f;
    public float CurrentBCPressureKPa => brakeSystem != null ? brakeSystem.currentBCPressureKPa : 0f;
    public float CurrentAccelerationMS2 => currentAccelerationMS2;
    public System.Collections.Generic.IReadOnlyList<CarBrakeState> CurrentCarBrakeStates => brakeSystem != null ? brakeSystem.CarBrakeStates : null;

    void Awake()
    {
        if (trainSpec == null)
        {
            Debug.LogError($"{nameof(TrainController)} on {name}: TrainSpec is not assigned.", this);
            enabled = false;
            return;
        }

        if (notchManager == null)
        {
            notchManager = GetComponent<NotchManager>();
        }
        if (notchManager == null)
        {
            notchManager = gameObject.AddComponent<NotchManager>();
        }
        if (brakeSystem == null)
        {
            brakeSystem = GetComponent<BrakeSystemController>();
        }
        if (tractionSystem == null)
        {
            tractionSystem = GetComponent<TractionSystemController>();
        }

        notchManager.ConfigureLimits(trainSpec.maxPowerNotch, EmergencyBrakeNotch);
    }

    void Update()
    {
        HandleInput();    // 1. 入力を処理
        ApplyPhysics();   // 2. 物理計算（速度と距離）
        MoveTrain(); 
    }


    void HandleInput()
    {
        if (notchManager == null)
        {
            return;
        }

        int powerNotch = notchManager.ManualPowerNotch;
        int brakeNotch = notchManager.ManualBrakeNotch;
        int emergencyBrakeNotch = EmergencyBrakeNotch;

        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (brakeNotch == 0 && powerNotch < trainSpec.maxPowerNotch) powerNotch++;
        }
        if (Input.GetKeyDown(KeyCode.X))
        {
            if (powerNotch > 0) powerNotch--;
        }

        if (Input.GetKeyDown(KeyCode.Comma))
        {
            if (powerNotch > 0) powerNotch = 0; 
            if (brakeNotch < emergencyBrakeNotch) brakeNotch++;
        }
        if (Input.GetKeyDown(KeyCode.Period))
        {
            if (brakeNotch > 0) brakeNotch--;
        }

        notchManager.SetManualNotches(powerNotch, brakeNotch);
    }

    void ApplyPhysics()
    {
        notchManager.ConfigureLimits(trainSpec.maxPowerNotch, EmergencyBrakeNotch);

        int powerNotch = PowerNotch; // 高位優先解決後の力行ノッチ
        int brakeNotch = BrakeNotch; // 高位優先解決後の制動ノッチ
        bool isEmergencyBrake = brakeNotch >= EmergencyBrakeNotch;

        if (brakeSystem != null)
        {
            brakeSystem.UpdateBrake(brakeNotch, speedMS, Time.deltaTime, isEmergencyBrake);
        }

        float massKg = 0f;
        if (brakeSystem != null && brakeSystem.CurrentConsistMassKg > 0f)
        {
            massKg = brakeSystem.CurrentConsistMassKg;
        }
        else if (tractionSystem != null && tractionSystem.CurrentConsistMassKg > 0f)
        {
            massKg = tractionSystem.CurrentConsistMassKg;
        }
        else
        {
            massKg = Mathf.Max(1f, trainSpec.massKg);
        }

        float brakeDeceleration = 0f; // ブレーキの減速度指令[m/s^2]
        float brakeForceN = 0f; // 制動力[N]
        if (brakeSystem != null)
        {
            brakeDeceleration = brakeSystem.totalBrakeDecel;
            brakeForceN = brakeSystem.totalBrakeForceN;
        }
        else if (brakeNotch > 0)
        {
            brakeDeceleration = trainSpec.GetBrakeDeceleration(brakeNotch);
            brakeForceN = Mathf.Max(0f, brakeDeceleration) * massKg;
        }

        float runningResistanceForceN = ExternalForceCalculator.GetRunningResistanceForceN(trainSpec, speedMS); // 走行抵抗+空気抵抗[N]
        float coastResistanceForceN = 0f; // 惰行追加抵抗[N]
        if (powerNotch <= 0 && brakeDeceleration <= 0f)
        {
            // 既存の惰行フィールを維持するため、暫定で追加抵抗として扱う
            coastResistanceForceN = ExternalForceCalculator.GetCoastExtraResistanceForceN(trainSpec, speedMS);
        }
        float externalForceN = runningResistanceForceN + coastResistanceForceN; // 外部から受ける抵抗力[N]

        float tractionForceN = 0f;
        if (tractionSystem != null)
        {
            tractionSystem.UpdateTraction(powerNotch, speedMS, externalForceN);
            tractionForceN = tractionSystem.CurrentTotalTractionForceN;
        }
        else
        {
            tractionForceN = trainSpec.GetTractionDemandForceN(
                powerNotch,
                speedMS,
                massKg,
                externalForceN
            ); // 力行指令力[N]（定加速/定トルク/定出力を内包）
        }
        float vehicleForceN = tractionForceN - brakeForceN; // 車両側で作る合成力[N]

        float netForceN = vehicleForceN - externalForceN; // 外力込みの正味合力[N]
        float acceleration = netForceN / massKg; // 正味加速度[m/s^2]

        currentAccelerationMS2 = acceleration;
        speedMS += acceleration * Time.deltaTime;
        speedMS = Mathf.Clamp(speedMS, 0f, trainSpec.maxSpeedMS);

        distance += speedMS * Time.deltaTime;

    }
    void MoveTrain()
    {
        if (spline != null)
        {
            float length = spline.CalculateLength();
            float t = Mathf.Repeat(distance / length, 1.0f);
            transform.position = (Vector3)spline.EvaluatePosition(t);
            transform.rotation = Quaternion.LookRotation((Vector3)spline.EvaluateTangent(t));
        }
    }
}
