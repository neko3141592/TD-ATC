using UnityEngine;

public class VVVFController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainController train;
    [SerializeField] private MotorSpec motorSpec;
    [SerializeField] private MotorModel[] motors;


    [Header("Control")]
    [SerializeField, Range(0f, 0.2f)] private float targetSlipRatio = 0.03f;

    public float FrequencyHz { get; private set; }
    public int PoleCount => motorSpec != null ? motorSpec.poleCount : 0;
    public float SyncRpm { get; private set; }
    public float SlipRatio { get; private set; }
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
        if (train == null || train.Spec == null || motorSpec == null)
        {
            ResetValues();
            return;
        }

        TrainSpec trainSpec = train.Spec;
        WheelRpm = VVVFMath.GetWheelRpm(train.SpeedMS, trainSpec.wheelRadiusM);
        MotorRpm = VVVFMath.GetMotorRpm(WheelRpm, trainSpec.gearRatio);

        SyncRpm = VVVFMath.GetSynchronousRpmFromMotorRpm(
            MotorRpm,
            targetSlipRatio
        );

        FrequencyHz = VVVFMath.GetFrequencyFromSynchronousRpm(
            SyncRpm,
            motorSpec.poleCount
        );

        SlipRatio = VVVFMath.GetSlipRatio(SyncRpm, MotorRpm);

        VoltageRatio = VVVFMath.GetVoltageRatio(
            FrequencyHz,
            motorSpec.ratedFrequencyHz
        );

        LineVoltageRmsV = motorSpec.ratedLineVoltageV * VoltageRatio;
        UpdateThreePhaseWave();
        UpdateMotors(MotorRpm);

    }

    private void UpdateThreePhaseWave()
    {
        PhaseVoltagePeakV = VVVFMath.GetPhaseVoltagePeakFromLineVoltageRms(LineVoltageRmsV);

        PhaseRad += 2f * Mathf.PI * FrequencyHz * Time.deltaTime;
        PhaseRad = Mathf.Repeat(PhaseRad, 2f * Mathf.PI);

        UPhaseV = PhaseVoltagePeakV * Mathf.Sin(PhaseRad);
        VPhaseV = PhaseVoltagePeakV * Mathf.Sin(PhaseRad - 2f * Mathf.PI / 3f);
        WPhaseV = PhaseVoltagePeakV * Mathf.Sin(PhaseRad + 2f * Mathf.PI / 3f);
    }

    private void UpdateMotors(float motorRpm)
    {
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
    }

    private void ResetValues()
    {
        FrequencyHz = 0f;
        SyncRpm = 0f;
        SlipRatio = 0f;
        LineVoltageRmsV = 0f;
        PhaseVoltagePeakV = 0f;
        VoltageRatio = 0f;
        WheelRpm = 0f;
        MotorRpm = 0f;
        UPhaseV = 0f;
        VPhaseV = 0f;
        WPhaseV = 0f;

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
