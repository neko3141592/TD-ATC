using UnityEngine;
using System;   
using System.Collections.Generic;

[Serializable]
public class TrackOffsetDistanceMap
{
    [Min(0.001f)]
    public float sampleIntervalM = 0.1f;
    [Min(0f)]
    public float offsetLengthM;
    public List<float> baseDistanceByOffsetIndex = new();

    public float OffsetLengthM =>
        offsetLengthM > 0f
            ? offsetLengthM
            :
        baseDistanceByOffsetIndex == null || baseDistanceByOffsetIndex.Count == 0
            ? 0f
            : (baseDistanceByOffsetIndex.Count - 1) * sampleIntervalM;

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

        float clampedDistance = Mathf.Clamp(offsetDistanceM, 0f, OffsetLengthM);

        if (offsetLengthM > 0f)
        {
            float secondLastSampleDistanceM = Mathf.Max(0f, (baseDistanceByOffsetIndex.Count - 2) * sampleIntervalM);
            if (clampedDistance >= secondLastSampleDistanceM)
            {
                float lastSpanM = Mathf.Max(0.001f, offsetLengthM - secondLastSampleDistanceM);
                float lastT = Mathf.Clamp01((clampedDistance - secondLastSampleDistanceM) / lastSpanM);
                return Mathf.Lerp(
                    baseDistanceByOffsetIndex[baseDistanceByOffsetIndex.Count - 2],
                    baseDistanceByOffsetIndex[baseDistanceByOffsetIndex.Count - 1],
                    lastT
                );
            }
        }

        float rawIndex = clampedDistance / sampleIntervalM;
        int index0 = Mathf.FloorToInt(rawIndex);
        int index1 = Math.Min(index0 + 1, baseDistanceByOffsetIndex.Count - 1);

        float t = Mathf.Clamp01(rawIndex - index0);

        return Mathf.Lerp(baseDistanceByOffsetIndex[index0], baseDistanceByOffsetIndex[index1], t);
    }
}
