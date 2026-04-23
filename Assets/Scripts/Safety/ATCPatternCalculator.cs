using UnityEngine;

internal static class ATCPatternCalculator
{
    // 許容速度の式: v_allow = sqrt(v_target^2 + 2 * a * d)
    // 各変数の意味: v_target は目標速度[m/s]、a は想定減速度[m/s^2]、d は目標地点までの残距離[m]
    /// <summary>
    /// 役割: CalculateAllowSpeedMS の処理を行います。
    /// </summary>
    /// <param name="targetSpeedMS">targetSpeedMS を指定します。</param>
    /// <param name="decelerationMS2">decelerationMS2 を指定します。</param>
    /// <param name="remainDistanceM">remainDistanceM を指定します。</param>
    /// <returns>計算または参照した値を返します。</returns>
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
