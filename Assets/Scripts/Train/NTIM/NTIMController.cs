using UnityEngine;

public class NTIMController : MonoBehaviour
{
    [Header("references")]
    [SerializeField] private VVVFController[] vvvfControllers;
    [SerializeField] private TrainController train;
    [SerializeField] private NotchManager notchManager;

    private float targetForceN;
    public float TargetForceN => targetForceN;
    public float CurrentRatedConsistPowerW { get; private set; }
    public float TargetAccelerationMS2 { get; private set; }
    public float TargetForcePerVvvfN { get; private set; }
    public int ActiveVvvfCount { get; private set; }
    public string CurrentRegionLabel { get; private set; } = "--";
    public bool SpeedHoldActive => speedHoldState == SpeedHoldState.Active;
    public float SpeedHoldTargetMS => speedHoldTargetMS;
    public string SpeedHoldStateLabel => speedHoldState.ToString();

    [Header("Torque Controll")]

    [SerializeField, Min(0f)] private float targetAccelerationMS2 = 0.8f;
    [SerializeField] private float constantAccelerationEndSpeedMS = 15f;
    [SerializeField] private float[] powerNotchRatios = { 0.2f, 0.4f, 0.6f, 0.8f, 1.0f };
    [SerializeField] private AnimationCurve[] powerNotchGainCurves;

    [Header("Speed Hold")]
    [SerializeField, Min(0f)] private float speedHoldArmingSeconds = 1f;

    private enum SpeedHoldState
    {
        Off,
        Arming,
        Active
    }

    private SpeedHoldState speedHoldState = SpeedHoldState.Off;
    private float speedHoldArmingTimer = 0f;
    private float speedHoldTargetMS = 0f;

    private void Awake()
    {
        ResolveReferences();
    }

    void Update ()
    {
        ResolveReferences();

        if (train == null || train.Spec == null)
        {
            targetForceN = 0f;
            CurrentRatedConsistPowerW = 0f;
            TargetAccelerationMS2 = 0f;
            TargetForcePerVvvfN = 0f;
            ActiveVvvfCount = 0;
            CurrentRegionLabel = "--";
            ClearSpeedHold();
            DistributeTargetForce(0f);
            return;
        }

        UpdateSpeedHoldState(Time.deltaTime);

        float ratedConsistPowerW = CalculateRatedConsistPowerW();
        CurrentRatedConsistPowerW = ratedConsistPowerW;
        TargetAccelerationMS2 = targetAccelerationMS2;

        constantAccelerationEndSpeedMS = CalculateConstantAccelerationEndSpeedMS(
            ratedConsistPowerW,
            train.CurrentConsistMassKg,
            TargetAccelerationMS2
        );


        float maxAvailableForceN = CalculateMaxAvailableForceN(ratedConsistPowerW);

        targetForceN = CalculateTargetForceNFromNotch(maxAvailableForceN);


        DistributeTargetForce(targetForceN);

    }

    private void ResolveReferences()
    {
        if (train == null)
        {
            train = GetComponentInParent<TrainController>();
        }

        if (vvvfControllers == null || vvvfControllers.Length == 0)
        {
            RefreshVVVFControllersFromChildren();
        }

        if (notchManager == null)
        {
            notchManager = train != null
                ? train.GetComponent<NotchManager>()
                : GetComponentInParent<NotchManager>();
        }
    }

    public void SetVVVFControllers(VVVFController[] controllers)
    {
        vvvfControllers = controllers ?? System.Array.Empty<VVVFController>();
    }

    public void RefreshVVVFControllersFromChildren()
    {
        vvvfControllers = GetComponentsInChildren<VVVFController>(true);
    }

    private void UpdateSpeedHoldState(float deltaTime)
    {
        if (notchManager == null || train == null)
        {
            ClearSpeedHold();
            return;
        }

        if (notchManager.ConsumeSpeedHoldRequest())
        {
            speedHoldState = SpeedHoldState.Arming;
            speedHoldArmingTimer = 0f;
            speedHoldTargetMS = 0f;
        }

        if (speedHoldState == SpeedHoldState.Off)
        {
            return;
        }

        if (ShouldCancelSpeedHold())
        {
            ClearSpeedHold();
            return;
        }

        if (speedHoldState == SpeedHoldState.Arming)
        {
            speedHoldArmingTimer += Mathf.Max(0f, deltaTime);

            if (speedHoldArmingTimer >= speedHoldArmingSeconds)
            {
                SetSpeedHold();
            }
        }
    }

    private bool ShouldCancelSpeedHold()
    {
        if (train == null)
        {
            return true;
        }

        return
            train.ManualPowerNotch != 2 ||
            train.ManualBrakeNotch > 0 ||
            train.ATCBrakeNotch > 0 ||
            train.TASCBrakeStep > 0;
    }

    private void SetSpeedHold()
    {
        speedHoldTargetMS = train.SpeedMS;
        speedHoldState = SpeedHoldState.Active;
    }

    private void ClearSpeedHold()
    {
        speedHoldState = SpeedHoldState.Off;
        speedHoldArmingTimer = 0f;
        speedHoldTargetMS = 0f;
    }

    private void DistributeTargetForce(float totalTargetForceN)
    {
        if (vvvfControllers == null)
        {
            return;
        }

        ActiveVvvfCount = 0;
        foreach (VVVFController vvvfController in vvvfControllers)
        {
            if (vvvfController != null)
            {
                ActiveVvvfCount++;
            }
        }

        if (ActiveVvvfCount <= 0)
        {
            TargetForcePerVvvfN = 0f;
            return;
        }

        TargetForcePerVvvfN = totalTargetForceN / ActiveVvvfCount;
        foreach (VVVFController vvvfController in vvvfControllers)
        {
            if (vvvfController == null)
            {
                continue;
            }

            vvvfController.SetTargetTractionForce(TargetForcePerVvvfN);
        }
    }

    private float CalculateRatedConsistPowerW()
    {
        float totalPowerW = 0f;

        if (vvvfControllers == null)
        {
            return 0f;
        }

        foreach (VVVFController vvvfController in vvvfControllers)
        {
            if (vvvfController == null || vvvfController.MotorSpec == null)
            {
                continue;
            }

            int motorCount = vvvfController.MotorCount;
            MotorSpec motorSpec = vvvfController.MotorSpec;

            totalPowerW += motorSpec.ratedPowerW * motorCount;
        }

        return totalPowerW;
    }

    private float CalculateConstantAccelerationEndSpeedMS(float ratedConsistPowerW, float massKg, float targetAccelerationMS2)
    {
        float safePowerW = Mathf.Max(0f, ratedConsistPowerW);
        float safeMassKg = Mathf.Max(1f, massKg);
        float safeAccelerationMS2 = Mathf.Max(0.01f, targetAccelerationMS2);

        return safePowerW / (safeMassKg * safeAccelerationMS2);
    }

    private float CalculateTargetForceNFromNotch(float baseTargetForceN)
    {
        if (train == null || train.Spec == null)
        {
            return 0f;
        }

        if (speedHoldState == SpeedHoldState.Arming)
        {
            return 0f;
        }

        int powerNotch = train.PowerNotch;

        if (powerNotch <= 0)
        {
            return 0f;
        }

        float notchGain = GetPowerNotchSpeedGain(
            powerNotch,
            train.SpeedMS
        );

        return baseTargetForceN * notchGain;
    }

    private float CalculateMaxAvailableForceN (float ratedConsistPowerW)
    {

        float maxAvailableForceN = 0f;
        if (train.SpeedMS <= constantAccelerationEndSpeedMS)
        {
            // 定加速領域
            CurrentRegionLabel = "Const Accel";
            maxAvailableForceN = train.CurrentConsistMassKg * TargetAccelerationMS2;
        }
        else
        {
            // 定出力領域
            CurrentRegionLabel = "Const Power";
            maxAvailableForceN = ratedConsistPowerW / Mathf.Max(0.1f, train.SpeedMS);
        }

        return maxAvailableForceN;

    }

    private float GetPowerNotchSpeedGain(int notch, float speedMS)
    {
        if (train == null || train.Spec == null || notch <= 0)
        {
            return 0f;
        }

        int maxPowerNotch = Mathf.Max(1, train.Spec.maxPowerNotch);
        int clampedNotch = Mathf.Clamp(notch, 1, maxPowerNotch);
        int index = clampedNotch - 1;
        float speed01 = train.Spec.maxSpeedMS > 0f
            ? Mathf.Clamp01(speedMS / train.Spec.maxSpeedMS)
            : 0f;

        if (powerNotchGainCurves != null &&
            index < powerNotchGainCurves.Length &&
            powerNotchGainCurves[index] != null &&
            powerNotchGainCurves[index].length > 0)
        {
            return Mathf.Max(0f, powerNotchGainCurves[index].Evaluate(speed01));
        }

        return GetPowerNotchRatio(clampedNotch);
    }

    private float GetPowerNotchRatio(int notch)
    {
        if (notch <= 0 || powerNotchRatios == null || powerNotchRatios.Length == 0)
        {
            return 0f;
        }

        int index = Mathf.Clamp(notch - 1, 0, powerNotchRatios.Length - 1);
        return Mathf.Max(0f, powerNotchRatios[index]);
    }

    private void OnValidate()
    {
        targetAccelerationMS2 = Mathf.Max(0f, targetAccelerationMS2);
        int maxPowerNotch = train != null && train.Spec != null
            ? Mathf.Max(1, train.Spec.maxPowerNotch)
            : Mathf.Max(1, powerNotchRatios != null ? powerNotchRatios.Length : 5);

        powerNotchRatios = ResizeArray(powerNotchRatios, maxPowerNotch);
        powerNotchGainCurves = ResizeCurveArray(powerNotchGainCurves, maxPowerNotch, powerNotchRatios);

        for (int i = 0; i < powerNotchRatios.Length; i++)
        {
            powerNotchRatios[i] = Mathf.Max(0f, powerNotchRatios[i]);
        }

        for (int i = 0; i < powerNotchGainCurves.Length; i++)
        {
            if (powerNotchGainCurves[i] == null || powerNotchGainCurves[i].length == 0)
            {
                powerNotchGainCurves[i] = CreateConstantCurve(powerNotchRatios[i]);
            }
        }
    }

    private float[] ResizeArray(float[] source, int size)
    {
        float[] result = new float[size];
        int copied = 0;
        if (source != null)
        {
            copied = Mathf.Min(source.Length, size);
            for (int i = 0; i < copied; i++)
            {
                result[i] = source[i];
            }
        }

        for (int i = copied; i < size; i++)
        {
            result[i] = (i + 1f) / size;
        }

        return result;
    }

    private AnimationCurve[] ResizeCurveArray(AnimationCurve[] source, int size, float[] fallbackRatios)
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

        for (int i = copied; i < size; i++)
        {
            float fallback = fallbackRatios != null && i < fallbackRatios.Length
                ? Mathf.Max(0f, fallbackRatios[i])
                : 0f;
            result[i] = CreateConstantCurve(fallback);
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
