using UnityEngine;

internal static class ATCPatternCalculator
{
    // v_allow = sqrt(v_target^2 + 2 * a * d)
    // v_target: 目標速度[m/s], a: 想定減速度[m/s^2], d: 目標地点までの残距離[m]
    public static float CalculateAllowSpeedMS(float targetSpeedMS, float decelerationMS2, float remainDistanceM)
    {
        float clampedTarget = Mathf.Max(0f, targetSpeedMS);
        float clampedDecel = Mathf.Max(0f, decelerationMS2);
        float clampedDistance = Mathf.Max(0f, remainDistanceM);

        // 減速度が0以下なら式が成立しないため、目標速度をそのまま返す
        if (clampedDecel <= Mathf.Epsilon)
        {
            return clampedTarget;
        }

        float allowSpeedSquared = clampedTarget * clampedTarget + 2f * clampedDecel * clampedDistance;
        return Mathf.Sqrt(Mathf.Max(0f, allowSpeedSquared));
    }
}
