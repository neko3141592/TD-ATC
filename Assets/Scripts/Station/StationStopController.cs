using UnityEngine;

public enum StationStopJudgeState
{
    NoTarget,
    Approaching,
    InRange,
    Holding,
    Overshot,
}

public class StationStopController : MonoBehaviour
{
    // trackGraph は任意です。未設定なら train に割り当てられた TrackGraph をそのまま使います。
    [SerializeField] private TrackGraph trackGraph;
    [SerializeField] private TrainServiceDefinition service;
    [SerializeField] private TrainController train;
    [SerializeField] private NextStationResolver nextStationResolver;

    [SerializeField, Min(0f)] private float stationLookaheadDistanceM = 3000f;
    [SerializeField, Min(0f)] private float stopSpeedThresholdMS = 0.2f;
    [SerializeField, Min(0f)] private float stopHoldSeconds = 1.0f;

    private int currentStopIndex = 0;
    private int resolvedStopIndex = -1;
    private StationData currentTargetStation;
    private float distanceToStopM;
    private float stopHoldTimer;
    private StationStopJudgeState judgeState = StationStopJudgeState.NoTarget;
    private string lastCompletedStationName;
    private float lastCompletedStopErrorM;

    public int CurrentStopIndex => currentStopIndex;
    public int CuttentStopIndex => currentStopIndex;
    public int ResolvedStopIndex => resolvedStopIndex;
    public float DistanceToStopM => distanceToStopM;
    public float StopErrorM => distanceToStopM;
    public bool HasTargetStation => currentTargetStation != null;
    public StationData CurrentTargetStation => currentTargetStation;
    public string CurrentTargetStationName =>
        currentTargetStation != null && !string.IsNullOrEmpty(currentTargetStation.stationName)
            ? currentTargetStation.stationName
            : currentTargetStation != null
                ? currentTargetStation.stationId
                : "--";
    public float StopMarginM => currentTargetStation != null ? Mathf.Max(0f, currentTargetStation.stopMarginM) : 0f;
    public bool IsWithinStopRange => HasTargetStation && Mathf.Abs(distanceToStopM) <= StopMarginM;
    public bool IsStopSpeedSatisfied => train != null && Mathf.Abs(train.SpeedMS) <= stopSpeedThresholdMS;
    public bool IsHoldingForStop => IsWithinStopRange && IsStopSpeedSatisfied && stopHoldTimer > 0f;
    public float StopHoldTimer => stopHoldTimer;
    public float StopHoldSeconds => stopHoldSeconds;
    public float StopHoldProgress01 => stopHoldSeconds > 0f ? Mathf.Clamp01(stopHoldTimer / stopHoldSeconds) : 0f;
    public StationStopJudgeState JudgeState => judgeState;
    public string JudgeStateLabel => GetJudgeStateLabel(judgeState);
    public string LastCompletedStationName => string.IsNullOrEmpty(lastCompletedStationName) ? "--" : lastCompletedStationName;
    public float LastCompletedStopErrorM => lastCompletedStopErrorM;

    /// <summary>
    /// 役割: 毎フレームの更新処理を行います。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    void Update()
    {
        // 運転系の参照が揃っていない間は、HUD やデバッグ表示に出す状態を「対象なし」で初期化しておきます。
        if (train == null || nextStationResolver == null || service == null)
        {
            ClearTarget();
            return;
        }

        TrackGraph activeTrackGraph = trackGraph != null ? trackGraph : train.Graph;
        if (activeTrackGraph == null)
        {
            ClearTarget();
            return;
        }

        var currentEdgeId = train.CurrentEdgeId;
        var distanceOnEdgeM = train.DistanceOnEdgeM;
        var speedMS = train.SpeedMS;

        // 分岐切替の影響をすぐ反映できるよう、現在位置から毎フレーム次の停車駅を引き直します。
        if (nextStationResolver.TryGetNextStopStation(
            activeTrackGraph,
            service,
            currentStopIndex,
            currentEdgeId,
            distanceOnEdgeM,
            stationLookaheadDistanceM,
            out int nextResolvedStopIndex,
            out StationData station,
            out float nextDistanceToStopM
        ))
        {
            resolvedStopIndex = nextResolvedStopIndex;
            currentTargetStation = station;
            distanceToStopM = nextDistanceToStopM;
            UpdateJudgeState(speedMS);
            UpdateStopCompletion(speedMS);
        }
        else
        {
            ClearTarget();
        }
    }

    /// <summary>
    /// 役割: UpdateStopCompletion の処理を更新します。
    /// </summary>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void UpdateStopCompletion(float speedMS)
    {
        // サービスの停車インデックスが先に進んだ後の古い解決結果は無視します。
        if (currentTargetStation == null || resolvedStopIndex < currentStopIndex)
        {
            stopHoldTimer = 0f;
            return;
        }

        float stopMarginM = Mathf.Max(0f, currentTargetStation.stopMarginM);
        bool isWithinStopRange = Mathf.Abs(distanceToStopM) <= stopMarginM;
        bool isStopped = Mathf.Abs(speedMS) <= stopSpeedThresholdMS;

        // 停車完了とみなすには、停止位置に収まり、かつ短時間しっかり停止し続ける必要があります。
        if (!isWithinStopRange || !isStopped)
        {
            stopHoldTimer = 0f;
            return;
        }

        stopHoldTimer += Time.deltaTime;
        if (stopHoldTimer < stopHoldSeconds)
        {
            return;
        }

        // インデックスを進める前に結果を保持し、HUD やデバッグ表示で直前の成功停車を見られるようにします。
        lastCompletedStationName = CurrentTargetStationName;
        lastCompletedStopErrorM = distanceToStopM;
        currentStopIndex = resolvedStopIndex + 1;
        ClearTarget();
    }

    /// <summary>
    /// 役割: UpdateJudgeState の処理を更新します。
    /// </summary>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void UpdateJudgeState(float speedMS)
    {
        if (!HasTargetStation)
        {
            judgeState = StationStopJudgeState.NoTarget;
            return;
        }

        if (distanceToStopM < -StopMarginM)
        {
            judgeState = StationStopJudgeState.Overshot;
            return;
        }

        if (!IsWithinStopRange)
        {
            judgeState = StationStopJudgeState.Approaching;
            return;
        }

        // 停止許容範囲に入った後は、まだ転動中なのか、停止保持に入ったのかを分けて扱います。
        judgeState = Mathf.Abs(speedMS) <= stopSpeedThresholdMS
            ? StationStopJudgeState.Holding
            : StationStopJudgeState.InRange;
    }

    /// <summary>
    /// 役割: ClearTarget の処理をクリアします。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ClearTarget()
    {
        resolvedStopIndex = -1;
        currentTargetStation = null;
        distanceToStopM = 0f;
        stopHoldTimer = 0f;
        judgeState = StationStopJudgeState.NoTarget;
    }

    /// <summary>
    /// 役割: インスペクター変更時の値補正を行います。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void OnValidate()
    {
        stationLookaheadDistanceM = Mathf.Max(0f, stationLookaheadDistanceM);
        stopSpeedThresholdMS = Mathf.Max(0f, stopSpeedThresholdMS);
        stopHoldSeconds = Mathf.Max(0f, stopHoldSeconds);
    }

    /// <summary>
    /// 役割: GetJudgeStateLabel の処理を取得します。
    /// </summary>
    /// <param name="state">state を指定します。</param>
    /// <returns>文字列結果を返します。</returns>
    private static string GetJudgeStateLabel(StationStopJudgeState state)
    {
        switch (state)
        {
            case StationStopJudgeState.Approaching:
                return "Approaching";
            case StationStopJudgeState.InRange:
                return "In Range";
            case StationStopJudgeState.Holding:
                return "Holding";
            case StationStopJudgeState.Overshot:
                return "Overshot";
            default:
                return "No Target";
        }
    }
}
