using System.Collections.Generic;
using UnityEngine;

public class NextStationResolver : MonoBehaviour
{
    // Reused buffers avoid per-frame allocations while tracing the route ahead and behind the train.
    private readonly List<TrackTraceSegment> aheadTraceSegments = new();
    private readonly List<TrackTraceSegment> behindTraceSegments = new();

    public bool TryGetNextStopStation(
        TrackGraph graph,
        TrainServiceDefinition service,
        int currentStopIndex,
        string currentEdgeId,
        float distanceOnEdgeM,
        float lookaheadDistanceM,
        out int resolvedStopIndex,
        out StationData station,
        out float distanceToStopM
    )
    {
        resolvedStopIndex = -1;
        station = null;
        distanceToStopM = 0f;

        // Every dependency must be valid because this resolver is used directly from runtime HUD/stop logic.
        if (graph == null ||
            graph.stations == null ||
            service == null ||
            service.stops == null ||
            string.IsNullOrEmpty(currentEdgeId) ||
            lookaheadDistanceM < 0f)
        {
            return false;
        }

        // Start searching from the current service index so already-completed stops are skipped.
        int startIndex = Mathf.Max(0, currentStopIndex);
        if (startIndex >= service.stops.Count)
        {
            return false;
        }

        bool hasAheadTrace = TrackRouteTracer.TryTraceAhead(
            graph,
            currentEdgeId,
            distanceOnEdgeM,
            lookaheadDistanceM,
            aheadTraceSegments
        );
        bool hasBehindTrace = TrackRouteTracer.TryTraceBehind(
            graph,
            currentEdgeId,
            distanceOnEdgeM,
            lookaheadDistanceM,
            behindTraceSegments
        );

        if (!hasAheadTrace && !hasBehindTrace)
        {
            return false;
        }

        // Walk the service definition in timetable order and return the first stop that exists on the traced route.
        for (int i = startIndex; i < service.stops.Count; i++)
        {
            ServiceStop serviceStop = service.stops[i];
            if (serviceStop == null ||
                !serviceStop.stopHere ||
                string.IsNullOrEmpty(serviceStop.stationId))
            {
                continue;
            }


            string searchStationId = serviceStop.stationId;
            StationData candidateStation = FindStation(graph, searchStationId);
            if (candidateStation == null || string.IsNullOrEmpty(candidateStation.edgeId))
            {
                return false;
            }

            if (hasAheadTrace &&
                TryGetDistanceInTrace(aheadTraceSegments, candidateStation, isBehindTrace: false, out distanceToStopM))
            {
                resolvedStopIndex = i;
                station = candidateStation;
                return true;
            }

            if (hasBehindTrace &&
                TryGetDistanceInTrace(behindTraceSegments, candidateStation, isBehindTrace: true, out distanceToStopM))
            {
                resolvedStopIndex = i;
                station = candidateStation;
                // A negative distance means the train has already passed the stop point on the traced route.
                distanceToStopM = -distanceToStopM;
                return true;
            }

        }

        resolvedStopIndex = -1;
        station = null;
        distanceToStopM = 0f;
        return false;
    }

    private static bool TryGetDistanceInTrace(
        List<TrackTraceSegment> traceSegments,
        StationData station,
        bool isBehindTrace,
        out float distanceToStopM
    )
    {
        // Find the traced segment that contains the station and convert that local position into distance from the train.
        for (int i = 0; i < traceSegments.Count; i++)
        {
            TrackTraceSegment segment = traceSegments[i];

            if (segment.edgeId != station.edgeId ||
                station.distanceFromEdgeStart < segment.startDistanceOnEdgeM ||
                station.distanceFromEdgeStart > segment.endDistanceOnEdgeM)
            {
                continue;
            }

            if (isBehindTrace)
            {
                distanceToStopM = segment.startDistanceFromOriginM + segment.endDistanceOnEdgeM - station.distanceFromEdgeStart;
            }
            else
            {
                distanceToStopM = segment.startDistanceFromOriginM + station.distanceFromEdgeStart - segment.startDistanceOnEdgeM;
            }

            return true;
        }

        distanceToStopM = 0f;
        return false;
    }

    private static StationData FindStation(TrackGraph graph, string stationId)
    {
        // TrackGraph.stations is small today, so a linear lookup is fine and keeps the data model simple.
        for (int i = 0; i < graph.stations.Count; i++)
        {
            StationData station = graph.stations[i];
            if (station != null && station.stationId == stationId)
            {
                return station;
            }
        }

        return null;
    }
}
