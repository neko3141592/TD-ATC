using UnityEngine;

public class ATCController : MonoBehaviour
{
    [SerializeField] private TrainController train;
    [SerializeField] private TrainSpec trainSpec;
    [SerializeField] private NotchManager notchManager;
    [SerializeField] private BlockOccupancyManager blockOccupancyManager;

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
    [SerializeField] private float occupiedBlockSafetyMarginM = 50f;
    [SerializeField] private int atcBrakeNotch = 7;

    private bool hasPreviousLimit = false;
    private float previousLimitSpeedMS = 0f;
    private bool isATCBrakeLatched = false;

    public float CurrentLimitSpeedKmH => currentLimitSpeedMS * 3.6f;
    public float CurrentPatternAllowSpeedKmH => patternAllowSpeedMS * 3.6f;
    public float CurrentPatternTargetDistanceM => patternTargetDistanceM;
    public float CurrentPatternTargetSpeedKmH => patternTargetSpeedMS * 3.6f;
    public bool IsPatternApproaching => train != null && patternAllowSpeedMS < currentLimitSpeedMS && patternTargetSpeedMS < train.SpeedMS;

    /// <summary>
    /// 役割: ATC 制限候補の情報をひとまとめに保持します。
    /// </summary>
    private struct AtcTargetCandidate
    {
        public bool isValid;
        public string sourceLabel;
        public float distanceM;
        public float targetSpeedMS;
        public float allowedSpeedMS;
    }

    /// <summary>
    /// 役割: Awake の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    void Awake()
    {
        ResolveRuntimeReferences();
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

    /// <summary>
    /// 役割: Update の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    void Update()
    {
        ResolveRuntimeReferences();

        if (train == null || train.Graph == null || string.IsNullOrEmpty(train.CurrentEdgeId))
        {
            ResetPatternState();
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

        AtcTargetCandidate speedLimitCandidate = BuildSpeedLimitCandidate(currentEdge);
        ApplyPatternCandidate(speedLimitCandidate);
        UpdateATCBrakeLatch();
        SendATCBrake(isATCBrakeLatched ? atcBrakeNotch : 0);
    }

    /// <summary>
    /// 役割: PlayDing の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void PlayDing()
    {
        if (audioSource == null || dingClip == null)
        {
            return;
        }

        audioSource.PlayOneShot(dingClip);
    }

    /// <summary>
    /// 役割: SendATCBrake の処理を実行します。
    /// </summary>
    /// <param name="brakeNotch">brakeNotch を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void SendATCBrake(int brakeNotch)
    {
        if (notchManager == null)
        {
            return;
        }

        notchManager.SetATCBrakeNotch(Mathf.Max(0, brakeNotch));
    }

    /// <summary>
    /// 役割: UpdateATCBrakeLatch の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
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

    /// <summary>
    /// 役割: 現在位置から見た速度制限候補を組み立てます。
    /// </summary>
    /// <param name="currentEdge">現在走行中のエッジを指定します。</param>
    /// <returns>速度制限由来の ATC 制限候補を返します。</returns>
    private AtcTargetCandidate BuildSpeedLimitCandidate(TrackEdge currentEdge)
    {
        AtcTargetCandidate candidate = new AtcTargetCandidate
        {
            isValid = true,
            sourceLabel = "Speed Limit",
            distanceM = 0f,
            targetSpeedMS = currentLimitSpeedMS,
            allowedSpeedMS = currentLimitSpeedMS,
        };

        if (train == null || train.Graph == null || currentEdge == null)
        {
            return candidate;
        }

        float patternDecelerationMS2 = GetPatternDecelerationMS2();
        float accumulatedDistM = currentEdge.lengthM - train.DistanceOnEdgeM;
        string edgeId = currentEdge.edgeId;
        string nextEdgeId = train.Graph.ResolveNextEdgeId(currentEdge.toNodeId, edgeId);

        float lookaheadLimitM = 1000f;

        while (accumulatedDistM < lookaheadLimitM)
        {
            if (string.IsNullOrEmpty(nextEdgeId))
            {
                break;
            }

            TrackEdge nextEdge = train.Graph.FindEdge(nextEdgeId);
            if (nextEdge == null)
            {
                break;
            }

            float nextLimitSpeedMS = nextEdge.speedLimitMS;


            if (nextLimitSpeedMS < currentLimitSpeedMS)
            {
                float allowSpeedMS = ATCPatternCalculator.CalculateAllowSpeedMS(
                    nextLimitSpeedMS,
                    patternDecelerationMS2,
                    accumulatedDistM - safetyDistance
                );

                if (allowSpeedMS < candidate.allowedSpeedMS)
                {
                    candidate.distanceM = accumulatedDistM;
                    candidate.targetSpeedMS = nextLimitSpeedMS;
                    candidate.allowedSpeedMS = allowSpeedMS;
                }
            }

            accumulatedDistM += nextEdge.lengthM;
            edgeId = nextEdgeId;
            nextEdgeId = train.Graph.ResolveNextEdgeId(nextEdge.toNodeId, edgeId);
        }

        return candidate;
    }

    /// <summary>
    /// 役割: 組み立てた ATC 制限候補を現在の表示用状態へ反映します。
    /// </summary>
    /// <param name="candidate">反映する ATC 制限候補を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ApplyPatternCandidate(AtcTargetCandidate candidate)
    {
        if (!candidate.isValid)
        {
            ResetPatternState();
            return;
        }

        patternAllowSpeedMS = candidate.allowedSpeedMS;
        patternTargetDistanceM = train != null ? train.DistanceM + candidate.distanceM : candidate.distanceM;
        patternTargetSpeedMS = candidate.targetSpeedMS;
    }

    /// <summary>
    /// 役割: GetPatternDecelerationMS2 の処理を実行します。
    /// </summary>
    /// <returns>処理結果を返します。</returns>
    private float GetPatternDecelerationMS2()
    {
        if (trainSpec != null)
        {
            return Mathf.Max(0f, trainSpec.GetBrakeDeceleration(5));
        }

        return Mathf.Max(0f, fallbackPatternDecelerationMS2);
    }

    /// <summary>
    /// 役割: ResolveRuntimeReferences の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ResolveRuntimeReferences()
    {
        if (trainSpec == null && train != null)
        {
            trainSpec = train.Spec;
        }

        if (notchManager == null && train != null)
        {
            notchManager = train.GetComponent<NotchManager>();
        }
    }

    /// <summary>
    /// 役割: ResetPatternState の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ResetPatternState()
    {
        currentLimitSpeedMS = 0f;
        patternAllowSpeedMS = 0f;
        patternTargetDistanceM = 0f;
        patternTargetSpeedMS = 0f;
    }
}
