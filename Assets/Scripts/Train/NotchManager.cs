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

    public void ConfigureLimits(int maxPower, int maxBrake)
    {
        maxPowerNotch = Mathf.Max(1, maxPower);
        maxBrakeNotch = Mathf.Max(1, maxBrake);
        Resolve();
    }

    public void SetManualNotches(int powerNotch, int brakeNotch)
    {
        manualPowerNotch = Mathf.Clamp(powerNotch, 0, maxPowerNotch);
        manualBrakeNotch = Mathf.Clamp(brakeNotch, 0, maxBrakeNotch);
        Resolve();
    }

    public void SetATCBrakeNotch(int brakeNotch)
    {
        atcBrakeNotch = Mathf.Clamp(brakeNotch, 0, maxBrakeNotch);
        Resolve();
    }

    public void SetTASCBrakeNotch(int brakeNotch)
    {
        tascBrakeNotch = Mathf.Clamp(brakeNotch, 0, maxBrakeNotch);
        Resolve();
    }

    private void Resolve()
    {
        ResolvedBrakeNotch = Mathf.Max(Mathf.Max(manualBrakeNotch, atcBrakeNotch), tascBrakeNotch);
        ResolvedPowerNotch = ResolvedBrakeNotch > 0 ? 0 : manualPowerNotch;
    }
}
