using UnityEngine;

public class NotchManager : MonoBehaviour
{
    private int maxPowerNotch = 5;
    private int maxBrakeNotch = 8;

    private int manualPowerNotch = 0;
    private int manualBrakeNotch = 0;
    private int atcBrakeNotch = 0;
    private int tascBrakeNotch = 0;

    public int ManualPowerNotch => manualPowerNotch;
    public int ManualBrakeNotch => manualBrakeNotch;
    public int ATCBrakeNotch => atcBrakeNotch;
    public int TASCBrakeNotch => tascBrakeNotch;

    public int ResolvedPowerNotch { get; private set; }
    public int ResolvedBrakeNotch { get; private set; }

    /// <summary>
    /// 役割: ConfigureLimits の処理を実行します。
    /// </summary>
    /// <param name="maxPower">maxPower を指定します。</param>
    /// <param name="maxBrake">maxBrake を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void ConfigureLimits(int maxPower, int maxBrake)
    {
        maxPowerNotch = Mathf.Max(1, maxPower);
        maxBrakeNotch = Mathf.Max(1, maxBrake);
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
        tascBrakeNotch = Mathf.Clamp(brakeNotch, 0, maxBrakeNotch);
        Resolve();
    }

    /// <summary>
    /// 役割: Resolve の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Resolve()
    {
        ResolvedBrakeNotch = Mathf.Max(Mathf.Max(manualBrakeNotch, atcBrakeNotch), tascBrakeNotch);
        ResolvedPowerNotch = ResolvedBrakeNotch > 0 ? 0 : manualPowerNotch;
    }
}
