using UnityEngine;

public enum TascControlMode
{
    Inactive,
    PatternControl,
    Holding,
}

public class TASCController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainController train;
    [SerializeField] private TrainSpec trainSpec;
    [SerializeField] private NotchManager notchManager;
    [SerializeField] private StationStopController stationStop;
    [SerializeField] private TASCProfile tascProfile;

    [Header("Runtime Status (Debug)")]
    [SerializeField] private bool isTascActive = false;
    [SerializeField] private float targetDistanceM = 0f;
    [SerializeField] private float patternAllowSpeedMS = 0f;
    [SerializeField] private int currentTascBrakeStep = 0;
    [SerializeField] private float currentSpeedErrorKmH = 0f;
    [SerializeField] private int currentBaseTascBrakeStep = 0;
    [SerializeField] private int targetTascBrakeStep = 0;
    [SerializeField] private int currentControlZoneIndex = -1;
    [SerializeField] private int currentPatternBrakeRuleIndex = -1;
    [SerializeField] private int currentHoldingBrakeRuleIndex = -1;
    [SerializeField] private TascControlMode currentControlMode = TascControlMode.Inactive;
    [SerializeField] private float stepFollowTimer = 0f;
    private int lockedHoldingBrakeStep = 0;
    private bool hasLoggedMissingProfile = false;
    private bool hasLoggedInvalidZone = false;
    private bool hasLoggedInvalidRules = false;
    private bool hasLoggedInvalidHoldingRules = false;

    public bool IsTascActive => isTascActive;
    public float CurrentPatternAllowSpeedKmH => patternAllowSpeedMS * 3.6f;
    public float CurrentTargetDistanceM => targetDistanceM;
    public int CurrentTascBrakeStep => currentTascBrakeStep;
    public int CurrentTascBrakeNotch => GetServiceNotchFromTascStep(currentTascBrakeStep);
    public float CurrentSpeedErrorKmH => currentSpeedErrorKmH;
    public int CurrentBaseTascBrakeStep => currentBaseTascBrakeStep;
    public int CurrentTargetTascBrakeStep => targetTascBrakeStep;
    public string CurrentControlModeLabel => currentControlMode.ToString();


    /// <summary>
    /// 役割: コンポーネント初期化時に参照を補完します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Awake()
    {
        ResolveRuntimeReferences();
    }

    /// <summary>
    /// 役割: 毎フレーム、TASC の許容速度を計算してブレーキ段を更新します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Update()
    {
        ResolveRuntimeReferences();

        if (!CanRunTasc())
        {
            ClearTascCommand();
            return;
        }

        targetDistanceM = Mathf.Max(0f, stationStop.DistanceToStopM - GetSafetyMarginM());
        if (IsHoldingDistanceActive(stationStop.DistanceToStopM))
        {
            patternAllowSpeedMS = 0f;
            targetTascBrakeStep = ResolveLockedHoldingBrakeStep(Mathf.Abs(train.SpeedKmH));
            currentTascBrakeStep = MoveStepTowardTarget(currentTascBrakeStep, targetTascBrakeStep);
            isTascActive = true;
            currentControlMode = TascControlMode.Holding;

            notchManager.SetTASCBrakeStep(currentTascBrakeStep);
            return;
        }

        RefreshActiveControlZone(targetDistanceM);
        if (!CanUseActiveControlZone())
        {
            ClearTascCommand();
            return;
        }

        patternAllowSpeedMS = BuildTascAllowSpeed(targetDistanceM);
        targetTascBrakeStep = SolvePatternTargetBrakeStep(train.SpeedMS, patternAllowSpeedMS);
        currentTascBrakeStep = MoveStepTowardTarget(currentTascBrakeStep, targetTascBrakeStep);
        isTascActive = true;
        currentControlMode = TascControlMode.PatternControl;

        notchManager.SetTASCBrakeStep(currentTascBrakeStep);
    }

    /// <summary>
    /// 役割: TASC が現在作動してよい状態か判定します。
    /// </summary>
    /// <returns>TASC を作動できる場合は true、それ以外は false を返します。</returns>
    private bool CanRunTasc()
    {
        if (tascProfile == null)
        {
            LogMissingProfileOnce();
            return false;
        }

        if (train == null || trainSpec == null || notchManager == null || stationStop == null)
        {
            return false;
        }

        if (!stationStop.HasTargetStation)
        {
            return false;
        }

        if (stationStop.DistanceToStopM < -GetStopCompletionDistanceM())
        {
            return false;
        }

        if (stationStop.DistanceToStopM > GetStartDistanceM())
        {
            return false;
        }

        if (Mathf.Abs(stationStop.DistanceToStopM) <= GetStopCompletionDistanceM() &&
            Mathf.Abs(train.SpeedMS) <= GetHoldingCompleteSpeedMS())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 役割: 停止目標までの距離から TASC の許容速度を計算します。
    /// </summary>
    /// <param name="remainingDistanceM">停止目標までの残距離[m]を指定します。</param>
    /// <returns>TASC の許容速度[m/s]を返します。</returns>
    private float BuildTascAllowSpeed(float remainingDistanceM)
    {
        return ATCPatternCalculator.CalculateAllowSpeedMS(
            0f,
            GetActivePatternDecelerationMS2(),
            Mathf.Max(0f, remainingDistanceM)
        );
    }

    /// <summary>
    /// 役割: TASC が出してよい最大連続ブレーキ段を返します。
    /// </summary>
    /// <returns>最大常用ノッチと細分化段数から求めた最大 TASC 連続段を返します。</returns>
    private int GetMaxAllowedTascBrakeStep()
    {
        if (trainSpec == null)
        {
            return 0;
        }

        int maxServiceNotch = Mathf.Clamp(GetMaxServiceBrakeNotch(), 1, Mathf.Max(1, trainSpec.maxBrakeNotch));
        return maxServiceNotch * trainSpec.GetTascBrakeSubstepsPerNotch();
    }

    /// <summary>
    /// 役割: パターン速度との偏差をブレーキ表へ当てはめ、目標TASC段を決めます。
    /// </summary>
    /// <param name="currentSpeedMS">現在速度[m/s]を指定します。</param>
    /// <param name="allowSpeedMS">TASC 許容速度[m/s]を指定します。</param>
    /// <returns>目標TASC連続ブレーキ段を返します。</returns>
    private int SolvePatternTargetBrakeStep(float currentSpeedMS, float allowSpeedMS)
    {
        currentSpeedErrorKmH = (currentSpeedMS - allowSpeedMS) * 3.6f;

        // ブレーキ投入時と解除時のしきい値を分けて、許容速度付近で段が揺れるのを抑えます。
        float releaseThresholdKmH = targetTascBrakeStep > 0 || currentTascBrakeStep > 0
            ? GetReleaseErrorKmH()
            : GetEnterErrorKmH();
        if (currentSpeedErrorKmH < releaseThresholdKmH)
        {
            currentBaseTascBrakeStep = 0;
            currentPatternBrakeRuleIndex = -1;
            return 0;
        }

        TascSpeedErrorBrakeRule[] activeRules = GetActiveSpeedErrorRules();
        if (activeRules == null || activeRules.Length == 0)
        {
            currentBaseTascBrakeStep = 0;
            currentPatternBrakeRuleIndex = -1;
            return 0;
        }

        currentBaseTascBrakeStep = FindClosestTascStepForDeceleration(GetActivePatternDecelerationMS2());
        int selectedRuleIndex = SelectPatternBrakeRuleIndex(currentSpeedErrorKmH, activeRules);
        if (selectedRuleIndex < 0)
        {
            currentPatternBrakeRuleIndex = -1;
            return 0;
        }

        int selectedOffset = activeRules[selectedRuleIndex].baseStepOffset;
        return ClampTascBrakeStep(currentBaseTascBrakeStep + selectedOffset);
    }

    /// <summary>
    /// 役割: 偏差テーブルの行をヒステリシス付きで選択します。
    /// </summary>
    /// <param name="speedErrorKmH">現在速度とパターン速度の差[km/h]を指定します。</param>
    /// <returns>選択した偏差テーブルの行番号を返します。該当なしの場合は -1 を返します。</returns>
    private int SelectPatternBrakeRuleIndex(float speedErrorKmH, TascSpeedErrorBrakeRule[] rules)
    {
        if (rules == null || rules.Length == 0)
        {
            currentPatternBrakeRuleIndex = -1;
            return -1;
        }

        int selectedIndex = Mathf.Clamp(currentPatternBrakeRuleIndex, -1, rules.Length - 1);
        if (selectedIndex < 0)
        {
            selectedIndex = FindHighestMatchingBrakeRuleIndex(speedErrorKmH, rules);
            currentPatternBrakeRuleIndex = selectedIndex;
            return selectedIndex;
        }

        // 上の行へ移る時は通常のしきい値を使い、必要な時だけ素早く強めます。
        while (selectedIndex + 1 < rules.Length &&
               speedErrorKmH >= rules[selectedIndex + 1].minSpeedErrorKmH)
        {
            selectedIndex++;
        }

        // 下の行へ戻る時はヒステリシス幅だけ低い値まで待ち、境界付近の往復を抑えます。
        while (selectedIndex > 0 &&
               speedErrorKmH < rules[selectedIndex].minSpeedErrorKmH - GetBrakeRuleHysteresisKmH())
        {
            selectedIndex--;
        }

        currentPatternBrakeRuleIndex = selectedIndex;
        return selectedIndex;
    }

    /// <summary>
    /// 役割: Holding制御に入る距離範囲か判定します。
    /// </summary>
    /// <param name="distanceToStopM">停止目標までの距離[m]を指定します。</param>
    /// <returns>Holding制御を使う距離なら true、それ以外は false を返します。</returns>
    private bool IsHoldingDistanceActive(float distanceToStopM)
    {
        return distanceToStopM <= GetHoldingEnterDistanceM() &&
               distanceToStopM >= -GetStopCompletionDistanceM();
    }

    /// <summary>
    /// 役割: Holding突入時に速度テーブルからTASC段を一度だけ決め、その後は停止まで同じ段を返します。
    /// </summary>
    /// <param name="currentSpeedKmH">現在速度[km/h]を指定します。</param>
    /// <returns>目標TASC連続ブレーキ段を返します。</returns>
    private int ResolveLockedHoldingBrakeStep(float currentSpeedKmH)
    {
        if (lockedHoldingBrakeStep > 0)
        {
            return lockedHoldingBrakeStep;
        }

        currentSpeedErrorKmH = currentSpeedKmH;
        currentBaseTascBrakeStep = 0;
        currentPatternBrakeRuleIndex = -1;

        TascHoldingBrakeRule[] rules = GetHoldingSpeedRules();
        if (rules == null || rules.Length == 0)
        {
            LogInvalidHoldingRulesOnce();
            currentHoldingBrakeRuleIndex = -1;
            return 0;
        }

        int selectedRuleIndex = FindHighestMatchingHoldingRuleIndex(currentSpeedKmH, rules);
        if (selectedRuleIndex < 0)
        {
            currentHoldingBrakeRuleIndex = -1;
            return 0;
        }

        currentHoldingBrakeRuleIndex = selectedRuleIndex;
        lockedHoldingBrakeStep = ConvertBrakeNotchToTascStep(rules[selectedRuleIndex].brakeNotch);
        return lockedHoldingBrakeStep;
    }

    /// <summary>
    /// 役割: 現在速度に該当する最も高いHolding用速度テーブル行を探します。
    /// </summary>
    /// <param name="currentSpeedKmH">現在速度[km/h]を指定します。</param>
    /// <param name="rules">Holding用速度テーブルを指定します。</param>
    /// <returns>該当する最も高い行番号を返します。該当なしの場合は -1 を返します。</returns>
    private int FindHighestMatchingHoldingRuleIndex(float currentSpeedKmH, TascHoldingBrakeRule[] rules)
    {
        int selectedIndex = -1;
        for (int i = 0; i < rules.Length; i++)
        {
            if (currentSpeedKmH >= rules[i].minSpeedKmH)
            {
                selectedIndex = i;
            }
        }

        return selectedIndex;
    }

    /// <summary>
    /// 役割: 現在の速度偏差に該当する最も高い偏差テーブル行を探します。
    /// </summary>
    /// <param name="speedErrorKmH">現在速度とパターン速度の差[km/h]を指定します。</param>
    /// <returns>該当する最も高い行番号を返します。該当なしの場合は -1 を返します。</returns>
    private int FindHighestMatchingBrakeRuleIndex(float speedErrorKmH, TascSpeedErrorBrakeRule[] rules)
    {
        int selectedIndex = -1;
        for (int i = 0; i < rules.Length; i++)
        {
            if (speedErrorKmH >= rules[i].minSpeedErrorKmH)
            {
                selectedIndex = i;
            }
        }

        return selectedIndex;
    }

    /// <summary>
    /// 役割: 指定減速度に最も近いTASC連続ブレーキ段を探します。
    /// </summary>
    /// <param name="decelerationMS2">探したい減速度[m/s^2]を指定します。</param>
    /// <returns>最も近い減速度を持つTASC連続ブレーキ段を返します。</returns>
    private int FindClosestTascStepForDeceleration(float decelerationMS2)
    {
        if (trainSpec == null || decelerationMS2 <= 0f)
        {
            return 0;
        }

        int maxStep = GetMaxAllowedTascBrakeStep();
        int closestStep = 1;
        float closestError = float.MaxValue;

        for (int step = 1; step <= maxStep; step++)
        {
            float stepDecelerationMS2 = trainSpec.GetTascBrakeStepDeceleration(step);
            float error = Mathf.Abs(stepDecelerationMS2 - decelerationMS2);
            if (error < closestError)
            {
                closestError = error;
                closestStep = step;
            }
        }

        return closestStep;
    }

    /// <summary>
    /// 役割: TASC連続ブレーキ段を有効範囲に収めます。
    /// </summary>
    /// <param name="step">補正前のTASC連続ブレーキ段を指定します。</param>
    /// <returns>有効範囲へ丸めたTASC連続ブレーキ段を返します。</returns>
    private int ClampTascBrakeStep(int step)
    {
        return Mathf.Clamp(step, 0, GetMaxAllowedTascBrakeStep());
    }

    /// <summary>
    /// 役割: 現在のTASC段を、目標TASC段へ一定間隔で1段ずつ近づけます。
    /// </summary>
    /// <param name="currentStep">現在出力しているTASC連続ブレーキ段を指定します。</param>
    /// <param name="targetStep">偏差テーブルが要求する目標TASC連続ブレーキ段を指定します。</param>
    /// <returns>今回出力するTASC連続ブレーキ段を返します。</returns>
    private int MoveStepTowardTarget(int currentStep, int targetStep)
    {
        int clampedCurrentStep = ClampTascBrakeStep(currentStep);
        int clampedTargetStep = ClampTascBrakeStep(targetStep);
        if (clampedCurrentStep == clampedTargetStep)
        {
            stepFollowTimer = 0f;
            return clampedCurrentStep;
        }

        stepFollowTimer += Time.deltaTime;
        if (stepFollowTimer < GetStepFollowIntervalSeconds())
        {
            return clampedCurrentStep;
        }

        stepFollowTimer = 0f;
        if (clampedCurrentStep < clampedTargetStep)
        {
            return clampedCurrentStep + 1;
        }

        return clampedCurrentStep - 1;
    }

    /// <summary>
    /// 役割: TASC 連続段から表示用の整数ブレーキノッチを返します。
    /// </summary>
    /// <param name="brakeStep">TASC 連続ブレーキ段を指定します。</param>
    /// <returns>対応する整数ブレーキノッチを返します。</returns>
    private int GetServiceNotchFromTascStep(int brakeStep)
    {
        if (trainSpec == null || brakeStep <= 0)
        {
            return 0;
        }

        int substeps = trainSpec.GetTascBrakeSubstepsPerNotch();
        return Mathf.Clamp(((brakeStep - 1) / substeps) + 1, 1, trainSpec.maxBrakeNotch);
    }

    /// <summary>
    /// 役割: TASC 指令とデバッグ状態を解除します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ClearTascCommand()
    {
        isTascActive = false;
        targetDistanceM = 0f;
        patternAllowSpeedMS = 0f;
        currentSpeedErrorKmH = 0f;
        currentBaseTascBrakeStep = 0;
        targetTascBrakeStep = 0;
        currentTascBrakeStep = 0;
        currentControlZoneIndex = -1;
        currentPatternBrakeRuleIndex = -1;
        currentHoldingBrakeRuleIndex = -1;
        currentControlMode = TascControlMode.Inactive;
        lockedHoldingBrakeStep = 0;
        stepFollowTimer = 0f;

        if (notchManager != null)
        {
            notchManager.SetTASCBrakeStep(0);
        }
    }

    /// <summary>
    /// 役割: 未設定の参照を同じ GameObject や TrainController から補完します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ResolveRuntimeReferences()
    {
        if (train == null)
        {
            train = GetComponent<TrainController>();
        }

        if (trainSpec == null && train != null)
        {
            trainSpec = train.Spec;
        }

        if (notchManager == null && train != null)
        {
            notchManager = train.GetComponent<NotchManager>();
        }
    }

    /// <summary>
    /// 役割: 現在距離に対応する TASC 制御ゾーンを更新します。
    /// </summary>
    /// <param name="distanceM">停止目標までの残距離[m]を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void RefreshActiveControlZone(float distanceM)
    {
        int nextZoneIndex = tascProfile.FindControlZoneIndex(distanceM);
        if (nextZoneIndex == currentControlZoneIndex)
        {
            return;
        }

        currentControlZoneIndex = nextZoneIndex;
        currentPatternBrakeRuleIndex = -1;
        currentHoldingBrakeRuleIndex = -1;
        lockedHoldingBrakeStep = 0;
    }

    /// <summary>
    /// 役割: 現在の制御ゾーンと偏差テーブルが使える状態か判定します。
    /// </summary>
    /// <returns>制御ゾーンと偏差テーブルが有効な場合は true、それ以外は false を返します。</returns>
    private bool CanUseActiveControlZone()
    {
        if (!IsCurrentControlZoneValid())
        {
            LogInvalidZoneOnce();
            return false;
        }

        TascSpeedErrorBrakeRule[] rules = tascProfile.controlZones[currentControlZoneIndex].speedErrorRules;
        if (rules == null || rules.Length == 0)
        {
            LogInvalidRulesOnce();
            return false;
        }

        return true;
    }

    /// <summary>
    /// 役割: 現在の制御ゾーンで使う想定減速度を返します。
    /// </summary>
    /// <returns>パターン計算に使う想定減速度[m/s^2]を返します。</returns>
    private float GetActivePatternDecelerationMS2()
    {
        TascControlZone zone = tascProfile.controlZones[currentControlZoneIndex];
        int maxBrakeNotch = Mathf.Max(1, trainSpec.maxBrakeNotch);
        int farPatternBrakeNotch = Mathf.Clamp(zone.farPatternBrakeNotch, 1, maxBrakeNotch);
        int nearPatternBrakeNotch = Mathf.Clamp(zone.nearPatternBrakeNotch, 1, maxBrakeNotch);
        float farDecelerationMS2 = trainSpec.GetBrakeDeceleration(farPatternBrakeNotch);
        float nearDecelerationMS2 = trainSpec.GetBrakeDeceleration(nearPatternBrakeNotch);

        switch (zone.interpolationMode)
        {
            case TascPatternInterpolationMode.Linear:
                return InterpolatePatternDeceleration(zone, nearDecelerationMS2, farDecelerationMS2, false);

            case TascPatternInterpolationMode.SmoothStep:
                return InterpolatePatternDeceleration(zone, nearDecelerationMS2, farDecelerationMS2, true);

            case TascPatternInterpolationMode.Constant:
            default:
                return farDecelerationMS2;
        }
    }

    /// <summary>
    /// 役割: 制御ゾーン内の位置から想定減速度を補間します。
    /// </summary>
    /// <param name="zone">現在使っている制御ゾーンを指定します。</param>
    /// <param name="nearDecelerationMS2">近い側ノッチの減速度[m/s^2]を指定します。</param>
    /// <param name="farDecelerationMS2">遠い側ノッチの減速度[m/s^2]を指定します。</param>
    /// <param name="useSmoothStep">滑らかな補間にする場合は true を指定します。</param>
    /// <returns>距離に応じて補間した想定減速度[m/s^2]を返します。</returns>
    private float InterpolatePatternDeceleration(
        TascControlZone zone,
        float nearDecelerationMS2,
        float farDecelerationMS2,
        bool useSmoothStep)
    {
        float nearDistanceM = Mathf.Min(zone.nearDistanceM, zone.farDistanceM);
        float farDistanceM = Mathf.Max(zone.nearDistanceM, zone.farDistanceM);
        float t = Mathf.InverseLerp(nearDistanceM, farDistanceM, targetDistanceM);
        if (useSmoothStep)
        {
            t = Mathf.SmoothStep(0f, 1f, t);
        }

        return Mathf.Lerp(nearDecelerationMS2, farDecelerationMS2, t);
    }

    /// <summary>
    /// 役割: 現在の制御ゾーンで使う偏差テーブルを返します。
    /// </summary>
    /// <returns>偏差テーブルを返します。</returns>
    private TascSpeedErrorBrakeRule[] GetActiveSpeedErrorRules()
    {
        return tascProfile.controlZones[currentControlZoneIndex].speedErrorRules;
    }

    /// <summary>
    /// 役割: Holding制御で使う速度テーブルを返します。
    /// </summary>
    /// <returns>Holding用速度テーブルを返します。</returns>
    private TascHoldingBrakeRule[] GetHoldingSpeedRules()
    {
        return tascProfile != null ? tascProfile.holdingSpeedRules : null;
    }

    /// <summary>
    /// 役割: 現在の制御ゾーン番号が profile 内で有効か判定します。
    /// </summary>
    /// <returns>有効な制御ゾーンを選択している場合は true、それ以外は false を返します。</returns>
    private bool IsCurrentControlZoneValid()
    {
        return tascProfile != null &&
               tascProfile.controlZones != null &&
               currentControlZoneIndex >= 0 &&
               currentControlZoneIndex < tascProfile.controlZones.Length;
    }

    /// <summary>
    /// 役割: TASCProfile 未設定エラーを一度だけ出力します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void LogMissingProfileOnce()
    {
        if (hasLoggedMissingProfile)
        {
            return;
        }

        hasLoggedMissingProfile = true;
        Debug.LogError($"{nameof(TASCController)} on {name}: TASCProfile is not assigned. TASC will stay disabled.", this);
    }

    /// <summary>
    /// 役割: 有効な制御ゾーンが見つからないエラーを一度だけ出力します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void LogInvalidZoneOnce()
    {
        if (hasLoggedInvalidZone)
        {
            return;
        }

        hasLoggedInvalidZone = true;
        Debug.LogError($"{nameof(TASCController)} on {name}: no TASC control zone matches the current target distance. TASC will stay disabled.", this);
    }

    /// <summary>
    /// 役割: 制御ゾーンの偏差テーブル未設定エラーを一度だけ出力します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void LogInvalidRulesOnce()
    {
        if (hasLoggedInvalidRules)
        {
            return;
        }

        hasLoggedInvalidRules = true;
        Debug.LogError($"{nameof(TASCController)} on {name}: active TASC control zone has no speed error rules. TASC will stay disabled.", this);
    }

    /// <summary>
    /// 役割: Holding用速度テーブル未設定エラーを一度だけ出力します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void LogInvalidHoldingRulesOnce()
    {
        if (hasLoggedInvalidHoldingRules)
        {
            return;
        }

        hasLoggedInvalidHoldingRules = true;
        Debug.LogError($"{nameof(TASCController)} on {name}: TASCProfile has no holding speed rules. Holding control will output B0.", this);
    }

    /// <summary>
    /// 役割: 停止目標の手前に置く安全余裕距離を取得します。
    /// </summary>
    /// <returns>安全余裕距離[m]を返します。</returns>
    private float GetSafetyMarginM() => Mathf.Max(0f, tascProfile.safetyMarginM);

    /// <summary>
    /// 役割: TASC を作動開始できる最大距離を取得します。
    /// </summary>
    /// <returns>TASC 作動開始距離[m]を返します。</returns>
    private float GetStartDistanceM() => Mathf.Max(0f, tascProfile.startDistanceM);

    /// <summary>
    /// 役割: 停止完了とみなす停止位置許容距離を取得します。
    /// </summary>
    /// <returns>停止完了距離[m]を返します。</returns>
    private float GetStopCompletionDistanceM() => Mathf.Max(0f, tascProfile.stopCompletionDistanceM);

    /// <summary>
    /// 役割: 停止完了とみなす速度しきい値を取得します。
    /// </summary>
    /// <returns>停止完了速度[m/s]を返します。</returns>
    private float GetStopSpeedThresholdMS() => Mathf.Max(0f, tascProfile.stopSpeedThresholdMS);

    /// <summary>
    /// 役割: Holding制御へ入る停止目標付近の距離を取得します。
    /// </summary>
    /// <returns>Holding開始距離[m]を返します。</returns>
    private float GetHoldingEnterDistanceM() => Mathf.Max(0f, tascProfile.holdingEnterDistanceM);

    /// <summary>
    /// 役割: TASCを完全停止扱いで解除する速度を取得します。
    /// </summary>
    /// <returns>Holding完了速度[m/s]を返します。</returns>
    private float GetHoldingCompleteSpeedMS() => Mathf.Max(0f, tascProfile.holdingCompleteSpeedKmH) / 3.6f;

    /// <summary>
    /// 役割: TASC が使用してよい最大常用ブレーキノッチを取得します。
    /// </summary>
    /// <returns>最大常用ブレーキノッチを返します。</returns>
    private int GetMaxServiceBrakeNotch() => Mathf.Max(1, tascProfile.maxServiceBrakeNotch);

    /// <summary>
    /// 役割: TASC 連続段を1段進める間隔を取得します。
    /// </summary>
    /// <returns>段追従間隔[秒]を返します。</returns>
    private float GetStepFollowIntervalSeconds() => Mathf.Max(0.01f, tascProfile.stepFollowIntervalSeconds);

    /// <summary>
    /// 役割: TASC ブレーキを投入する速度偏差しきい値を取得します。
    /// </summary>
    /// <returns>投入速度偏差[km/h]を返します。</returns>
    private float GetEnterErrorKmH() => tascProfile.controlZones[currentControlZoneIndex].enterErrorKmH;

    /// <summary>
    /// 役割: TASC ブレーキを解除する速度偏差しきい値を取得します。
    /// </summary>
    /// <returns>解除速度偏差[km/h]を返します。</returns>
    private float GetReleaseErrorKmH() => Mathf.Min(tascProfile.controlZones[currentControlZoneIndex].releaseErrorKmH, GetEnterErrorKmH());

    /// <summary>
    /// 役割: 偏差テーブルの行を下げる時に使うヒステリシス幅を取得します。
    /// </summary>
    /// <returns>偏差テーブルのヒステリシス幅[km/h]を返します。</returns>
    private float GetBrakeRuleHysteresisKmH() => Mathf.Max(0f, tascProfile.controlZones[currentControlZoneIndex].brakeRuleHysteresisKmH);

    /// <summary>
    /// 役割: 整数ブレーキノッチをTASC連続段へ変換します。
    /// </summary>
    /// <param name="brakeNotch">整数ブレーキノッチを指定します。</param>
    /// <returns>対応するTASC連続段を返します。</returns>
    private int ConvertBrakeNotchToTascStep(int brakeNotch)
    {
        if (trainSpec == null || brakeNotch <= 0)
        {
            return 0;
        }

        int maxServiceNotch = GetMaxServiceBrakeNotch();
        int clampedNotch = Mathf.Clamp(brakeNotch, 1, maxServiceNotch);
        int substeps = trainSpec.GetTascBrakeSubstepsPerNotch();
        return ClampTascBrakeStep(((clampedNotch - 1) * substeps) + 1);
    }
}
