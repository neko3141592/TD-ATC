using UnityEngine;

public class CabActivationSwitch : MonoBehaviour, ITimsDataSource
{
    [SerializeField] private CabDirectionSelection selection = CabDirectionSelection.Forward;

    public CabDirectionSelection Selection => selection;
    public float TransmissionIntervalSeconds => 0.05f;

    public void SetSelection(CabDirectionSelection newSelection)
    {
        selection = newSelection;
    }

    public void SetForward()
    {
        SetSelection(CabDirectionSelection.Forward);
    }

    public void SetReverse()
    {
        SetSelection(CabDirectionSelection.Reverse);
    }

    public void WriteTimsData(TimsCarTerminal terminal)
    {
        if (terminal == null)
        {
            return;
        }

        TimsDataBus localBus = terminal.LocalBus;
        localBus.SetInt(new TimsTagKey("CabActivationSwitch", "Selection"), (int)selection);
        localBus.SetString(new TimsTagKey("CabActivationSwitch", "SelectionLabel"), selection.ToString());
    }
}
