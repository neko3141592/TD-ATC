using System.Collections.Generic;
using UnityEngine;

public enum BrakeDistributionPriority
{
    AllCars,
    TrailerCars
}

[CreateAssetMenu(fileName = "TimsControlConfig", menuName = "Train/NTIMS/Tims Control Config")]
public class TimsControlConfig : ScriptableObject
{
    [Header("Power")]
    [Min(1)] public int powerStepCount = 5;
    public AnimationCurve[] powerCurves;
    [Min(0f)] public float launchAccelerationKmhPerSec = 3f;

    [Header("Brake")]
    public List<float> brakeTargetDecelerationsKmhPerSec = new();
    [Min(1)] public int brakeStepCount = 7;
    [Min(1)] public int brakeSubstepCount = 4;
    [Min(0f)] public float minimumServiceBrakePressureKPa = 40f;
    [Min(1f)] public float minimumServiceBrakePressureLoadScaleMax = 3f;

    public BrakeDistributionPriority brakeDistributionPriority = BrakeDistributionPriority.TrailerCars;

    public List<float> BrakeTargetDecelerationsMS2
    {
        get
        {
            List<float> decelerationsMS2 = new();

            for (int i = 0; i < brakeTargetDecelerationsKmhPerSec.Count; i++)
            {
                decelerationsMS2.Add(brakeTargetDecelerationsKmhPerSec[i] / 3.6f);
            }

            return decelerationsMS2;
        }
    }

    public float LaunchAccelerationMS2 => Mathf.Max(0f, launchAccelerationKmhPerSec) / 3.6f;

    public float GetPowerStepGain(int powerStep, float speed01)
    {
        if (powerStep <= 0 || powerCurves == null || powerCurves.Length == 0)
        {
            return 0f;
        }

        int index = Mathf.Clamp(powerStep - 1, 0, powerCurves.Length - 1);
        AnimationCurve curve = powerCurves[index];
        if (curve == null || curve.length == 0)
        {
            return 0f;
        }

        return Mathf.Max(0f, curve.Evaluate(Mathf.Clamp01(speed01)));
    }

    private void OnValidate()
    {
        powerStepCount = Mathf.Max(1, powerStepCount);
        launchAccelerationKmhPerSec = Mathf.Max(0f, launchAccelerationKmhPerSec);
        brakeStepCount = Mathf.Max(1, brakeStepCount);
        brakeSubstepCount = Mathf.Max(1, brakeSubstepCount);
        minimumServiceBrakePressureKPa = Mathf.Max(0f, minimumServiceBrakePressureKPa);
        minimumServiceBrakePressureLoadScaleMax = Mathf.Max(1f, minimumServiceBrakePressureLoadScaleMax);
        powerCurves = ResizePowerCurves(powerCurves, powerStepCount);
    }

    private AnimationCurve[] ResizePowerCurves(AnimationCurve[] source, int size)
    {
        AnimationCurve[] result = new AnimationCurve[size];
        int copied = 0;

        if (source != null)
        {
            copied = Mathf.Min(source.Length, size);
            for (int i = 0; i < copied; i++)
            {
                result[i] = source[i];
            }
        }

        for (int i = 0; i < size; i++)
        {
            if (result[i] == null || result[i].length == 0)
            {
                float fallback = (i + 1f) / size;
                result[i] = CreateConstantCurve(fallback);
            }
        }

        return result;
    }

    private AnimationCurve CreateConstantCurve(float value)
    {
        float v = Mathf.Max(0f, value);
        return new AnimationCurve(
            new Keyframe(0f, v),
            new Keyframe(1f, v)
        );
    }
}
