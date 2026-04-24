using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Categorization;

public class ATCController : MonoBehaviour
{
    private enum AtcControlState
    {
        Normal,
        ServicePattern,
        EmergencyPattern,
    }

    [Header("References")]
    [SerializeField] private TrainController train;
    [SerializeField] private TrainSpec trainSpec;
    [SerializeField] private NotchManager notchManager;
    [SerializeField] private BlockOccupancyManager blockOccupancyManager;

    [Header("Runtime Status (Debug)")]
    [SerializeField] private float currentLimitSpeedMS = 0f;
    [SerializeField] private float patternAllowSpeedMS = 0f;
    [SerializeField] private float patternEmergencyAllowSpeedMS = 0f;
    [SerializeField] private float patternTargetDistanceM = 0f;
    [SerializeField] private float patternTargetSpeedMS = 0f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip dingClip;

    [Header("Pattern / ATC Tuning")]
    [SerializeField] private float limitChangeEpsilonMS = 0.01f;
    [SerializeField] private float fallbackPatternDecelerationMS2 = 1.8f;
    [SerializeField] private float atcReleaseMarginKmH = 3f;
    [SerializeField] private float overspeedToleranceMS = 0.1f;

    [Header("Safety Margins")]
    [SerializeField] private float safetyDistance = 50f;
    [SerializeField] private float safetyDecelMS = 0.1f;
    [SerializeField] private float occupiedBlockSafetyMarginM = 50f;

    [Header("Brake Command")]
    [SerializeField] private int atcBrakeNotch = 7;

    private bool hasPreviousLimit = false;
    private float previousLimitSpeedMS = 0f;
    private bool isATCBrakeLatched = false;
    [SerializeField] private AtcControlState currentAtcState = AtcControlState.Normal;
    private readonly List<AtcTargetCandidate> candidateBuffer = new();

    public float CurrentLimitSpeedKmH => currentLimitSpeedMS * 3.6f;
    public float CurrentPatternAllowSpeedKmH => patternAllowSpeedMS * 3.6f;
    public float CurrentPatternEmergencyAllowSpeedKmH => patternEmergencyAllowSpeedMS * 3.6f;
    public float CurrentPatternTargetDistanceM => patternTargetDistanceM;
    public float CurrentPatternTargetSpeedKmH => patternTargetSpeedMS * 3.6f;
    public bool IsPatternApproaching => train != null && patternAllowSpeedMS < currentLimitSpeedMS && patternTargetSpeedMS < train.SpeedMS;
    public string CurrentAtcStateLabel => currentAtcState.ToString();

    /// <summary>
    /// 役割: ATC 制限候補の情報をひとまとめに保持します。
    /// </summary>
    private struct AtcTargetCandidate
    {
        public bool isValid;
        public string sourceLabel;
        public float distanceM;
        // 目標速度
        public float targetSpeedMS;
        // 常用ブレーキパターン
        public float allowedSpeedMS;
        // 非常ブレーキパターン
        public float allowedEmergencySpeedMS;
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
        AtcTargetCandidate occupiedBlockCandidate = BuildOccupiedBlockCandidate();

        candidateBuffer.Clear();
        candidateBuffer.Add(speedLimitCandidate);
        candidateBuffer.Add(occupiedBlockCandidate);

        AtcTargetCandidate selectedCandidate = ChooseMoreRestrictive(candidateBuffer);
        ApplyPatternCandidate(selectedCandidate);
        UpdateAtcControlState();
        UpdateATCBrakeLatch();

        // ATCブレーキ司令を送信
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
            allowedEmergencySpeedMS = currentLimitSpeedMS,
        };

        if (train == null || train.Graph == null || currentEdge == null)
        {
            return candidate;
        }

        float servicePatternDecelerationMS2 = GetPatternDecelerationMS2();
        float emergencyPatternDecelerationMS2 = trainSpec != null
            ? Mathf.Max(0f, trainSpec.GetEstimatedEmergencyBrakeDeceleration() - safetyDecelMS)
            : servicePatternDecelerationMS2;
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
                    servicePatternDecelerationMS2,
                    accumulatedDistM - safetyDistance
                );
                float emergencyAllowSpeedMS = ATCPatternCalculator.CalculateAllowSpeedMS(
                    nextLimitSpeedMS,
                    emergencyPatternDecelerationMS2,
                    accumulatedDistM - safetyDistance
                );

                if (allowSpeedMS < candidate.allowedSpeedMS)
                {
                    candidate.distanceM = accumulatedDistM;
                    candidate.targetSpeedMS = nextLimitSpeedMS;
                    candidate.allowedSpeedMS = allowSpeedMS;
                    candidate.allowedEmergencySpeedMS = emergencyAllowSpeedMS;
                }
            }

            accumulatedDistM += nextEdge.lengthM;
            edgeId = nextEdgeId;
            nextEdgeId = train.Graph.ResolveNextEdgeId(nextEdge.toNodeId, edgeId);
        }

        return candidate;
    }

    /// <summary>
    /// 役割: 前方在線閉塞を停止目標にした ATC 制限候補を組み立てます。
    /// </summary>
    /// <returns>在線閉塞由来の ATC 制限候補を返します。</returns>
    private AtcTargetCandidate BuildOccupiedBlockCandidate()
    {
        AtcTargetCandidate candidate = new AtcTargetCandidate
        {
            isValid = false,
            sourceLabel = "Occupied Block",
            distanceM = 0f,
            targetSpeedMS = 0f,
            allowedSpeedMS = 0f,
            allowedEmergencySpeedMS = 0f
        };

        if (train == null || train.Spec == null || blockOccupancyManager == null)
        {
            return candidate;
        }

        float decel = Mathf.Max(0f, train.Spec.GetBrakeDeceleration(5));
        float emergencyDecel = Mathf.Max(0f, train.Spec.GetEstimatedEmergencyBrakeDeceleration() - safetyDecelMS);

        if (!blockOccupancyManager.TryFindFirstOccupiedBlockAhead(
            train,
            out string occupiedBlockId,
            out float distanceToBlockM
        ))
        {
            return candidate;
        }

        float targetDistanceM = Mathf.Max(0f, distanceToBlockM - occupiedBlockSafetyMarginM);

        candidate.isValid = true;
        candidate.distanceM = targetDistanceM;
        candidate.targetSpeedMS = 0f;
        candidate.allowedSpeedMS = ATCPatternCalculator.CalculateAllowSpeedMS(
            0f,
            decel,
            targetDistanceM
        );
        candidate.allowedEmergencySpeedMS = ATCPatternCalculator.CalculateAllowSpeedMS(
            0f,
            emergencyDecel,
            targetDistanceM
        );

        return candidate;
    }

    /// <summary>
    /// 役割: 複数の ATC 制限候補から最も厳しい候補を選びます。
    /// </summary>
    /// <param name="atcTargetCandidates">比較対象の ATC 制限候補一覧を指定します。</param>
    /// <returns>最も厳しい ATC 制限候補を返します。</returns>
    private AtcTargetCandidate ChooseMoreRestrictive(List<AtcTargetCandidate> atcTargetCandidates)
    {
        AtcTargetCandidate selectedCandidate = new AtcTargetCandidate
        {
            isValid = false,
            sourceLabel = "Solved Speed",
            distanceM = 0f,
            targetSpeedMS = 0f,
            allowedSpeedMS = 0f,
            allowedEmergencySpeedMS = 0f
        };

        if (atcTargetCandidates == null || atcTargetCandidates.Count == 0)
        {
            return selectedCandidate;
        }

        foreach (AtcTargetCandidate atcTargetCandidate in atcTargetCandidates)
        {
            if (!atcTargetCandidate.isValid)
            {
                continue;
            }

            if (!selectedCandidate.isValid)
            {
                selectedCandidate = atcTargetCandidate;
                continue;
            }

            if (atcTargetCandidate.allowedSpeedMS < selectedCandidate.allowedSpeedMS)
            {
                selectedCandidate = atcTargetCandidate;
                continue;
            }

            if (Mathf.Approximately(atcTargetCandidate.allowedSpeedMS, selectedCandidate.allowedSpeedMS) &&
                atcTargetCandidate.allowedEmergencySpeedMS < selectedCandidate.allowedEmergencySpeedMS)
            {
                selectedCandidate = atcTargetCandidate;
                continue;
            }

            if (Mathf.Approximately(atcTargetCandidate.allowedSpeedMS, selectedCandidate.allowedSpeedMS) &&
                Mathf.Approximately(atcTargetCandidate.allowedEmergencySpeedMS, selectedCandidate.allowedEmergencySpeedMS) &&
                atcTargetCandidate.distanceM < selectedCandidate.distanceM)
            {
                selectedCandidate = atcTargetCandidate;
            }
        }

        return selectedCandidate;
    }

    /// <summary>
    /// 役割: 現在速度と常用・非常パターンのしきい値から ATC 状態を更新します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void UpdateAtcControlState()
    {
        if (train == null)
        {
            currentAtcState = AtcControlState.Normal;
            return;
        }

        bool hasAtcBrakeCommand = isATCBrakeLatched || train.ATCBrakeNotch > 0;
        if (!hasAtcBrakeCommand)
        {
            currentAtcState = AtcControlState.Normal;
            return;
        }

        float serviceThresholdMS = patternAllowSpeedMS + Mathf.Max(0f, overspeedToleranceMS);
        float emergencyThresholdMS = patternEmergencyAllowSpeedMS + Mathf.Max(0f, overspeedToleranceMS);

        if (train.SpeedMS > emergencyThresholdMS)
        {
            currentAtcState = AtcControlState.EmergencyPattern;
            return;
        }

        if (train.SpeedMS > serviceThresholdMS || hasAtcBrakeCommand)
        {
            currentAtcState = AtcControlState.ServicePattern;
            return;
        }

        currentAtcState = AtcControlState.Normal;
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
        patternEmergencyAllowSpeedMS = candidate.allowedEmergencySpeedMS;
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
        patternEmergencyAllowSpeedMS = 0f;
        patternTargetDistanceM = 0f;
        patternTargetSpeedMS = 0f;
        currentAtcState = AtcControlState.Normal;
    }
}
