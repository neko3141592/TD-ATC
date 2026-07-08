using TMPro;
using UnityEngine;

public enum TimsValueDisplayBus
{
    MasterBus,
    LocalCarBus
}

public enum TimsValueDisplayType
{
    Int,
    Float,
    Bool,
    String
}

[DisallowMultipleComponent]
[AddComponentMenu("TD-ATC/UI/TIMS Bound Value Display")]
public class TimsBoundValueDisplay : MonoBehaviour
{
    [SerializeField] private TimsSystem tims;
    [SerializeField] private TimsValueDisplayBus bus = TimsValueDisplayBus.MasterBus;
    [SerializeField] private TimsValueDisplayType valueType = TimsValueDisplayType.Int;
    [SerializeField] private string deviceName = "Notch";
    [SerializeField] private string itemName = "BrakeStep";
    [SerializeField, Min(0)] private int localCarIndex = 0;

    [Header("Display")]
    [SerializeField] private string titleText = "ノッチ";
    [SerializeField] private string separator = ": ";
    [SerializeField] private string prefix = "B";
    [SerializeField] private string suffix = "";
    [SerializeField] private string floatFormat = "0";
    [SerializeField] private string boolTrueText = "ON";
    [SerializeField] private string boolFalseText = "OFF";
    [SerializeField] private string missingText = "ノッチ: --";

    private TMP_Text targetText;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
        UpdateText();
    }

    private void ResolveReferences()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }

        if (tims == null)
        {
            tims = GetComponentInParent<TimsSystem>();
        }

        if (tims == null)
        {
            tims = FindAnyObjectByType<TimsSystem>();
        }
    }

    private void UpdateText()
    {
        if (targetText == null)
        {
            return;
        }

        TimsDataBus dataBus = GetDataBus();
        if (dataBus == null ||
            !TryReadValueText(dataBus, new TimsTagKey(deviceName, itemName), out string valueText))
        {
            targetText.text = missingText;
            return;
        }

        targetText.text = FormatDisplayText(valueText);
    }

    private bool TryReadValueText(TimsDataBus dataBus, TimsTagKey key, out string valueText)
    {
        valueText = string.Empty;

        switch (valueType)
        {
            case TimsValueDisplayType.Int:
                if (!dataBus.TryGetInt(key, out int intValue))
                {
                    return false;
                }

                valueText = intValue.ToString();
                return true;

            case TimsValueDisplayType.Float:
                if (!dataBus.TryGetFloat(key, out float floatValue))
                {
                    return false;
                }

                valueText = floatValue.ToString(floatFormat);
                return true;

            case TimsValueDisplayType.Bool:
                if (!dataBus.TryGetBool(key, out bool boolValue))
                {
                    return false;
                }

                valueText = boolValue ? boolTrueText : boolFalseText;
                return true;

            case TimsValueDisplayType.String:
                return dataBus.TryGetString(key, out valueText);

            default:
                return false;
        }
    }

    private string FormatDisplayText(string valueText)
    {
        string formattedValue = $"{prefix}{valueText}{suffix}";
        if (string.IsNullOrEmpty(titleText))
        {
            return formattedValue;
        }

        return $"{titleText}{separator}{formattedValue}";
    }

    private TimsDataBus GetDataBus()
    {
        if (tims == null)
        {
            return null;
        }

        if (bus == TimsValueDisplayBus.MasterBus)
        {
            return tims.MasterBus;
        }

        if (tims.Terminals == null ||
            localCarIndex < 0 ||
            localCarIndex >= tims.Terminals.Count ||
            tims.Terminals[localCarIndex] == null)
        {
            return null;
        }

        return tims.Terminals[localCarIndex].LocalBus;
    }
}
