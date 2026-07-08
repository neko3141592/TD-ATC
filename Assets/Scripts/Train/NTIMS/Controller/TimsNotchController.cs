using System.Collections.Generic;
using UnityEngine;

public class TimsNotchController : MonoBehaviour, ITimsMasterDataSource
{
    [SerializeField] private TimsSystem tims;

    private int cabIndex = -1;
    private int manualPowerNotch;
    private int manualBrakeStep;
    private int atcBrakeStep;
    private int resolvedPowerNotch;
    private int resolvedBrakeStep;
    private TrainController.ReverserPosition reverserPosition = TrainController.ReverserPosition.Neutral;


    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        cabIndex = ResolveActivatedCabIndex();
        CollectCommandNotch();
        ResolveCommandNotch();
        WriteTimsMasterData(tims);
    }

    private void ResolveReferences()
    {
        if (tims == null)
        {
            tims = GetComponent<TimsSystem>();
        }

    }

    private int ResolveActivatedCabIndex()
    {
        if (tims == null || tims.Terminals == null || tims.Terminals.Count == 0)
        {
            return -1;
        }

        int frontIndex = 0;
        int rearIndex = tims.Terminals.Count - 1;

        bool frontSelected = TryGetCabSelection(frontIndex, out CabDirectionSelection frontSelection)
            && frontSelection == CabDirectionSelection.Forward;

        bool rearSelected = TryGetCabSelection(rearIndex, out CabDirectionSelection rearSelection)
            && rearSelection == CabDirectionSelection.Reverse;

        if (frontSelected == rearSelected)
        {
            return -1;
        }

        return frontSelected ? frontIndex : rearIndex;
    }

    private bool TryGetCabSelection(int carIndex, out CabDirectionSelection selection)
    {
        selection = CabDirectionSelection.Forward;

        TimsCarTerminal terminal = tims.Terminals[carIndex];
        if (terminal == null)
        {
            return false;
        }

        if (!terminal.LocalBus.TryGetInt(
            new TimsTagKey("CabActivationSwitch", "Selection"),
            out int rawSelection))
        {
            return false;
        }

        selection = (CabDirectionSelection)rawSelection;
        return true;
    }

    private void CollectCommandNotch()
    {
        manualPowerNotch = 0;
        manualBrakeStep = 0;
        atcBrakeStep = 0;
        reverserPosition = TrainController.ReverserPosition.Neutral;

        if (tims == null || tims.ConsistDefinition == null)
        {
            return;
        }

        if (tims == null || tims.Terminals == null)
        {
            return;
        }

        atcBrakeStep = CollectAtcBrakeStep();

        if (cabIndex == -1)
        {
            return;
        }

        List<TimsCarTerminal> terminals = tims.Terminals;
        if (terminals[cabIndex].LocalBus.TryGetInt(new TimsTagKey("MasterController", "PowerPosition"), out int powerNotch))
        {
            manualPowerNotch = powerNotch;
        }

        if (terminals[cabIndex].LocalBus.TryGetInt(new TimsTagKey("MasterController", "BrakePosition"), out int brakeNotch))
        {
            manualBrakeStep = ConvertBrakeNotchToStep(brakeNotch);
        }

        if (terminals[cabIndex].LocalBus.TryGetInt(new TimsTagKey("MasterController", "ReverserPosition"), out int rawReverserPosition))
        {
            reverserPosition = (TrainController.ReverserPosition)rawReverserPosition;
        }
    }

    private int CollectAtcBrakeStep()
    {
        int brakeStep = 0;

        if (tims != null && tims.Terminals != null)
        {
            for (int i = 0; i < tims.Terminals.Count; i++)
            {
                TimsCarTerminal terminal = tims.Terminals[i];
                if (terminal == null)
                {
                    continue;
                }

                if (terminal.LocalBus.TryGetInt(new TimsTagKey("ATC", "BrakeStep"), out int localBrakeStep))
                {
                    brakeStep = Mathf.Max(brakeStep, localBrakeStep);
                }
            }
        }

        return Mathf.Max(0, brakeStep);
    }

    private void ResolveCommandNotch()
    {
        resolvedBrakeStep = Mathf.Max(manualBrakeStep, atcBrakeStep);
        resolvedPowerNotch = resolvedBrakeStep > 0 ? 0 : manualPowerNotch;

    }

    private int ConvertBrakeNotchToStep(int brakeNotch)
    {
        if (brakeNotch <= 0)
        {
            return 0;
        }

        int subStepCount = tims != null && tims.ControlConfig != null
            ? tims.ControlConfig.brakeSubstepCount
            : 1;

        TimsNotchHelper.ToContinuousBrakeNotch(
            brakeNotch,
            0,
            Mathf.Max(1, subStepCount),
            out int brakeStep
        );

        return Mathf.Max(0, brakeStep);
    }

    public void WriteTimsMasterData(TimsSystem tims)
    {
        if (tims == null)
        {
            return;
        }

        TimsDataBus masterBus = tims.MasterBus;
        masterBus.SetInt(new TimsTagKey("Notch", "ActivatedCabIndex"), cabIndex);
        masterBus.SetInt(new TimsTagKey("Notch", "ManualPowerNotch"), manualPowerNotch);
        masterBus.SetInt(new TimsTagKey("Notch", "ManualBrakeStep"), manualBrakeStep);
        masterBus.SetInt(new TimsTagKey("Notch", "ATCBrakeStep"), atcBrakeStep);
        masterBus.SetInt(new TimsTagKey("Notch", "PowerNotch"), resolvedPowerNotch);
        masterBus.SetInt(new TimsTagKey("Notch", "BrakeStep"), resolvedBrakeStep);
        masterBus.SetInt(new TimsTagKey("Notch", "ReverserPosition"), (int)reverserPosition);
        masterBus.SetString(new TimsTagKey("Notch", "ManualBrakeStepLabel"), FormatBrakeStepLabel(manualBrakeStep));
        masterBus.SetString(new TimsTagKey("Notch", "ATCBrakeStepLabel"), FormatBrakeStepLabel(atcBrakeStep));
        masterBus.SetString(new TimsTagKey("Notch", "BrakeStepLabel"), FormatBrakeStepLabel(resolvedBrakeStep));
        masterBus.SetString(new TimsTagKey("Notch", "ResolvedNotchLabel"), FormatResolvedNotchLabel());
    }

    private string FormatBrakeStepLabel(int brakeStep)
    {
        int subStepCount = tims != null && tims.ControlConfig != null
            ? tims.ControlConfig.brakeSubstepCount
            : 1;

        return TimsNotchHelper.FormatBrakeStepLabel(brakeStep, subStepCount);
    }

    private string FormatResolvedNotchLabel()
    {
        if (resolvedBrakeStep > 0)
        {
            return FormatBrakeStepLabel(resolvedBrakeStep);
        }

        if (resolvedPowerNotch > 0)
        {
            return $"P{resolvedPowerNotch}";
        }

        return "N";
    }
}
