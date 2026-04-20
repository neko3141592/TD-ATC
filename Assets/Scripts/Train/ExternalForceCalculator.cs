using UnityEngine;

internal static class ExternalForceCalculator
{
    private static float GetRollingResistanceForceN(TrainSpec spec, float speedMS)
    {
        if (spec == null)
        {
            return 0f;
        }

        float v = Mathf.Max(0f, speedMS);
        return Mathf.Max(0f, spec.resistanceA + (spec.resistanceB * v));
    }

    private static float GetAerodynamicDragForceN(TrainSpec spec, float speedMS)
    {
        if (spec == null)
        {
            return 0f;
        }

        float v = Mathf.Max(0f, speedMS);
        return Mathf.Max(0f, spec.resistanceC * v * v);
    }

    public static float GetRunningResistanceForceN(TrainSpec spec, float speedMS)
    {
        return GetRollingResistanceForceN(spec, speedMS) + GetAerodynamicDragForceN(spec, speedMS);
    }

    public static float GetCoastExtraResistanceForceN(TrainSpec spec, float speedMS)
    {
        if (spec == null)
        {
            return 0f;
        }

        float massKg = Mathf.Max(1f, spec.massKg);
        return Mathf.Max(0f, spec.GetCoastDeceleration(speedMS)) * massKg;
    }
}
