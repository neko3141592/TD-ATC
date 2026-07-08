using System.Collections.Generic;
using UnityEngine;

public class TimsTractionForceController : MonoBehaviour
{
    [Header("references")]
    [SerializeField] private TimsSystem tims;
    [SerializeField] private VVVFController[] vvvfControllers;
    [SerializeField] private TrainController train;

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
    public bool BCReleaseInterlockActive { get; private set; }

    private float constantAccelerationEndSpeedMS = 15f;

    [Header("BC Release Interlock")]
    [SerializeField, Min(0f)] private float bcReleaseThresholdKPa = 5f;

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

        if (train == null || train.Spec == null || tims == null || tims.ControlConfig == null)
        {
            targetForceN = 0f;
            CurrentRatedConsistPowerW = 0f;
            TargetAccelerationMS2 = 0f;
            TargetForcePerVvvfN = 0f;
            ActiveVvvfCount = 0;
            CurrentRegionLabel = "--";
            BCReleaseInterlockActive = false;
            ClearSpeedHold();
            DistributeTargetForce(0f);
            return;
        }

        if (train.BrakeStep > 0)
        {
            targetForceN = 0f;
            TargetForcePerVvvfN = 0f;
            ActiveVvvfCount = 0;
            CurrentRegionLabel = "Brake";
            BCReleaseInterlockActive = false;
            ClearSpeedHold();
            return;
        }

        UpdateSpeedHoldState(Time.deltaTime);

        float ratedConsistPowerW = CalculateRatedConsistPowerW();
        CurrentRatedConsistPowerW = ratedConsistPowerW;
        TargetAccelerationMS2 = tims.ControlConfig.LaunchAccelerationMS2;

        constantAccelerationEndSpeedMS = CalculateConstantAccelerationEndSpeedMS(
            ratedConsistPowerW,
            train.CurrentConsistMassKg,
            TargetAccelerationMS2
        );


        float maxAvailableForceN = CalculateMaxAvailableForceN(ratedConsistPowerW);

        targetForceN = CalculateTargetForceNFromNotch(maxAvailableForceN);

        BCReleaseInterlockActive = !AreAllBCReleasedForTraction();
        if (BCReleaseInterlockActive)
        {
            targetForceN = 0f;
            CurrentRegionLabel = "BC Interlock";
        }

        DistributeTargetForce(targetForceN);

    }

    private void ResolveReferences()
    {
        if (train == null)
        {
            train = GetComponentInParent<TrainController>();
        }

        if (tims == null)
        {
            tims = GetComponentInParent<TimsSystem>();
        }

        if (vvvfControllers == null || vvvfControllers.Length == 0 || !HasValidVVVFController())
        {
            RefreshVVVFControllersFromChildren();
        }
    }

    private bool HasValidVVVFController()
    {
        if (vvvfControllers == null)
        {
            return false;
        }

        for (int i = 0; i < vvvfControllers.Length; i++)
        {
            if (vvvfControllers[i] != null)
            {
                return true;
            }
        }

        return false;
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
        if (train == null)
        {
            ClearSpeedHold();
            return;
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

        float speed01 = train.Spec.maxSpeedMS > 0f
            ? Mathf.Clamp01(speedMS / train.Spec.maxSpeedMS)
            : 0f;

        if (tims == null || tims.ControlConfig == null)
        {
            return 0f;
        }

        return tims.ControlConfig.GetPowerStepGain(notch, speed01);
    }

    private bool AreAllBCReleasedForTraction()
    {
        if (train == null)
        {
            return false;
        }

        IReadOnlyList<CarBrakeState> carBrakeStates = train.CurrentCarBrakeStates;

        if (train.IsGradientStart)
        {
            return true;
        }
        if (carBrakeStates == null || carBrakeStates.Count == 0)
        {
            return train.CurrentBCPressureKPa < bcReleaseThresholdKPa;
        }

        for (int i = 0; i < carBrakeStates.Count; i++)
        {
            CarBrakeState state = carBrakeStates[i];
            if (state != null && state.bcPressureKPa >= bcReleaseThresholdKPa)
            {
                return false;
            }
        }

        return true;
    }

    private void OnValidate()
    {
        bcReleaseThresholdKPa = Mathf.Max(0f, bcReleaseThresholdKPa);
    }

}
