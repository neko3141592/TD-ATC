using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum TimsGraphBindingMode
{
    LocalCarsFloat,
    MasterFloatArray
}

[ExecuteAlways]
public class TimsBoundBarGraphGroup : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform graphRoot;

    [Header("Runtime")]
    [SerializeField] private TimsSystem tims;

    [Header("Binding")]
    [SerializeField] private TimsGraphBindingMode bindingMode = TimsGraphBindingMode.LocalCarsFloat;
    [SerializeField] private string deviceName = "BrakeSystem";
    [SerializeField] private string itemName = "BCPressureKPa";
    [SerializeField] private bool useConsistCarCount = true;
    [SerializeField, Min(1)] private int fixedBarCount = 10;
    [SerializeField, Min(0)] private int startIndex = 0;
    [SerializeField] private float valueScale = 1f;
    [SerializeField] private float valueOffset = 0f;

    [Header("Scale")]
    [SerializeField] private float minValue = 0f;
    [SerializeField] private float maxValue = 400f;
    [SerializeField] private bool zeroCentered = false;
    [SerializeField, Min(0f)] private float visibleThreshold = 0f;

    [Header("Layout")]
    [SerializeField] private Vector2 barSize = new Vector2(12f, 80f);
    [SerializeField, Min(0f)] private float spacing = 4f;
    [SerializeField, Min(0f)] private float labelHeight = 14f;
    [SerializeField] private Vector2 valueLabelOffset = new Vector2(0f, 47f);
    [SerializeField] private bool regenerateOnValidate = false;

    [Header("Labels")]
    [SerializeField] private bool showValueLabels = true;
    [SerializeField] private string valueFormat = "0";
    [SerializeField, Min(1f)] private float valueFontSize = 8f;
    [SerializeField] private Color valueTextColor = Color.white;
    [SerializeField] private TMP_FontAsset fontAsset;

    [Header("Title")]
    [SerializeField] private bool showTitle = false;
    [SerializeField] private string titleText = "GRAPH";
    [SerializeField] private Vector2 titleOffset = new Vector2(0f, 64f);
    [SerializeField] private Vector2 titleSize = new Vector2(120f, 18f);
    [SerializeField, Min(1f)] private float titleFontSize = 10f;
    [SerializeField] private Color titleColor = Color.white;

    [Header("Axis")]
    [SerializeField] private bool showVerticalAxis = true;
    [SerializeField] private bool showAxisLabels = true;
    [SerializeField] private bool showAxisLine = true;
    [SerializeField] private bool showHorizontalGridLines = true;
    [SerializeField] private float[] axisTicks = new float[] { 0f, 200f, 400f, 600f };
    [SerializeField] private Vector2 axisOriginOffset = new Vector2(-16f, 0f);
    [SerializeField] private Vector2 axisLabelOffset = Vector2.zero;
    [SerializeField, Min(0f)] private float axisLabelWidth = 32f;
    [SerializeField, Min(0f)] private float axisTickLength = 5f;
    [SerializeField, Min(1f)] private float axisLineThickness = 1f;
    [SerializeField, Min(0f)] private float horizontalGridLineWidth = 180f;
    [SerializeField] private bool emphasizeAxisValue = true;
    [SerializeField] private float emphasizedAxisValue = 0f;
    [SerializeField, Min(0f)] private float emphasizedGridLineWidth = 240f;
    [SerializeField, Min(1f)] private float emphasizedGridLineThickness = 1f;
    [SerializeField] private Color emphasizedGridLineColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField, Min(1f)] private float axisFontSize = 8f;
    [SerializeField] private string axisValueFormat = "0";
    [SerializeField] private Color axisColor = new Color(1f, 1f, 1f, 0.72f);
    [SerializeField] private Color gridLineColor = new Color(1f, 1f, 1f, 0.28f);

    [Header("Colors")]
    [SerializeField] private Color backgroundColor = new Color(1f, 1f, 1f, 0.14f);
    [SerializeField] private Color positiveFillColor = new Color(0.34f, 1f, 1f, 0.95f);
    [SerializeField] private Color negativeFillColor = new Color(0.55f, 0.8f, 1f, 0.95f);
    [SerializeField] private Color missingFillColor = new Color(1f, 1f, 1f, 0.08f);

    private const string BarPrefix = "Bar_";
    private const string AxisRootName = "Axis";
    private const string TitleName = "Title";
    private const string AxisLineName = "AxisLine";
    private const string GridLinePrefix = "GridLine_";
    private const string TickPrefix = "Tick_";
    private const string TickMarkName = "Mark";
    private const string TickLabelName = "Label";
    private const string TrackName = "Track";
    private const string FillName = "Fill";
    private const string ValueLabelName = "ValueLabel";

    private readonly List<Image> trackImages = new List<Image>();
    private readonly List<Image> fillImages = new List<Image>();
    private readonly List<TextMeshProUGUI> valueLabels = new List<TextMeshProUGUI>();
    private readonly List<float> values = new List<float>();
    private readonly List<bool> foundValues = new List<bool>();

    private void Reset()
    {
        graphRoot = transform as RectTransform;
    }

    private void OnEnable()
    {
        ResolveReferences();
        RebuildGeneratedCache();

        if (Application.isPlaying && fillImages.Count != GetBarCount())
        {
            Generate();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveReferences();

        if (fillImages.Count != GetBarCount())
        {
            Generate();
        }

        ReadValues();
        ApplyValues();
    }

    private void OnValidate()
    {
        NormalizeSettings();
        ResolveReferences();
        ApplyLayoutToGenerated();

        if (!regenerateOnValidate)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Generate();
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= DelayedGenerateInEditor;
        UnityEditor.EditorApplication.delayCall += DelayedGenerateInEditor;
#endif
    }

#if UNITY_EDITOR
    private void DelayedGenerateInEditor()
    {
        if (this == null || Application.isPlaying)
        {
            return;
        }

        Generate();
    }
#endif

    [ContextMenu("Generate TIMS Bar Graph Group")]
    public void Generate()
    {
        NormalizeSettings();
        ResolveReferences();
        if (graphRoot == null)
        {
            return;
        }

        EnsureHorizontalLayout(graphRoot);
        ClearGenerated();
        CreateTitle();
        CreateAxis();

        int count = GetBarCount();
        for (int i = 0; i < count; i++)
        {
            CreateBar(i);
        }

        ReadValues();
        ApplyValues();
    }

    [ContextMenu("Clear TIMS Bar Graph Group")]
    public void ClearGenerated()
    {
        trackImages.Clear();
        fillImages.Clear();
        valueLabels.Clear();
        values.Clear();
        foundValues.Clear();

        if (graphRoot == null)
        {
            return;
        }

        for (int i = graphRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = graphRoot.GetChild(i);
            if (!child.name.StartsWith(BarPrefix) && child.name != AxisRootName && child.name != TitleName)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void ResolveReferences()
    {
        if (graphRoot == null)
        {
            graphRoot = transform as RectTransform;
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

    private void NormalizeSettings()
    {
        fixedBarCount = Mathf.Max(1, fixedBarCount);
        startIndex = Mathf.Max(0, startIndex);
        barSize.x = Mathf.Max(1f, barSize.x);
        barSize.y = Mathf.Max(1f, barSize.y);
        spacing = Mathf.Max(0f, spacing);
        labelHeight = Mathf.Max(0f, labelHeight);
        valueFontSize = Mathf.Max(1f, valueFontSize);
        titleSize.x = Mathf.Max(1f, titleSize.x);
        titleSize.y = Mathf.Max(1f, titleSize.y);
        titleFontSize = Mathf.Max(1f, titleFontSize);
        axisLabelWidth = Mathf.Max(0f, axisLabelWidth);
        axisTickLength = Mathf.Max(0f, axisTickLength);
        axisLineThickness = Mathf.Max(1f, axisLineThickness);
        horizontalGridLineWidth = Mathf.Max(0f, horizontalGridLineWidth);
        emphasizedGridLineWidth = Mathf.Max(0f, emphasizedGridLineWidth);
        emphasizedGridLineThickness = Mathf.Max(1f, emphasizedGridLineThickness);
        axisFontSize = Mathf.Max(1f, axisFontSize);

        if (Mathf.Approximately(minValue, maxValue))
        {
            maxValue = minValue + 1f;
        }
    }

    private int GetBarCount()
    {
        if (!useConsistCarCount || tims == null || tims.ConsistDefinition == null)
        {
            return fixedBarCount;
        }

        return Mathf.Max(0, tims.ConsistDefinition.CarCount - startIndex);
    }

    private void EnsureHorizontalLayout(RectTransform root)
    {
        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        }

        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childScaleWidth = false;
        layout.childScaleHeight = false;
    }

    private void CreateTitle()
    {
        if (!showTitle || graphRoot == null)
        {
            return;
        }

        GameObject titleGo = new GameObject(TitleName, typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.SetParent(graphRoot, false);
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = titleOffset;
        titleRect.sizeDelta = titleSize;

        LayoutElement layoutElement = titleGo.GetComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        TextMeshProUGUI title = titleGo.GetComponent<TextMeshProUGUI>();
        title.text = titleText;
        title.fontSize = titleFontSize;
        title.color = titleColor;
        title.alignment = TextAlignmentOptions.Center;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Overflow;
        title.raycastTarget = false;
        if (fontAsset != null)
        {
            title.font = fontAsset;
        }
    }

    private void CreateAxis()
    {
        if (!showVerticalAxis || graphRoot == null)
        {
            return;
        }

        GameObject axisGo = new GameObject(AxisRootName, typeof(RectTransform), typeof(LayoutElement));
        RectTransform axisRect = axisGo.GetComponent<RectTransform>();
        axisRect.SetParent(graphRoot, false);
        axisRect.anchorMin = axisRect.anchorMax = new Vector2(0.5f, 0.5f);
        axisRect.pivot = new Vector2(0.5f, 0.5f);
        axisRect.anchoredPosition = axisOriginOffset;
        axisRect.sizeDelta = new Vector2(axisLabelWidth + axisTickLength + horizontalGridLineWidth, barSize.y);

        LayoutElement layoutElement = axisGo.GetComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        if (showAxisLine)
        {
            Image axisLine = CreateImage(axisRect, AxisLineName, axisColor);
            RectTransform lineRect = axisLine.rectTransform;
            lineRect.anchorMin = lineRect.anchorMax = new Vector2(0f, 0.5f);
            lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.anchoredPosition = Vector2.zero;
            lineRect.sizeDelta = new Vector2(axisLineThickness, barSize.y);
        }

        if (axisTicks == null)
        {
            return;
        }

        for (int i = 0; i < axisTicks.Length; i++)
        {
            CreateAxisTick(axisRect, i, axisTicks[i]);
        }
    }

    private void CreateAxisTick(RectTransform axisRoot, int tickIndex, float tickValue)
    {
        float y = ValueToAxisY(tickValue);

        if (showHorizontalGridLines && horizontalGridLineWidth > 0f)
        {
            bool emphasized = IsEmphasizedAxisTick(tickValue);
            Image gridLine = CreateImage(axisRoot, $"{GridLinePrefix}{tickIndex + 1:00}", emphasized ? emphasizedGridLineColor : gridLineColor);
            RectTransform gridRect = gridLine.rectTransform;
            gridRect.anchorMin = gridRect.anchorMax = new Vector2(0f, 0.5f);
            gridRect.pivot = new Vector2(0f, 0.5f);
            gridRect.anchoredPosition = new Vector2(0f, y);
            gridRect.sizeDelta = new Vector2(
                emphasized ? emphasizedGridLineWidth : horizontalGridLineWidth,
                emphasized ? emphasizedGridLineThickness : axisLineThickness);
        }

        GameObject tickGo = new GameObject($"{TickPrefix}{tickIndex + 1:00}", typeof(RectTransform));
        RectTransform tickRect = tickGo.GetComponent<RectTransform>();
        tickRect.SetParent(axisRoot, false);
        tickRect.anchorMin = tickRect.anchorMax = new Vector2(0f, 0.5f);
        tickRect.pivot = new Vector2(0f, 0.5f);
        tickRect.anchoredPosition = new Vector2(0f, y);
        tickRect.sizeDelta = new Vector2(axisLabelWidth + axisTickLength, labelHeight);

        if (axisTickLength > 0f)
        {
            Image mark = CreateImage(tickRect, TickMarkName, axisColor);
            RectTransform markRect = mark.rectTransform;
            markRect.anchorMin = markRect.anchorMax = new Vector2(0f, 0.5f);
            markRect.pivot = new Vector2(0f, 0.5f);
            markRect.anchoredPosition = Vector2.zero;
            markRect.sizeDelta = new Vector2(axisTickLength, axisLineThickness);
        }

        if (!showAxisLabels)
        {
            return;
        }

        TextMeshProUGUI label = CreateLabel(tickRect, TickLabelName, axisColor, axisFontSize);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(1f, 0.5f);
        labelRect.anchoredPosition = new Vector2(-axisTickLength, 0f) + axisLabelOffset;
        labelRect.sizeDelta = new Vector2(axisLabelWidth, labelHeight);
        label.alignment = TextAlignmentOptions.MidlineRight;
        label.text = tickValue.ToString(axisValueFormat);
    }

    private void CreateBar(int barIndex)
    {
        GameObject barGo = new GameObject($"{BarPrefix}{barIndex + 1:00}", typeof(RectTransform), typeof(LayoutElement));
        RectTransform barRect = barGo.GetComponent<RectTransform>();
        barRect.SetParent(graphRoot, false);
        barRect.sizeDelta = new Vector2(barSize.x, barSize.y + labelHeight);

        LayoutElement layoutElement = barGo.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = barSize.x;
        layoutElement.preferredHeight = barSize.y + labelHeight;

        Image track = CreateImage(barRect, TrackName, backgroundColor);
        Image fill = CreateImage(track.rectTransform, FillName, positiveFillColor);
        TextMeshProUGUI valueLabel = CreateLabel(barRect, ValueLabelName, valueTextColor, valueFontSize);

        trackImages.Add(track);
        fillImages.Add(fill);
        valueLabels.Add(valueLabel);

        ApplyBarLayout(barIndex);
    }

    private Image CreateImage(RectTransform parent, string objectName, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI CreateLabel(RectTransform parent, string objectName, Color color, float fontSize)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.text = string.Empty;
        label.color = color;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        if (fontAsset != null)
        {
            label.font = fontAsset;
        }

        return label;
    }

    private void RebuildGeneratedCache()
    {
        trackImages.Clear();
        fillImages.Clear();
        valueLabels.Clear();

        if (graphRoot == null)
        {
            return;
        }

        for (int i = 0; i < graphRoot.childCount; i++)
        {
            Transform bar = graphRoot.GetChild(i);
            if (!bar.name.StartsWith(BarPrefix))
            {
                continue;
            }

            trackImages.Add(bar.Find(TrackName)?.GetComponent<Image>());
            fillImages.Add(bar.Find($"{TrackName}/{FillName}")?.GetComponent<Image>());
            valueLabels.Add(bar.Find(ValueLabelName)?.GetComponent<TextMeshProUGUI>());
        }
    }

    private void ReadValues()
    {
        int count = GetBarCount();
        EnsureValueCache(count);
        TimsTagKey key = new TimsTagKey(deviceName, itemName);

        float[] masterArray = null;
        if (bindingMode == TimsGraphBindingMode.MasterFloatArray && tims != null)
        {
            tims.MasterBus.TryGetFloatArray(key, out masterArray);
        }

        for (int i = 0; i < count; i++)
        {
            int sourceIndex = startIndex + i;
            bool found = TryReadValue(key, sourceIndex, masterArray, out float rawValue);
            foundValues[i] = found;
            values[i] = found ? (rawValue * valueScale) + valueOffset : 0f;
        }
    }

    private bool TryReadValue(TimsTagKey key, int sourceIndex, float[] masterArray, out float value)
    {
        value = 0f;

        switch (bindingMode)
        {
            case TimsGraphBindingMode.LocalCarsFloat:
                TimsDataBus localBus = GetLocalBus(sourceIndex);
                return localBus != null && localBus.TryGetFloat(key, out value);
            case TimsGraphBindingMode.MasterFloatArray:
                if (masterArray == null || sourceIndex < 0 || sourceIndex >= masterArray.Length)
                {
                    return false;
                }

                value = masterArray[sourceIndex];
                return true;
            default:
                return false;
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

    private void EnsureValueCache(int count)
    {
        while (values.Count < count)
        {
            values.Add(0f);
            foundValues.Add(false);
        }

        if (values.Count > count)
        {
            values.RemoveRange(count, values.Count - count);
            foundValues.RemoveRange(count, foundValues.Count - count);
        }
    }

    private void ApplyValues()
    {
        int count = Mathf.Min(values.Count, fillImages.Count);
        for (int i = 0; i < count; i++)
        {
            ApplyBarLayout(i);
            ApplyBarValue(i, values[i], foundValues[i]);
        }
    }

    private void ApplyLayoutToGenerated()
    {
        if (graphRoot == null)
        {
            return;
        }

        EnsureHorizontalLayout(graphRoot);
        RebuildGeneratedCache();
        ApplyTitleLayoutToGenerated();
        ApplyAxisLayoutToGenerated();
        int count = Mathf.Min(trackImages.Count, fillImages.Count);
        for (int i = 0; i < count; i++)
        {
            ApplyBarLayout(i);
        }
    }

    private void ApplyTitleLayoutToGenerated()
    {
        Transform titleTransform = graphRoot != null ? graphRoot.Find(TitleName) : null;
        if (titleTransform == null)
        {
            return;
        }

        titleTransform.gameObject.SetActive(showTitle);
        RectTransform titleRect = titleTransform.GetComponent<RectTransform>();
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = titleOffset;
        titleRect.sizeDelta = titleSize;

        TextMeshProUGUI title = titleTransform.GetComponent<TextMeshProUGUI>();
        if (title == null)
        {
            return;
        }

        title.text = titleText;
        title.fontSize = titleFontSize;
        title.color = titleColor;
        title.alignment = TextAlignmentOptions.Center;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Overflow;
        if (fontAsset != null)
        {
            title.font = fontAsset;
        }
    }

    private void ApplyAxisLayoutToGenerated()
    {
        Transform axis = graphRoot != null ? graphRoot.Find(AxisRootName) : null;
        if (axis == null)
        {
            return;
        }

        if (!showVerticalAxis)
        {
            axis.gameObject.SetActive(false);
            return;
        }

        axis.gameObject.SetActive(true);
        RectTransform axisRect = axis.GetComponent<RectTransform>();
        axisRect.anchorMin = axisRect.anchorMax = new Vector2(0.5f, 0.5f);
        axisRect.pivot = new Vector2(0.5f, 0.5f);
        axisRect.anchoredPosition = axisOriginOffset;
        axisRect.sizeDelta = new Vector2(axisLabelWidth + axisTickLength + horizontalGridLineWidth, barSize.y);

        Image axisLine = axis.Find(AxisLineName)?.GetComponent<Image>();
        if (axisLine != null)
        {
            axisLine.enabled = showAxisLine;
            axisLine.color = axisColor;
            RectTransform lineRect = axisLine.rectTransform;
            lineRect.sizeDelta = new Vector2(axisLineThickness, barSize.y);
        }
    }

    private void ApplyBarLayout(int barIndex)
    {
        Image track = barIndex < trackImages.Count ? trackImages[barIndex] : null;
        Image fill = barIndex < fillImages.Count ? fillImages[barIndex] : null;
        TextMeshProUGUI valueLabel = barIndex < valueLabels.Count ? valueLabels[barIndex] : null;

        if (track != null)
        {
            RectTransform trackRect = track.rectTransform;
            trackRect.anchorMin = trackRect.anchorMax = new Vector2(0.5f, 0.5f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.anchoredPosition = Vector2.zero;
            trackRect.sizeDelta = barSize;
            track.color = backgroundColor;
        }

        if (fill != null)
        {
            fill.raycastTarget = false;
        }

        if (valueLabel != null)
        {
            RectTransform valueRect = valueLabel.rectTransform;
            valueRect.anchorMin = valueRect.anchorMax = new Vector2(0.5f, 0.5f);
            valueRect.pivot = new Vector2(0.5f, 0.5f);
            valueRect.anchoredPosition = valueLabelOffset;
            valueRect.sizeDelta = new Vector2(Mathf.Max(barSize.x * 2f, 24f), labelHeight);
            valueLabel.enabled = showValueLabels;
            valueLabel.fontSize = valueFontSize;
            valueLabel.color = valueTextColor;
            if (fontAsset != null)
            {
                valueLabel.font = fontAsset;
            }
        }
    }

    private void ApplyBarValue(int barIndex, float value, bool found)
    {
        Image fill = barIndex < fillImages.Count ? fillImages[barIndex] : null;
        TextMeshProUGUI valueLabel = barIndex < valueLabels.Count ? valueLabels[barIndex] : null;

        if (fill != null)
        {
            ApplyFill(fill, value, found);
        }

        if (valueLabel != null)
        {
            valueLabel.enabled = showValueLabels && found;
            valueLabel.text = found ? value.ToString(valueFormat) : string.Empty;
        }
    }

    private void ApplyFill(Image fill, float value, bool found)
    {
        RectTransform fillRect = fill.rectTransform;

        if (!found)
        {
            fill.enabled = true;
            fill.color = missingFillColor;
            fillRect.anchorMin = fillRect.anchorMax = new Vector2(0.5f, 0f);
            fillRect.pivot = new Vector2(0.5f, 0f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(barSize.x, 0f);
            return;
        }

        float min = Mathf.Min(minValue, maxValue);
        float max = Mathf.Max(minValue, maxValue);
        float clamped = Mathf.Clamp(value, min, max);
        fill.enabled = Mathf.Abs(value) > visibleThreshold;
        fill.color = value < 0f ? negativeFillColor : positiveFillColor;

        if (zeroCentered)
        {
            float zeroRatio = Mathf.InverseLerp(min, max, Mathf.Clamp(0f, min, max));
            float valueRatio = Mathf.InverseLerp(min, max, clamped);
            float fillHeight = Mathf.Abs(valueRatio - zeroRatio) * barSize.y;
            fillRect.anchorMin = fillRect.anchorMax = new Vector2(0.5f, zeroRatio);
            fillRect.pivot = new Vector2(0.5f, valueRatio >= zeroRatio ? 0f : 1f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(barSize.x, fillHeight);
            return;
        }

        float ratio = Mathf.InverseLerp(min, max, clamped);
        fillRect.anchorMin = fillRect.anchorMax = new Vector2(0.5f, 0f);
        fillRect.pivot = new Vector2(0.5f, 0f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(barSize.x, barSize.y * ratio);
    }

    private float ValueToAxisY(float value)
    {
        float min = Mathf.Min(minValue, maxValue);
        float max = Mathf.Max(minValue, maxValue);
        float ratio = Mathf.InverseLerp(min, max, Mathf.Clamp(value, min, max));
        return Mathf.Lerp(-barSize.y * 0.5f, barSize.y * 0.5f, ratio);
    }

    private bool IsEmphasizedAxisTick(float tickValue)
    {
        return emphasizeAxisValue && Mathf.Approximately(tickValue, emphasizedAxisValue);
    }
}
