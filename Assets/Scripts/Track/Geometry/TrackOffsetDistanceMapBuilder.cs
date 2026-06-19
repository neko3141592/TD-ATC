using System.Collections.Generic;
using UnityEngine;

public static class TrackOffsetDistanceMapBuilder
{
    public static TrackOffsetDistanceMap Build(
        TrackGraph graph,
        TrackRuntimeResolver resolver,
        string baseGeometryId,
        List<TrackOffsetSegment> offsetSegments,
        float sampleIntervalM,
        float integrationStepM = 0.05f)
    {
        TrackOffsetDistanceMap map = new TrackOffsetDistanceMap
        {
            sampleIntervalM = Mathf.Max(0.001f, sampleIntervalM),
            offsetLengthM = 0f,
            baseDistanceByOffsetIndex = new()
        };

        if (graph == null || resolver == null || string.IsNullOrEmpty(baseGeometryId))
        {
            return map;
        }

        TrackGeometry baseGeometry = graph.FindGeometry(baseGeometryId);
        if (baseGeometry == null || baseGeometry.lengthM <= 0f)
        {
            return map;
        }

        integrationStepM = Mathf.Max(0.001f, integrationStepM);
        GetBaseDistanceRange(
            baseGeometry,
            offsetSegments,
            out float startBaseDistanceM,
            out float endBaseDistanceM
        );

        if (endBaseDistanceM <= startBaseDistanceM)
        {
            return map;
        }

        float previousBaseDistanceM = startBaseDistanceM;
        if (!TryResolveOffsetPosition(
            graph,
            resolver,
            baseGeometryId,
            offsetSegments,
            previousBaseDistanceM,
            out Vector3 previousOffsetPosition))
        {
            return map;
        }

        map.baseDistanceByOffsetIndex.Add(startBaseDistanceM);

        double accumulatedOffsetDistanceM = 0.0;
        double nextSampleOffsetDistanceM = map.sampleIntervalM;

        while (previousBaseDistanceM < endBaseDistanceM)
        {
            float nextBaseDistanceM = Mathf.Min(
                previousBaseDistanceM + integrationStepM,
                endBaseDistanceM
            );

            if (!TryResolveOffsetPosition(
                graph,
                resolver,
                baseGeometryId,
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

        map.offsetLengthM = (float)accumulatedOffsetDistanceM;
        if (
            map.offsetLengthM > 0f &&
            map.baseDistanceByOffsetIndex.Count > 0 &&
            Mathf.Abs(map.baseDistanceByOffsetIndex[map.baseDistanceByOffsetIndex.Count - 1] - endBaseDistanceM) > 0.0001f
        )
        {
            map.baseDistanceByOffsetIndex.Add(endBaseDistanceM);
        }

        return map;
    }

    private static void GetBaseDistanceRange(
        TrackGeometry baseGeometry,
        List<TrackOffsetSegment> offsetSegments,
        out float startBaseDistanceM,
        out float endBaseDistanceM)
    {
        startBaseDistanceM = 0f;
        endBaseDistanceM = baseGeometry != null ? Mathf.Max(0f, baseGeometry.lengthM) : 0f;

        if (baseGeometry == null || offsetSegments == null || offsetSegments.Count == 0)
        {
            return;
        }

        float minStartM = float.PositiveInfinity;
        float maxEndM = float.NegativeInfinity;
        for (int i = 0; i < offsetSegments.Count; i++)
        {
            TrackOffsetSegment segment = offsetSegments[i];
            if (segment == null || segment.baseLengthM <= 0f)
            {
                continue;
            }

            minStartM = Mathf.Min(minStartM, segment.startBaseDistanceM);
            maxEndM = Mathf.Max(maxEndM, segment.EndBaseDistanceM);
        }

        if (float.IsInfinity(minStartM) || float.IsInfinity(maxEndM))
        {
            return;
        }

        startBaseDistanceM = Mathf.Clamp(minStartM, 0f, baseGeometry.lengthM);
        endBaseDistanceM = Mathf.Clamp(maxEndM, startBaseDistanceM, baseGeometry.lengthM);
    }

    private static bool TryResolveOffsetPosition(
        TrackGraph graph,
        TrackRuntimeResolver resolver,
        string baseGeometryId,
        List<TrackOffsetSegment> offsetSegments,
        float baseDistanceM,
        out Vector3 offsetPosition)
    {
        offsetPosition = Vector3.zero;

        float offsetM = TrackOffsetUtility.EvaluateOffsetAtBaseDistance(
            offsetSegments,
            baseDistanceM
        );

        if (!resolver.TryResolveGeometryPose(
            graph,
            baseGeometryId,
            baseDistanceM,
            out Vector3 basePosition,
            out _,
            out Quaternion baseRotation))
        {
            return false;
        }

        offsetPosition = basePosition + baseRotation * Vector3.right * offsetM;
        return true;
    }
}
