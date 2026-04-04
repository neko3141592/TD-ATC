using UnityEngine;

public class ATCController : MonoBehaviour
{
    [SerializeField] private TrainController train;
    [SerializeField] private TrainSpec trainSpec;
    [SerializeField] private NotchManager notchManager;
    [SerializeField] private ATCProfile profile;
    [SerializeField] private float currentLimitSpeedMS = 0f;
    [SerializeField] private float patternAllowSpeedMS = 0f;
    [SerializeField] private float patternTargetDistanceM = 0f;
    [SerializeField] private float patternTargetSpeedMS = 0f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip dingClip;
    [SerializeField] private float limitChangeEpsilonMS = 0.01f;
    [SerializeField] private float fallbackPatternDecelerationMS2 = 1.8f;
    [SerializeField] private float atcReleaseMarginKmH = 3f;
    [SerializeField] private float overspeedToleranceMS = 0.1f;
    [SerializeField] private int atcBrakeNotch = 7;

    private bool hasPreviousLimit = false;
    private float previousLimitSpeedMS = 0f;
    private bool isATCBrakeLatched = false;

    public float CurrentLimitSpeedKmH => currentLimitSpeedMS * 3.6f;
    public float CurrentPatternAllowSpeedKmH => patternAllowSpeedMS * 3.6f;
    public float CurrentPatternTargetDistanceM => patternTargetDistanceM;
    public float CurrentPatternTargetSpeedKmH => patternTargetSpeedMS * 3.6f;
    public bool IsPatternApproaching => train != null && patternAllowSpeedMS < currentLimitSpeedMS && patternTargetSpeedMS < train.SpeedMS;

    void Awake()
    {
        if (trainSpec == null && train != null)
        {
            trainSpec = train.Spec;
        }

        if (notchManager == null && train != null)
        {
            notchManager = train.GetComponent<NotchManager>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    void Update()
    {
        if (trainSpec == null && train != null)
        {
            trainSpec = train.Spec;
        }

        if (notchManager == null && train != null)
        {
            notchManager = train.GetComponent<NotchManager>();
        }

        if (train == null || profile == null)
        {
            currentLimitSpeedMS = 0f;
            patternAllowSpeedMS = 0f;
            patternTargetDistanceM = 0f;
            patternTargetSpeedMS = 0f;
            hasPreviousLimit = false;
            isATCBrakeLatched = false;
            SendATCBrake(0);
            return;
        }

        float nextLimitSpeedMS = profile.GetLimitSpeed(train.DistanceM);

        if (hasPreviousLimit && Mathf.Abs(nextLimitSpeedMS - previousLimitSpeedMS) > limitChangeEpsilonMS)
        {
            PlayDing();
        }

        currentLimitSpeedMS = nextLimitSpeedMS;
        previousLimitSpeedMS = nextLimitSpeedMS;
        hasPreviousLimit = true;

        UpdatePatternAllowSpeed();
        UpdateATCBrakeLatch();
        SendATCBrake(isATCBrakeLatched ? atcBrakeNotch : 0);
    }

    private void PlayDing()
    {
        if (audioSource == null || dingClip == null)
        {
            return;
        }

        audioSource.PlayOneShot(dingClip);
    }

    private void SendATCBrake(int brakeNotch)
    {
        if (notchManager == null)
        {
            return;
        }

        notchManager.SetATCBrakeNotch(Mathf.Max(0, brakeNotch));
    }

    private void UpdateATCBrakeLatch()
    {
        float applyThresholdMS = patternAllowSpeedMS + Mathf.Max(0f, overspeedToleranceMS);
        float releaseMarginMS = Mathf.Max(0f, atcReleaseMarginKmH) / 3.6f;
        float releaseThresholdMS = Mathf.Max(0f, patternAllowSpeedMS - releaseMarginMS);

        if (!isATCBrakeLatched)
        {
            bool isOverSpeed = train.SpeedMS > applyThresholdMS;
            if (isOverSpeed)
            {
                isATCBrakeLatched = true;
            }
            return;
        }

        if (train.SpeedMS <= releaseThresholdMS)
        {
            isATCBrakeLatched = false;
        }
    }

    private void UpdatePatternAllowSpeed()
    {
        patternAllowSpeedMS = currentLimitSpeedMS;
        patternTargetDistanceM = train.DistanceM;
        patternTargetSpeedMS = currentLimitSpeedMS;

        if (profile == null || train == null)
        {
            return;
        }

        if (!profile.TryGetNextLowerLimitTarget(train.DistanceM, out float targetDistanceM, out float targetSpeedMS))
        {
            return;
        }

        float remainDistanceM = Mathf.Max(0f, targetDistanceM - train.DistanceM);
        float patternDecelerationMS2 = GetPatternDecelerationMS2();
        float allowSpeedMS = ATCPatternCalculator.CalculateAllowSpeedMS(
            targetSpeedMS,
            patternDecelerationMS2,
            remainDistanceM
        );

        // ATCで使う許容速度は、現在制限とパターン許容速度の低い方を使う
        patternAllowSpeedMS = Mathf.Min(currentLimitSpeedMS, allowSpeedMS);
        patternTargetDistanceM = targetDistanceM;
        patternTargetSpeedMS = targetSpeedMS;
    }

    private float GetPatternDecelerationMS2()
    {
        if (trainSpec != null)
        {
            // ATCパターンの想定減速度は TrainSpec の B4 を使う
            return Mathf.Max(0f, trainSpec.GetBrakeDeceleration(4));
        }

        return Mathf.Max(0f, fallbackPatternDecelerationMS2);
    }
}
