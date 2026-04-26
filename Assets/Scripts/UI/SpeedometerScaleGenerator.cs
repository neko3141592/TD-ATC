using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class SpeedometerScaleGenerator : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform scaleRoot;

    [Header("Speed Range (km/h)")]
    [SerializeField] private float minSpeedKmH = 0f;
    [SerializeField] private float maxSpeedKmH = 120f;

    [Header("Angle Range (deg)")]
    [SerializeField] private float minAngleDeg = 210f;
    [SerializeField] private float maxAngleDeg = -30f;
    [SerializeField] private float angleOffsetDeg = 0f;
    [SerializeField] private bool invertDirection = false;

    [Header("Tick Settings")]
    [SerializeField, Min(0.1f)] private float majorStepKmH = 10f;
    [SerializeField, Min(0.1f)] private float minorStepKmH = 5f;
    [SerializeField] private bool generateMiddleTicks = true;
    [SerializeField, Min(0.1f)] private float middleStepKmH = 2.5f;
    [SerializeField, Min(1f)] private float outerRadius = 170f;
    [SerializeField, Min(1f)] private float majorTickLength = 18f;
    [SerializeField, Min(1f)] private float middleTickLength = 14f;
    [SerializeField, Min(1f)] private float minorTickLength = 10f;
    [SerializeField, Min(1f)] private float majorTickWidth = 4f;
    [SerializeField, Min(1f)] private float middleTickWidth = 3f;
    [SerializeField, Min(1f)] private float minorTickWidth = 2f;
    [SerializeField] private Color tickColor = Color.black;

    [Header("Label Settings")]
    [SerializeField] private bool generateLabels = true;
    [SerializeField, Min(1f)] private float labelRadius = 140f;
    [SerializeField] private Vector2 labelSize = new Vector2(90f, 36f);
    [SerializeField, Min(8f)] private float labelFontSize = 24f;
    [SerializeField] private Color labelColor = Color.black;
    [SerializeField] private TMP_FontAsset labelFont;

    [Header("Editor")]
    [SerializeField] private bool regenerateOnValidate = true;

    private const string TickPrefix = "Tick_";
    private const string LabelPrefix = "Label_";

    /// <summary>
    /// 役割: Reset の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Reset()
    {
        scaleRoot = transform as RectTransform;
    }

    /// <summary>
    /// 役割: OnValidate の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
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
        // Unity のオブジェクト初期化が終わる前に生成してしまうのを防ぎます。
        UnityEditor.EditorApplication.delayCall -= DelayedGenerateInEditor;
        UnityEditor.EditorApplication.delayCall += DelayedGenerateInEditor;
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// 役割: DelayedGenerateInEditor の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
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
    /// <summary>
    /// 役割: GenerateScale の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    public void GenerateScale()
    {
        if (scaleRoot == null)
        {
            Debug.LogWarning("Scale root is not assigned.", this);
            return;
        }

        ClearGeneratedObjects();

        float safeMinorStep = Mathf.Max(0.1f, minorStepKmH);
        float safeMiddleStep = Mathf.Max(0.1f, middleStepKmH);
        float safeMajorStep = Mathf.Max(safeMinorStep, majorStepKmH);
        float baseStep = generateMiddleTicks ? Mathf.Min(safeMinorStep, safeMiddleStep) : safeMinorStep;
        float speedRange = Mathf.Max(0.001f, maxSpeedKmH - minSpeedKmH);
        int guard = 0;

        for (float speed = minSpeedKmH; speed <= maxSpeedKmH + 0.001f; speed += baseStep)
        {
            guard++;
            if (guard > 2000)
            {
                Debug.LogWarning("Scale generation aborted: too many ticks.", this);
                break;
            }

            float offset = speed - minSpeedKmH;
            bool isMajor = IsMultipleOfStep(offset, safeMajorStep);
            bool isMiddle = !isMajor && generateMiddleTicks && IsMultipleOfStep(offset, safeMiddleStep);
            bool isMinor = !isMajor && !isMiddle && IsMultipleOfStep(offset, safeMinorStep);
            if (!isMajor && !isMiddle && !isMinor)
            {
                continue;
            }

            float t = Mathf.Clamp01((speed - minSpeedKmH) / speedRange);
            if (invertDirection)
            {
                t = 1f - t;
            }

            float angle = Mathf.Lerp(minAngleDeg, maxAngleDeg, t) + angleOffsetDeg;
            float tickLength = isMajor ? majorTickLength : (isMiddle ? middleTickLength : minorTickLength);
            float tickWidth = isMajor ? majorTickWidth : (isMiddle ? middleTickWidth : minorTickWidth);

            CreateTick(speed, angle, tickLength, tickWidth);

            if (isMajor && generateLabels)
            {
                CreateLabel(speed, angle);
            }
        }
    }

    [ContextMenu("Clear Generated")]
    /// <summary>
    /// 役割: ClearGeneratedObjects の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    public void ClearGeneratedObjects()
    {
        if (scaleRoot == null)
        {
            return;
        }

        for (int i = scaleRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = scaleRoot.GetChild(i);
            if (!child.name.StartsWith(TickPrefix) && !child.name.StartsWith(LabelPrefix))
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

    /// <summary>
    /// 役割: CreateTick の処理を実行します。
    /// </summary>
    /// <param name="speed">speed を指定します。</param>
    /// <param name="angle">angle を指定します。</param>
    /// <param name="length">length を指定します。</param>
    /// <param name="width">width を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void CreateTick(float speed, float angle, float length, float width)
    {
        GameObject tick = new GameObject($"{TickPrefix}{Mathf.RoundToInt(speed)}", typeof(RectTransform));
        tick.transform.SetParent(scaleRoot, false);

        RectTransform tickRt = tick.GetComponent<RectTransform>();
        tickRt.anchorMin = new Vector2(0.5f, 0.5f);
        tickRt.anchorMax = new Vector2(0.5f, 0.5f);
        tickRt.pivot = new Vector2(0.5f, 0.5f);
        tickRt.anchoredPosition = Vector2.zero;
        tickRt.localEulerAngles = new Vector3(0f, 0f, angle);

        GameObject line = new GameObject("Line", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(tickRt, false);

        RectTransform lineRt = line.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0.5f, 0.5f);
        lineRt.anchorMax = new Vector2(0.5f, 0.5f);
        lineRt.pivot = new Vector2(0.5f, 0.5f);
        lineRt.sizeDelta = new Vector2(width, length);
        lineRt.anchoredPosition = new Vector2(0f, outerRadius - (length * 0.5f));

        Image image = line.GetComponent<Image>();
        image.color = tickColor;
        image.raycastTarget = false;
    }

    /// <summary>
    /// 役割: CreateLabel の処理を実行します。
    /// </summary>
    /// <param name="speed">speed を指定します。</param>
    /// <param name="angle">angle を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void CreateLabel(float speed, float angle)
    {
        GameObject label = new GameObject($"{LabelPrefix}{Mathf.RoundToInt(speed)}", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(scaleRoot, false);

        RectTransform labelRt = label.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0.5f, 0.5f);
        labelRt.anchorMax = new Vector2(0.5f, 0.5f);
        labelRt.pivot = new Vector2(0.5f, 0.5f);
        labelRt.sizeDelta = new Vector2(Mathf.Max(1f, labelSize.x), Mathf.Max(1f, labelSize.y));

        float rad = angle * Mathf.Deg2Rad;
        // Tick と同じ「上方向基準」の回転系に合わせる
        Vector2 dir = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
        labelRt.anchoredPosition = dir * labelRadius;

        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        text.text = Mathf.RoundToInt(speed).ToString();
        text.fontSize = labelFontSize;
        text.color = labelColor;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        if (labelFont != null)
        {
            text.font = labelFont;
        }
    }

    /// <summary>
    /// 役割: IsMultipleOfStep の処理を実行します。
    /// </summary>
    /// <param name="value">value を指定します。</param>
    /// <param name="step">step を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private static bool IsMultipleOfStep(float value, float step)
    {
        if (step <= 0f)
        {
            return true;
        }

        float ratio = value / step;
        return Mathf.Abs(ratio - Mathf.Round(ratio)) <= 0.001f;
    }
}
