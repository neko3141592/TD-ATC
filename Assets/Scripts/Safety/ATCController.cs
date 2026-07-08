using System.Collections.Generic;
using UnityEngine;

public enum ATCSignalAspect
{
    Green,
    Red,
    Off
}

public class ATCController : MonoBehaviour, ITimsDataSource
{
    private enum AtcControlState
    {
        Normal,
        ServicePattern,
        EmergencyPattern,
        ORP,
    }

    private enum AtcMode
    {
        Normal,
        Emergency,
        CutOut,
    }

    private const float MinimumEmergencyPatternGapKmH = 10f;
    private const float StopIndicationThresholdKmH = 0.5f;

    [Header("References")]
    [SerializeField] private TimsSystem tims;
    [SerializeField] private TrainController train;
    [SerializeField] private TrainSpec trainSpec;
    [SerializeField] private BlockOccupancyManager blockOccupancyManager;
    [SerializeField] private TrainServiceDefinition trainService;

    [Header("Runtime Status (Debug)")]
    [SerializeField] private float currentLimitSpeedMS = 0f;
    [SerializeField] private float patternAllowSpeedMS = 0f;
    [SerializeField] private float patternEmergencyAllowSpeedMS = 0f;
    [SerializeField] private float patternTargetDistanceM = 0f;
    [SerializeField] private float patternTargetSpeedMS = 0f;
    [SerializeField] private string currentPatternSourceLabel = "--";
    [SerializeField] private bool isNextBlockOccupied = false;
    [SerializeField] private string nextBlockSignalBlockId = "--";
    [SerializeField] private float nextBlockSignalDistanceM = 0f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip dingClip;

    [Header("ATC Mode")]
    [SerializeField] private AtcMode atcMode = AtcMode.Normal;

    [Header("Emergency Operation")]
    [SerializeField] private float emergencyOperationMaxSpeedMS = 4.167f;
    private bool emergencyOperationBrakeHolding = false;
    

    [Header("Pattern / ATC Tuning")]
    [SerializeField] private float limitChangeEpsilonMS = 0.01f;
    [SerializeField] private int patternBrakeNotch = 4;
    [SerializeField] private float fallbackPatternDecelerationMS2 = 1.8f;
    [SerializeField] private float atcReleaseMarginKmH = 3f;
    [SerializeField] private float overspeedToleranceMS = 0.1f;
    [SerializeField] private float safetyMarginKmH = 5f;
    [SerializeField] private float patternApproachLampOnMarginKmH = 5f;
    [SerializeField] private float patternApproachLampOffMarginKmH = 7f;

    [Header("Safety Margins")]
    [SerializeField] private float safetyDistance = 50f;
    [SerializeField] private float safetyDecelMS = 0.1f;
    [SerializeField] private float occupiedBlockSafetyMarginM = 50f;

    [Header("Brake Command")]    
    [SerializeField] private int atcBrakeNotch = 7;

    [Header("ATC Emergency Release")]
    [SerializeField] private float atcEmergencyReleaseMaxSpeedMS = 0.01f;

    private bool hasPreviousLimit = false;
    private float previousLimitSpeedMS = 0f;
    private bool isATCBrakeLatched = false;
    private int previousManualBrakeNotch = 0;
    private int currentAtcBrakeStep = 0;
    [SerializeField] private AtcControlState currentAtcState = AtcControlState.Normal;
    [SerializeField] private bool isPatternApproachLampActive = false;
    private bool hasPreviousNextBlockSignalState = false;
    private bool hasPreviousPatternAllowSpeed = false;
    private float previousPatternAllowSpeedMS = 0f;
    private readonly List<AtcTargetCandidate> candidateBuffer = new();
    private readonly List<TrackTraceSegment> speedLimitTraceBuffer = new();
    private AtcTargetCandidate lastSpeedLimitCandidate;
    private AtcTargetCandidate lastOccupiedBlockCandidate;
    private AtcTargetCandidate lastServiceSpeedLimitCandidate;
    private AtcTargetCandidate lastSelectedCandidate;
    private string lastSpeedLimitRawTargetDebugLabel = "Raw Speed Limit: --";

    public float CurrentLimitSpeedKmH => currentLimitSpeedMS * 3.6f;
    public float CurrentPatternAllowSpeedKmH => patternAllowSpeedMS * 3.6f;
    public float CurrentPatternEmergencyAllowSpeedKmH => patternEmergencyAllowSpeedMS * 3.6f;
    public float CurrentPatternTargetDistanceM => patternTargetDistanceM;
    public float CurrentPatternTargetSpeedKmH => patternTargetSpeedMS * 3.6f;
    public string CurrentPatternSourceLabel => currentPatternSourceLabel;
    public bool IsPatternApproaching => isPatternApproachLampActive;
    public string CurrentAtcStateLabel => currentAtcState.ToString();
    public bool IsEmergencyOperationActive => atcMode == AtcMode.Emergency;
    public bool IsAtcCutOutActive => atcMode == AtcMode.CutOut;
    public bool IsAtcBrakeLatched => isATCBrakeLatched;
    public bool IsAtcServiceBrakeActive => currentAtcState == AtcControlState.ServicePattern;
    public bool IsAtcEmergencyBrakeActive => currentAtcState == AtcControlState.EmergencyPattern;
    public bool IsNextBlockOccupied => isNextBlockOccupied;
    public ATCSignalAspect CurrentSignalAspect => ResolveSignalAspect();
    public string NextBlockSignalBlockId => nextBlockSignalBlockId;
    public float NextBlockSignalDistanceM => nextBlockSignalDistanceM;
    public string SpeedLimitCandidateDebugLabel => FormatCandidateDebugLabel(lastSpeedLimitCandidate);
    public string OccupiedBlockCandidateDebugLabel => FormatCandidateDebugLabel(lastOccupiedBlockCandidate);
    public string ServiceSpeedLimitCandidateDebugLabel => FormatCandidateDebugLabel(lastServiceSpeedLimitCandidate);
    public string SelectedCandidateDebugLabel => FormatCandidateDebugLabel(lastSelectedCandidate);
    public string SpeedLimitRawTargetDebugLabel => lastSpeedLimitRawTargetDebugLabel;
    public float TransmissionIntervalSeconds => 0.05f;

    private ATCSignalAspect ResolveSignalAspect()
    {
        if (IsAtcCutOutActive)
        {
            return ATCSignalAspect.Off;
        }

        if (isNextBlockOccupied || (HasAtcIndication && CurrentPatternAllowSpeedKmH <= StopIndicationThresholdKmH))
        {
            return ATCSignalAspect.Red;
        }

        return ATCSignalAspect.Green;
    }

    private bool HasAtcIndication => !string.IsNullOrEmpty(currentPatternSourceLabel) && currentPatternSourceLabel != "--";

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

        if (train == null || train.Graph == null || string.IsNullOrEmpty(train.CurrentEdgeId) || trainService == null)
        {
            ResetPatternState();
            hasPreviousLimit = false;
            isATCBrakeLatched = false;
            previousManualBrakeNotch = train != null ? train.ManualBrakeNotch : 0;
            SetATCBrake(0);
            return;
        }

        TrackEdge currentEdge = train.Graph.FindEdge(train.CurrentEdgeId);
        float nextLimitSpeedMS = currentEdge != null ? currentEdge.speedLimitMS : 0f;

        bool isLimitRaised =
            hasPreviousLimit &&
            nextLimitSpeedMS > previousLimitSpeedMS + Mathf.Max(0f, limitChangeEpsilonMS);

        if (isLimitRaised)
        {
            PlayDing();
        }

        if (atcMode == AtcMode.CutOut)
        {
            ResetPatternState();
            hasPreviousLimit = false;
            isATCBrakeLatched = false;
            emergencyOperationBrakeHolding = false;
            previousManualBrakeNotch = train.ManualBrakeNotch;
            SetATCBrake(0);
            return;
        }

        currentLimitSpeedMS = nextLimitSpeedMS;
        previousLimitSpeedMS = nextLimitSpeedMS;
        hasPreviousLimit = true;

        AtcTargetCandidate speedLimitCandidate = BuildSpeedLimitCandidate(currentEdge);
        AtcTargetCandidate occupiedBlockCandidate = BuildOccupiedBlockCandidate();
        AtcTargetCandidate serviceSpeedLimitCandidate = BuildServiceSpeedLimitCandidate();
        UpdateNextBlockSignalState();
        lastSpeedLimitCandidate = speedLimitCandidate;
        lastOccupiedBlockCandidate = occupiedBlockCandidate;
        lastServiceSpeedLimitCandidate = serviceSpeedLimitCandidate;

        candidateBuffer.Clear();
        candidateBuffer.Add(speedLimitCandidate);
        candidateBuffer.Add(occupiedBlockCandidate);
        candidateBuffer.Add(serviceSpeedLimitCandidate);

        AtcTargetCandidate selectedCandidate = ChooseMoreRestrictive(candidateBuffer);
        lastSelectedCandidate = selectedCandidate;
        ApplyPatternCandidate(selectedCandidate);
        UpdatePatternRaiseDingState();
        UpdatePatternApproachLampState();
        UpdateAtcControlState();
        UpdateATCBrakeLatch();
        UpdateAtcEmergencyReleaseSequence();

        // ATCブレーキ司令をTIMSへ送信する。非常パターン時だけ非常ノッチに切り替える。
        SetATCBrake(ResolveATCBrakeCommandNotch());
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
    /// 役割: SetATCBrake の処理を実行します。
    /// </summary>
    /// <param name="brakeNotch">brakeNotch を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void SetATCBrake(int brakeNotch)
    {
        currentAtcBrakeStep = ConvertBrakeNotchToStep(brakeNotch);
    }

    /// <summary>
    /// 役割: 現在のATC状態から、TIMSへ送信するATCブレーキノッチを決定します。
    /// </summary>
    /// <returns>送信するATCブレーキノッチを返します。</returns>
    
    private int ResolveATCBrakeCommandNotch()
    {
        if (train == null)
        {
            return 0;
        }

        if (atcMode == AtcMode.Emergency)
        {
            if (emergencyOperationBrakeHolding)
            {
                return train.EmergencyBrakeNotch;
            } else
            {
                return 0;
            }
        }

        if (currentAtcState == AtcControlState.EmergencyPattern)
        {
            return train.EmergencyBrakeNotch;
        }

        
        bool shouldApplyATCBrake = isATCBrakeLatched || isLocking();
        if (!shouldApplyATCBrake)
        {
            return 0;
        } 

        return atcBrakeNotch;
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
    /// 役割: ATC非常中に、手動ブレーキが非常位置から常用位置へ戻った瞬間を検出して緩解します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void UpdateAtcEmergencyReleaseSequence()
    {
        if (train == null)
        {
            previousManualBrakeNotch = 0;
            return;
        }

        int manualBrakeNotch = train.ManualBrakeNotch;
        int emergencyBrakeNotch = Mathf.Max(1, train.EmergencyBrakeNotch);

        bool movedFromEmergencyToService =
            previousManualBrakeNotch >= emergencyBrakeNotch &&
            manualBrakeNotch > 0 &&
            manualBrakeNotch < emergencyBrakeNotch;

        bool canReleaseBySpeed =
            train.SpeedMS < Mathf.Max(0f, atcEmergencyReleaseMaxSpeedMS);

        if (movedFromEmergencyToService && canReleaseBySpeed)
        {
            currentAtcState = AtcControlState.Normal;
            emergencyOperationBrakeHolding = false;
            isATCBrakeLatched = false;
        }



        previousManualBrakeNotch = manualBrakeNotch;
    }

    /// <summary>
    /// 役割: 現在位置から見た速度制限候補を組み立てます。
    /// </summary>
    /// <param name="currentEdge">現在走行中のエッジを指定します。</param>
    /// <returns>速度制限由来の ATC 制限候補を返します。</returns>
    private AtcTargetCandidate BuildSpeedLimitCandidate(TrackEdge currentEdge)
    {
        lastSpeedLimitRawTargetDebugLabel = "Raw Speed Limit: --";
        AtcTargetCandidate candidate = new AtcTargetCandidate
        {
            isValid = true,
            sourceLabel = "Speed Limit",
            distanceM = 0f,
            targetSpeedMS = currentLimitSpeedMS,
            allowedSpeedMS = currentLimitSpeedMS,
            allowedEmergencySpeedMS = EnsureEmergencyPatternAboveServicePattern(
                currentLimitSpeedMS,
                currentLimitSpeedMS
            ),
        };

        if (train == null || train.Graph == null || currentEdge == null)
        {
            return candidate;
        }

        float servicePatternDecelerationMS2 = GetPatternDecelerationMS2();
        float emergencyPatternDecelerationMS2 = trainSpec != null
            ? Mathf.Max(0f, trainSpec.GetEstimatedEmergencyBrakeDeceleration() - safetyDecelMS)
            : servicePatternDecelerationMS2;
        float lookaheadLimitM = 1000f;
        if (!TrackRouteTracer.TryTraceAhead(
                train.Graph,
                currentEdge.edgeId,
                train.DistanceOnEdgeM,
                lookaheadLimitM,
                speedLimitTraceBuffer,
                train.CurrentMovementDirection
            ))
        {
            lastSpeedLimitRawTargetDebugLabel = "Raw Speed Limit: trace failed";
            return candidate;
        }

        bool hasRawSpeedLimitTarget = false;
        for (int i = 0; i < speedLimitTraceBuffer.Count; i++)
        {
            TrackTraceSegment segment = speedLimitTraceBuffer[i];
            TrackEdge aheadEdge = train.Graph.FindEdge(segment.edgeId);
            if (aheadEdge == null)
            {
                continue;
            }

            float aheadLimitSpeedMS = aheadEdge.speedLimitMS;
            if (aheadLimitSpeedMS < currentLimitSpeedMS)
            {
                float targetDistanceM = Mathf.Max(0f, segment.startDistanceFromOriginM);
                float allowSpeedMS = ATCPatternCalculator.CalculateAllowSpeedMS(
                    aheadLimitSpeedMS,
                    servicePatternDecelerationMS2,
                    targetDistanceM - safetyDistance
                );
                float rawEmergencyAllowSpeedMS = ATCPatternCalculator.CalculateAllowSpeedMS(
                    aheadLimitSpeedMS,
                    emergencyPatternDecelerationMS2,
                    targetDistanceM - safetyDistance
                );

                float emergencyAllowSpeedMS = EnsureEmergencyPatternAboveServicePattern(
                    rawEmergencyAllowSpeedMS,
                    allowSpeedMS
                );

                if (!hasRawSpeedLimitTarget)
                {
                    bool isAdoptable = allowSpeedMS < currentLimitSpeedMS;
                    lastSpeedLimitRawTargetDebugLabel =
                        $"Raw Speed Limit: {aheadEdge.edgeId} D {targetDistanceM:0.0} m / RawA {allowSpeedMS * 3.6f:0.0} km/h / T {aheadLimitSpeedMS * 3.6f:0.0} km/h / Adopt {(isAdoptable ? "Y" : "N")}";
                    hasRawSpeedLimitTarget = true;
                }

                if (allowSpeedMS < candidate.allowedSpeedMS)
                {
                    candidate.distanceM = targetDistanceM;
                    candidate.targetSpeedMS = aheadLimitSpeedMS;
                    candidate.allowedSpeedMS = allowSpeedMS;
                    candidate.allowedEmergencySpeedMS = emergencyAllowSpeedMS;
                }
            }
        }

        return candidate;
    }


    private AtcTargetCandidate BuildServiceSpeedLimitCandidate ()
    {
        AtcTargetCandidate candidate = new AtcTargetCandidate
        {
            isValid = true,
            sourceLabel = "Service Speed Limit",
            distanceM = 0f,
            targetSpeedMS = trainService.speedLimit,
            allowedSpeedMS = trainService.speedLimit,
            allowedEmergencySpeedMS = 120f,
        };

        return candidate;
    }

    /// <summary>
    /// 役割: 信号現示用に、直近の次閉塞が他列車で埋まっているか更新します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void UpdateNextBlockSignalState()
    {
        bool wasNextBlockOccupied = isNextBlockOccupied;
        isNextBlockOccupied = false;
        nextBlockSignalBlockId = "--";
        nextBlockSignalDistanceM = 0f;

        if (train == null || blockOccupancyManager == null)
        {
            hasPreviousNextBlockSignalState = false;
            return;
        }

        if (blockOccupancyManager.TryFindFirstOccupiedBlockAhead(
            train,
            out string occupiedBlockId,
            out float distanceToBlockM,
            nextBlockOnly: true
        ))
        {
            isNextBlockOccupied = true;
            nextBlockSignalBlockId = occupiedBlockId;
            nextBlockSignalDistanceM = distanceToBlockM;
        }

        if (hasPreviousNextBlockSignalState && !wasNextBlockOccupied && isNextBlockOccupied)
        {
            PlayDing();
        }

        hasPreviousNextBlockSignalState = true;
    }

    /// <summary>
    /// 役割: ATC パターン許容速度が上がった瞬間に通知音を鳴らします。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void UpdatePatternRaiseDingState()
    {
        if (!hasPreviousPatternAllowSpeed)
        {
            previousPatternAllowSpeedMS = patternAllowSpeedMS;
            hasPreviousPatternAllowSpeed = true;
            return;
        }

        if (patternAllowSpeedMS > previousPatternAllowSpeedMS + Mathf.Max(0f, limitChangeEpsilonMS))
        {
            PlayDing();
        }

        previousPatternAllowSpeedMS = patternAllowSpeedMS;
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

        float decel = Mathf.Max(0f, train.Spec.GetBrakeDeceleration(patternBrakeNotch));
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
        candidate.sourceLabel = string.IsNullOrEmpty(occupiedBlockId)
            ? "Occupied Block"
            : $"Occupied Block {occupiedBlockId} RawD {distanceToBlockM:0.0} m";
        candidate.distanceM = targetDistanceM;
        candidate.targetSpeedMS = 0f;
        candidate.allowedSpeedMS = ATCPatternCalculator.CalculateAllowSpeedMS(
            0f,
            decel,
            targetDistanceM
        );
        float rawEmergencyAllowSpeedMS = ATCPatternCalculator.CalculateAllowSpeedMS(
            0f,
            emergencyDecel,
            targetDistanceM
        );
        candidate.allowedEmergencySpeedMS = EnsureEmergencyPatternAboveServicePattern(
            rawEmergencyAllowSpeedMS,
            candidate.allowedSpeedMS
        );

        return candidate;
    }

    private float EnsureEmergencyPatternAboveServicePattern(float emergencyAllowSpeedMS, float serviceAllowSpeedMS)
    {
        float minimumGapMS = Mathf.Max(safetyMarginKmH, MinimumEmergencyPatternGapKmH) / 3.6f;
        return Mathf.Max(emergencyAllowSpeedMS, serviceAllowSpeedMS + minimumGapMS);
    }

    /// <summary>
    /// 役割: パターンが0のときに、電車を発進できないようにします。
    /// </summary>
    /// <returns></returns>
    private bool isLocking()
    {

        if (patternAllowSpeedMS < 0.1f && train.SpeedKmH < 0.5f)
        {
            return true;
        }

        return false;
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
            currentAtcState = AtcControlState.EmergencyPattern;
            return;
        }

        if (atcMode == AtcMode.Emergency && train.SpeedMS > emergencyOperationMaxSpeedMS)
        {
            emergencyOperationBrakeHolding = true;
        }

        if (currentAtcState == AtcControlState.EmergencyPattern)
        {
            return;
        }

        bool hasAtcBrakeCommand = isATCBrakeLatched || isLocking() || currentAtcBrakeStep > 0;
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

        if (isLocking())
        {
            currentAtcState = AtcControlState.ServicePattern;
            return;
        }

        currentAtcState = AtcControlState.Normal;
    }

    /// <summary>
    /// 役割: 前方接近ランプの点灯状態をヒステリシス付きで更新します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void UpdatePatternApproachLampState()
    {
        if (train == null)
        {
            isPatternApproachLampActive = false;
            return;
        }

        bool isPatternLowering = patternAllowSpeedMS < currentLimitSpeedMS;
        if (!isPatternLowering)
        {
            isPatternApproachLampActive = false;
            return;
        }

        if (train.SpeedMS <= patternTargetSpeedMS)
        {
            isPatternApproachLampActive = false;
            return;
        }

        float speedDeltaKmH = Mathf.Abs(train.SpeedKmH - CurrentPatternAllowSpeedKmH);
        float onMarginKmH = Mathf.Max(0f, patternApproachLampOnMarginKmH);
        float offMarginKmH = Mathf.Max(onMarginKmH, patternApproachLampOffMarginKmH);

        if (!isPatternApproachLampActive)
        {
            isPatternApproachLampActive = speedDeltaKmH <= onMarginKmH;
            return;
        }

        isPatternApproachLampActive = speedDeltaKmH <= offMarginKmH;
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
        currentPatternSourceLabel = string.IsNullOrEmpty(candidate.sourceLabel) ? "--" : candidate.sourceLabel;
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

    private string FormatCandidateDebugLabel(AtcTargetCandidate candidate)
    {
        string source = string.IsNullOrEmpty(candidate.sourceLabel) ? "--" : candidate.sourceLabel;
        if (!candidate.isValid)
        {
            return $"{source}: --";
        }

        return $"{source}: D {candidate.distanceM:0.0} m / A {candidate.allowedSpeedMS * 3.6f:0.0} km/h / T {candidate.targetSpeedMS * 3.6f:0.0} km/h";
    }

    private void ResetCandidateDebugState()
    {
        lastSpeedLimitCandidate = new AtcTargetCandidate { sourceLabel = "Speed Limit" };
        lastOccupiedBlockCandidate = new AtcTargetCandidate { sourceLabel = "Occupied Block" };
        lastServiceSpeedLimitCandidate = new AtcTargetCandidate { sourceLabel = "Service Speed Limit" };
        lastSelectedCandidate = new AtcTargetCandidate { sourceLabel = "Selected" };
        lastSpeedLimitRawTargetDebugLabel = "Raw Speed Limit: --";
    }

    /// <summary>
    /// 役割: ResolveRuntimeReferences の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ResolveRuntimeReferences()
    {
        if (tims == null)
        {
            tims = GetComponentInParent<TimsSystem>();
        }

        if (train == null)
        {
            train = GetComponentInParent<TrainController>();
        }

        if (trainSpec == null && train != null)
        {
            trainSpec = train.Spec;
        }
    }

    private int ConvertBrakeNotchToStep(int brakeNotch)
    {
        if (brakeNotch <= 0)
        {
            return 0;
        }

        int subStepCount = tims != null && tims.ControlConfig != null
            ? tims.ControlConfig.brakeSubstepCount
            : 1;

        TimsNotchHelper.ToContinuousBrakeNotch(
            brakeNotch,
            0,
            Mathf.Max(1, subStepCount),
            out int brakeStep
        );

        return Mathf.Max(0, brakeStep);
    }

    public void WriteTimsData(TimsCarTerminal terminal)
    {
        if (terminal == null)
        {
            return;
        }

        TimsDataBus localBus = terminal.LocalBus;
        localBus.SetInt(new TimsTagKey("ATC", "BrakeStep"), currentAtcBrakeStep);
        localBus.SetFloat(new TimsTagKey("ATC", "LimitSpeedKmh"), CurrentLimitSpeedKmH);
        localBus.SetFloat(new TimsTagKey("ATC", "PatternAllowSpeedKmh"), CurrentPatternAllowSpeedKmH);
        localBus.SetFloat(new TimsTagKey("ATC", "PatternEmergencyAllowSpeedKmh"), CurrentPatternEmergencyAllowSpeedKmH);
        localBus.SetFloat(new TimsTagKey("ATC", "PatternTargetDistanceM"), CurrentPatternTargetDistanceM);
        localBus.SetString(new TimsTagKey("ATC", "State"), CurrentAtcStateLabel);
        localBus.SetString(new TimsTagKey("ATC", "SignalAspect"), CurrentSignalAspect.ToString());
        localBus.SetBool(new TimsTagKey("ATC", "IsBrakeLatched"), isATCBrakeLatched);
        localBus.SetBool(new TimsTagKey("ATC", "IsServiceBrakeActive"), IsAtcServiceBrakeActive);
        localBus.SetBool(new TimsTagKey("ATC", "IsEmergencyBrakeActive"), IsAtcEmergencyBrakeActive);
        localBus.SetBool(new TimsTagKey("ATC", "IsPatternApproaching"), isPatternApproachLampActive);
        localBus.SetBool(new TimsTagKey("ATC", "IsCutOut"), IsAtcCutOutActive);
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
        currentPatternSourceLabel = "--";
        currentAtcState = AtcControlState.Normal;
        isPatternApproachLampActive = false;
        isNextBlockOccupied = false;
        nextBlockSignalBlockId = "--";
        nextBlockSignalDistanceM = 0f;
        hasPreviousNextBlockSignalState = false;
        hasPreviousPatternAllowSpeed = false;
        previousPatternAllowSpeedMS = 0f;
        ResetCandidateDebugState();
    }
}
