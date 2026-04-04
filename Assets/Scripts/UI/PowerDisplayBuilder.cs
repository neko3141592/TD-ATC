using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class PowerDisplayBuilder : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform powerDisplayRoot;

    [Header("Runtime")]
    [SerializeField] private TrainController train;
    [SerializeField] private bool autoFindTrain = true;
    [SerializeField] private bool showOnlyCurrentNotchText = true;

    [Header("Read Delay")]
    [SerializeField] private bool enableReadDelay = true;
    [SerializeField, Min(0f)] private float readDelaySeconds = 0.06f;

    [Header("Notch")]
    [SerializeField, Min(1)] private int maxPowerNotch = 5;

    [Header("Layout")]
    [SerializeField, Min(0f)] private float spacing = 8f;
    [SerializeField] private Vector4 padding = new Vector4(8f, 8f, 8f, 8f); // left, top, right, bottom
    [SerializeField, Min(8f)] private float notchItemHeight = 28f;

    [Header("Colors")]
    [SerializeField] private Color notchBackgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.75f);
    [SerializeField] private Color notchFillColor = new Color(0.25f, 0.85f, 1f, 1f);
    [SerializeField] private Color notchTextColor = new Color(0.08f, 0.08f, 0.08f, 1f);

    [Header("Font")]
    [SerializeField, Min(8f)] private float notchFontSize = 20f;
    [SerializeField] private TMP_FontAsset fontAsset;

    [Header("Editor")]
    [SerializeField] private bool regenerateOnValidate = false;

    private const string NotchPrefix = "Power_";
    private const string BackgroundName = "Background";
    private const string FillName = "Fill";
    private const string LabelName = "Label";

    private readonly Dictionary<int, GameObject> notchFillObjects = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, GameObject> notchLabelObjects = new Dictionary<int, GameObject>();
    private bool runtimeRefDirty = true;
    private readonly Queue<NotchSample> notchReadBuffer = new Queue<NotchSample>();
    private int displayedPowerNotch;
    private int lastBufferedPowerNotch = int.MinValue;

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
        powerDisplayRoot = transform as RectTransform;
    }

    private void Awake()
    {
        runtimeRefDirty = true;
        ResetPowerDelayState(0);
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

    [ContextMenu("Generate Power Display")]
    public void Generate()
    {
        RectTransform root = ResolveRoot();
        if (root == null)
        {
            Debug.LogWarning("PowerDisplay root is not assigned.", this);
            return;
        }

        EnsureVerticalLayout(root);
        ClearGeneratedChildren(root);

        // 上から Power_1 ... Power_5 の順で作る
        // （結果として P1 から上段で点灯する）
        int safeMaxNotch = Mathf.Max(1, maxPowerNotch);
        for (int notch = 1; notch <= safeMaxNotch; notch++)
        {
            CreateNotchItem(root, notch);
        }

        runtimeRefDirty = true;
    }

    [ContextMenu("Clear Generated Power Display")]
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
        if (powerDisplayRoot != null)
        {
            return powerDisplayRoot;
        }

        powerDisplayRoot = transform as RectTransform;
        return powerDisplayRoot;
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
        return childName.StartsWith(NotchPrefix);
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

        int appliedPowerNotch = Mathf.Max(0, train.PowerNotch);
        int displayPowerNotch = ResolveDelayedPowerNotch(appliedPowerNotch);

        for (int notch = 1; notch <= Mathf.Max(1, maxPowerNotch); notch++)
        {
            if (!notchFillObjects.TryGetValue(notch, out GameObject fillObject) || fillObject == null)
            {
                continue;
            }

            bool isLit = displayPowerNotch >= notch;
            fillObject.SetActive(isLit);
        }

        if (showOnlyCurrentNotchText)
        {
            for (int notch = 1; notch <= Mathf.Max(1, maxPowerNotch); notch++)
            {
                if (!notchLabelObjects.TryGetValue(notch, out GameObject labelObject) || labelObject == null)
                {
                    continue;
                }

                bool shouldShowLabel = displayPowerNotch > 0 && notch == displayPowerNotch;
                labelObject.SetActive(shouldShowLabel);
            }
        }
    }

    private int ResolveDelayedPowerNotch(int rawNotch)
    {
        rawNotch = Mathf.Max(0, rawNotch);

        if (!Application.isPlaying || !enableReadDelay || readDelaySeconds <= 0f)
        {
            ResetPowerDelayState(rawNotch);
            return displayedPowerNotch;
        }

        float now = Time.unscaledTime;
        if (notchReadBuffer.Count == 0 || rawNotch != lastBufferedPowerNotch)
        {
            notchReadBuffer.Enqueue(new NotchSample(now, rawNotch));
            lastBufferedPowerNotch = rawNotch;
        }

        float readableTime = now - readDelaySeconds;
        while (notchReadBuffer.Count > 0 && notchReadBuffer.Peek().timestamp <= readableTime)
        {
            displayedPowerNotch = notchReadBuffer.Dequeue().value;
        }

        return displayedPowerNotch;
    }

    private void ResetPowerDelayState(int rawNotch)
    {
        displayedPowerNotch = Mathf.Max(0, rawNotch);
        notchReadBuffer.Clear();
        lastBufferedPowerNotch = displayedPowerNotch;
    }

    private void CacheRuntimeReferences()
    {
        runtimeRefDirty = false;
        notchFillObjects.Clear();
        notchLabelObjects.Clear();

        RectTransform root = ResolveRoot();
        if (root == null)
        {
            return;
        }

        int safeMaxNotch = Mathf.Max(1, maxPowerNotch);
        for (int notch = 1; notch <= safeMaxNotch; notch++)
        {
            Transform notchRoot = root.Find($"{NotchPrefix}{notch}");
            if (notchRoot == null)
            {
                continue;
            }

            // 既存生成物が旧順序でも、P1 が上段になる順序へ補正
            notchRoot.SetSiblingIndex(notch - 1);

            Transform fill = notchRoot.Find(FillName);
            if (fill != null)
            {
                notchFillObjects[notch] = fill.gameObject;
            }

            Transform label = notchRoot.Find(LabelName);
            if (label != null)
            {
                notchLabelObjects[notch] = label.gameObject;

                // 既存生成物（旧フォーマット）でも P 表示へ揃える
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
        return $"P{Mathf.Max(0, notch)}";
    }
}
