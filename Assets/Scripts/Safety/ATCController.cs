using UnityEngine;

public class ATCController : MonoBehaviour
{
    [SerializeField] private TrainController train;
    [SerializeField] private NotchManager notchManager;
    [SerializeField] private ATCProfile profile;
    [SerializeField] private float currentLimitSpeedMS = 0f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip dingClip;
    [SerializeField] private float limitChangeEpsilonMS = 0.01f;
    [SerializeField] private float atcReleaseMarginKmH = 3f;
    [SerializeField] private float overspeedToleranceMS = 0.1f;
    [SerializeField] private int atcBrakeNotch = 7;

    private bool hasPreviousLimit = false;
    private float previousLimitSpeedMS = 0f;
    private bool isATCBrakeLatched = false;

    public float CurrentLimitSpeedKmH => currentLimitSpeedMS * 3.6f;

    void Awake()
    {
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
        if (notchManager == null && train != null)
        {
            notchManager = train.GetComponent<NotchManager>();
        }

        if (train == null || profile == null)
        {
            currentLimitSpeedMS = 0f;
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
        if (!isATCBrakeLatched)
        {
            bool isOverSpeed = train.SpeedMS > (currentLimitSpeedMS + overspeedToleranceMS);
            if (isOverSpeed)
            {
                isATCBrakeLatched = true;
            }
            return;
        }

        float releaseMarginMS = Mathf.Max(0f, atcReleaseMarginKmH) / 3.6f;
        float releaseSpeedMS = Mathf.Max(0f, currentLimitSpeedMS - releaseMarginMS);
        if (train.SpeedMS <= releaseSpeedMS)
        {
            isATCBrakeLatched = false;
        }
    }
}
