using UnityEngine;

public class ATCController : MonoBehaviour
{
    [SerializeField] private TrainController train;
    [SerializeField] private TrainSpec trainSpec;
    [SerializeField] private NotchManager notchManager;

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
    [SerializeField] private float safetyDistance = 50f;
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

        if (train == null || train.Graph == null || string.IsNullOrEmpty(train.CurrentEdgeId))
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

        TrackEdge currentEdge = train.Graph.FindEdge(train.CurrentEdgeId);
        float nextLimitSpeedMS = currentEdge != null ? currentEdge.speedLimitMS : 0f;

        if (hasPreviousLimit && Mathf.Abs(nextLimitSpeedMS - previousLimitSpeedMS) > limitChangeEpsilonMS)
        {
            PlayDing();
        }

        currentLimitSpeedMS = nextLimitSpeedMS;
        previousLimitSpeedMS = nextLimitSpeedMS;
        hasPreviousLimit = true;

        UpdatePatternAllowSpeed(currentEdge);
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

    private void UpdatePatternAllowSpeed(TrackEdge currentEdge)
    {
        patternAllowSpeedMS = currentLimitSpeedMS;
        patternTargetDistanceM = train.DistanceM;
        patternTargetSpeedMS = currentLimitSpeedMS;

        if (train == null || train.Graph == null || currentEdge == null)
        {
            return;
        }

        float patternDecelerationMS2 = GetPatternDecelerationMS2();
        
        float accumulatedDistM = currentEdge.lengthM - train.DistanceOnEdgeM;
        string edgeId = currentEdge.edgeId;
        string nextEdgeId = train.Graph.ResolveNextEdgeId(currentEdge.toNodeId, edgeId);
        
        float minCalculatedAllowSpeedMS = currentLimitSpeedMS;
        float finalTargetDistM = train.DistanceM;
        float finalTargetSpeedMS = currentLimitSpeedMS;
        bool foundLower = false;

        float lookaheadLimitM = 1000f; 
        
        while (accumulatedDistM < lookaheadLimitM)
        {
            float nextLimitSpeedMS;
            if (string.IsNullOrEmpty(nextEdgeId)) {
                nextLimitSpeedMS = 0f; 
            } else {
                nextLimitSpeedMS = train.Graph.FindEdge(nextEdgeId).speedLimitMS;
            }


            if (nextLimitSpeedMS < currentLimitSpeedMS)
            {
                float allowSpeedMS = ATCPatternCalculator.CalculateAllowSpeedMS(
                    nextLimitSpeedMS,
                    patternDecelerationMS2,
                    accumulatedDistM - safetyDistance
                );
                
                if (allowSpeedMS < minCalculatedAllowSpeedMS)
                {
                    minCalculatedAllowSpeedMS = allowSpeedMS;
                    // 目標地点絶対距離
                    finalTargetDistM = train.DistanceM + accumulatedDistM;
                    finalTargetSpeedMS = nextLimitSpeedMS;
                    foundLower = true;
                }
            }

            if (string.IsNullOrEmpty(nextEdgeId)) {
                break;
            }

            TrackEdge nextEdge = train.Graph.FindEdge(nextEdgeId);
            accumulatedDistM += nextEdge.lengthM;
            edgeId = nextEdgeId;
            nextEdgeId = train.Graph.ResolveNextEdgeId(nextEdge.toNodeId, edgeId);
        }

        // 最も低いパターンを使用
        patternAllowSpeedMS = Mathf.Min(currentLimitSpeedMS, minCalculatedAllowSpeedMS);
        
        if (foundLower) {
            patternTargetDistanceM = finalTargetDistM;
            patternTargetSpeedMS = finalTargetSpeedMS;
        }
    }

    private float GetPatternDecelerationMS2()
    {
        if (trainSpec != null)
        {
            return Mathf.Max(0f, trainSpec.GetBrakeDeceleration(5));
        }

        return Mathf.Max(0f, fallbackPatternDecelerationMS2);
    }

    private float GetEmergencyPatternDecelerationMS2 () {
        if (trainSpec != null)
        {
            return Mathf.Max(0f, trainSpec.GetBrakeDeceleration());
        }

        return Mathf.Max(0f, fallbackPatternDecelerationMS2);
    }
}
