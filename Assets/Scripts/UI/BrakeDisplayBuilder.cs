using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class BrakeDisplayBuilder : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform brakeDisplayRoot;

    [Header("Runtime")]
    [SerializeField] private TrainController train;
    [SerializeField] private bool autoFindTrain = true;
    [SerializeField] private bool showOnlyCurrentNotchText = true;

    [Header("Read Delay")]
    [SerializeField] private bool enableReadDelay = true;
    [SerializeField, Min(0f)] private float readDelaySeconds = 0.06f;

    [Header("Notch")]
    [SerializeField, Min(1)] private int maxBrakeNotch = 8;

    [Header("Layout")]
    [SerializeField, Min(0f)] private float spacing = 8f;
    [SerializeField] private Vector4 padding = new Vector4(8f, 8f, 8f, 8f); // left, top, right, bottom
    [SerializeField, Min(8f)] private float notchItemHeight = 28f;
    [SerializeField, Min(8f)] private float emergencyLabelHeight = 28f;

    [Header("Colors")]
    [SerializeField] private Color notchBackgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.75f);
    [SerializeField] private Color notchFillColor = new Color(1f, 0.86f, 0.2f, 1f);
    [SerializeField] private Color notchTextColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color emergencyTextColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color emergencyBorderColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color emergencyBackgroundColor = new Color(0.4f, 0.05f, 0.05f, 0.25f);

    [Header("Font")]
    [SerializeField, Min(8f)] private float notchFontSize = 20f;
    [SerializeField, Min(8f)] private float emergencyFontSize = 20f;
    [SerializeField] private TMP_FontAsset fontAsset;

    [Header("Editor")]
    [SerializeField] private bool regenerateOnValidate = false;

    private const string EmergencyLabelName = "EmergencyLabel";
    private const string NotchPrefix = "Notch_";
    private const string BackgroundName = "Background";
    private const string FillName = "Fill";
    private const string LabelName = "Label";

    private readonly Dictionary<int, GameObject> notchFillObjects = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, GameObject> notchLabelObjects = new Dictionary<int, GameObject>();
    private GameObject emergencyLabelObject;
    private bool runtimeRefDirty = true;
    private readonly Queue<NotchSample> notchReadBuffer = new Queue<NotchSample>();
    private int displayedBrakeNotch;
    private int lastBufferedBrakeNotch = int.MinValue;

    private struct NotchSample
    {
        public float timestamp;
        public int value;

        public NotchSample(float timestamp, int value)
        {
            this.timestamp = timestamp;
            this.value = value;
        }
    }

    private void Reset()
    {
        brakeDisplayRoot = transform as RectTransform;
    }

    private void Awake()
    {
        runtimeRefDirty = true;
        ResetBrakeDelayState(0);
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        UpdateRuntimeDisplay();
    }

    private void OnValidate()
    {
        runtimeRefDirty = true;

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

    [ContextMenu("Generate Brake Display")]
    public void Generate()
    {
        RectTransform root = ResolveRoot();
        if (root == null)
        {
            Debug.LogWarning("BrakeDisplay root is not assigned.", this);
            return;
        }

        EnsureVerticalLayout(root);
        ClearGeneratedChildren(root);

        CreateEmergencyLabel(root);

        // 上から Notch_8 ... Notch_1 の順で作る
        // （結果として Notch_1 が最下段になる）
        int safeMaxNotch = Mathf.Max(1, maxBrakeNotch);
        for (int notch = safeMaxNotch; notch >= 1; notch--)
        {
            CreateNotchItem(root, notch);
        }

        runtimeRefDirty = true;
    }

    [ContextMenu("Clear Generated Brake Display")]
    public void ClearGenerated()
    {
        RectTransform root = ResolveRoot();
        if (root == null)
        {
            return;
        }

        ClearGeneratedChildren(root);
        runtimeRefDirty = true;
    }

    private RectTransform ResolveRoot()
    {
        if (brakeDisplayRoot != null)
        {
            return brakeDisplayRoot;
        }

        brakeDisplayRoot = transform as RectTransform;
        return brakeDisplayRoot;
    }

    private void EnsureVerticalLayout(RectTransform root)
    {
        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layout.spacing = Mathf.Max(0f, spacing);
        layout.padding = new RectOffset(
            Mathf.RoundToInt(Mathf.Max(0f, padding.x)),
            Mathf.RoundToInt(Mathf.Max(0f, padding.z)),
            Mathf.RoundToInt(Mathf.Max(0f, padding.y)),
            Mathf.RoundToInt(Mathf.Max(0f, padding.w))
        );
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childScaleWidth = false;
        layout.childScaleHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private void ClearGeneratedChildren(RectTransform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (!ShouldDeleteChild(child.name))
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

    private bool ShouldDeleteChild(string childName)
    {
        return childName == EmergencyLabelName || childName.StartsWith(NotchPrefix);
    }

    private void CreateEmergencyLabel(RectTransform parent)
    {
        GameObject root = new GameObject(EmergencyLabelName, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        root.transform.SetParent(parent, false);

        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, emergencyLabelHeight);

        LayoutElement le = root.GetComponent<LayoutElement>();
        le.preferredHeight = emergencyLabelHeight;
        le.minHeight = emergencyLabelHeight;
        le.flexibleHeight = 0f;

        Image bg = root.GetComponent<Image>();
        bg.color = emergencyBackgroundColor;
        bg.raycastTarget = false;

        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = emergencyBorderColor;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        GameObject textGo = new GameObject(LabelName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(rt, false);

        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = "非常";
        tmp.fontSize = emergencyFontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = emergencyTextColor;
        tmp.raycastTarget = false;
        tmp.fontStyle = FontStyles.Bold;
        if (fontAsset != null)
        {
            tmp.font = fontAsset;
        }

        // 普段は非表示
        root.SetActive(false);
    }

    private void CreateNotchItem(RectTransform parent, int notch)
    {
        GameObject root = new GameObject($"{NotchPrefix}{notch}", typeof(RectTransform), typeof(LayoutElement));
        root.transform.SetParent(parent, false);

        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, notchItemHeight);

        LayoutElement le = root.GetComponent<LayoutElement>();
        le.preferredHeight = notchItemHeight;
        le.minHeight = notchItemHeight;
        le.flexibleHeight = 0f;

        GameObject bgGo = new GameObject(BackgroundName, typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(rt, false);
        StretchToParent(bgGo.GetComponent<RectTransform>());
        Image bg = bgGo.GetComponent<Image>();
        bg.color = notchBackgroundColor;
        bg.raycastTarget = false;

        GameObject fillGo = new GameObject(FillName, typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(rt, false);
        StretchToParent(fillGo.GetComponent<RectTransform>());
        Image fill = fillGo.GetComponent<Image>();
        fill.color = notchFillColor;
        fill.raycastTarget = false;
        fillGo.SetActive(false); // 消灯状態で生成

        GameObject labelRoot = new GameObject(LabelName, typeof(RectTransform));
        labelRoot.transform.SetParent(rt, false);
        StretchToParent(labelRoot.GetComponent<RectTransform>());

        // 通常数字（前面）
        GameObject valueGo = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
        valueGo.transform.SetParent(labelRoot.transform, false);
        RectTransform valueRt = valueGo.GetComponent<RectTransform>();
        StretchToParent(valueRt);

        TextMeshProUGUI valueText = valueGo.GetComponent<TextMeshProUGUI>();
        valueText.text = FormatNotchLabel(notch);
        valueText.fontSize = notchFontSize;
        valueText.alignment = TextAlignmentOptions.Center;
        valueText.color = notchTextColor;
        valueText.raycastTarget = false;
        valueText.fontStyle = FontStyles.Bold;
        if (fontAsset != null)
        {
            valueText.font = fontAsset;
        }
    }

    private void UpdateRuntimeDisplay()
    {
        if (autoFindTrain && train == null)
        {
            train = FindFirstObjectByType<TrainController>();
        }

        if (train == null)
        {
            return;
        }

        if (runtimeRefDirty)
        {
            CacheRuntimeReferences();
        }

        int appliedBrakeNotch = Mathf.Max(0, train.BrakeNotch);
        int emergencyBrakeNotch = Mathf.Max(1, train.EmergencyBrakeNotch);
        bool isEmergency = train.IsEmergencyBrakeActive || appliedBrakeNotch >= emergencyBrakeNotch;
        int displayBrakeNotch = isEmergency
            ? 0
            : ResolveDelayedBrakeNotch(appliedBrakeNotch);

        if (isEmergency)
        {
            // 非常時は即時で消灯状態へ（別UIで非常表示を担当）
            ResetBrakeDelayState(0);
        }

        if (emergencyLabelObject != null)
        {
            // 非常表示は別UIで扱うため、このビルダーでは常に非表示
            emergencyLabelObject.SetActive(false);
        }

        for (int notch = 1; notch <= Mathf.Max(1, maxBrakeNotch); notch++)
        {
            if (!notchFillObjects.TryGetValue(notch, out GameObject fillObject) || fillObject == null)
            {
                continue;
            }

            // 非常時は全段を消灯
            bool isLit = !isEmergency && displayBrakeNotch >= notch;
            fillObject.SetActive(isLit);
        }

        if (showOnlyCurrentNotchText)
        {
            for (int notch = 1; notch <= Mathf.Max(1, maxBrakeNotch); notch++)
            {
                if (!notchLabelObjects.TryGetValue(notch, out GameObject labelObject) || labelObject == null)
                {
                    continue;
                }

                bool shouldShowLabel = !isEmergency && displayBrakeNotch > 0 && notch == displayBrakeNotch;
                labelObject.SetActive(shouldShowLabel);
            }
        }
    }

    private int ResolveDelayedBrakeNotch(int rawNotch)
    {
        rawNotch = Mathf.Max(0, rawNotch);

        if (!Application.isPlaying || !enableReadDelay || readDelaySeconds <= 0f)
        {
            ResetBrakeDelayState(rawNotch);
            return displayedBrakeNotch;
        }

        float now = Time.unscaledTime;
        if (notchReadBuffer.Count == 0 || rawNotch != lastBufferedBrakeNotch)
        {
            notchReadBuffer.Enqueue(new NotchSample(now, rawNotch));
            lastBufferedBrakeNotch = rawNotch;
        }

        float readableTime = now - readDelaySeconds;
        while (notchReadBuffer.Count > 0 && notchReadBuffer.Peek().timestamp <= readableTime)
        {
            displayedBrakeNotch = notchReadBuffer.Dequeue().value;
        }

        return displayedBrakeNotch;
    }

    private void ResetBrakeDelayState(int rawNotch)
    {
        displayedBrakeNotch = Mathf.Max(0, rawNotch);
        notchReadBuffer.Clear();
        lastBufferedBrakeNotch = displayedBrakeNotch;
    }

    private void CacheRuntimeReferences()
    {
        runtimeRefDirty = false;
        notchFillObjects.Clear();
        notchLabelObjects.Clear();
        emergencyLabelObject = null;

        RectTransform root = ResolveRoot();
        if (root == null)
        {
            return;
        }

        Transform emergency = root.Find(EmergencyLabelName);
        if (emergency != null)
        {
            emergencyLabelObject = emergency.gameObject;
        }

        int safeMaxNotch = Mathf.Max(1, maxBrakeNotch);
        for (int notch = 1; notch <= safeMaxNotch; notch++)
        {
            Transform notchRoot = root.Find($"{NotchPrefix}{notch}");
            if (notchRoot == null)
            {
                continue;
            }

            Transform fill = notchRoot.Find(FillName);
            if (fill != null)
            {
                notchFillObjects[notch] = fill.gameObject;
            }

            Transform label = notchRoot.Find(LabelName);
            if (label != null)
            {
                notchLabelObjects[notch] = label.gameObject;

                // 既存生成物（旧フォーマット）でも B 表示へ揃える
                TextMeshProUGUI valueText = label.GetComponentInChildren<TextMeshProUGUI>(true);
                if (valueText != null)
                {
                    valueText.text = FormatNotchLabel(notch);
                }
            }
        }
    }

    private void StretchToParent(RectTransform rt)
    {
        if (rt == null)
        {
            return;
        }

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private string FormatNotchLabel(int notch)
    {
        return $"B{Mathf.Max(0, notch)}";
    }
}
