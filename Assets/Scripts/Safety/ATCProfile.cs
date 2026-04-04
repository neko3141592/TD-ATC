using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ATCProfile", menuName = "Train/ATC Profile")]
public class ATCProfile : ScriptableObject
{
    [Min(0f)] public float defaultLimitSpeedMS = 33.33f;
    public List<SpeedBlock> blocks = new List<SpeedBlock>();

    private const float KmHToMS = 0.2777778f;

    public float GetLimitSpeed(float distanceM)
    {
        if (blocks == null || blocks.Count == 0)
        {
            return defaultLimitSpeedMS;
        }

        for (int i = 0; i < blocks.Count; i++)
        {
            SpeedBlock block = blocks[i];
            if (block != null && block.Contains(distanceM))
            {
                return GetBlockLimitSpeedMS(block);
            }
        }

        return defaultLimitSpeedMS;
    }

    // 現在位置より先で、現在制限より低い制限が始まる最初の地点を返す
    public bool TryGetNextLowerLimitTarget(float currentDistanceM, out float targetDistanceM, out float targetLimitSpeedMS)
    {
        targetDistanceM = 0f;
        targetLimitSpeedMS = 0f;

        if (blocks == null || blocks.Count == 0)
        {
            return false;
        }

        float currentLimitSpeedMS = GetLimitSpeed(currentDistanceM);
        float nearestStart = Mathf.Infinity;
        float nearestLimit = 0f;
        bool found = false;

        for (int i = 0; i < blocks.Count; i++)
        {
            SpeedBlock block = blocks[i];
            if (block == null)
            {
                continue;
            }

            float start = Mathf.Min(block.startDistanceM, block.endDistanceM);
            if (start <= currentDistanceM)
            {
                continue;
            }

            float blockLimitSpeedMS = GetBlockLimitSpeedMS(block);
            if (blockLimitSpeedMS >= currentLimitSpeedMS)
            {
                continue;
            }

            if (start < nearestStart)
            {
                nearestStart = start;
                nearestLimit = blockLimitSpeedMS;
                found = true;
            }
        }

        if (!found)
        {
            return false;
        }

        targetDistanceM = nearestStart;
        targetLimitSpeedMS = nearestLimit;
        return true;
    }

    private float GetBlockLimitSpeedMS(SpeedBlock block)
    {
        if (block == null)
        {
            return Mathf.Max(0f, defaultLimitSpeedMS);
        }

        return Mathf.Max(0f, block.limitSpeedKmH) * KmHToMS;
    }
}
