public static class TimsNotchHelper
{
    public static void ToContinuousBrakeNotch (int discreteBrakeNotch, int subStep, int subStepCount, out int continuousBrakeNotch)
    {
        continuousBrakeNotch = (discreteBrakeNotch - 1) * subStepCount + subStep + 1;
    }

    public static void ToSubStepBrakeNotch (int continuousBrakeNotch, int subStepCount, out int discreteBrakeNotch, out int subStep)
    {
        discreteBrakeNotch = ((continuousBrakeNotch - 1) / subStepCount) + 1;
        subStep = (continuousBrakeNotch - 1) % subStepCount;
    }

    public static string FormatBrakeStepLabel(int continuousBrakeNotch, int subStepCount)
    {
        if (continuousBrakeNotch <= 0)
        {
            return "B0-0";
        }

        ToSubStepBrakeNotch(
            continuousBrakeNotch,
            UnityEngine.Mathf.Max(1, subStepCount),
            out int discreteBrakeNotch,
            out int subStep
        );

        return $"B{discreteBrakeNotch}-{subStep}";
    }
}
