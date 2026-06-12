using UnityEngine;
using System;

public enum TrackOffsetCurveType
{
    Constant,
    Linear,
    Cubic
}

[Serializable]
public class TrackOffsetSegment
{
    [Min(0f)] public float startBaseDistanceM;
    [Min(0f)] public float baseLengthM;

    public float startOffsetM;
    public float endOffsetM;

    public TrackOffsetCurveType curveType;

    public float EndBaseDistanceM => startBaseDistanceM + baseLengthM;


    public float EvaluateOffset(float baseDistanceM)
    {
        float localBaseDistanceM = baseDistanceM - startBaseDistanceM;
        float t = Mathf.Clamp01(localBaseDistanceM / Mathf.Max(0.001f, baseLengthM));

        if (curveType == TrackOffsetCurveType.Constant)
        {
            return startOffsetM;
        }

        if (curveType == TrackOffsetCurveType.Linear)
        {
            float currentOffset = Mathf.Lerp(startOffsetM, endOffsetM, t);
            return currentOffset;
        }

        if (curveType == TrackOffsetCurveType.Cubic)
        {
            float smoothT = t * t * (3f - 2f * t);
            return startOffsetM + (endOffsetM - startOffsetM) * smoothT;
        }

        return 0f;
    }

}
