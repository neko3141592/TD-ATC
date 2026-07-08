using UnityEngine;

[CreateAssetMenu(fileName = "BrakeCylinderSpec", menuName = "Train/Brake/Brake Cylinder Spec")]
public class BrakeCylinderSpec : ScriptableObject
{
    [Header("Pressure")]
    [Min(0f)] public float maxPressurePa = 380000f;
    [Min(0f)] public float pressureRiseRatePaPerSec = 300000f;
    [Min(0f)] public float pressureFallRatePaPerSec = 400000f;

    [Header("Mechanism")]
    [Min(0f)] public float pistonAreaM2 = 0.01f;
    [Min(0f)] public float leverRatio = 6f;
    [Range(0f, 1f)] public float mechanicalEfficiency = 0.9f;

    public float MaxPressureKPa => maxPressurePa * 0.001f;

    private void OnValidate()
    {
        maxPressurePa = Mathf.Max(0f, maxPressurePa);
        pressureRiseRatePaPerSec = Mathf.Max(0f, pressureRiseRatePaPerSec);
        pressureFallRatePaPerSec = Mathf.Max(0f, pressureFallRatePaPerSec);
        pistonAreaM2 = Mathf.Max(0f, pistonAreaM2);
        leverRatio = Mathf.Max(0f, leverRatio);
        mechanicalEfficiency = Mathf.Clamp01(mechanicalEfficiency);
    }
}
