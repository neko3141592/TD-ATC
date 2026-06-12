using System.Collections.Generic;
using UnityEngine;

public static class TrackOffsetDistanceMapBuilder
{
    public static TrackOffsetDistanceMap Build(
        TrackGraph graph,
        TrackRuntimeResolver resolver,
        string baseEdgeId,
        List<TrackOffsetSegment> offsetSegments,
        float sampleIntervalM,
        float integrationStepM = 0.05f)
    {
        TrackOffsetDistanceMap map = new TrackOffsetDistanceMap
        {
            sampleIntervalM = Mathf.Max(0.001f, sampleIntervalM),
            baseDistanceByOffsetIndex = new()
        };

        if (graph == null || resolver == null || string.IsNullOrEmpty(baseEdgeId))
        {
            return map;
        }

        TrackEdge baseEdge = graph.FindEdge(baseEdgeId);
        if (baseEdge == null || string.IsNullOrEmpty(baseEdge.geometryId))
        {
            return map;
        }

        TrackGeometry baseGeometry = graph.FindGeometry(baseEdge.geometryId);
        if (baseGeometry == null || baseGeometry.lengthM <= 0f)
        {
            return map;
        }

        integrationStepM = Mathf.Max(0.001f, integrationStepM);

        float previousBaseDistanceM = 0f;
        if (!TryResolveOffsetPosition(
            graph,
            resolver,
            baseEdgeId,
            offsetSegments,
            previousBaseDistanceM,
            out Vector3 previousOffsetPosition))
        {
            return map;
        }

        map.baseDistanceByOffsetIndex.Add(0f);

        double accumulatedOffsetDistanceM = 0.0;
        double nextSampleOffsetDistanceM = map.sampleIntervalM;

        while (previousBaseDistanceM < baseGeometry.lengthM)
        {
            float nextBaseDistanceM = Mathf.Min(
                previousBaseDistanceM + integrationStepM,
                baseGeometry.lengthM
            );

            if (!TryResolveOffsetPosition(
                graph,
                resolver,
                baseEdgeId,
                offsetSegments,
                nextBaseDistanceM,
                out Vector3 nextOffsetPosition))
            {
                break;
            }

            double previousAccumulatedOffsetDistanceM = accumulatedOffsetDistanceM;
            accumulatedOffsetDistanceM += Vector3.Distance(previousOffsetPosition, nextOffsetPosition);

            while (nextSampleOffsetDistanceM <= accumulatedOffsetDistanceM)
            {
                double spanM = accumulatedOffsetDistanceM - previousAccumulatedOffsetDistanceM;
                double t = spanM > 0.000001
                    ? (nextSampleOffsetDistanceM - previousAccumulatedOffsetDistanceM) / spanM
                    : 0.0;

                float sampledBaseDistanceM = Mathf.Lerp(
                    previousBaseDistanceM,
                    nextBaseDistanceM,
                    (float)t
                );

                map.baseDistanceByOffsetIndex.Add(sampledBaseDistanceM);
                nextSampleOffsetDistanceM += map.sampleIntervalM;
            }

            previousBaseDistanceM = nextBaseDistanceM;
            previousOffsetPosition = nextOffsetPosition;
        }

        return map;
    }

    private static bool TryResolveOffsetPosition(
        TrackGraph graph,
        TrackRuntimeResolver resolver,
        string baseEdgeId,
        List<TrackOffsetSegment> offsetSegments,
        float baseDistanceM,
        out Vector3 offsetPosition)
    {
        offsetPosition = Vector3.zero;

        float offsetM = TrackOffsetUtility.EvaluateOffsetAtBaseDistance(
            offsetSegments,
            baseDistanceM
        );

        if (!resolver.TryResolveOffsetPose(
            graph,
            baseEdgeId,
            baseDistanceM,
            offsetM,
            out offsetPosition,
            out _,
            out _))
        {
            offsetPosition = Vector3.zero;
            return false;
        }

        return true;
    }
}
