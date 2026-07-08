using System.Collections.Generic;
using UnityEngine;

public static class TimsBrakeHelper
{
    public static float GetBrakeDecelerationFromStep(int brakeStep, int subStepCount, List<float> decelerations)
    {
        if (decelerations == null || decelerations.Count == 0)
        {
            return 0f;
        }

        int brakeNotchCount = decelerations.Count;
        int brakeStepCount = (brakeNotchCount - 1) * subStepCount + 1;

        if (brakeStep == 0)
        {
            return 0f;
        }

        if (brakeStep >= brakeStepCount || brakeStep < 0 || subStepCount <= 0)
        {
            return decelerations[brakeNotchCount - 1];
        }

        TimsNotchHelper.ToSubStepBrakeNotch(brakeStep, subStepCount, out int brakeNotch, out int subStep);

        int currentIndex = brakeNotch - 1;
        int nextIndex = brakeNotch;

        float baseDeceleration = decelerations[currentIndex];
        float interpolatedDeceleration = (decelerations[nextIndex] - decelerations[currentIndex]) / subStepCount * subStep;

        return baseDeceleration + interpolatedDeceleration;
    }

    public static List<float> AllocateEvenlyWithSaturation(IReadOnlyList<float> caps, float target)
    {
        // caps[i] の範囲で target をなるべく均等に配る（水位均し）
        int count = caps?.Count ?? 0;
        List<float> allocated = new();

        for (int i = 0; i < count; i++)
        {
            allocated.Add(0f);
        }

        if (count == 0 || target <= 0f)
        {
            return allocated;
        }

        bool[] active = new bool[count];
        int activeCount = 0;
        for (int i = 0; i < count; i++)
        {
            float cap = Mathf.Max(0f, caps[i]);
            if (cap > 0f)
            {
                active[i] = true;
                activeCount++;
            }
        }

        float remain = target;
        const float epsilon = 0.0001f;

        int guard = Mathf.Max(1, count * 4); // 無限ループ防止
        for (int loop = 0; loop < guard && remain > epsilon && activeCount > 0; loop++)
        {
            // 現在飽和していない車両に残りを均等割り
            float share = remain / activeCount;

            bool anyChanged = false;

            for (int i = 0; i < count; i++)
            {
                // 既に飽和している場合
                if (!active[i])
                {
                    continue;
                }

                float cap = Mathf.Max(0f, caps[i]);

                // 残り
                float room = cap - allocated[i];

                if (room <= epsilon)
                {
                    active[i] = false;
                    activeCount--;
                    continue;
                }

                float add = Mathf.Min(share, room);
                if (add > 0f)
                {
                    allocated[i] += add;
                    remain -= add;
                    anyChanged = true;
                }

                if (room - add <= epsilon)
                {
                    active[i] = false;
                    activeCount--;
                }
            }

            if (!anyChanged)
            {
                break;
            }
        }

        return allocated;
    }
}