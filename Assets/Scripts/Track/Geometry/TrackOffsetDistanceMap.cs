using UnityEngine;
using System;   
using System.Collections.Generic;

[Serializable]
public class TrackOffsetDistanceMap
{
    [Min(0.001f)]
    public float sampleIntervalM = 0.1f;
    public List<float> baseDistanceByOffsetIndex = new();

    public float SampleBaseDistance (float offsetDistanceM)
    {
        if (baseDistanceByOffsetIndex == null || baseDistanceByOffsetIndex.Count == 0)
        {
            return offsetDistanceM;
        }

        if (baseDistanceByOffsetIndex.Count == 1)
        {
            return baseDistanceByOffsetIndex[0];
        }

        float clampedDistance = Mathf.Clamp(
            offsetDistanceM,
            0f,
        (baseDistanceByOffsetIndex.Count - 1) * sampleIntervalM
        );

        float rawIndex = clampedDistance / sampleIntervalM;
        int index0 = Mathf.FloorToInt(rawIndex);
        int index1 = Math.Min(index0 + 1, baseDistanceByOffsetIndex.Count - 1);

        float t = Mathf.Clamp01(rawIndex - index0);

        return Mathf.Lerp(baseDistanceByOffsetIndex[index0], baseDistanceByOffsetIndex[index1], t);
    }
}