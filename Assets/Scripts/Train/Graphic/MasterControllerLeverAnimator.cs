using UnityEngine;

public class MasterControllerLeverAnimator : MonoBehaviour
{
    private enum Axis
    {
        X,
        Y,
        Z
    }

    [Header("References")]
    [SerializeField] private TrainController train;
    [SerializeField] private NotchManager notchManager;
    [SerializeField] private Transform lever;
    [SerializeField] private bool useResolvedNotches = false;

    [Header("Rotation")]
    [SerializeField] private Axis axis = Axis.X;
    [SerializeField] private bool invert = false;
    [SerializeField, Min(0.01f)] private float responseTimeSeconds = 0.08f;
    [SerializeField] private float neutralAngleDegrees = 0f;
    [SerializeField]
    private float[] powerNotchAnglesDegrees =
    {
        0f,
        8f,
        16f,
        24f,
        32f,
        40f
    };
    [SerializeField]
    private float[] brakeNotchAnglesDegrees =
    {
        0f,
        -6f,
        -12f,
        -18f,
        -24f,
        -30f,
        -36f,
        -90f,
        -125f,
        -150f
    };

    private Quaternion neutralLocalRotation;
    private float displayedAngleDegrees;
    private float angleVelocity;
    private bool initialized;

    private void Awake()
    {
        ResolveReferences();
        CaptureNeutralPose();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureNeutralPose();
        displayedAngleDegrees = GetTargetAngleDegrees();
        ApplyAngle(displayedAngleDegrees);
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (lever == null)
        {
            return;
        }

        float targetAngleDegrees = GetTargetAngleDegrees();
        displayedAngleDegrees = Mathf.SmoothDamp(
            displayedAngleDegrees,
            targetAngleDegrees,
            ref angleVelocity,
            responseTimeSeconds,
            Mathf.Infinity,
            Time.deltaTime
        );

        ApplyAngle(displayedAngleDegrees);
    }

    private void ResolveReferences()
    {
        if (lever == null)
        {
            lever = transform;
        }

        train = CabReferenceResolver.ResolveTrain(this, train);

        if (notchManager == null)
        {
            notchManager = CabReferenceResolver.ResolveTrainComponent(this, train, notchManager);
        }
    }

    private void CaptureNeutralPose()
    {
        if (initialized || lever == null)
        {
            return;
        }

        neutralLocalRotation = lever.localRotation;
        initialized = true;
    }

    private float GetTargetAngleDegrees()
    {
        int powerNotch = 0;
        int brakeNotch = 0;

        if (useResolvedNotches && notchManager != null)
        {
            powerNotch = notchManager.ResolvedPowerNotch;
            brakeNotch = notchManager.ResolvedBrakeNotch;
        }
        else if (train != null)
        {
            powerNotch = train.ManualPowerNotch;
            brakeNotch = train.ManualBrakeNotch;
        }
        else if (notchManager != null)
        {
            powerNotch = notchManager.ManualPowerNotch;
            brakeNotch = notchManager.ManualBrakeNotch;
        }

        float angleDegrees = neutralAngleDegrees;
        if (brakeNotch > 0 && TryGetNotchValue(brakeNotchAnglesDegrees, brakeNotch, out float brakeAngleDegrees))
        {
            angleDegrees = brakeAngleDegrees;
        }
        else if (powerNotch > 0 && TryGetNotchValue(powerNotchAnglesDegrees, powerNotch, out float powerAngleDegrees))
        {
            angleDegrees = powerAngleDegrees;
        }

        return invert ? -angleDegrees : angleDegrees;
    }

    private void ApplyAngle(float angleDegrees)
    {
        lever.localRotation = neutralLocalRotation * Quaternion.AngleAxis(angleDegrees, GetAxisVector(1f));
    }

    private static bool TryGetNotchValue(float[] values, int notch, out float value)
    {
        if (values != null && notch >= 0 && notch < values.Length)
        {
            value = values[notch];
            return true;
        }

        value = 0f;
        return false;
    }

    private Vector3 GetAxisVector(float value)
    {
        switch (axis)
        {
            case Axis.X:
                return new Vector3(value, 0f, 0f);
            case Axis.Y:
                return new Vector3(0f, value, 0f);
            case Axis.Z:
                return new Vector3(0f, 0f, value);
            default:
                return Vector3.zero;
        }
    }

    private void OnValidate()
    {
        responseTimeSeconds = Mathf.Max(0.01f, responseTimeSeconds);
    }
}
