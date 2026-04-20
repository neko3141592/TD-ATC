using System.Collections.Generic;
using UnityEngine;

public static class TrackRouteTracer
{
    public static bool TryTraceAhead(
        TrackGraph graph,
        string currentEdgeId,
        float distanceOnEdgeM,
        float lookaheadDistanceM,
        List<TrackTraceSegment> results
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
            float availableOnEdgeM = Mathf.Max(0f, edgeLengthM - currentDistanceOnEdgeM);
            float traceLengthM = Mathf.Min(availableOnEdgeM, remainingLookaheadM);

            if (traceLengthM > 0f)
            {
                results.Add(new TrackTraceSegment
                {
                    edgeId = currentEdge.edgeId,
                    startDistanceOnEdgeM = currentDistanceOnEdgeM,
                    endDistanceOnEdgeM = currentDistanceOnEdgeM + traceLengthM,
                    startDistanceFromOriginM = distanceFromOriginM,
                    endDistanceFromOriginM = distanceFromOriginM + traceLengthM
                });

                remainingLookaheadM -= traceLengthM;
                distanceFromOriginM += traceLengthM;
                currentDistanceOnEdgeM += traceLengthM;
            }

            if (remainingLookaheadM <= 0f)
            {
                return true;
            }

            if (currentDistanceOnEdgeM < edgeLengthM)
            {
                return true;
            }

            string nextEdgeId = graph.ResolveNextEdgeId(currentEdge.toNodeId, currentEdge.edgeId);
            if (string.IsNullOrEmpty(nextEdgeId))
            {
                return true;
            }

            TrackEdge nextEdge = graph.FindEdge(nextEdgeId);
            if (nextEdge == null)
            {
                return false;
            }

            currentEdge = nextEdge;
            currentDistanceOnEdgeM = 0f;
        }

        return false;
    }

    public static bool TryTraceBehind(
        TrackGraph graph,
        string currentEdgeId,
        float distanceOnEdgeM,
        float lookaheadDistanceM,
        List<TrackTraceSegment> results
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

            float availableOnEdgeM = Mathf.Max(0f, currentDistanceOnEdgeM);
            float traceLengthM = Mathf.Min(availableOnEdgeM, remainingLookaheadM);

            if (traceLengthM > 0f)
            {
                results.Add(new TrackTraceSegment
                {
                    edgeId = currentEdge.edgeId,
                    startDistanceOnEdgeM = currentDistanceOnEdgeM - traceLengthM,
                    endDistanceOnEdgeM = currentDistanceOnEdgeM,
                    startDistanceFromOriginM = distanceFromOriginM,
                    endDistanceFromOriginM = distanceFromOriginM + traceLengthM
                });

                remainingLookaheadM -= traceLengthM;
                distanceFromOriginM += traceLengthM;
                currentDistanceOnEdgeM -= traceLengthM;
            }

            if (remainingLookaheadM <= 0f)
            {
                return true;
            }

            if (currentDistanceOnEdgeM > 0f)
            {
                return true;
            }

            string previousEdgeId = graph.ResolvePreviousEdgeId(currentEdge.fromNodeId, currentEdge.edgeId);
            if (string.IsNullOrEmpty(previousEdgeId))
            {
                return true;
            }

            TrackEdge previousEdge = graph.FindEdge(previousEdgeId);
            if (previousEdge == null)
            {
                return false;
            }

            currentEdge = previousEdge;
            currentDistanceOnEdgeM = Mathf.Max(0f, currentEdge.lengthM);
        }

        return false;
    }
}
