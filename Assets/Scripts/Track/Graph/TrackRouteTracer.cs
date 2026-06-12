using System.Collections.Generic;
using UnityEngine;

public static class TrackRouteTracer
{
    public static bool TryTraceAhead(
        TrackGraph graph,
        string currentEdgeId,
        float distanceOnEdgeM,
        float lookaheadDistanceM,
        List<TrackTraceSegment> results,
        EdgeTravelDirection direction = EdgeTravelDirection.AtoB
    )
    {
        if (results == null)
        {
            return false;
        }

        results.Clear();

        if (graph == null || string.IsNullOrEmpty(currentEdgeId))
        {
            return false;
        }

        if (lookaheadDistanceM < 0f)
        {
            return false;
        }

        TrackEdge currentEdge = graph.FindEdge(currentEdgeId);
        if (currentEdge == null)
        {
            return false;
        }

        float remainingLookaheadM = lookaheadDistanceM;
        float initialEdgeLengthM = Mathf.Max(0f, currentEdge.lengthM);
        float currentDistanceOnEdgeM = Mathf.Clamp(distanceOnEdgeM, 0f, initialEdgeLengthM);
        float distanceFromOriginM = 0f;

        const int maxSegments = 256;
        int guard = 0;

        while (guard < maxSegments)
        {
            guard++;

            float edgeLengthM = Mathf.Max(0f, currentEdge.lengthM);
            float availableOnEdgeM = TrackGraphUndirectedHelpers.GetDistanceToExit(
                currentEdge,
                currentDistanceOnEdgeM,
                direction
            );
            float traceLengthM = Mathf.Min(availableOnEdgeM, remainingLookaheadM);

            if (traceLengthM > 0f)
            {
                AddTraceSegment(
                    results,
                    currentEdge.edgeId,
                    currentDistanceOnEdgeM,
                    traceLengthM,
                    distanceFromOriginM,
                    direction
                );

                remainingLookaheadM -= traceLengthM;
                distanceFromOriginM += traceLengthM;
                currentDistanceOnEdgeM += direction == EdgeTravelDirection.AtoB
                    ? traceLengthM
                    : -traceLengthM;
            }

            if (remainingLookaheadM <= 0f)
            {
                return true;
            }

            if (direction == EdgeTravelDirection.AtoB && currentDistanceOnEdgeM < edgeLengthM)
            {
                return true;
            }

            if (direction == EdgeTravelDirection.BtoA && currentDistanceOnEdgeM > 0f)
            {
                return true;
            }

            string currentNodeId = TrackGraphUndirectedHelpers.GetExitNodeId(currentEdge, direction);
            string nextEdgeId = TrackGraphUndirectedHelpers.ResolveConnectedEdge(graph, currentNodeId, currentEdgeId);

            if (string.IsNullOrEmpty(nextEdgeId))
            {
                return true;
            }

            TrackEdge nextEdge = graph.FindEdge(nextEdgeId);

            if (nextEdge == null)
            {
                return false;
            }

            EdgeTravelDirection nextDirection = TrackGraphUndirectedHelpers.GetTravelDirectionFromNode(nextEdge, currentNodeId);

            currentEdge = nextEdge;
            direction = nextDirection;
            currentDistanceOnEdgeM = TrackGraphUndirectedHelpers.GetEntryDistanceOnEdge(nextEdge, currentNodeId);
        }

        return false;
    }

    private static void AddTraceSegment(
        List<TrackTraceSegment> results,
        string edgeId,
        float currentDistanceOnEdgeM,
        float traceLengthM,
        float distanceFromOriginM,
        EdgeTravelDirection direction)
    {
        float startDistanceOnEdgeM = direction == EdgeTravelDirection.AtoB
            ? currentDistanceOnEdgeM
            : currentDistanceOnEdgeM - traceLengthM;
        float endDistanceOnEdgeM = direction == EdgeTravelDirection.AtoB
            ? currentDistanceOnEdgeM + traceLengthM
            : currentDistanceOnEdgeM;

        results.Add(new TrackTraceSegment
        {
            direction = direction,
            edgeId = edgeId,
            startDistanceOnEdgeM = startDistanceOnEdgeM,
            endDistanceOnEdgeM = endDistanceOnEdgeM,
            startDistanceFromOriginM = distanceFromOriginM,
            endDistanceFromOriginM = distanceFromOriginM + traceLengthM
        });
    }

    public static bool TryTraceBehind(
        TrackGraph graph,
        string currentEdgeId,
        float distanceOnEdgeM,
        float lookaheadDistanceM,
        List<TrackTraceSegment> results,
        EdgeTravelDirection direction = EdgeTravelDirection.AtoB
    )
    {
        EdgeTravelDirection behindDirection = TrackGraphUndirectedHelpers.GetOppositeDirection(direction);

        return TryTraceAhead(
            graph,
            currentEdgeId,
            distanceOnEdgeM,
            lookaheadDistanceM,
            results,
            behindDirection
        );
    }
}
