using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class TimsDebugHUD : MonoBehaviour
{
    [SerializeField] private TimsSystem tims;
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private bool showBrakeFeedback = true;
    [SerializeField] private bool showMasterBus = true;
    [SerializeField] private bool showLocalBuses = true;
    [SerializeField, Min(1)] private int maxTagsPerBus = 40;
    [SerializeField, Min(0.1f)] private float updateIntervalSeconds = 0.1f;
    [SerializeField] private bool useOnGUIFallback = true;
    [SerializeField] private Rect onGUIRect = new Rect(12f, 80f, 900f, 1600f);

    private readonly StringBuilder builder = new();
    private readonly StringBuilder valueBuilder = new();
    private float nextUpdateTime;
    private string latestText = string.Empty;
    private GUIStyle guiStyle;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();

        if (Time.unscaledTime < nextUpdateTime)
        {
            return;
        }

        nextUpdateTime = Time.unscaledTime + updateIntervalSeconds;
        latestText = BuildText();

        if (targetText != null)
        {
            targetText.text = latestText;
        }
    }

    private void OnGUI()
    {
        if (!useOnGUIFallback || targetText != null)
        {
            return;
        }

        if (guiStyle == null)
        {
            guiStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.green },
                alignment = TextAnchor.UpperLeft,
                wordWrap = false
            };
        }

        GUI.Label(onGUIRect, latestText, guiStyle);
    }

    private void ResolveReferences()
    {
        if (tims == null)
        {
            tims = GetComponentInParent<TimsSystem>();
        }

        if (tims == null)
        {
            tims = FindAnyObjectByType<TimsSystem>();
        }

        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }
    }

    private string BuildText()
    {
        builder.Clear();

        if (tims == null)
        {
            builder.AppendLine("[TIMS Debug]");
            builder.AppendLine("TimsSystem: Not Found");
            return builder.ToString();
        }

        int carCount = tims.ConsistDefinition != null ? tims.ConsistDefinition.CarCount : 0;
        builder.AppendLine($"[TIMS Debug] cars={carCount} terminals={tims.Terminals.Count}");

        if (showBrakeFeedback)
        {
            AppendBrakeFeedback(carCount);
        }

        if (showMasterBus)
        {
            AppendBus("MasterBus", tims.MasterBus);
        }

        if (showLocalBuses)
        {
            AppendLocalBuses(carCount);
        }

        return builder.ToString();
    }

    private void AppendBrakeFeedback(int carCount)
    {
        int count = carCount > 0 ? carCount : tims.Terminals.Count;
        float targetBrakeTotalN = SumMasterArray(new TimsTagKey("Brake", "TargetBrakeForcesN"));
        float targetRegenTotalN = SumMasterArray(new TimsTagKey("Brake", "TargetRegenForcesN"));
        float targetAirTotalN = SumMasterArray(new TimsTagKey("Brake", "TargetAirForcesN"));
        float localTargetRegenTotalN = 0f;
        float actualRegenTotalN = 0f;
        float regenCapTotalN = 0f;
        float vvvfBrakeTotalN = 0f;

        builder.AppendLine();
        builder.AppendLine("[Brake Feedback]");
        builder.AppendLine($"Master Target Total: brake={targetBrakeTotalN:0.###} regen={targetRegenTotalN:0.###} air={targetAirTotalN:0.###}");

        for (int i = 0; i < count; i++)
        {
            TimsDataBus localBus = GetLocalBus(i);
            float masterTargetRegenN = GetMasterArrayValue(new TimsTagKey("Brake", "TargetRegenForcesN"), i);
            float localTargetRegenN = GetLocalFloat(localBus, new TimsTagKey("BrakeSystem", "TargetRegenForcekN")) * 1000f;
            float actualRegenN = GetLocalFloat(localBus, new TimsTagKey("BrakeSystem", "RegenForcekN")) * 1000f;
            float regenCapN = GetLocalFloat(localBus, new TimsTagKey("BrakeSystem", "RegenCapN"));
            float vvvfForceN = GetLocalFloat(localBus, new TimsTagKey("VVVF", "TotalMotorTractionForceN"));
            string driveMode = GetLocalString(localBus, new TimsTagKey("VVVF", "DriveMode"));

            localTargetRegenTotalN += localTargetRegenN;
            actualRegenTotalN += actualRegenN;
            regenCapTotalN += regenCapN;
            vvvfBrakeTotalN += Mathf.Max(0f, -vvvfForceN);

            builder.AppendLine(
                $"Car {i}: cmd={masterTargetRegenN:0.###} localCmd={localTargetRegenN:0.###} feedback={actualRegenN:0.###} cap={regenCapN:0.###} vvvf={vvvfForceN:0.###} mode={driveMode}"
            );
        }

        builder.AppendLine($"Local Totals: localCmd={localTargetRegenTotalN:0.###} feedback={actualRegenTotalN:0.###} cap={regenCapTotalN:0.###} vvvfBrake={vvvfBrakeTotalN:0.###}");
    }

    private void AppendLocalBuses(int carCount)
    {
        int count = carCount > 0 ? carCount : tims.Terminals.Count;

        for (int i = 0; i < count; i++)
        {
            TimsCarTerminal terminal = i < tims.Terminals.Count ? tims.Terminals[i] : null;
            if (terminal == null)
            {
                builder.AppendLine();
                builder.AppendLine($"[Car {i}] No Terminal");
                continue;
            }

            AppendBus($"Car {i} LocalBus", terminal.LocalBus);
        }
    }

    private void AppendBus(string title, TimsDataBus bus)
    {
        builder.AppendLine();
        builder.AppendLine($"[{title}]");

        if (bus == null)
        {
            builder.AppendLine("No Bus");
            return;
        }

        List<KeyValuePair<TimsTagKey, TimsValue>> snapshot = bus.GetSnapshot();
        snapshot.Sort((a, b) => string.CompareOrdinal(a.Key.ToString(), b.Key.ToString()));

        if (snapshot.Count == 0)
        {
            builder.AppendLine("No Data");
            return;
        }

        int count = Mathf.Min(snapshot.Count, maxTagsPerBus);
        for (int i = 0; i < count; i++)
        {
            KeyValuePair<TimsTagKey, TimsValue> entry = snapshot[i];
            builder.AppendLine($"{entry.Key}: {FormatValue(entry.Value)}");
        }

        if (snapshot.Count > count)
        {
            builder.AppendLine($"... {snapshot.Count - count} more");
        }
    }

    private TimsDataBus GetLocalBus(int carIndex)
    {
        if (tims == null || tims.Terminals == null || carIndex < 0 || carIndex >= tims.Terminals.Count)
        {
            return null;
        }

        TimsCarTerminal terminal = tims.Terminals[carIndex];
        return terminal != null ? terminal.LocalBus : null;
    }

    private float SumMasterArray(TimsTagKey key)
    {
        if (tims == null || !tims.MasterBus.TryGetFloatArray(key, out float[] values))
        {
            return 0f;
        }

        float total = 0f;
        for (int i = 0; i < values.Length; i++)
        {
            total += values[i];
        }

        return total;
    }

    private float GetMasterArrayValue(TimsTagKey key, int index)
    {
        if (tims == null || index < 0 || !tims.MasterBus.TryGetFloatArray(key, out float[] values))
        {
            return 0f;
        }

        return index < values.Length ? values[index] : 0f;
    }

    private float GetLocalFloat(TimsDataBus bus, TimsTagKey key)
    {
        return bus != null && bus.TryGetFloat(key, out float value) ? value : 0f;
    }

    private string GetLocalString(TimsDataBus bus, TimsTagKey key)
    {
        return bus != null && bus.TryGetString(key, out string value) ? value : "--";
    }

    private string FormatValue(TimsValue value)
    {
        switch (value.Type)
        {
            case TimsValueType.Bool:
                return value.BoolValue ? "true" : "false";
            case TimsValueType.Int:
                return value.IntValue.ToString();
            case TimsValueType.Float:
                return value.FloatValue.ToString("0.###");
            case TimsValueType.String:
                return value.StringValue;
            case TimsValueType.IntArray:
                return FormatArray(value.IntArrayValue);
            case TimsValueType.FloatArray:
                return FormatArray(value.FloatArrayValue);
            case TimsValueType.StringArray:
                return FormatArray(value.StringArrayValue);
            default:
                return "--";
        }
    }

    private string FormatArray(int[] values)
    {
        if (values == null || values.Length == 0)
        {
            return "[]";
        }

        valueBuilder.Clear();
        valueBuilder.Append('[');
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                valueBuilder.Append(", ");
            }

            valueBuilder.Append(values[i]);
        }
        valueBuilder.Append(']');
        return valueBuilder.ToString();
    }

    private string FormatArray(float[] values)
    {
        if (values == null || values.Length == 0)
        {
            return "[]";
        }

        valueBuilder.Clear();
        valueBuilder.Append('[');
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                valueBuilder.Append(", ");
            }

            valueBuilder.Append(values[i].ToString("0.###"));
        }
        valueBuilder.Append(']');
        return valueBuilder.ToString();
    }

    private string FormatArray(string[] values)
    {
        if (values == null || values.Length == 0)
        {
            return "[]";
        }

        valueBuilder.Clear();
        valueBuilder.Append('[');
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                valueBuilder.Append(", ");
            }

            valueBuilder.Append(values[i]);
        }
        valueBuilder.Append(']');
        return valueBuilder.ToString();
    }
}
