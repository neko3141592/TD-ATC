using UnityEngine;

[CreateAssetMenu(fileName = "TrainSpec", menuName = "Train/Train Spec")]
public class TrainSpec : ScriptableObject
{
    [Header("Vehicle")]
    [Min(1f)] public float massKg = 280000f;

    [Header("Speed (m/s)")]
    [Min(0f)] public float maxSpeedMS = 33.33f;

    [Header("Deceleration (m/s^2)")]
    [Min(0f)] public float coastDeceleration = 0.1f;

    [Header("Traction Limits")]
    [Min(0f)] public float maxTractionForceN = 220000f; // 力行の絶対上限牽引力[N]
    [Min(0f)] public float maxTractionPowerW = 3200000f; // 力行の最大出力[W]
    [Min(0f)] public float motorPowerPerUnitW = 150000f; // モーター1基あたり最大出力[W]
    [Min(0.1f)] public float tractionPowerSpeedFloorMS = 1.0f; // P/v計算でゼロ割りを防ぐ最小速度[m/s]

    [Header("Traction Regions")]
    [Min(0f)] public float maxTargetAccelerationMS2 = 0.8f; // 全ノッチ時の目標加速度[m/s^2]（定加速領域）
    [Min(0f)] public float accelControlEndSpeedMS = 8.33f; // 定加速領域の終了速度[m/s]
    [Min(0f)] public float torqueControlEndSpeedMS = 22.22f; // 定トルク領域の終了速度[m/s]
    [Min(0f)] public float maxMotorTorqueNm = 15000f; // モーター最大トルク[Nm]
    [Min(0f)] public float motorTorquePerUnitNm = 1250f; // モーター1基あたり最大トルク[Nm]
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

    [Header("Power Notch Ratios")]
    public float[] powerNotchRatios = { 0.2f, 0.4f, 0.6f, 0.8f, 1.0f };

    [Header("Power Notch Gain Curves (0..1 speed ratio -> force gain)")]
    public AnimationCurve[] powerNotchGainCurves;

    [Header("Brake Notch Decelerations (m/s^2)")]
    public float[] brakeNotchDecelerations = { 0.35f, 0.55f, 0.75f, 0.95f, 1.1f, 1.2f, 1.3f, 1.4f };
    public float estimatedEmergencyBrakeDeceleration = 1.25f;

    [Header("Brake Blend (Regen + Air)")]
    [Min(0f)] public float maxRegenDecelerationMS2 = 1.1f;
    [Min(0f)] public float regenRiseRateMS3 = 1.8f;
    [Min(0f)] public float regenFallRateMS3 = 2.4f;
    [Min(0f)] public float regenCutOutStartSpeedMS = 4.1667f;
    [Min(0f)] public float regenCutOutEndSpeedMS = 1.3889f;
    [Min(0f)] public float bcMinPressureDuringCooperativeKPa = 50f; // 協調制御時に保持するBC圧の下限[kPa]
    [Min(0f)] public float bcFillRateKPaPerSec = 120f;
    [Min(0f)] public float bcReleaseRateKPaPerSec = 180f;

    [Header("Air Brake Force Model")]
    [Min(0f)] public float brakeFrictionBaseMu = 0.35f; // 摩擦係数の基準値[-]
    [Min(0f)] public float brakeFrictionOffsetKmH = 100f; // μ式の速度オフセット[km/h]
    [Min(0f)] public float brakeFrictionSlope = 3f; // μ式の速度係数[-]

    [Header("Regen Fluctuation")]
    public bool enableRegenFluctuation = true; // 回生の効き揺らぎを有効化するか
    [Range(0f, 0.30f)] public float regenFluctuationAmplitude = 0.04f; // 揺らぎ量（±比率）
    [Min(0f)] public float regenFluctuationFrequencyHz = 0.9f; // 揺らぐ速さ[Hz]

    [Header("Regen Cap Curve (0..1 speed ratio -> cap multiplier)")]
    public AnimationCurve regenCapCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.2f, 0.8f),
        new Keyframe(1f, 1f)
    );

    /// <summary>
    /// 役割: GetPowerNotchRatio の処理を実行します。
    /// </summary>
    /// <param name="notch">notch を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private float GetPowerNotchRatio(int notch)
    {
        if (notch <= 0)
        {
            return 0f;
        }

        int clampedNotch = Mathf.Clamp(notch, 1, maxPowerNotch);
        int index = clampedNotch - 1;
        if (powerNotchRatios == null || powerNotchRatios.Length == 0)
        {
            return 0f;
        }

        if (index >= powerNotchRatios.Length)
        {
            index = powerNotchRatios.Length - 1;
        }

        return Mathf.Max(0f, powerNotchRatios[index]);
    }

    /// <summary>
    /// 役割: GetPowerNotchSpeedGain の処理を実行します。
    /// </summary>
    /// <param name="notch">notch を指定します。</param>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    public float GetPowerNotchSpeedGain(int notch, float speedMS)
    {
        if (notch <= 0)
        {
            return 0f;
        }

        int clampedNotch = Mathf.Clamp(notch, 1, maxPowerNotch);
        int index = clampedNotch - 1;
        float speed01 = maxSpeedMS > 0f ? Mathf.Clamp01(speedMS / maxSpeedMS) : 0f;

        if (powerNotchGainCurves != null &&
            index < powerNotchGainCurves.Length &&
            powerNotchGainCurves[index] != null &&
            powerNotchGainCurves[index].length > 0)
        {
            return Mathf.Max(0f, powerNotchGainCurves[index].Evaluate(speed01));
        }

        // 旧データ互換: カーブ未設定時はノッチ倍率で代替
        return GetPowerNotchRatio(notch);
    }

    /// <summary>
    /// 役割: GetTractionDemandForceN の処理を実行します。
    /// </summary>
    /// <param name="notch">notch を指定します。</param>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <param name="massKg">massKg を指定します。</param>
    /// <param name="externalForceN">externalForceN を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    public float GetTractionDemandForceN(int notch, float speedMS, float massKg, float externalForceN)
    {
        // 既存互換: motorCount未指定時は従来の編成固定上限を使う
        return GetTractionDemandForceNInternal(
            notch,
            speedMS,
            massKg,
            externalForceN,
            Mathf.Max(0f, maxTractionPowerW),
            Mathf.Max(0f, maxMotorTorqueNm)
        );
    }

    /// <summary>
    /// 役割: GetTractionDemandForceN の処理を実行します。
    /// </summary>
    /// <param name="notch">notch を指定します。</param>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <param name="massKg">massKg を指定します。</param>
    /// <param name="externalForceN">externalForceN を指定します。</param>
    /// <param name="totalMotorCount">totalMotorCount を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    public float GetTractionDemandForceN(int notch, float speedMS, float massKg, float externalForceN, int totalMotorCount)
    {
        // 新仕様: 1基あたり値 * 総モータ基数で、出力/トルク上限を作る
        float effectiveMaxPowerW = GetMaxTractionPowerByMotorCountW(totalMotorCount);
        float effectiveMaxMotorTorqueNm = GetMaxMotorTorqueByMotorCountNm(totalMotorCount);

        return GetTractionDemandForceNInternal(
            notch,
            speedMS,
            massKg,
            externalForceN,
            effectiveMaxPowerW,
            effectiveMaxMotorTorqueNm
        );
    }

    private float GetTractionDemandForceNInternal(
        int notch,
        float speedMS,
        float massKg,
        float externalForceN,
        float effectiveMaxTractionPowerW,
        float effectiveMaxMotorTorqueNm
    )
    {
        if (notch <= 0)
        {
            return 0f;
        }

        float safeSpeedMS = Mathf.Max(0f, speedMS); // 負速度を防いだ現在速度[m/s]
        float safeMassKg = Mathf.Max(1f, massKg); // 0除算を防ぐ質量[kg]
        float safeExternalForceN = Mathf.Max(0f, externalForceN); // 外力（抵抗など）[N]

        float notchGain = GetPowerNotchSpeedGain(notch, safeSpeedMS); // ノッチ別速度ゲイン[-]

        float targetAccelerationMS2 = maxTargetAccelerationMS2 * notchGain; // ノッチに応じた目標加速度[m/s^2]
        float accelRegionForceN = (safeMassKg * targetAccelerationMS2) + safeExternalForceN; // 定加速領域で必要な力[N]

        // まず各領域の生値を作る
        float rawTorqueRegionForceN = GetTorqueBasedForceN(effectiveMaxMotorTorqueNm) * notchGain; // 定トルク領域の生指令力[N]
        float rawPowerRegionForceN = (Mathf.Max(0f, effectiveMaxTractionPowerW) / Mathf.Max(tractionPowerSpeedFloorMS, safeSpeedMS)) * notchGain; // 定出力領域の生指令力[N]

        // 境界整合:
        // 定加速 -> 定トルクで力が増えないよう、定トルクを定加速境界力以下に制限する
        float torqueRegionForceN = Mathf.Min(rawTorqueRegionForceN, accelRegionForceN);
        // 定トルク -> 定出力でも増えないよう、定出力を整合済み定トルク以下に制限する
        float powerRegionForceN = Mathf.Min(rawPowerRegionForceN, torqueRegionForceN);

        float regionForceN; // 領域選択後の基準指令力[N]
        if (safeSpeedMS <= accelControlEndSpeedMS)
        {
            regionForceN = accelRegionForceN;
        }
        else if (safeSpeedMS <= torqueControlEndSpeedMS)
        {
            regionForceN = torqueRegionForceN;
        }
        else
        {
            regionForceN = powerRegionForceN;
        }

        float shapedForceN = Mathf.Max(0f, regionForceN); // 領域ロジック後の指令力[N]
        float capForceN = Mathf.Min(
            GetTractionForceCapN(safeSpeedMS, effectiveMaxTractionPowerW),
            GetTorqueBasedForceN(effectiveMaxMotorTorqueNm)
        ); // 車両上限力[N]
        return Mathf.Max(0f, Mathf.Min(shapedForceN, capForceN));
    }

    /// <summary>
    /// 役割: GetMaxTractionPowerByMotorCountW の処理を実行します。
    /// </summary>
    /// <param name="totalMotorCount">totalMotorCount を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private float GetMaxTractionPowerByMotorCountW(int totalMotorCount)
    {
        if (totalMotorCount > 0)
        {
            return Mathf.Max(0f, motorPowerPerUnitW) * totalMotorCount;
        }

        return Mathf.Max(0f, maxTractionPowerW);
    }

    /// <summary>
    /// 役割: GetMaxMotorTorqueByMotorCountNm の処理を実行します。
    /// </summary>
    /// <param name="totalMotorCount">totalMotorCount を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private float GetMaxMotorTorqueByMotorCountNm(int totalMotorCount)
    {
        if (totalMotorCount > 0)
        {
            return Mathf.Max(0f, motorTorquePerUnitNm) * totalMotorCount;
        }

        return Mathf.Max(0f, maxMotorTorqueNm);
    }

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
    /// 役割: GetCoastDeceleration の処理を実行します。
    /// </summary>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    public float GetCoastDeceleration(float speedMS)
    {
        return Mathf.Max(0f, coastDeceleration);
    }

    /// <summary>
    /// 役割: GetTractionForceCapN の処理を実行します。
    /// </summary>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <param name="tractionPowerWOverride">tractionPowerWOverride を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private float GetTractionForceCapN(float speedMS, float tractionPowerWOverride)
    {
        float speedForPowerLimit = Mathf.Max(tractionPowerSpeedFloorMS, speedMS);
        float powerLimitedForce = Mathf.Max(0f, tractionPowerWOverride) / speedForPowerLimit;
        return Mathf.Max(0f, Mathf.Min(maxTractionForceN, powerLimitedForce));
    }

    /// <summary>
    /// 役割: GetTorqueBasedForceN の処理を実行します。
    /// </summary>
    /// <param name="motorTorqueNmOverride">motorTorqueNmOverride を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private float GetTorqueBasedForceN(float motorTorqueNmOverride)
    {
        float safeWheelRadius = Mathf.Max(0.01f, wheelRadiusM);
        float safeGearRatio = Mathf.Max(0.01f, gearRatio);
        float safeEfficiency = Mathf.Clamp01(drivelineEfficiency);
        float safeMotorTorqueNm = Mathf.Max(0f, motorTorqueNmOverride);
        return Mathf.Max(0f, (safeMotorTorqueNm * safeGearRatio * safeEfficiency) / safeWheelRadius);
    }

    /// <summary>
    /// 役割: GetRegenCurveCapDeceleration の処理を実行します。
    /// </summary>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    public float GetRegenCurveCapDeceleration(float speedMS)
    {
        float speed01 = maxSpeedMS > 0f ? Mathf.Clamp01(speedMS / maxSpeedMS) : 0f;
        float curveMultiplier = regenCapCurve != null ? Mathf.Max(0f, regenCapCurve.Evaluate(speed01)) : 0f;
        return Mathf.Max(0f, maxRegenDecelerationMS2 * curveMultiplier);
    }

    /// <summary>
    /// 役割: GetRegenCutOutFactor の処理を実行します。
    /// </summary>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    public float GetRegenCutOutFactor(float speedMS)
    {
        if (regenCutOutStartSpeedMS <= regenCutOutEndSpeedMS)
        {
            return speedMS > regenCutOutStartSpeedMS ? 1f : 0f;
        }

        if (speedMS >= regenCutOutStartSpeedMS)
        {
            return 1f;
        }
        if (speedMS <= regenCutOutEndSpeedMS)
        {
            return 0f;
        }

        return Mathf.InverseLerp(regenCutOutEndSpeedMS, regenCutOutStartSpeedMS, speedMS);
    }

    /// <summary>
    /// 役割: OnValidate の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void OnValidate()
    {
        massKg = Mathf.Max(1f, massKg);
        maxSpeedMS = Mathf.Max(0f, maxSpeedMS);
        coastDeceleration = Mathf.Max(0f, coastDeceleration);
        maxTractionForceN = Mathf.Max(0f, maxTractionForceN);
        maxTractionPowerW = Mathf.Max(0f, maxTractionPowerW);
        motorPowerPerUnitW = Mathf.Max(0f, motorPowerPerUnitW);
        tractionPowerSpeedFloorMS = Mathf.Max(0.1f, tractionPowerSpeedFloorMS);
        maxTargetAccelerationMS2 = Mathf.Max(0f, maxTargetAccelerationMS2);
        accelControlEndSpeedMS = Mathf.Max(0f, accelControlEndSpeedMS);
        torqueControlEndSpeedMS = Mathf.Max(accelControlEndSpeedMS, torqueControlEndSpeedMS);
        maxMotorTorqueNm = Mathf.Max(0f, maxMotorTorqueNm);
        motorTorquePerUnitNm = Mathf.Max(0f, motorTorquePerUnitNm);
        gearRatio = Mathf.Max(0.01f, gearRatio);
        drivelineEfficiency = Mathf.Clamp01(drivelineEfficiency);
        wheelRadiusM = Mathf.Max(0.01f, wheelRadiusM);
        resistanceA = Mathf.Max(0f, resistanceA);
        resistanceB = Mathf.Max(0f, resistanceB);
        resistanceC = Mathf.Max(0f, resistanceC);
        maxRegenDecelerationMS2 = Mathf.Max(0f, maxRegenDecelerationMS2);
        regenRiseRateMS3 = Mathf.Max(0f, regenRiseRateMS3);
        regenFallRateMS3 = Mathf.Max(0f, regenFallRateMS3);
        regenCutOutStartSpeedMS = Mathf.Max(0f, regenCutOutStartSpeedMS);
        regenCutOutEndSpeedMS = Mathf.Max(0f, regenCutOutEndSpeedMS);
        bcMinPressureDuringCooperativeKPa = Mathf.Max(0f, bcMinPressureDuringCooperativeKPa);
        bcFillRateKPaPerSec = Mathf.Max(0f, bcFillRateKPaPerSec);
        bcReleaseRateKPaPerSec = Mathf.Max(0f, bcReleaseRateKPaPerSec);
        brakeFrictionBaseMu = Mathf.Max(0f, brakeFrictionBaseMu);
        brakeFrictionOffsetKmH = Mathf.Max(0f, brakeFrictionOffsetKmH);
        brakeFrictionSlope = Mathf.Max(0f, brakeFrictionSlope);
        regenFluctuationAmplitude = Mathf.Clamp(regenFluctuationAmplitude, 0f, 0.30f);
        regenFluctuationFrequencyHz = Mathf.Max(0f, regenFluctuationFrequencyHz);

        maxPowerNotch = Mathf.Max(1, maxPowerNotch);
        maxBrakeNotch = Mathf.Max(1, maxBrakeNotch);

        powerNotchRatios = ResizeArray(powerNotchRatios, maxPowerNotch, true);
        powerNotchGainCurves = ResizeCurveArray(powerNotchGainCurves, maxPowerNotch, powerNotchRatios);
        brakeNotchDecelerations = ResizeArray(brakeNotchDecelerations, maxBrakeNotch, false);

        for (int i = 0; i < powerNotchRatios.Length; i++)
        {
            powerNotchRatios[i] = Mathf.Max(0f, powerNotchRatios[i]);
        }

        for (int i = 0; i < powerNotchGainCurves.Length; i++)
        {
            if (powerNotchGainCurves[i] == null || powerNotchGainCurves[i].length == 0)
            {
                powerNotchGainCurves[i] = CreateConstantCurve(powerNotchRatios[i]);
            }
        }

        for (int i = 0; i < brakeNotchDecelerations.Length; i++)
        {
            brakeNotchDecelerations[i] = Mathf.Max(0f, brakeNotchDecelerations[i]);
        }

        if (regenCapCurve == null || regenCapCurve.length == 0)
        {
            regenCapCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.2f, 0.8f),
                new Keyframe(1f, 1f)
            );
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

    /// <summary>
    /// 役割: ResizeCurveArray の処理を実行します。
    /// </summary>
    /// <param name="source">source を指定します。</param>
    /// <param name="size">size を指定します。</param>
    /// <param name="fallbackRatios">fallbackRatios を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private AnimationCurve[] ResizeCurveArray(AnimationCurve[] source, int size, float[] fallbackRatios)
    {
        AnimationCurve[] result = new AnimationCurve[size];
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
            float fallback = 0f;
            if (fallbackRatios != null && i < fallbackRatios.Length)
            {
                fallback = Mathf.Max(0f, fallbackRatios[i]);
            }
            result[i] = CreateConstantCurve(fallback);
        }

        return result;
    }

    /// <summary>
    /// 役割: CreateConstantCurve の処理を実行します。
    /// </summary>
    /// <param name="value">value を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private AnimationCurve CreateConstantCurve(float value)
    {
        float v = Mathf.Max(0f, value);
        return new AnimationCurve(
            new Keyframe(0f, v),
            new Keyframe(1f, v)
        );
    }
}
