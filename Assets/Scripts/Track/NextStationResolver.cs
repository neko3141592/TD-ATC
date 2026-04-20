using System.Collections.Generic;
using UnityEngine;

public class NextStationResolver : MonoBehaviour
{
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

        if (graph == null ||
            graph.stations == null ||
            service == null ||
            service.stops == null ||
            string.IsNullOrEmpty(currentEdgeId) ||
            lookaheadDistanceM < 0f)
        {
            return false;
        }

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
