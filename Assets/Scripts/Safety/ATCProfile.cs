using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ATCProfile", menuName = "Train/ATC Profile")]
public class ATCProfile : ScriptableObject
{
    [Min(0f)] public float defaultLimitSpeedMS = 33.33f;
    public List<SpeedBlock> blocks = new List<SpeedBlock>();

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
                return Mathf.Max(0f, block.limitSpeedKmH) * 0.2777778f; // km/h -> m/s
            }
        }

        return defaultLimitSpeedMS;
    }
}
