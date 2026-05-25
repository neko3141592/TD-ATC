using UnityEngine;

[CreateAssetMenu(fileName = "TrainSpec", menuName = "Train/Core/Train Spec")]
public class TrainSpec : ScriptableObject
{
    [Header("Vehicle")]
    [Min(1f)] public float massKg = 280000f;

    [Header("Speed (m/s)")]
    [Min(0f)] public float maxSpeedMS = 33.33f;

    [Header("Drive Geometry")]
    [Min(0.01f)] public float gearRatio = 6.5f; // 減速比[-]
    [Range(0f, 1f)] public float drivelineEfficiency = 0.92f; // 伝達効率[-]
    [Min(0.01f)] public float wheelRadiusM = 0.43f; // 車輪半径[m]

    [Header("Running Resistance (N)")]
    [Min(0f)] public float resistanceA = 2000f;
    [Min(0f)] public float resistanceB = 30f;
    [Min(0f)] public float resistanceC = 3f;

    [Header("Notch Count")]
    [Min(1)] public int maxPowerNotch = 5;
    [Min(1)] public int maxBrakeNotch = 8;
    [Min(1)] public int tascBrakeSubstepsPerNotch = 4;

    [Header("Speed Hold Active Threshold")]
    [Min(25)] public float speedHoldActiveThresholdKmh = 40f;

    [Header("Brake Notch Decelerations (m/s^2)")]
    public float[] brakeNotchDecelerations = { 0.35f, 0.55f, 0.75f, 0.95f, 1.1f, 1.2f, 1.3f, 1.4f };
    public float estimatedEmergencyBrakeDeceleration = 1.25f;

    [Header("Air Brake Response")]
    [Min(0f)] public float bcFillRateKPaPerSec = 120f;
    [Min(0f)] public float bcReleaseRateKPaPerSec = 180f;

    [Header("Air Brake Force Model")]
    [Min(0f)] public float brakeFrictionBaseMu = 0.35f; // 摩擦係数の基準値[-]
    [Min(0f)] public float brakeFrictionOffsetKmH = 100f; // μ式の速度オフセット[km/h]
    [Min(0f)] public float brakeFrictionSlope = 3f; // μ式の速度係数[-]

    /// <summary>
    /// 役割: GetBrakeDeceleration の処理を実行します。
    /// </summary>
    /// <param name="notch">notch を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    public float GetBrakeDeceleration(int notch)
    {
        if (notch <= 0)
        {
            return 0f;
        }

        if (brakeNotchDecelerations == null || brakeNotchDecelerations.Length == 0)
        {
            return 0f;
        }

        int clampedNotch = Mathf.Clamp(notch, 1, maxBrakeNotch);
        int index = clampedNotch - 1;
        index = Mathf.Min(index, brakeNotchDecelerations.Length - 1);
        return Mathf.Max(0f, brakeNotchDecelerations[index]);
    }

    /// <summary>
    /// 役割: TASC 用に 1 ブレーキノッチを何段へ細分化するかを返します。
    /// </summary>
    /// <returns>TASC の 1 ノッチあたり細分化段数を返します。</returns>
    public int GetTascBrakeSubstepsPerNotch()
    {
        return Mathf.Max(1, tascBrakeSubstepsPerNotch);
    }

    /// <summary>
    /// 役割: TASC 用の最大連続ブレーキ段数を返します。
    /// </summary>
    /// <returns>常用最大ブレーキノッチ数と細分化段数から求めた TASC 最大段数を返します。</returns>
    public int GetMaxTascBrakeStep()
    {
        return Mathf.Max(1, maxBrakeNotch) * GetTascBrakeSubstepsPerNotch();
    }

    /// <summary>
    /// 役割: TASC 連続段に対応する要求減速度を返します。
    /// </summary>
    /// <param name="tascBrakeStep">TASC の連続ブレーキ段を指定します。</param>
    /// <returns>指定段に対応する補間済み減速度[m/s^2]を返します。</returns>
    public float GetTascBrakeStepDeceleration(int tascBrakeStep)
    {
        if (tascBrakeStep <= 0)
        {
            return 0f;
        }

        int substeps = GetTascBrakeSubstepsPerNotch();
        int maxStep = GetMaxTascBrakeStep();
        int clampedStep = Mathf.Clamp(tascBrakeStep, 1, maxStep);
        int baseNotch = ((clampedStep - 1) / substeps) + 1;
        int substepIndex = ((clampedStep - 1) % substeps) + 1;

        float baseDeceleration = GetBrakeDeceleration(baseNotch);
        float nextDeceleration = baseNotch < maxBrakeNotch
            ? GetBrakeDeceleration(baseNotch + 1)
            : baseDeceleration;

        // B1-1 は B1 と同じ値にし、B1-2 以降を次ノッチへ向けて少しずつ補間する。
        float t = (substepIndex - 1f) / (substeps + 1f);
        return Mathf.Lerp(baseDeceleration, nextDeceleration, Mathf.Clamp01(t));
    }

    /// <summary>
    /// 役割: GetEmergencyBrakeDeceleration の処理を実行します。
    /// </summary>

    public float GetEstimatedEmergencyBrakeDeceleration()
    {
        return estimatedEmergencyBrakeDeceleration;
    }

    /// <summary>
    /// 役割: GetEmergencyBrakeNotch の処理を実行します。
    /// </summary>
    /// <returns>処理結果を返します。</returns>
    public int GetEmergencyBrakeNotch()
    {
        // 常用最大ノッチ(B8想定)の1段上を非常段(B9)とする
        return Mathf.Max(2, maxBrakeNotch + 1);
    }


    /// <summary>
    /// 役割: GetBrakeFrictionCoefficientMu の処理を実行します。
    /// </summary>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    public float GetBrakeFrictionCoefficientMu(float speedMS)
    {
        // 指定式: μ = 0.35 * ((v + 100) / (3v + 100))
        // ここで v は km/h として扱う。
        float speedKmH = Mathf.Max(0f, speedMS * 3.6f);
        float numerator = speedKmH + brakeFrictionOffsetKmH;
        float denominator = (brakeFrictionSlope * speedKmH) + brakeFrictionOffsetKmH;
        if (denominator <= 0f)
        {
            return 0f;
        }

        float mu = brakeFrictionBaseMu * (numerator / denominator);
        return Mathf.Max(0f, mu);
    }

    /// <summary>
    /// 役割: OnValidate の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void OnValidate()
    {
        massKg = Mathf.Max(1f, massKg);
        maxSpeedMS = Mathf.Max(0f, maxSpeedMS);
        gearRatio = Mathf.Max(0.01f, gearRatio);
        drivelineEfficiency = Mathf.Clamp01(drivelineEfficiency);
        wheelRadiusM = Mathf.Max(0.01f, wheelRadiusM);
        resistanceA = Mathf.Max(0f, resistanceA);
        resistanceB = Mathf.Max(0f, resistanceB);
        resistanceC = Mathf.Max(0f, resistanceC);
        bcFillRateKPaPerSec = Mathf.Max(0f, bcFillRateKPaPerSec);
        bcReleaseRateKPaPerSec = Mathf.Max(0f, bcReleaseRateKPaPerSec);
        brakeFrictionBaseMu = Mathf.Max(0f, brakeFrictionBaseMu);
        brakeFrictionOffsetKmH = Mathf.Max(0f, brakeFrictionOffsetKmH);
        brakeFrictionSlope = Mathf.Max(0f, brakeFrictionSlope);

        maxPowerNotch = Mathf.Max(1, maxPowerNotch);
        maxBrakeNotch = Mathf.Max(1, maxBrakeNotch);

        brakeNotchDecelerations = ResizeArray(brakeNotchDecelerations, maxBrakeNotch, false);

        for (int i = 0; i < brakeNotchDecelerations.Length; i++)
        {
            brakeNotchDecelerations[i] = Mathf.Max(0f, brakeNotchDecelerations[i]);
        }

    }

    /// <summary>
    /// 役割: ResizeArray の処理を実行します。
    /// </summary>
    /// <param name="source">source を指定します。</param>
    /// <param name="size">size を指定します。</param>
    /// <param name="useRatioDefault">useRatioDefault を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private float[] ResizeArray(float[] source, int size, bool useRatioDefault)
    {
        float[] result = new float[size];
        int copied = 0;
        if (source != null)
        {
            copied = Mathf.Min(source.Length, size);
            for (int i = 0; i < copied; i++)
            {
                result[i] = source[i];
            }
        }

        for (int i = copied; i < size; i++)
        {
            if (useRatioDefault)
            {
                result[i] = (i + 1f) / size;
            }
            else
            {
                result[i] = 0.5f + (0.1f * i);
            }
        }

        return result;
    }

}
