using UnityEngine;
using System.Collections.Generic;

public static class TrackOffsetUtility
{
    public static float EvaluateOffsetAtBaseDistance(
        List<TrackOffsetSegment> segments,
        float baseDistanceM)
    {
        if (segments == null || segments.Count == 0)
        {
            return 0f;
        }

        TrackOffsetSegment lastValidSegment = null;
        for (int i = 0; i < segments.Count; i++)
        {
            TrackOffsetSegment segment = segments[i];
            if (segment == null)
            {
                continue;
            }

            if (baseDistanceM < segment.startBaseDistanceM)
            {
                return lastValidSegment != null ? lastValidSegment.endOffsetM : segment.startOffsetM;
            }

            if (baseDistanceM <= segment.EndBaseDistanceM)
            {
                return segment.EvaluateOffset(baseDistanceM);
            }

            lastValidSegment = segment;
        }

        return lastValidSegment != null ? lastValidSegment.endOffsetM : 0f;
    }
}
