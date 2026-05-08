using System.Collections.Generic;
using UnityEngine;

public static class TrackGradientUtility
{
    private const float MinSegmentLengthM = 0.001f;
    private const float PermilleScale = 1000f;

    public static float GetHeightDeltaM(
        float localDistanceM,
        float segmentLengthM,
        float startGradientPermille,
        float endGradientPermille)
    {
        if (segmentLengthM <= MinSegmentLengthM)
        {
            return 0f;
        }

        float x = Mathf.Clamp(localDistanceM, 0f, segmentLengthM);
        float p0 = startGradientPermille;
        float p1 = endGradientPermille;

        return (p0 * x + (p1 - p0) * x * x / (2f * segmentLengthM)) / PermilleScale;
    }

    public static float GetGradientPermilleAt(
        float localDistanceM,
        float segmentLengthM,
        float startGradientPermille,
        float endGradientPermille)
    {
        if (segmentLengthM <= MinSegmentLengthM)
        {
            return endGradientPermille;
        }

        float t = Mathf.Clamp01(localDistanceM / segmentLengthM);
        return Mathf.Lerp(startGradientPermille, endGradientPermille, t);
    }

    public static float GetVerticalHeightAt(List<TrackVerticalSegment> segments, float distanceOnEdgeM)
    {
        if (segments == null || segments.Count == 0)
        {
            return 0f;
        }

        float heightM = 0f;
        float cursorDistanceM = 0f;
        float currentGradientPermille = 0f;
        float targetDistanceM = Mathf.Max(0f, distanceOnEdgeM);

        for (int i = 0; i < segments.Count; i++)
        {
            TrackVerticalSegment segment = segments[i];
            if (segment == null || segment.lengthM <= MinSegmentLengthM)
            {
                continue;
            }

            float segmentStartM = Mathf.Max(0f, segment.startDistanceM);
            float segmentEndM = segmentStartM + Mathf.Max(0f, segment.lengthM);

            if (targetDistanceM <= segmentStartM)
            {
                heightM += currentGradientPermille * Mathf.Max(0f, targetDistanceM - cursorDistanceM) / PermilleScale;
                return heightM;
            }

            if (segmentStartM > cursorDistanceM)
            {
                heightM += currentGradientPermille * (segmentStartM - cursorDistanceM) / PermilleScale;
            }

            float localDistanceM = Mathf.Min(targetDistanceM, segmentEndM) - segmentStartM;
            heightM += GetHeightDeltaM(
                localDistanceM,
                segment.lengthM,
                segment.startGradientPermille,
                segment.endGradientPermille
            );

            if (targetDistanceM <= segmentEndM)
            {
                return heightM;
            }

            cursorDistanceM = segmentEndM;
            currentGradientPermille = segment.endGradientPermille;
        }

        heightM += currentGradientPermille * Mathf.Max(0f, targetDistanceM - cursorDistanceM) / PermilleScale;
        return heightM;
    }

    public static float GetGradientPermilleAt(List<TrackVerticalSegment> segments, float distanceOnEdgeM)
    {
        if (segments == null || segments.Count == 0)
        {
            return 0f;
        }

        TrackVerticalSegment lastPassedSegment = null;
        for (int i = 0; i < segments.Count; i++)
        {
            TrackVerticalSegment segment = segments[i];
            if (segment == null || segment.lengthM <= MinSegmentLengthM)
            {
                continue;
            }

            float segmentStartM = Mathf.Max(0f, segment.startDistanceM);
            float segmentEndM = segmentStartM + Mathf.Max(0f, segment.lengthM);
            if (distanceOnEdgeM < segmentStartM)
            {
                break;
            }

            if (distanceOnEdgeM <= segmentEndM)
            {
                return GetGradientPermilleAt(
                    distanceOnEdgeM - segmentStartM,
                    segment.lengthM,
                    segment.startGradientPermille,
                    segment.endGradientPermille
                );
            }

            lastPassedSegment = segment;
        }

        return lastPassedSegment != null ? lastPassedSegment.endGradientPermille : 0f;
    }

    public static float GetCantMmAt(List<TrackCantSegment> segments, float distanceOnEdgeM)
    {
        if (segments == null || segments.Count == 0)
        {
            return 0f;
        }

        TrackCantSegment lastPassedSegment = null;
        for (int i = 0; i < segments.Count; i++)
        {
            TrackCantSegment segment = segments[i];
            if (segment == null || segment.lengthM <= MinSegmentLengthM)
            {
                continue;
            }

            float segmentStartM = Mathf.Max(0f, segment.startDistanceM);
            float segmentEndM = segmentStartM + Mathf.Max(0f, segment.lengthM);
            if (distanceOnEdgeM < segmentStartM)
            {
                break;
            }

            if (distanceOnEdgeM <= segmentEndM)
            {
                float t = Mathf.Clamp01((distanceOnEdgeM - segmentStartM) / segment.lengthM);
                return Mathf.Lerp(segment.startCantMm, segment.endCantMm, t);
            }

            lastPassedSegment = segment;
        }

        return lastPassedSegment != null ? lastPassedSegment.endCantMm : 0f;
    }
}
