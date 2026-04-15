using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

[ExecuteAlways]
public class TrainFormationDisplayBuilder : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform formationDisplayRoot;

    [Header("Runtime")]
    [SerializeField] private TrainController train;
    [SerializeField, Min(0f)] private float displayUpdateLagSeconds = 0f;
    [SerializeField] private BrakeSystemController brakeSystem;
    [SerializeField] private TractionSystemController tractionSystem;
    [SerializeField] private ConsistDefinition consistDefinition;

    [Header("Editor")]
    [SerializeField] private bool regenerateOnValidate = false;

    [Header("Sprites")]
    [SerializeField] private Sprite normalMotor;
    [SerializeField] private Sprite normalTrailer;
    [SerializeField] private Sprite normalMotorRegen;
    [SerializeField] private Sprite normalMotorAccel;
    [FormerlySerializedAs("CabLeft")]
    [SerializeField] private Sprite normalCabLeft;
    [SerializeField] private Vector2 spriteSize = new Vector2(32f, 12f);
    [SerializeField] private float shift = 2.0f;

    [Header("Car Number")]
    [SerializeField] private bool showCarNumbers = true;
    [SerializeField] private Vector2 carNumberOffset = new Vector2(0f, -14f);
    [SerializeField, Min(1f)] private float carNumberFontSize = 12f;
    [SerializeField] private Color carNumberColor = Color.white;
    [SerializeField] private TMP_FontAsset carNumberFontAsset;

    public TrainController Train => train;
    public BrakeSystemController BrakeSystem => brakeSystem;
    public TractionSystemController TractionSystem => tractionSystem;
    public ConsistDefinition ConsistDefinition => consistDefinition;
    public int CarCount => consistDefinition != null ? consistDefinition.CarCount : 0;

    private readonly List<Image> generatedCarImages = new List<Image>();
    private readonly Queue<VisualSnapshot> pendingVisualSnapshots = new Queue<VisualSnapshot>();

    private struct VisualSnapshot
    {
        public readonly float sampledTime;
        public readonly Sprite[] sprites;

        public VisualSnapshot(float sampledTime, Sprite[] sprites)
        {
            this.sampledTime = sampledTime;
            this.sprites = sprites;
        }
    }

    private void Reset()
    {
        formationDisplayRoot = transform as RectTransform;
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        UpdateRuntimeVisuals();
    }

    private void OnValidate()
    {
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

    [ContextMenu("Generate Train Formation Display")]
    public void Generate()
    {
        if (formationDisplayRoot == null)
        {
            formationDisplayRoot = transform as RectTransform;
        }

        if (formationDisplayRoot == null)
        {
            return;
        }

        ClearGenerated();
        Vector2 currentPos = new Vector2(0, 0);
        for (int i = 0; i < CarCount; i++)
        {
            Sprite selectedSprite = GetDisplaySpriteForCar(i);

            if (selectedSprite == null)
            {
                generatedCarImages.Add(null);
                continue;
            }

            bool mirrorX = i == 0;
            Vector2 anchoredPos = new Vector2(currentPos.x + (shift * i), currentPos.y);
            Image createdImage = CreateSpriteCar(selectedSprite, $"Car_{i + 1}", i + 1, anchoredPos, mirrorX);
            generatedCarImages.Add(createdImage);
        }
    }

    private Image CreateSpriteCar(Sprite sprite, string objectName, int carNumber, Vector2 anchoredPosition, bool mirrorX)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(formationDisplayRoot, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = spriteSize;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        if (mirrorX)
        {
            Vector3 mirroredScale = rect.localScale;
            mirroredScale.x = -Mathf.Abs(mirroredScale.x);
            rect.localScale = mirroredScale;
        }

        CreateCarNumberLabel(rect, carNumber, mirrorX);

        return image;
    }

    private void CreateCarNumberLabel(RectTransform parent, int carNumber, bool parentMirroredX)
    {
        if (!showCarNumbers || parent == null)
        {
            return;
        }

        GameObject textGo = new GameObject("CarNumber", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.SetParent(parent, false);
        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = carNumberOffset;
        textRect.sizeDelta = new Vector2(Mathf.Max(24f, spriteSize.x), Mathf.Max(12f, carNumberFontSize + 4f));

        if (parentMirroredX)
        {
            // 先頭車の左右反転を数字には適用しない
            textRect.localScale = new Vector3(-1f, 1f, 1f);
        }

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = carNumber.ToString();
        tmp.fontSize = carNumberFontSize;
        tmp.color = carNumberColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (carNumberFontAsset != null)
        {
            tmp.font = carNumberFontAsset;
        }
    }

    private Sprite GetDisplaySpriteForCar(int carIndex)
    {
        bool isHeadOrTail = carIndex == 0 || carIndex == CarCount - 1;
        if (isHeadOrTail)
        {
            return normalCabLeft;
        }

        CarType carType = GetCarTypeAtOrDefault(carIndex, CarType.Trailer);
        if (carType != CarType.Motor)
        {
            return normalTrailer;
        }

        bool isTraction = TryGetCarTractionForceN(carIndex, out float tractionForceN) && tractionForceN > 0f;
        bool isRegen = TryGetCarRegenForceN(carIndex, out float regenForceN) && regenForceN > 0f;

        if (isRegen && normalMotorRegen != null)
        {
            return normalMotorRegen;
        }

        if (isTraction && normalMotorAccel != null)
        {
            return normalMotorAccel;
        }

        return normalMotor;
    }

    private void UpdateRuntimeVisuals()
    {
        if (formationDisplayRoot == null || CarCount <= 0)
        {
            pendingVisualSnapshots.Clear();
            return;
        }

        if (generatedCarImages.Count != CarCount)
        {
            RebuildGeneratedImageCache();
        }

        if (displayUpdateLagSeconds <= 0f)
        {
            pendingVisualSnapshots.Clear();
            ApplySnapshot(CaptureCurrentSnapshot());
            return;
        }

        pendingVisualSnapshots.Enqueue(CaptureCurrentSnapshot());
        float thresholdTime = Time.time - displayUpdateLagSeconds;
        while (pendingVisualSnapshots.Count > 0 && pendingVisualSnapshots.Peek().sampledTime <= thresholdTime)
        {
            ApplySnapshot(pendingVisualSnapshots.Dequeue());
        }
    }

    private VisualSnapshot CaptureCurrentSnapshot()
    {
        int count = CarCount;
        Sprite[] sprites = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            sprites[i] = GetDisplaySpriteForCar(i);
        }

        return new VisualSnapshot(Time.time, sprites);
    }

    private void ApplySnapshot(VisualSnapshot snapshot)
    {
        if (snapshot.sprites == null)
        {
            return;
        }

        int count = Mathf.Min(generatedCarImages.Count, snapshot.sprites.Length);
        for (int i = 0; i < count; i++)
        {
            Image image = generatedCarImages[i];
            if (image == null)
            {
                continue;
            }

            Sprite desiredSprite = snapshot.sprites[i];
            if (desiredSprite != null && image.sprite != desiredSprite)
            {
                image.sprite = desiredSprite;
            }
        }
    }

    private void RebuildGeneratedImageCache()
    {
        generatedCarImages.Clear();
        pendingVisualSnapshots.Clear();
        int childCount = formationDisplayRoot != null ? formationDisplayRoot.childCount : 0;
        for (int i = 0; i < CarCount; i++)
        {
            if (i < childCount)
            {
                Image image = formationDisplayRoot.GetChild(i).GetComponent<Image>();
                generatedCarImages.Add(image);
            }
            else
            {
                generatedCarImages.Add(null);
            }
        }
    }

    [ContextMenu("Clear Generated Train Formation Display")]
    public void ClearGenerated()
    {
        generatedCarImages.Clear();
        pendingVisualSnapshots.Clear();
        if (formationDisplayRoot == null)
        {
            return;
        }

        for (int i = formationDisplayRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = formationDisplayRoot.GetChild(i);
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

    public CarSpec GetCarSpecAt(int carIndex)
    {
        return consistDefinition != null ? consistDefinition.GetCar(carIndex) : null;
    }

    public bool TryGetCarTypeAt(int carIndex, out CarType carType)
    {
        if (consistDefinition == null)
        {
            carType = CarType.Trailer;
            return false;
        }

        return consistDefinition.TryGetCarType(carIndex, out carType);
    }

    public CarType GetCarTypeAtOrDefault(int carIndex, CarType fallback = CarType.Trailer)
    {
        return TryGetCarTypeAt(carIndex, out CarType carType) ? carType : fallback;
    }

    public bool TryGetCarTractionForceN(int carIndex, out float tractionForceN)
    {
        tractionForceN = 0f;

        if (train == null)
        {
            return false;
        }

        var states = train.CurrentCarTractionStates;
        if (states == null || carIndex < 0 || carIndex >= states.Count)
        {
            return false;
        }

        CarTractionState state = states[carIndex];
        if (state == null)
        {
            return false;
        }

        tractionForceN = state.tractionForceN;
        return true;
    }

    public bool TryGetCarRegenForceN(int carIndex, out float regenForceN)
    {
        regenForceN = 0f;

        if (train == null)
        {
            return false;
        }

        var states = train.CurrentCarBrakeStates;
        if (states == null || carIndex < 0 || carIndex >= states.Count)
        {
            return false;
        }

        CarBrakeState state = states[carIndex];
        if (state == null)
        {
            return false;
        }

        regenForceN = state.regenForceN;
        return true;
    }
}
