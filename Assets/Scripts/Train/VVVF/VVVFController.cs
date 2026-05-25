using UnityEngine;

public class VVVFController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainController train;
    [SerializeField] private VVVFSpec vvvfSpec;
    [SerializeField] private MotorSpec motorSpec;
    [SerializeField] private MotorModel[] motors;
    [SerializeField] private int assignedCarIndex = -1;

    public MotorSpec MotorSpec => motorSpec;
    public MotorModel[] Motors => motors;
    public int AssignedCarIndex => assignedCarIndex;
    public int MotorCount => motors != null ? motors.Length : 0;
    public float RatedPowerW =>
        motorSpec != null ? motorSpec.ratedPowerW * MotorCount : 0f;


    

    // [Header("Control")]
    // [SerializeField, Min(0f)] private float launchFrequencyHz = 3f;
    // [SerializeField, Range(0f, 0.5f)] private float launchVoltageBoostRatio = 0.12f;
    // [SerializeField, Min(0f)] private float slipFrequencyControlRateHzPerSec = 2f;
    // [SerializeField, Min(0f)] private float voltageControlRateRatioPerSec = 5f;
    // [SerializeField, Min(0f)] private float torqueDeadbandNm = 5f;
    // [SerializeField, Min(0f)] private float maxSlipFrequencyHz = 8f;

    [SerializeField, Min(0f)] private float slipFrequencyHz;

    // Speed Hold
    private bool speedHoldActive = false;
    private float speedHoldTargetMS = 0f;


    public float FrequencyHz { get; private set; }
    public int PoleCount => motorSpec != null ? motorSpec.poleCount : 0;
    public float SyncRpm { get; private set; }
    public float SlipRatio { get; private set; }
    public float RotorBaseFrequencyHz { get; private set; }
    public float SlipFrequencyHz { get; private set; }
    public float LineVoltageRmsV { get; private set; }
    public float PhaseVoltagePeakV { get; private set; }
    public float VoltageRatio { get; private set; }
    public float PhaseRad { get; private set; }
    public float WheelRpm { get; private set; }
    public float MotorRpm { get; private set; }

    public float UPhaseV { get; private set; }
    public float VPhaseV { get; private set; }
    public float WPhaseV { get; private set; }
    public float PhaseSumV => UPhaseV + VPhaseV + WPhaseV;
    public float TotalMotorTractionForceN { get; private set; }
    public float TargetTractionForceN { get; private set; }
    public float TargetMotorTorqueNm { get; private set; }

    public MotorModel PrimaryMotor => motors != null && motors.Length > 0 ? motors[0] : null;

    private void Awake()
    {
        if (train == null)
        {
            train = GetComponentInParent<TrainController>();
        }

        if (motors == null || motors.Length == 0)
        {
            motors = GetComponentsInChildren<MotorModel>();
        }
    }

    private void OnValidate()
    {
        if (motors == null || motors.Length == 0)
        {
            motors = GetComponentsInChildren<MotorModel>();
        }
    }

    private void Update()
    {
        UpdateDrive(Time.deltaTime);
    }

    public void Bind(TrainController ownerTrain, int carIndex)
    {
        train = ownerTrain;
        assignedCarIndex = carIndex;

        if (motors == null || motors.Length == 0)
        {
            motors = GetComponentsInChildren<MotorModel>();
        }
    }

    public void UpdateDrive(float deltaTime)
    {
        if (deltaTime < 0f)
        {
            deltaTime = 0f;
        }

        UpdateDriveInternal(deltaTime);
    }

    private void UpdateDriveInternal(float deltaTime)
    {
        if (train == null || train.Spec == null || motorSpec == null || vvvfSpec == null)
        {
            ResetValues();
            return;
        }

        TrainSpec trainSpec = train.Spec;

        // 車輪回転数・モーター回転数を取得
        WheelRpm = VVVFMath.GetWheelRpm(train.SpeedMS, trainSpec.wheelRadiusM);
        MotorRpm = VVVFMath.GetMotorRpm(WheelRpm, trainSpec.gearRatio);

        float notchRatio = trainSpec.maxPowerNotch > 0
            ? Mathf.Clamp01(train.PowerNotch / (float)trainSpec.maxPowerNotch)
            : 0f;

        // 力行司令がないときは、目標牽引力・目標トルクを0
        if (train.PowerNotch <= 0)
        {
            TargetTractionForceN = 0f;
            TargetMotorTorqueNm = 0f;
        }

        // 回転子に同期するための回転数
        RotorBaseFrequencyHz = VVVFMath.GetFrequencyFromSynchronousRpm(MotorRpm, PoleCount);

        // 目標トルクに近づくまですべり周波数を増加させる
        UpdateSlipFrequencyFromTorque(deltaTime);

        bool hasDriveCommand = TargetMotorTorqueNm > 0f && train.PowerNotch > 0;
        bool keepOutputFrequencyWhileVoltageFalls = VoltageRatio > 0.001f;
        float targetFrequencyHz = RotorBaseFrequencyHz + slipFrequencyHz;

        FrequencyHz = hasDriveCommand || keepOutputFrequencyWhileVoltageFalls
            ? Mathf.Max(targetFrequencyHz, vvvfSpec.launchVoltageBoostRatio * 0)
            : 0f;

        SyncRpm = VVVFMath.GetSynchronousRpm(FrequencyHz, motorSpec.poleCount);
        SlipRatio = VVVFMath.GetSlipRatio(SyncRpm, MotorRpm);
        SlipFrequencyHz = FrequencyHz > 0.001f
            ? Mathf.Max(0f, FrequencyHz - RotorBaseFrequencyHz)
            : 0f;

        float targetVoltageRatio = 0f;
        if (hasDriveCommand)
        {
            targetVoltageRatio = VVVFMath.GetVoltageRatio(
                FrequencyHz,
                motorSpec.ratedFrequencyHz
            );
            targetVoltageRatio = Mathf.Max(targetVoltageRatio, vvvfSpec.launchVoltageBoostRatio * notchRatio);
        }

        VoltageRatio = Mathf.MoveTowards(
            VoltageRatio,
            targetVoltageRatio,
            vvvfSpec.voltageControlRateRatioPerSec * deltaTime
        );

        if (VoltageRatio < 0.0001f)
        {
            VoltageRatio = 0f;
        }

        LineVoltageRmsV = motorSpec.ratedLineVoltageV * VoltageRatio;
        UpdateThreePhaseWave(deltaTime);
        UpdateMotors(MotorRpm);
    }

    private void UpdateThreePhaseWave(float deltaTime)
    {
        PhaseVoltagePeakV = VVVFMath.GetPhaseVoltagePeakFromLineVoltageRms(LineVoltageRmsV);

        PhaseRad += 2f * Mathf.PI * FrequencyHz * deltaTime;
        PhaseRad = Mathf.Repeat(PhaseRad, 2f * Mathf.PI);

        UPhaseV = PhaseVoltagePeakV * Mathf.Sin(PhaseRad);
        VPhaseV = PhaseVoltagePeakV * Mathf.Sin(PhaseRad - 2f * Mathf.PI / 3f);
        WPhaseV = PhaseVoltagePeakV * Mathf.Sin(PhaseRad + 2f * Mathf.PI / 3f);
    }

    private void UpdateMotors(float motorRpm)
    {
        TotalMotorTractionForceN = 0f;

        if (motors == null)
        {
            return;
        }

        for (int i = 0; i < motors.Length; i++)
        {
            MotorModel motor = motors[i];
            if (motor == null)
            {
                continue;
            }

            motor.SetInput(motorSpec, LineVoltageRmsV, FrequencyHz, motorRpm);
        }

        TotalMotorTractionForceN = MotorTractionCalculator.GetTotalTractionForceN(
            motors, 
            train.Spec
        );
    }

    private void UpdateSlipFrequencyFromTorque(float deltaTime)
    {
        if (TargetMotorTorqueNm <= 0f)
        {
            slipFrequencyHz = Mathf.MoveTowards(
                slipFrequencyHz,
                0f,
                vvvfSpec.slipFrequencyControlRateHzPerSec * deltaTime
            );
            return;
        }

        float actualMotorTorqueNm = GetAverageMotorTorqueNm();
        float torqueErrorNm = TargetMotorTorqueNm - actualMotorTorqueNm;
        if (Mathf.Abs(torqueErrorNm) <= vvvfSpec.torqueDeadbandNm)
        {
            return;
        }

        float targetSlipFrequencyHz = torqueErrorNm > 0f ? vvvfSpec.maxSlipFrequencyHz : 0f;
        slipFrequencyHz = Mathf.MoveTowards(
            slipFrequencyHz,
            targetSlipFrequencyHz,
            vvvfSpec.slipFrequencyControlRateHzPerSec * deltaTime
        );

        slipFrequencyHz = Mathf.Clamp(slipFrequencyHz, 0f, vvvfSpec.maxSlipFrequencyHz);
    }

    private float GetAverageMotorTorqueNm()
    {
        if (motors == null || motors.Length == 0)
        {
            return 0f;
        }

        float totalTorqueNm = 0f;
        int activeMotorCount = 0;
        for (int i = 0; i < motors.Length; i++)
        {
            MotorModel motor = motors[i];
            if (motor == null)
            {
                continue;
            }

            totalTorqueNm += motor.MotorTorqueNm;
            activeMotorCount++;
        }

        return activeMotorCount > 0 ? totalTorqueNm / activeMotorCount : 0f;
    }

    private void UpdateTargetMotorTorque()
    {
        if (train == null || train.Spec == null || MotorCount <= 0)
        {
            TargetMotorTorqueNm = 0f;
            return;
        }

        TrainSpec spec = train.Spec;

        TargetMotorTorqueNm =
            TargetTractionForceN * spec.wheelRadiusM /
            (spec.gearRatio * spec.drivelineEfficiency * MotorCount);
    }

    public void SetTargetTractionForce(float targetTractionForceN)
    {
        TargetTractionForceN = Mathf.Max(0f, targetTractionForceN);
        UpdateTargetMotorTorque();
    }

    public void SetSpeedHold(float targetMS)
    {
        speedHoldActive = true;
        speedHoldTargetMS = targetMS;

    }

    public void ClearSpeedHold()
    {
        speedHoldActive = false;

    }

    private void ResetValues()
    {
        FrequencyHz = 0f;
        SyncRpm = 0f;
        SlipRatio = 0f;
        RotorBaseFrequencyHz = 0f;
        SlipFrequencyHz = 0f;
        slipFrequencyHz = 0f;
        LineVoltageRmsV = 0f;
        PhaseVoltagePeakV = 0f;
        VoltageRatio = 0f;
        WheelRpm = 0f;
        MotorRpm = 0f;
        UPhaseV = 0f;
        VPhaseV = 0f;
        WPhaseV = 0f;
        TotalMotorTractionForceN = 0f;
        TargetTractionForceN = 0f;
        TargetMotorTorqueNm = 0f;

        if (motors == null)
        {
            return;
        }

        for (int i = 0; i < motors.Length; i++)
        {
            if (motors[i] != null)
            {
                motors[i].ResetModel();
            }
        }
    }
}
