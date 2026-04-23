using UnityEngine;

internal static class ExternalForceCalculator
{
    /// <summary>
    /// 役割: GetRollingResistanceForceN の処理を実行します。
    /// </summary>
    /// <param name="spec">spec を指定します。</param>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private static float GetRollingResistanceForceN(TrainSpec spec, float speedMS)
    {
        if (spec == null)
        {
            return 0f;
        }

        float v = Mathf.Max(0f, speedMS);
        return Mathf.Max(0f, spec.resistanceA + (spec.resistanceB * v));
    }

    /// <summary>
    /// 役割: GetAerodynamicDragForceN の処理を実行します。
    /// </summary>
    /// <param name="spec">spec を指定します。</param>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private static float GetAerodynamicDragForceN(TrainSpec spec, float speedMS)
    {
        if (spec == null)
        {
            return 0f;
        }

        float v = Mathf.Max(0f, speedMS);
        return Mathf.Max(0f, spec.resistanceC * v * v);
    }

    /// <summary>
    /// 役割: GetRunningResistanceForceN の処理を実行します。
    /// </summary>
    /// <param name="spec">spec を指定します。</param>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    public static float GetRunningResistanceForceN(TrainSpec spec, float speedMS)
    {
        return GetRollingResistanceForceN(spec, speedMS) + GetAerodynamicDragForceN(spec, speedMS);
    }

    /// <summary>
    /// 役割: GetCoastExtraResistanceForceN の処理を実行します。
    /// </summary>
    /// <param name="spec">spec を指定します。</param>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    public static float GetCoastExtraResistanceForceN(TrainSpec spec, float speedMS)
    {
        if (spec == null)
        {
            return 0f;
        }

        float massKg = Mathf.Max(1f, spec.massKg);
        return Mathf.Max(0f, spec.GetCoastDeceleration(speedMS)) * massKg;
    }
}
