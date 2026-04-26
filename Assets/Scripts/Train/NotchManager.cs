using UnityEngine;

public class NotchManager : MonoBehaviour
{
    private int maxPowerNotch = 5;
    private int maxBrakeNotch = 8;
    private int maxTascServiceBrakeNotch = 7;
    private int tascBrakeSubstepsPerNotch = 1;

    private int manualPowerNotch = 0;
    private int manualBrakeNotch = 0;
    private int atcBrakeNotch = 0;
    private int tascBrakeStep = 0;

    public int ManualPowerNotch => manualPowerNotch;
    public int ManualBrakeNotch => manualBrakeNotch;
    public int ATCBrakeNotch => atcBrakeNotch;
    public int TASCBrakeNotch => GetServiceNotchFromTascStep(tascBrakeStep);
    public int TASCBrakeStep => tascBrakeStep;

    public int ResolvedPowerNotch { get; private set; }
    public int ResolvedBrakeNotch { get; private set; }
    public bool IsTASCBrakeSelected { get; private set; }

    /// <summary>
    /// 役割: ConfigureLimits の処理を実行します。
    /// </summary>
    /// <param name="maxPower">maxPower を指定します。</param>
    /// <param name="maxBrake">maxBrake を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void ConfigureLimits(int maxPower, int maxBrake)
    {
        ConfigureLimits(maxPower, maxBrake, 1);
    }

    /// <summary>
    /// 役割: ノッチ上限と TASC 細分化段数を設定します。
    /// </summary>
    /// <param name="maxPower">最大力行ノッチを指定します。</param>
    /// <param name="maxBrake">最大ブレーキノッチを指定します。</param>
    /// <param name="tascSubstepsPerNotch">TASC の 1 ノッチあたり細分化段数を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void ConfigureLimits(int maxPower, int maxBrake, int tascSubstepsPerNotch)
    {
        maxPowerNotch = Mathf.Max(1, maxPower);
        maxBrakeNotch = Mathf.Max(1, maxBrake);
        maxTascServiceBrakeNotch = Mathf.Max(1, maxBrakeNotch - 1);
        tascBrakeSubstepsPerNotch = Mathf.Max(1, tascSubstepsPerNotch);
        tascBrakeStep = Mathf.Clamp(tascBrakeStep, 0, GetMaxTascBrakeStep());
        Resolve();
    }

    /// <summary>
    /// 役割: SetManualNotches の処理を実行します。
    /// </summary>
    /// <param name="powerNotch">powerNotch を指定します。</param>
    /// <param name="brakeNotch">brakeNotch を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void SetManualNotches(int powerNotch, int brakeNotch)
    {
        manualPowerNotch = Mathf.Clamp(powerNotch, 0, maxPowerNotch);
        manualBrakeNotch = Mathf.Clamp(brakeNotch, 0, maxBrakeNotch);
        Resolve();
    }

    /// <summary>
    /// 役割: SetATCBrakeNotch の処理を実行します。
    /// </summary>
    /// <param name="brakeNotch">brakeNotch を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void SetATCBrakeNotch(int brakeNotch)
    {
        atcBrakeNotch = Mathf.Clamp(brakeNotch, 0, maxBrakeNotch);
        Resolve();
    }

    /// <summary>
    /// 役割: SetTASCBrakeNotch の処理を実行します。
    /// </summary>
    /// <param name="brakeNotch">brakeNotch を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void SetTASCBrakeNotch(int brakeNotch)
    {
        if (brakeNotch <= 0)
        {
            SetTASCBrakeStep(0);
            return;
        }

        int clampedNotch = Mathf.Clamp(brakeNotch, 1, maxTascServiceBrakeNotch);
        int brakeStep = ((clampedNotch - 1) * tascBrakeSubstepsPerNotch) + 1;
        SetTASCBrakeStep(brakeStep);
    }

    /// <summary>
    /// 役割: TASC の連続ブレーキ段を設定します。
    /// </summary>
    /// <param name="brakeStep">TASC の連続ブレーキ段を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void SetTASCBrakeStep(int brakeStep)
    {
        tascBrakeStep = Mathf.Clamp(brakeStep, 0, GetMaxTascBrakeStep());
        Resolve();
    }

    /// <summary>
    /// 役割: Resolve の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Resolve()
    {
        int manualRank = GetIntegerBrakeRank(manualBrakeNotch);
        int atcRank = GetIntegerBrakeRank(atcBrakeNotch);
        int tascRank = Mathf.Max(0, tascBrakeStep);

        IsTASCBrakeSelected = tascRank > 0 && tascRank >= manualRank && tascRank >= atcRank;
        if (IsTASCBrakeSelected)
        {
            ResolvedBrakeNotch = GetServiceNotchFromTascStep(tascBrakeStep);
        }
        else
        {
            ResolvedBrakeNotch = Mathf.Max(manualBrakeNotch, atcBrakeNotch);
        }

        ResolvedPowerNotch = ResolvedBrakeNotch > 0 ? 0 : manualPowerNotch;
    }

    /// <summary>
    /// 役割: TASC の最大連続ブレーキ段を返します。
    /// </summary>
    /// <returns>TASC 最大連続ブレーキ段を返します。</returns>
    private int GetMaxTascBrakeStep()
    {
        return Mathf.Max(1, maxTascServiceBrakeNotch) * Mathf.Max(1, tascBrakeSubstepsPerNotch);
    }

    /// <summary>
    /// 役割: 整数ブレーキノッチを TASC 連続段と比較するための順位へ変換します。
    /// </summary>
    /// <param name="brakeNotch">整数ブレーキノッチを指定します。</param>
    /// <returns>ブレーキ要求の強さを表す順位を返します。</returns>
    private int GetIntegerBrakeRank(int brakeNotch)
    {
        if (brakeNotch <= 0)
        {
            return 0;
        }

        if (brakeNotch > maxTascServiceBrakeNotch)
        {
            return GetMaxTascBrakeStep() + brakeNotch;
        }

        return ((brakeNotch - 1) * Mathf.Max(1, tascBrakeSubstepsPerNotch)) + 1;
    }

    /// <summary>
    /// 役割: TASC 連続段から表示・既存制御互換用の整数ブレーキノッチを返します。
    /// </summary>
    /// <param name="brakeStep">TASC の連続ブレーキ段を指定します。</param>
    /// <returns>対応する整数ブレーキノッチを返します。</returns>
    private int GetServiceNotchFromTascStep(int brakeStep)
    {
        if (brakeStep <= 0)
        {
            return 0;
        }

        int substeps = Mathf.Max(1, tascBrakeSubstepsPerNotch);
        return Mathf.Clamp(((brakeStep - 1) / substeps) + 1, 1, maxTascServiceBrakeNotch);
    }
}
