using UnityEngine;

public class VVVFMath : MonoBehaviour
{
    public static float GetWheelRpm(float speedMS, float wheelRadiusM)
    {
        float safeWheelRadiusM = Mathf.Max(0.01f, wheelRadiusM);
        float wheelAngularSpeedRadS = speedMS / safeWheelRadiusM;
        return wheelAngularSpeedRadS * 60f / (2f * Mathf.PI);
    }

    public static float GetMotorRpm(float wheelRpm, float gearRatio)
    {
        return wheelRpm * gearRatio;
    }

    public static float GetAngularSpeedRadSFromRpm(float rpm)
    {
        return rpm * 2f * Mathf.PI / 60f;
    }

    public static float GetTorqueFromPowerAndRpm(float powerW, float rpm)
    {
        float angularSpeedRadS = GetAngularSpeedRadSFromRpm(rpm);
        return Mathf.Abs(angularSpeedRadS) > 0.01f
            ? powerW / angularSpeedRadS
            : 0f;
    }

    public static float GetSynchronousRpm(float frequencyHz, int poleCount)
    {
        return poleCount > 0 ? 120f * frequencyHz / poleCount : 0f;
    }

    public static float GetFrequencyFromSynchronousRpm(float synchronousRpm, int poleCount)
    {
        return poleCount > 0 ? synchronousRpm * poleCount / 120f : 0f;
    }

    public static float GetSynchronousRpmFromMotorRpm(float motorRpm, float slipRatio)
    {
        float safeDenominator = Mathf.Max(0.05f, 1f - Mathf.Clamp(slipRatio, -0.95f, 0.95f));
        return motorRpm / safeDenominator;
    }

    public static float GetSlipRatio(float synchronousRpm, float motorRpm)
    {
        return Mathf.Abs(synchronousRpm) > 1f
            ? (synchronousRpm - motorRpm) / synchronousRpm
            : 0f;
    }

    public static float GetPhaseVoltageRmsFromLineVoltageRms(float lineVoltageRmsV)
    {
        return lineVoltageRmsV / Mathf.Sqrt(3f);
    }

    public static float GetPeakFromRms(float rms)
    {
        return rms * Mathf.Sqrt(2f);
    }

    public static float GetRmsFromPeak(float peak)
    {
        return peak / Mathf.Sqrt(2f);
    }

    public static float GetPhaseVoltagePeakFromLineVoltageRms(float lineVoltageRmsV)
    {
        return GetPeakFromRms(GetPhaseVoltageRmsFromLineVoltageRms(lineVoltageRmsV));
    }

    public static float GetVoltageRatio(float frequencyHz, float ratedFrequencyHz)
    {
        float safeRatedFrequencyHz = Mathf.Max(0.01f, ratedFrequencyHz);
        return Mathf.Clamp01(frequencyHz / safeRatedFrequencyHz);
    }

    public static float GetThreePhaseApparentPowerVA(float lineVoltageRmsV, float lineCurrentRmsA)
    {
        return Mathf.Sqrt(3f) * lineVoltageRmsV * lineCurrentRmsA;
    }

    public static ComplexValue CalculateSeriesImpedance(params ComplexValue[] impedances)
    {
        ComplexValue impedance = 0f;

        foreach(ComplexValue z in impedances)
        {
            impedance += z;
        }

        return impedance;
    }

    public static ComplexValue CalculateParallelImpedance(params ComplexValue[] impedances)
    {
        if (impedances == null || impedances.Length == 0)
        {
            return 0f;
        }

        ComplexValue admittance = 0f;

        foreach(ComplexValue z in impedances)
        {
            admittance += 1f / z;
        }

        return 1f / admittance;
    }
}
