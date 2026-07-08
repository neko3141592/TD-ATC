using UnityEngine;

public class BrakeCylinder : MonoBehaviour
{
    [SerializeField] private BrakeCylinderSpec spec;
    [SerializeField, Min(0f)] private float targetPressurePa;
    [SerializeField, Min(0f)] private float currentPressurePa;

    public BrakeCylinderSpec Spec => spec;
    public float TargetPressurePa => targetPressurePa;
    public float CurrentPressurePa => currentPressurePa;
    public float TargetPressureKPa => targetPressurePa * 0.001f;
    public float CurrentPressureKPa => currentPressurePa * 0.001f;
    public float BrakeForceN => CalculateBrakeForceN(currentPressurePa);

    private void Update()
    {
        UpdatePressure(Time.deltaTime);
    }

    public void SetSpec(BrakeCylinderSpec newSpec)
    {
        spec = newSpec;
        ClampPressureValues();
    }

    public void SetTargetPressurePa(float pressurePa)
    {
        targetPressurePa = ClampPressure(pressurePa);
    }

    public void SetTargetPressureKPa(float pressureKPa)
    {
        SetTargetPressurePa(Mathf.Max(0f, pressureKPa) * 1000f);
    }

    public void Release()
    {
        targetPressurePa = 0f;
    }

    public void ResetPressure()
    {
        targetPressurePa = 0f;
        currentPressurePa = 0f;
    }

    public void UpdatePressure(float deltaTime)
    {
        float safeDeltaTime = Mathf.Max(0f, deltaTime);
        float clampedTargetPressurePa = ClampPressure(targetPressurePa);
        float ratePaPerSec = clampedTargetPressurePa > currentPressurePa
            ? GetPressureRiseRatePaPerSec()
            : GetPressureFallRatePaPerSec();

        currentPressurePa = Mathf.MoveTowards(
            ClampPressure(currentPressurePa),
            clampedTargetPressurePa,
            ratePaPerSec * safeDeltaTime
        );

        targetPressurePa = clampedTargetPressurePa;
    }

    public float CalculateBrakeForceN(float pressurePa)
    {
        if (spec == null)
        {
            return 0f;
        }

        float safePressurePa = ClampPressure(pressurePa);
        float pistonForceN = safePressurePa * Mathf.Max(0f, spec.pistonAreaM2);
        return Mathf.Max(0f, pistonForceN * Mathf.Max(0f, spec.leverRatio) * Mathf.Clamp01(spec.mechanicalEfficiency));
    }

    private void OnValidate()
    {
        ClampPressureValues();
    }

    private void ClampPressureValues()
    {
        targetPressurePa = ClampPressure(targetPressurePa);
        currentPressurePa = ClampPressure(currentPressurePa);
    }

    private float ClampPressure(float pressurePa)
    {
        float maxPressurePa = spec != null ? Mathf.Max(0f, spec.maxPressurePa) : Mathf.Infinity;
        return Mathf.Clamp(Mathf.Max(0f, pressurePa), 0f, maxPressurePa);
    }

    private float GetPressureRiseRatePaPerSec()
    {
        return spec != null ? Mathf.Max(0f, spec.pressureRiseRatePaPerSec) : 0f;
    }

    private float GetPressureFallRatePaPerSec()
    {
        return spec != null ? Mathf.Max(0f, spec.pressureFallRatePaPerSec) : 0f;
    }
}
