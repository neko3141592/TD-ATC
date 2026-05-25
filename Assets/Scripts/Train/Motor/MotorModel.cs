using UnityEngine;

public class MotorModel : MonoBehaviour
{
    [SerializeField] private MotorSpec spec;

    public MotorSpec Spec => spec;

    public float LineVoltageRmsV { get; private set; }
    public float FrequencyHz { get; private set; }
    public float MotorRpm { get; private set; }

    public float SyncRpm { get; private set; }
    public float SlipRatio { get; private set; }

    public float RatedTorqueNm { get; private set; }
    public float MotorTorqueNm { get; private set; }
    public float MotorCurrentRmsA { get; private set; }
    public float RotorCurrentRmsA { get; private set; }
    public float MotorOutputPowerW { get; private set; }
    public float InputActivePowerW { get; private set; }
    public float ApparentPowerVA { get; private set; }

    public void SetInput(float lineVoltageRmsV, float frequencyHz, float motorRpm)
    {
        SetInput(spec, lineVoltageRmsV, frequencyHz, motorRpm);
    }

    public void SetInput(MotorSpec inputSpec, float lineVoltageRmsV, float frequencyHz, float motorRpm)
    {
        LineVoltageRmsV = lineVoltageRmsV;
        FrequencyHz = frequencyHz;
        MotorRpm = motorRpm;

        UpdateModel(inputSpec != null ? inputSpec : spec);
    }

    private void UpdateModel(MotorSpec activeSpec)
    {
        if (activeSpec == null)
        {
            ResetOutputValues();
            return;
        }

        if (FrequencyHz < 0.01f || LineVoltageRmsV < 1f)
        {
            ResetOutputValues();
            return;
        }

        SyncRpm = VVVFMath.GetSynchronousRpm(FrequencyHz, activeSpec.poleCount);
        SlipRatio = VVVFMath.GetSlipRatio(SyncRpm, MotorRpm);
        float safeSlipRatio = SlipRatio;
        if (Mathf.Abs(safeSlipRatio) < 0.001f)
        {
            safeSlipRatio = safeSlipRatio >= 0f ? 0.001f : -0.001f;
        }

        float freqRatio = FrequencyHz / Mathf.Max(0.01f, activeSpec.ratedFrequencyHz);
        float statorReactanceOhm = activeSpec.statorReactanceOhm * freqRatio; // X1'
        float magnetizingReactanceOhm = activeSpec.magnetizingReactanceOhm * freqRatio; // Xm'
        float rotorReactanceOhm = activeSpec.rotorReactanceOhm * freqRatio; // X2'
        float rotorResistanceOhm = activeSpec.rotorResistanceOhm / safeSlipRatio; // R2 / s

        ComplexValue statorImpedance = activeSpec.statorResistanceOhm + ComplexValue.J * statorReactanceOhm; // R1 + jX1'
        ComplexValue magnetizingImpedance = ComplexValue.J * magnetizingReactanceOhm; // jXm'
        ComplexValue rotorImpedance = rotorResistanceOhm + ComplexValue.J * rotorReactanceOhm; // R2 / s + jX2'
        ComplexValue totalImpedance = VVVFMath.CalculateParallelImpedance(magnetizingImpedance, rotorImpedance) + statorImpedance;

        float phaseVoltageRmsV = VVVFMath.GetPhaseVoltageRmsFromLineVoltageRms(LineVoltageRmsV);
        ComplexValue phaseVoltage = new(phaseVoltageRmsV, 0f);

        ComplexValue statorCurrent = phaseVoltage / totalImpedance;
        MotorCurrentRmsA = statorCurrent.Magnitude;
        ApparentPowerVA = VVVFMath.GetThreePhaseApparentPowerVA(LineVoltageRmsV, MotorCurrentRmsA);

        ComplexValue rotorCurrent = statorCurrent * magnetizingImpedance / (rotorImpedance + magnetizingImpedance); 
        RotorCurrentRmsA = rotorCurrent.Magnitude;

        float i2Square = RotorCurrentRmsA * RotorCurrentRmsA;
        float airGapPowerW = 3f * i2Square * rotorResistanceOhm;

        // 同期角速度
        float synchronousAngularSpeed = VVVFMath.GetAngularSpeedRadSFromRpm(SyncRpm);
        float safeSynchronousAngularSpeed = Mathf.Max(0.1f, Mathf.Abs(synchronousAngularSpeed));

        MotorTorqueNm = airGapPowerW / safeSynchronousAngularSpeed;

        float motorAngularSpeed = VVVFMath.GetAngularSpeedRadSFromRpm(MotorRpm);
        MotorOutputPowerW = MotorTorqueNm * motorAngularSpeed;
        InputActivePowerW = MotorOutputPowerW / Mathf.Max(0.01f, activeSpec.efficiency);

    }

    public void ResetModel()
    {
        LineVoltageRmsV = 0f;
        FrequencyHz = 0f;
        MotorRpm = 0f;
        ResetOutputValues();
    }

    private void ResetOutputValues()
    {
        SyncRpm = 0f;
        SlipRatio = 0f;
        RatedTorqueNm = 0f;
        MotorTorqueNm = 0f;
        MotorCurrentRmsA = 0f;
        RotorCurrentRmsA = 0f;
        MotorOutputPowerW = 0f;
        InputActivePowerW = 0f;
        ApparentPowerVA = 0f;
    }
}
