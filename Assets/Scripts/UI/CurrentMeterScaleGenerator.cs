using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class CurrentMeterScaleGenerator : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform scaleRoot;

    [Header("Current Range (A)")]
    [SerializeField] private float minCurrentA = 0f;
    [SerializeField] private float maxCurrentA = 2000f;

    [Header("Graph Settings")]
    [SerializeField] private Vector2 graphOffset = Vector2.zero;
    [SerializeField, Min(1f)] private float graphHeight = 170f;
    [SerializeField, Min(1f)] private float graphWidth = 8f;
    [SerializeField] private Color graphTrackColor = new Color(0.06f, 0.13f, 0.13f, 0.85f);
    [SerializeField] private Color graphFillColor = new Color(0.34f, 1f, 1f, 0.95f);

    [Header("Tick Settings")]
    [SerializeField, Min(0.1f)] private float majorStepA = 1000f;
    [SerializeField, Min(0.1f)] private float minorStepA = 250f;
    [SerializeField, Min(1f)] private float majorTickLength = 28f;
    [SerializeField, Min(1f)] private float minorTickLength = 12f;
    [SerializeField, Min(1f)] private float majorTickWidth = 3f;
    [SerializeField, Min(1f)] private float minorTickWidth = 2f;
    [SerializeField, Min(0f)] private float tickGap = 12f;
    [SerializeField] private TickSide tickSide = TickSide.Right;
    [SerializeField] private Color tickColor = new Color(0.82f, 0.95f, 1f, 0.9f);

    [Header("Label Settings")]
    [SerializeField] private bool generateLabels = true;
    [SerializeField] private Vector2 labelSize = new Vector2(54f, 20f);
    [SerializeField, Min(1f)] private float labelGap = 6f;
    [SerializeField, Min(8f)] private float labelFontSize = 14f;
    [SerializeField] private Color labelColor = new Color(0.88f, 0.96f, 1f, 0.95f);
    [SerializeField] private TMP_FontAsset labelFont;

    [Header("Value Display")]
    [SerializeField] private bool generateValueDisplay = true;
    [SerializeField, Min(0f)] private float valueGapFromGraphBottom = 28f;
    [SerializeField] private Vector2 valueSize = new Vector2(70f, 42f);
    [SerializeField, Min(8f)] private float valueFontSize = 34f;
    [SerializeField] private Vector2 unitOffsetFromValue = new Vector2(34f, -7f);
    [SerializeField] private Vector2 unitSize = new Vector2(26f, 18f);
    [SerializeField, Min(8f)] private float unitFontSize = 14f;
    [SerializeField] private Color valueColor = new Color(0.88f, 0.96f, 1f, 0.95f);
    [SerializeField] private TMP_FontAsset valueFont;

    [Header("Editor")]
    [SerializeField] private bool regenerateOnValidate = true;

    private const string GraphTrackName = "CurrentGraph_Track";
    private const string GraphFillName = "CurrentGraph_Fill";
    private const string ValueName = "CurrentGraph_Value";
    private const string UnitName = "CurrentGraph_Unit";
    private const string TickPrefix = "CurrentTick_";
    private const string LabelPrefix = "CurrentLabel_";

    private enum TickSide
    {
        Left,
        Right
    }

    private void Reset()
    {
        scaleRoot = transform as RectTransform;
    }

    private void OnValidate()
    {
        if (!regenerateOnValidate)
        {
            return;
        }

        if (Application.isPlaying)
        {
            GenerateScale();
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

        GenerateScale();
    }
#endif

    [ContextMenu("Generate Scale")]
    public void GenerateScale()
    {
#if UNITY_EDITOR
        if (IsPrefabAssetContext())
        {
            return;
        }
#endif

        if (scaleRoot == null)
        {
            scaleRoot = transform as RectTransform;
        }

        if (scaleRoot == null)
        {
            Debug.LogWarning("Scale root is not assigned.", this);
            return;
        }

        ClearGeneratedObjects();
        CreateGraph();
        CreateTicksAndLabels();
        CreateValueDisplay();
    }

    [ContextMenu("Clear Generated")]
    public void ClearGeneratedObjects()
    {
#if UNITY_EDITOR
        if (IsPrefabAssetContext())
        {
            return;
        }
#endif

        if (scaleRoot == null)
        {
            return;
        }

        for (int i = scaleRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = scaleRoot.GetChild(i);
            if (child.name != GraphTrackName &&
                child.name != ValueName &&
                child.name != UnitName &&
                !child.name.StartsWith(TickPrefix) &&
                !child.name.StartsWith(LabelPrefix))
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

    private void CreateValueDisplay()
    {
        if (!generateValueDisplay)
        {
            return;
        }

        float valueY = graphOffset.y - (graphHeight * 0.5f) - valueGapFromGraphBottom;
        GameObject value = new GameObject(ValueName, typeof(RectTransform), typeof(TextMeshProUGUI));
        value.transform.SetParent(scaleRoot, false);

        RectTransform valueRt = value.GetComponent<RectTransform>();
        valueRt.anchorMin = new Vector2(0.5f, 0.5f);
        valueRt.anchorMax = new Vector2(0.5f, 0.5f);
        valueRt.pivot = new Vector2(1f, 0.5f);
        valueRt.anchoredPosition = new Vector2(graphOffset.x + (graphWidth * 0.5f), valueY);
        valueRt.sizeDelta = valueSize;

        TextMeshProUGUI valueText = value.GetComponent<TextMeshProUGUI>();
        valueText.text = "0";
        valueText.fontSize = valueFontSize;
        valueText.color = valueColor;
        valueText.alignment = TextAlignmentOptions.Right;
        valueText.textWrappingMode = TextWrappingModes.NoWrap;
        valueText.overflowMode = TextOverflowModes.Overflow;
        valueText.raycastTarget = false;
        if (valueFont != null)
        {
            valueText.font = valueFont;
        }

        GameObject unit = new GameObject(UnitName, typeof(RectTransform), typeof(TextMeshProUGUI));
        unit.transform.SetParent(scaleRoot, false);

        RectTransform unitRt = unit.GetComponent<RectTransform>();
        unitRt.anchorMin = new Vector2(0.5f, 0.5f);
        unitRt.anchorMax = new Vector2(0.5f, 0.5f);
        unitRt.pivot = new Vector2(0.5f, 0.5f);
        unitRt.anchoredPosition = valueRt.anchoredPosition + unitOffsetFromValue;
        unitRt.sizeDelta = unitSize;

        TextMeshProUGUI unitText = unit.GetComponent<TextMeshProUGUI>();
        unitText.text = "A";
        unitText.fontSize = unitFontSize;
        unitText.color = valueColor;
        unitText.alignment = TextAlignmentOptions.Center;
        unitText.textWrappingMode = TextWrappingModes.NoWrap;
        unitText.overflowMode = TextOverflowModes.Overflow;
        unitText.raycastTarget = false;
        if (valueFont != null)
        {
            unitText.font = valueFont;
        }
    }

    private void CreateGraph()
    {
        GameObject track = new GameObject(GraphTrackName, typeof(RectTransform), typeof(Image));
        track.transform.SetParent(scaleRoot, false);

        RectTransform trackRt = track.GetComponent<RectTransform>();
        trackRt.anchorMin = new Vector2(0.5f, 0.5f);
        trackRt.anchorMax = new Vector2(0.5f, 0.5f);
        trackRt.pivot = new Vector2(0.5f, 0.5f);
        trackRt.anchoredPosition = graphOffset;
        trackRt.sizeDelta = new Vector2(graphWidth, graphHeight);

        Image trackImage = track.GetComponent<Image>();
        trackImage.color = graphTrackColor;
        trackImage.raycastTarget = false;

        GameObject fill = new GameObject(GraphFillName, typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(trackRt, false);

        RectTransform fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(1f, 0f);
        fillRt.pivot = new Vector2(0.5f, 0f);
        fillRt.anchoredPosition = Vector2.zero;
        fillRt.sizeDelta = Vector2.zero;

        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = graphFillColor;
        fillImage.raycastTarget = false;
        fillImage.enabled = false;
    }

    private void CreateTicksAndLabels()
    {
        float safeMinorStep = Mathf.Max(0.1f, minorStepA);
        float safeMajorStep = Mathf.Max(safeMinorStep, majorStepA);
        float currentRange = Mathf.Max(0.001f, maxCurrentA - minCurrentA);
        int guard = 0;

        for (float currentA = minCurrentA; currentA <= maxCurrentA + 0.001f; currentA += safeMinorStep)
        {
            guard++;
            if (guard > 2000)
            {
                Debug.LogWarning("Current scale generation aborted: too many ticks.", this);
                break;
            }

            bool isMajor = IsMultipleOfStep(currentA - minCurrentA, safeMajorStep);
            float t = Mathf.Clamp01((currentA - minCurrentA) / currentRange);
            float y = graphOffset.y - (graphHeight * 0.5f) + (graphHeight * t);
            float tickLength = isMajor ? majorTickLength : minorTickLength;
            float tickWidth = isMajor ? majorTickWidth : minorTickWidth;

            CreateTick(currentA, y, tickLength, tickWidth, tickSide == TickSide.Right);

            if (isMajor && generateLabels)
            {
                CreateLabel(currentA, y);
            }
        }
    }

    private void CreateTick(float currentA, float y, float length, float width, bool rightSide)
    {
        string side = rightSide ? "R" : "L";
        GameObject tick = new GameObject($"{TickPrefix}{side}_{Mathf.RoundToInt(currentA)}", typeof(RectTransform), typeof(Image));
        tick.transform.SetParent(scaleRoot, false);

        RectTransform tickRt = tick.GetComponent<RectTransform>();
        tickRt.anchorMin = new Vector2(0.5f, 0.5f);
        tickRt.anchorMax = new Vector2(0.5f, 0.5f);
        tickRt.pivot = rightSide ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
        tickRt.sizeDelta = new Vector2(length, width);

        float xSign = rightSide ? 1f : -1f;
        float x = graphOffset.x + (xSign * ((graphWidth * 0.5f) + tickGap));
        tickRt.anchoredPosition = new Vector2(x, y);

        Image image = tick.GetComponent<Image>();
        image.color = tickColor;
        image.raycastTarget = false;
    }

    private void CreateLabel(float currentA, float y)
    {
        GameObject label = new GameObject($"{LabelPrefix}{Mathf.RoundToInt(currentA)}", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(scaleRoot, false);

        RectTransform labelRt = label.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0.5f, 0.5f);
        labelRt.anchorMax = new Vector2(0.5f, 0.5f);
        labelRt.pivot = new Vector2(1f, 0.5f);
        labelRt.sizeDelta = new Vector2(Mathf.Max(1f, labelSize.x), Mathf.Max(1f, labelSize.y));

        float x = graphOffset.x - ((graphWidth * 0.5f) + tickGap + majorTickLength + labelGap);
        labelRt.anchoredPosition = new Vector2(x, y);

        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        text.text = Mathf.RoundToInt(currentA).ToString();
        text.fontSize = labelFontSize;
        text.color = labelColor;
        text.alignment = TextAlignmentOptions.Right;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        if (labelFont != null)
        {
            text.font = labelFont;
        }
    }

    private static bool IsMultipleOfStep(float value, float step)
    {
        if (step <= 0f)
        {
            return true;
        }

        float ratio = value / step;
        return Mathf.Abs(ratio - Mathf.Round(ratio)) <= 0.001f;
    }

#if UNITY_EDITOR
    private bool IsPrefabAssetContext()
    {
        return UnityEditor.EditorUtility.IsPersistent(this) ||
            (scaleRoot != null && UnityEditor.EditorUtility.IsPersistent(scaleRoot));
    }
#endif
}
