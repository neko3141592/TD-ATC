using UnityEngine;

[CreateAssetMenu(menuName = "Train/TASC Profile", fileName = "TASCProfile")]
public class TASCProfile : ScriptableObject
{
    [Header("Activation")]
    [Min(0f)] public float safetyMarginM = 0.5f;
    [Min(0f)] public float startDistanceM = 800f;
    [Min(0f)] public float stopCompletionDistanceM = 0.5f;
    [Min(0f)] public float stopSpeedThresholdMS = 0.2f;

    [Header("Brake Limits")]
    [Min(1)] public int maxServiceBrakeNotch = 7;
    [Min(0.01f)] public float stepFollowIntervalSeconds = 0.2f;

    [Header("Control Zones")]
    public TascControlZone[] controlZones =
    {
        new TascControlZone
        {
            farDistanceM = 800f,
            nearDistanceM = 0f,
            farPatternBrakeNotch = 5,
            nearPatternBrakeNotch = 5,
            interpolationMode = TascPatternInterpolationMode.Constant,
            enterErrorKmH = -0.5f,
            releaseErrorKmH = -1.0f,
            brakeRuleHysteresisKmH = 0.3f,
            speedErrorRules = new TascSpeedErrorBrakeRule[]
            {
                new TascSpeedErrorBrakeRule { minSpeedErrorKmH = -0.5f, baseStepOffset = -2 },
                new TascSpeedErrorBrakeRule { minSpeedErrorKmH = 1.0f, baseStepOffset = 0 },
                new TascSpeedErrorBrakeRule { minSpeedErrorKmH = 2.0f, baseStepOffset = 2 },
                new TascSpeedErrorBrakeRule { minSpeedErrorKmH = 4.0f, baseStepOffset = 4 },
                new TascSpeedErrorBrakeRule { minSpeedErrorKmH = 7.0f, baseStepOffset = 7 },
                new TascSpeedErrorBrakeRule { minSpeedErrorKmH = 10.0f, baseStepOffset = 10 },
            },
        },
    };

    /// <summary>
    /// 役割: 指定距離に対応する制御ゾーンの番号を返します。
    /// </summary>
    /// <param name="distanceM">停止目標までの残距離[m]を指定します。</param>
    /// <returns>対応するゾーン番号を返します。見つからない場合は -1 を返します。</returns>
    public int FindControlZoneIndex(float distanceM)
    {
        if (controlZones == null)
        {
            return -1;
        }

        for (int i = 0; i < controlZones.Length; i++)
        {
            TascControlZone zone = controlZones[i];
            float farDistance = Mathf.Max(zone.farDistanceM, zone.nearDistanceM);
            float nearDistance = Mathf.Min(zone.farDistanceM, zone.nearDistanceM);
            if (distanceM <= farDistance && distanceM >= nearDistance)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 役割: インスペクター変更時に TASC 設定値を安全な範囲へ補正します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void OnValidate()
    {
        safetyMarginM = Mathf.Max(0f, safetyMarginM);
        startDistanceM = Mathf.Max(0f, startDistanceM);
        stopCompletionDistanceM = Mathf.Max(0f, stopCompletionDistanceM);
        stopSpeedThresholdMS = Mathf.Max(0f, stopSpeedThresholdMS);
        maxServiceBrakeNotch = Mathf.Max(1, maxServiceBrakeNotch);
        stepFollowIntervalSeconds = Mathf.Max(0.01f, stepFollowIntervalSeconds);

        if (controlZones == null)
        {
            return;
        }

        for (int i = 0; i < controlZones.Length; i++)
        {
            TascControlZone zone = controlZones[i];
            zone.farDistanceM = Mathf.Max(0f, zone.farDistanceM);
            zone.nearDistanceM = Mathf.Max(0f, zone.nearDistanceM);
            zone.farPatternBrakeNotch = Mathf.Max(1, zone.farPatternBrakeNotch);
            zone.nearPatternBrakeNotch = Mathf.Max(1, zone.nearPatternBrakeNotch);
            zone.brakeRuleHysteresisKmH = Mathf.Max(0f, zone.brakeRuleHysteresisKmH);
            if (zone.releaseErrorKmH > zone.enterErrorKmH)
            {
                zone.releaseErrorKmH = zone.enterErrorKmH;
            }
            NormalizeRules(zone.speedErrorRules);
            controlZones[i] = zone;
        }
    }

    /// <summary>
    /// 役割: 偏差テーブルのしきい値を小さい順に保てるよう補正します。
    /// </summary>
    /// <param name="rules">補正対象の偏差テーブルを指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void NormalizeRules(TascSpeedErrorBrakeRule[] rules)
    {
        if (rules == null || rules.Length == 0)
        {
            return;
        }

        float previousThresholdKmH = float.NegativeInfinity;
        for (int i = 0; i < rules.Length; i++)
        {
            TascSpeedErrorBrakeRule rule = rules[i];
            rule.minSpeedErrorKmH = Mathf.Max(previousThresholdKmH, rule.minSpeedErrorKmH);
            rules[i] = rule;
            previousThresholdKmH = rule.minSpeedErrorKmH;
        }
    }
}

[System.Serializable]
public struct TascControlZone
{
    [Header("Distance Range")]
    [Min(0f)] public float farDistanceM;
    [Min(0f)] public float nearDistanceM;

    [Header("Pattern Interpolation")]
    [Min(1)] public int farPatternBrakeNotch;
    [Min(1)] public int nearPatternBrakeNotch;
    public TascPatternInterpolationMode interpolationMode;

    [Header("Speed Error Hysteresis")]
    public float enterErrorKmH;
    public float releaseErrorKmH;
    [Min(0f)] public float brakeRuleHysteresisKmH;

    [Header("Brake Table")]
    public TascSpeedErrorBrakeRule[] speedErrorRules;
}

[System.Serializable]
public struct TascSpeedErrorBrakeRule
{
    public float minSpeedErrorKmH;
    public int baseStepOffset;
}

public enum TascPatternInterpolationMode
{
    Constant,
    Linear,
    SmoothStep,
}
