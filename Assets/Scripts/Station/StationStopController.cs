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
    // trackGraph is optional. Leaving it empty lets the controller follow the graph already assigned to the train.
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

    void Update()
    {
        // If the driving stack is not ready, keep the public state in a clean "no target" state for HUD/debug views.
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

        // Resolve the next stop every frame from the train's current route position so turnout changes are reflected immediately.
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

    private void UpdateStopCompletion(float speedMS)
    {
        // Ignore stale resolver results after the service index has already advanced.
        if (currentTargetStation == null || resolvedStopIndex < currentStopIndex)
        {
            stopHoldTimer = 0f;
            return;
        }

        float stopMarginM = Mathf.Max(0f, currentTargetStation.stopMarginM);
        bool isWithinStopRange = Mathf.Abs(distanceToStopM) <= stopMarginM;
        bool isStopped = Mathf.Abs(speedMS) <= stopSpeedThresholdMS;

        // The train must both stop accurately and stay settled for a short time before the stop counts as completed.
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

        // Cache the result before advancing so HUD/debug views can show the last successful stop.
        lastCompletedStationName = CurrentTargetStationName;
        lastCompletedStopErrorM = distanceToStopM;
        currentStopIndex = resolvedStopIndex + 1;
        ClearTarget();
    }

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

        // Once the train is inside the stop window, distinguish "still rolling" from "holding the stop".
        judgeState = Mathf.Abs(speedMS) <= stopSpeedThresholdMS
            ? StationStopJudgeState.Holding
            : StationStopJudgeState.InRange;
    }

    private void ClearTarget()
    {
        resolvedStopIndex = -1;
        currentTargetStation = null;
        distanceToStopM = 0f;
        stopHoldTimer = 0f;
        judgeState = StationStopJudgeState.NoTarget;
    }

    private void OnValidate()
    {
        stationLookaheadDistanceM = Mathf.Max(0f, stationLookaheadDistanceM);
        stopSpeedThresholdMS = Mathf.Max(0f, stopSpeedThresholdMS);
        stopHoldSeconds = Mathf.Max(0f, stopHoldSeconds);
    }

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
