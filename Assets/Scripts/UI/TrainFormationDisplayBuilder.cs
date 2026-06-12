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
    [SerializeField] private Color activeCarNumberColor = Color.black;
    [SerializeField] private TMP_FontAsset carNumberFontAsset;

    [Header("Door Status")]
    [SerializeField] private bool showDoorStatus = true;
    [SerializeField] private Sprite doorOpenSprite;
    [SerializeField] private Sprite doorClosedSprite;
    [SerializeField] private Vector2 doorStatusOffset = new Vector2(0f, 8f);
    [SerializeField] private Vector2 doorStatusSize = new Vector2(12f, 5f);
    [SerializeField] private Color doorStatusColor = Color.white;
    [SerializeField] private bool[] doorOpenStates = new bool[0];

    [Header("Current Bar")]
    [SerializeField] private bool showCurrentBars = true;
    [SerializeField, Min(1f)] private float currentBarMaxA = 1200f;
    [SerializeField] private Vector2 currentBarSize = new Vector2(3f, 16f);
    [SerializeField] private Vector2 currentBarOffset = new Vector2(0f, -28f);
    [SerializeField] private Color currentBarBackgroundColor = new Color(1f, 1f, 1f, 0.18f);
    [SerializeField] private Color currentBarFillColor = new Color(0.2f, 0.95f, 1f, 0.95f);
    [SerializeField] private Color currentBarRegenFillColor = new Color(0.55f, 0.8f, 1f, 0.95f);
    [SerializeField, Min(0f)] private float currentDirectionThresholdA = 0.5f;
    [SerializeField] private bool showCurrentNumbers = true;
    [SerializeField] private Vector2 currentNumberOffset = new Vector2(0f, -41f);
    [SerializeField, Min(1f)] private float currentNumberFontSize = 5f;
    [SerializeField] private Color currentNumberColor = new Color(0.82f, 1f, 1f, 0.95f);

    [Header("Brake Pressure")]
    [SerializeField] private bool showBrakePressureNumbers = true;
    [SerializeField] private Vector2 brakePressureNumberOffset = new Vector2(0f, -49f);
    [SerializeField, Min(1f)] private float brakePressureNumberFontSize = 5f;
    [SerializeField] private Color brakePressureNumberColor = new Color(1f, 0.92f, 0.6f, 0.95f);

    private const int DoorCountPerCar = 4;
    private int CarCount => consistDefinition != null ? consistDefinition.CarCount : 0;

    private readonly List<Image> generatedCarImages = new List<Image>();
    private readonly List<TextMeshProUGUI> generatedCarNumberLabels = new List<TextMeshProUGUI>();
    private readonly List<Image> generatedDoorStatusImages = new List<Image>();
    private readonly List<Image> generatedCurrentBarBackgrounds = new List<Image>();
    private readonly List<Image> generatedCurrentBarFills = new List<Image>();
    private readonly List<TextMeshProUGUI> generatedCurrentNumberLabels = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> generatedBrakePressureLabels = new List<TextMeshProUGUI>();
    private readonly List<float> displayedMotorCurrentsA = new List<float>();
    private readonly List<float> displayedBrakePressuresKPa = new List<float>();
    private readonly Queue<VisualSnapshot> pendingVisualSnapshots = new Queue<VisualSnapshot>();
    private float nextTrainStateSampleTime = 0f;

    private struct VisualSnapshot
    {
        public readonly float sampledTime;
        public readonly Sprite[] sprites;
        public readonly bool[] activeCarNumbers;

        /// <summary>
        /// 役割: VisualSnapshot の処理を実行します。
        /// </summary>
        /// <param name="sampledTime">sampledTime を指定します。</param>
        /// <param name="sprites">sprites を指定します。</param>
        /// <param name="activeCarNumbers">activeCarNumbers を指定します。</param>
        /// <returns>処理結果を返します。</returns>
        public VisualSnapshot(float sampledTime, Sprite[] sprites, bool[] activeCarNumbers)
        {
            this.sampledTime = sampledTime;
            this.sprites = sprites;
            this.activeCarNumbers = activeCarNumbers;
        }
    }

    /// <summary>
    /// 役割: Reset の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Reset()
    {
        formationDisplayRoot = transform as RectTransform;
    }

    /// <summary>
    /// 役割: Update の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        UpdateRuntimeVisuals();
    }

    /// <summary>
    /// 役割: OnValidate の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void OnValidate()
    {
        currentBarMaxA = Mathf.Max(1f, currentBarMaxA);
        currentNumberFontSize = Mathf.Max(1f, currentNumberFontSize);
        brakePressureNumberFontSize = Mathf.Max(1f, brakePressureNumberFontSize);
        doorStatusSize.x = Mathf.Max(1f, doorStatusSize.x);
        doorStatusSize.y = Mathf.Max(1f, doorStatusSize.y);
        EnsureDoorStateCache(CarCount);
        RefreshTrainStateDisplayLayout();
        ApplyDoorStatusSprites();

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

        Generate();
    }
#endif

    /// <summary>
    /// 役割: 指定車両・指定ドアの開閉状態を設定します。carIndex と doorIndex は 0 始まりです。
    /// </summary>
    /// <param name="carIndex">車両インデックスを指定します。</param>
    /// <param name="doorIndex">車両内のドアインデックスを指定します。</param>
    /// <param name="isOpen">開いている場合は true を指定します。</param>
    /// <returns>設定できた場合は true を返します。</returns>
    public bool SetDoorOpen(int carIndex, int doorIndex, bool isOpen)
    {
        if (carIndex < 0 || carIndex >= CarCount || doorIndex < 0 || doorIndex >= DoorCountPerCar)
        {
            return false;
        }

        EnsureDoorStateCache(CarCount);
        doorOpenStates[GetDoorStateIndex(carIndex, doorIndex)] = isOpen;
        ApplyDoorStatusSprites();
        return true;
    }

    /// <summary>
    /// 役割: 指定車両の 4 つのドア状態をまとめて設定します。
    /// </summary>
    /// <param name="carIndex">車両インデックスを指定します。</param>
    /// <param name="door1Open">1つ目のドアが開いている場合は true を指定します。</param>
    /// <param name="door2Open">2つ目のドアが開いている場合は true を指定します。</param>
    /// <param name="door3Open">3つ目のドアが開いている場合は true を指定します。</param>
    /// <param name="door4Open">4つ目のドアが開いている場合は true を指定します。</param>
    /// <returns>設定できた場合は true を返します。</returns>
    public bool SetCarDoorStates(int carIndex, bool door1Open, bool door2Open, bool door3Open, bool door4Open)
    {
        if (carIndex < 0 || carIndex >= CarCount)
        {
            return false;
        }

        EnsureDoorStateCache(CarCount);
        int baseIndex = GetDoorStateIndex(carIndex, 0);
        doorOpenStates[baseIndex] = door1Open;
        doorOpenStates[baseIndex + 1] = door2Open;
        doorOpenStates[baseIndex + 2] = door3Open;
        doorOpenStates[baseIndex + 3] = door4Open;
        ApplyDoorStatusSprites();
        return true;
    }

    /// <summary>
    /// 役割: 指定車両の全ドアを同じ開閉状態に設定します。
    /// </summary>
    /// <param name="carIndex">車両インデックスを指定します。</param>
    /// <param name="isOpen">開いている場合は true を指定します。</param>
    /// <returns>設定できた場合は true を返します。</returns>
    public bool SetAllDoorsOpenForCar(int carIndex, bool isOpen)
    {
        return SetCarDoorStates(carIndex, isOpen, isOpen, isOpen, isOpen);
    }

    [ContextMenu("Generate Train Formation Display")]
    /// <summary>
    /// 役割: Generate の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
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
        EnsureDoorStateCache(CarCount);
        Vector2 currentPos = new Vector2(0, 0);
        for (int i = 0; i < CarCount; i++)
        {
            Sprite selectedSprite = GetDisplaySpriteForCar(i);

            if (selectedSprite == null)
            {
                generatedCarImages.Add(null);
                generatedCarNumberLabels.Add(null);
                generatedDoorStatusImages.Add(null);
                generatedCurrentBarBackgrounds.Add(null);
                generatedCurrentBarFills.Add(null);
                generatedCurrentNumberLabels.Add(null);
                generatedBrakePressureLabels.Add(null);
                continue;
            }

            bool mirrorX = i == 0;
            Vector2 anchoredPos = new Vector2(currentPos.x + (shift * i), currentPos.y);
            Image createdImage = CreateSpriteCar(selectedSprite, $"Car_{i + 1}", i + 1, anchoredPos, mirrorX);
            generatedCarImages.Add(createdImage);
        }

        ApplyDoorStatusSprites();
    }

    /// <summary>
    /// 役割: CreateSpriteCar の処理を実行します。
    /// </summary>
    /// <param name="sprite">sprite を指定します。</param>
    /// <param name="objectName">objectName を指定します。</param>
    /// <param name="carNumber">carNumber を指定します。</param>
    /// <param name="anchoredPosition">anchoredPosition を指定します。</param>
    /// <param name="mirrorX">mirrorX を指定します。</param>
    /// <returns>処理結果を返します。</returns>
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

        generatedCarNumberLabels.Add(CreateCarNumberLabel(rect, carNumber, mirrorX));
        generatedDoorStatusImages.Add(CreateDoorStatusImage(rect, mirrorX));
        CreateCurrentBar(rect, mirrorX);
        generatedBrakePressureLabels.Add(CreateBrakePressureNumberLabel(rect, mirrorX));

        return image;
    }

    /// <summary>
    /// 役割: 車両ごとのドア開閉表示を作成します。
    /// </summary>
    /// <param name="parent">parent を指定します。</param>
    /// <param name="parentMirroredX">parentMirroredX を指定します。</param>
    /// <returns>作成した Image を返します。</returns>
    private Image CreateDoorStatusImage(RectTransform parent, bool parentMirroredX)
    {
        if (!showDoorStatus || parent == null)
        {
            return null;
        }

        GameObject imageGo = new GameObject("DoorStatus", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform imageRect = imageGo.GetComponent<RectTransform>();
        imageRect.SetParent(parent, false);

        if (parentMirroredX)
        {
            imageRect.localScale = new Vector3(-1f, 1f, 1f);
        }

        Image image = imageGo.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        ApplyDoorStatusLayout(image);

        return image;
    }

    /// <summary>
    /// 役割: 車両下部に電流バーを作成します。
    /// </summary>
    /// <param name="parent">parent を指定します。</param>
    /// <param name="parentMirroredX">parentMirroredX を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void CreateCurrentBar(RectTransform parent, bool parentMirroredX)
    {
        if (!showCurrentBars || parent == null)
        {
            generatedCurrentBarBackgrounds.Add(null);
            generatedCurrentBarFills.Add(null);
            generatedCurrentNumberLabels.Add(null);
            return;
        }

        GameObject backgroundGo = new GameObject("CurrentBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform backgroundRect = backgroundGo.GetComponent<RectTransform>();
        backgroundRect.SetParent(parent, false);
        backgroundRect.anchorMin = backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = currentBarOffset;
        backgroundRect.sizeDelta = currentBarSize;

        if (parentMirroredX)
        {
            backgroundRect.localScale = new Vector3(-1f, 1f, 1f);
        }

        Image backgroundImage = backgroundGo.GetComponent<Image>();
        backgroundImage.color = currentBarBackgroundColor;
        backgroundImage.enabled = false;
        backgroundImage.raycastTarget = false;

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.SetParent(backgroundRect, false);
        fillRect.anchorMin = fillRect.anchorMax = new Vector2(0.5f, 0f);
        fillRect.pivot = new Vector2(0.5f, 0f);
        fillRect.anchoredPosition = new Vector2(0f, -currentBarSize.y * 0.5f);
        fillRect.sizeDelta = new Vector2(currentBarSize.x, 0f);

        Image fillImage = fillGo.GetComponent<Image>();
        fillImage.color = currentBarFillColor;
        fillImage.enabled = false;
        fillImage.raycastTarget = false;

        generatedCurrentBarBackgrounds.Add(backgroundImage);
        generatedCurrentBarFills.Add(fillImage);
        generatedCurrentNumberLabels.Add(CreateCurrentNumberLabel(parent, parentMirroredX));
    }

    /// <summary>
    /// 役割: 電流値の数字ラベルを作成します。
    /// </summary>
    /// <param name="parent">parent を指定します。</param>
    /// <param name="parentMirroredX">parentMirroredX を指定します。</param>
    /// <returns>作成したラベルを返します。</returns>
    private TextMeshProUGUI CreateCurrentNumberLabel(RectTransform parent, bool parentMirroredX)
    {
        if (!showCurrentNumbers || parent == null)
        {
            return null;
        }

        GameObject textGo = new GameObject("CurrentNumber", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.SetParent(parent, false);
        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = currentNumberOffset;
        textRect.sizeDelta = new Vector2(Mathf.Max(28f, spriteSize.x), Mathf.Max(8f, currentNumberFontSize + 3f));

        if (parentMirroredX)
        {
            textRect.localScale = new Vector3(-1f, 1f, 1f);
        }

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = currentNumberFontSize;
        tmp.color = currentNumberColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enabled = false;
        tmp.raycastTarget = false;
        if (carNumberFontAsset != null)
        {
            tmp.font = carNumberFontAsset;
        }

        return tmp;
    }

    /// <summary>
    /// 役割: ブレーキ圧力の数字ラベルを作成します。
    /// </summary>
    /// <param name="parent">parent を指定します。</param>
    /// <param name="parentMirroredX">parentMirroredX を指定します。</param>
    /// <returns>作成したラベルを返します。</returns>
    private TextMeshProUGUI CreateBrakePressureNumberLabel(RectTransform parent, bool parentMirroredX)
    {
        if (!showBrakePressureNumbers || parent == null)
        {
            return null;
        }

        GameObject textGo = new GameObject("BrakePressureNumber", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.SetParent(parent, false);
        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = brakePressureNumberOffset;
        textRect.sizeDelta = new Vector2(Mathf.Max(28f, spriteSize.x), Mathf.Max(8f, brakePressureNumberFontSize + 3f));

        if (parentMirroredX)
        {
            textRect.localScale = new Vector3(-1f, 1f, 1f);
        }

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = brakePressureNumberFontSize;
        tmp.color = brakePressureNumberColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enabled = false;
        tmp.raycastTarget = false;
        if (carNumberFontAsset != null)
        {
            tmp.font = carNumberFontAsset;
        }

        return tmp;
    }

    /// <summary>
    /// 役割: CreateCarNumberLabel の処理を実行します。
    /// </summary>
    /// <param name="parent">parent を指定します。</param>
    /// <param name="carNumber">carNumber を指定します。</param>
    /// <param name="parentMirroredX">parentMirroredX を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private TextMeshProUGUI CreateCarNumberLabel(RectTransform parent, int carNumber, bool parentMirroredX)
    {
        if (!showCarNumbers || parent == null)
        {
            return null;
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

        return tmp;
    }

    /// <summary>
    /// 役割: GetDisplaySpriteForCar の処理を実行します。
    /// </summary>
    /// <param name="carIndex">carIndex を指定します。</param>
    /// <returns>処理結果を返します。</returns>
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

        float driveDirection = GetCarDriveDirection(carIndex);
        bool isTraction = driveDirection > 0f;
        bool isRegen = driveDirection < 0f;

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

    /// <summary>
    /// 役割: UpdateRuntimeVisuals の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void UpdateRuntimeVisuals()
    {
        ResolveRuntimeReferences();

        if (formationDisplayRoot == null || CarCount <= 0)
        {
            pendingVisualSnapshots.Clear();
            displayedMotorCurrentsA.Clear();
            displayedBrakePressuresKPa.Clear();
            generatedDoorStatusImages.Clear();
            nextTrainStateSampleTime = 0f;
            return;
        }

        if (generatedCarImages.Count != CarCount || generatedCarNumberLabels.Count != CarCount || generatedDoorStatusImages.Count != CarCount || generatedCurrentBarFills.Count != CarCount || generatedCurrentNumberLabels.Count != CarCount || generatedBrakePressureLabels.Count != CarCount || HasMissingCurrentDisplays() || HasMissingDoorStatusDisplays())
        {
            RebuildGeneratedImageCache();
        }

        EnsureDoorStateCache(CarCount);
        RefreshTrainStateDisplayLayout();
        ApplyDoorStatusSprites();
        UpdateTrainStateDisplaysWithSampleLag();

        pendingVisualSnapshots.Clear();
        ApplyVisualSnapshot(CaptureCurrentSnapshot());
    }

    private void ResolveRuntimeReferences()
    {
        train = CabReferenceResolver.ResolveTrain(this, train);
        brakeSystem = CabReferenceResolver.ResolveTrainComponent(this, train, brakeSystem);
        tractionSystem = CabReferenceResolver.ResolveTrainComponent(this, train, tractionSystem);

        if (consistDefinition == null && train != null)
        {
            consistDefinition = train.ConsistDefinition;
        }
    }

    /// <summary>
    /// 役割: CaptureCurrentSnapshot の処理を実行します。
    /// </summary>
    /// <returns>処理結果を返します。</returns>
    private VisualSnapshot CaptureCurrentSnapshot()
    {
        int count = CarCount;
        Sprite[] sprites = new Sprite[count];
        bool[] activeCarNumbers = new bool[count];
        for (int i = 0; i < count; i++)
        {
            sprites[i] = GetDisplaySpriteForCar(i);
            activeCarNumbers[i] = IsCarInTractionOrRegen(i);
        }

        return new VisualSnapshot(Time.time, sprites, activeCarNumbers);
    }

    /// <summary>
    /// 役割: ApplySnapshot の処理を実行します。
    /// </summary>
    /// <param name="snapshot">snapshot を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ApplyVisualSnapshot(VisualSnapshot snapshot)
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

            TextMeshProUGUI carNumberLabel = i < generatedCarNumberLabels.Count ? generatedCarNumberLabels[i] : null;
            if (carNumberLabel != null)
            {
                bool activeCarNumber = snapshot.activeCarNumbers != null &&
                    i < snapshot.activeCarNumbers.Length &&
                    snapshot.activeCarNumbers[i];
                carNumberLabel.color = activeCarNumber ? activeCarNumberColor : carNumberColor;
            }
        }
    }

    /// <summary>
    /// 役割: 電流とブレーキ圧力を指定秒数ごとの取得値で更新します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void UpdateTrainStateDisplaysWithSampleLag()
    {
        int count = CarCount;
        EnsureDisplayedTrainStateCache(count);

        float lagSeconds = 0f;
        if (lagSeconds <= 0f || Time.time >= nextTrainStateSampleTime)
        {
            for (int i = 0; i < count; i++)
            {
                displayedMotorCurrentsA[i] = TryGetCarMotorCurrentA(i, out float motorCurrentA)
                    ? motorCurrentA
                    : 0f;

                displayedBrakePressuresKPa[i] = TryGetCarBrakePressureKPa(i, out float pressureKPa)
                    ? Mathf.Max(0f, pressureKPa)
                    : 0f;
            }

            nextTrainStateSampleTime = lagSeconds <= 0f ? Time.time : Time.time + lagSeconds;
        }

        ApplyCurrentBars(displayedMotorCurrentsA);
        ApplyBrakePressureNumbers(displayedBrakePressuresKPa);
    }

    /// <summary>
    /// 役割: 表示用キャッシュの個数を車両数に合わせます。
    /// </summary>
    /// <param name="count">必要な要素数を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void EnsureDisplayedTrainStateCache(int count)
    {
        while (displayedMotorCurrentsA.Count < count)
        {
            displayedMotorCurrentsA.Add(0f);
        }

        while (displayedBrakePressuresKPa.Count < count)
        {
            displayedBrakePressuresKPa.Add(0f);
        }

        if (displayedMotorCurrentsA.Count > count)
        {
            displayedMotorCurrentsA.RemoveRange(count, displayedMotorCurrentsA.Count - count);
        }

        if (displayedBrakePressuresKPa.Count > count)
        {
            displayedBrakePressuresKPa.RemoveRange(count, displayedBrakePressuresKPa.Count - count);
        }
    }

    /// <summary>
    /// 役割: 車両ごとの電流バー表示を更新します。
    /// </summary>
    /// <param name="motorCurrentsA">motorCurrentsA を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ApplyCurrentBars(IReadOnlyList<float> motorCurrentsA)
    {
        if (motorCurrentsA == null)
        {
            return;
        }

        int count = motorCurrentsA.Count;
        for (int i = 0; i < count; i++)
        {
            Image background = i < generatedCurrentBarBackgrounds.Count ? generatedCurrentBarBackgrounds[i] : null;
            Image fill = i < generatedCurrentBarFills.Count ? generatedCurrentBarFills[i] : null;
            TextMeshProUGUI numberLabel = i < generatedCurrentNumberLabels.Count ? generatedCurrentNumberLabels[i] : null;
            ApplyCurrentDisplayLayout(background, fill, numberLabel);

            float signedCurrentA = motorCurrentsA[i];
            float currentA = Mathf.Abs(signedCurrentA);
            float ratio = Mathf.Clamp01(currentA / Mathf.Max(1f, currentBarMaxA));
            bool barVisible = showCurrentBars;
            bool fillVisible = barVisible && currentA > 0.5f;

            if (background != null)
            {
                background.enabled = barVisible;
            }

            if (fill != null)
            {
                fill.enabled = fillVisible;
                fill.color = signedCurrentA < -Mathf.Max(0f, currentDirectionThresholdA)
                    ? currentBarRegenFillColor
                    : currentBarFillColor;
                RectTransform fillRect = fill.rectTransform;
                fillRect.sizeDelta = new Vector2(currentBarSize.x, currentBarSize.y * ratio);
                fillRect.anchoredPosition = new Vector2(0f, -currentBarSize.y * 0.5f);
            }

            if (numberLabel != null)
            {
                bool numberVisible = showCurrentNumbers && (currentA > 0.5f || HasMotorCurrentDisplayAt(i));
                numberLabel.enabled = numberVisible;
                numberLabel.text = $"{currentA:0}";
            }
        }
    }

    /// <summary>
    /// 役割: ブレーキ圧力表示を更新します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ApplyBrakePressureNumbers(IReadOnlyList<float> brakePressuresKPa)
    {
        if (brakePressuresKPa == null)
        {
            return;
        }

        int count = brakePressuresKPa.Count;
        for (int i = 0; i < count; i++)
        {
            TextMeshProUGUI pressureLabel = i < generatedBrakePressureLabels.Count ? generatedBrakePressureLabels[i] : null;
            ApplyBrakePressureDisplayLayout(pressureLabel);
            if (pressureLabel == null)
            {
                continue;
            }

            pressureLabel.enabled = showBrakePressureNumbers;
            pressureLabel.text = $"{Mathf.Max(0f, brakePressuresKPa[i]):0}";
        }
    }

    /// <summary>
    /// 役割: Inspector の位置・サイズ変更を生成済み表示へ反映します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void RefreshTrainStateDisplayLayout()
    {
        int count = Mathf.Max(
            Mathf.Max(generatedCurrentBarBackgrounds.Count, generatedCurrentNumberLabels.Count),
            Mathf.Max(generatedBrakePressureLabels.Count, generatedDoorStatusImages.Count));
        for (int i = 0; i < count; i++)
        {
            Image doorStatus = i < generatedDoorStatusImages.Count ? generatedDoorStatusImages[i] : null;
            Image background = i < generatedCurrentBarBackgrounds.Count ? generatedCurrentBarBackgrounds[i] : null;
            Image fill = i < generatedCurrentBarFills.Count ? generatedCurrentBarFills[i] : null;
            TextMeshProUGUI numberLabel = i < generatedCurrentNumberLabels.Count ? generatedCurrentNumberLabels[i] : null;
            TextMeshProUGUI pressureLabel = i < generatedBrakePressureLabels.Count ? generatedBrakePressureLabels[i] : null;
            ApplyDoorStatusLayout(doorStatus);
            ApplyCurrentDisplayLayout(background, fill, numberLabel);
            ApplyBrakePressureDisplayLayout(pressureLabel);
        }
    }

    /// <summary>
    /// 役割: ドア開閉表示のレイアウトを更新します。
    /// </summary>
    /// <param name="doorStatus">doorStatus を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ApplyDoorStatusLayout(Image doorStatus)
    {
        if (doorStatus == null)
        {
            return;
        }

        RectTransform doorRect = doorStatus.rectTransform;
        doorRect.anchorMin = doorRect.anchorMax = new Vector2(0.5f, 0.5f);
        doorRect.pivot = new Vector2(0.5f, 0.5f);
        doorRect.anchoredPosition = doorStatusOffset;
        doorRect.sizeDelta = doorStatusSize;
        doorStatus.color = doorStatusColor;
        doorStatus.preserveAspect = true;
        doorStatus.raycastTarget = false;
    }

    /// <summary>
    /// 役割: 4 つのドア状態から車両ごとの開閉スプライトを反映します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ApplyDoorStatusSprites()
    {
        EnsureDoorStateCache(CarCount);

        int count = Mathf.Min(CarCount, generatedDoorStatusImages.Count);
        for (int i = 0; i < count; i++)
        {
            Image doorStatus = generatedDoorStatusImages[i];
            if (doorStatus == null)
            {
                continue;
            }

            bool anyDoorOpen = IsAnyDoorOpenInCar(i);
            Sprite sprite = anyDoorOpen ? doorOpenSprite : doorClosedSprite;
            doorStatus.sprite = sprite;
            doorStatus.enabled = showDoorStatus && sprite != null;
        }
    }

    /// <summary>
    /// 役割: 電流バーと数字のレイアウトを更新します。
    /// </summary>
    /// <param name="background">background を指定します。</param>
    /// <param name="fill">fill を指定します。</param>
    /// <param name="numberLabel">numberLabel を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ApplyCurrentDisplayLayout(Image background, Image fill, TextMeshProUGUI numberLabel)
    {
        if (background != null)
        {
            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = currentBarOffset;
            backgroundRect.sizeDelta = currentBarSize;
            background.color = currentBarBackgroundColor;
        }

        if (fill != null)
        {
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = fillRect.anchorMax = new Vector2(0.5f, 0f);
            fillRect.pivot = new Vector2(0.5f, 0f);
            fillRect.anchoredPosition = new Vector2(0f, -currentBarSize.y * 0.5f);
            fill.color = currentBarFillColor;
        }

        if (numberLabel != null)
        {
            RectTransform numberRect = numberLabel.rectTransform;
            numberRect.anchorMin = numberRect.anchorMax = new Vector2(0.5f, 0.5f);
            numberRect.pivot = new Vector2(0.5f, 0.5f);
            numberRect.anchoredPosition = currentNumberOffset;
            numberRect.sizeDelta = new Vector2(Mathf.Max(28f, spriteSize.x), Mathf.Max(8f, currentNumberFontSize + 3f));
            numberLabel.fontSize = currentNumberFontSize;
            numberLabel.color = currentNumberColor;
            numberLabel.alignment = TextAlignmentOptions.Center;
            if (carNumberFontAsset != null)
            {
                numberLabel.font = carNumberFontAsset;
            }
        }
    }

    /// <summary>
    /// 役割: ブレーキ圧力数字のレイアウトを更新します。
    /// </summary>
    /// <param name="pressureLabel">pressureLabel を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ApplyBrakePressureDisplayLayout(TextMeshProUGUI pressureLabel)
    {
        if (pressureLabel == null)
        {
            return;
        }

        RectTransform pressureRect = pressureLabel.rectTransform;
        pressureRect.anchorMin = pressureRect.anchorMax = new Vector2(0.5f, 0.5f);
        pressureRect.pivot = new Vector2(0.5f, 0.5f);
        pressureRect.anchoredPosition = brakePressureNumberOffset;
        pressureRect.sizeDelta = new Vector2(Mathf.Max(28f, spriteSize.x), Mathf.Max(8f, brakePressureNumberFontSize + 3f));
        pressureLabel.fontSize = brakePressureNumberFontSize;
        pressureLabel.color = brakePressureNumberColor;
        pressureLabel.alignment = TextAlignmentOptions.Center;
        if (carNumberFontAsset != null)
        {
            pressureLabel.font = carNumberFontAsset;
        }
    }

    /// <summary>
    /// 役割: 電流表示の生成漏れがあるか確認します。
    /// </summary>
    /// <returns>再構築が必要な場合は true を返します。</returns>
    private bool HasMissingCurrentDisplays()
    {
        if (!showCurrentBars)
        {
            return HasMissingCarNumberLabels();
        }

        for (int i = 0; i < CarCount; i++)
        {
            bool hasCarNumber = !showCarNumbers || (i < generatedCarNumberLabels.Count && generatedCarNumberLabels[i] != null);
            bool hasBackground = i < generatedCurrentBarBackgrounds.Count && generatedCurrentBarBackgrounds[i] != null;
            bool hasFill = i < generatedCurrentBarFills.Count && generatedCurrentBarFills[i] != null;
            bool hasNumber = !showCurrentNumbers || (i < generatedCurrentNumberLabels.Count && generatedCurrentNumberLabels[i] != null);
            bool hasPressureNumber = !showBrakePressureNumbers || (i < generatedBrakePressureLabels.Count && generatedBrakePressureLabels[i] != null);
            if (!hasCarNumber || !hasBackground || !hasFill || !hasNumber || !hasPressureNumber)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasMissingDoorStatusDisplays()
    {
        if (!showDoorStatus)
        {
            return false;
        }

        for (int i = 0; i < CarCount; i++)
        {
            if (i >= generatedDoorStatusImages.Count || generatedDoorStatusImages[i] == null)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasMissingCarNumberLabels()
    {
        if (!showCarNumbers)
        {
            return false;
        }

        for (int i = 0; i < CarCount; i++)
        {
            if (i >= generatedCarNumberLabels.Count || generatedCarNumberLabels[i] == null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 役割: 指定号車がモーター電流表示の対象かを判定します。
    /// </summary>
    /// <param name="carIndex">carIndex を指定します。</param>
    /// <returns>対象の場合は true を返します。</returns>
    private bool HasMotorCurrentDisplayAt(int carIndex)
    {
        if (consistDefinition == null || !consistDefinition.TryGetCar(carIndex, out CarSpec carSpec))
        {
            return false;
        }

        return carSpec != null && carSpec.carType == CarType.Motor && carSpec.motorCount > 0;
    }

    private void EnsureDoorStateCache(int carCount)
    {
        int requiredLength = Mathf.Max(0, carCount) * DoorCountPerCar;
        if (doorOpenStates == null)
        {
            doorOpenStates = new bool[requiredLength];
            return;
        }

        if (doorOpenStates.Length == requiredLength)
        {
            return;
        }

        System.Array.Resize(ref doorOpenStates, requiredLength);
    }

    private static int GetDoorStateIndex(int carIndex, int doorIndex)
    {
        return (carIndex * DoorCountPerCar) + doorIndex;
    }

    private bool IsAnyDoorOpenInCar(int carIndex)
    {
        if (carIndex < 0 || carIndex >= CarCount)
        {
            return false;
        }

        EnsureDoorStateCache(CarCount);
        int baseIndex = GetDoorStateIndex(carIndex, 0);
        for (int i = 0; i < DoorCountPerCar; i++)
        {
            if (doorOpenStates[baseIndex + i])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 役割: RebuildGeneratedImageCache の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void RebuildGeneratedImageCache()
    {
        generatedCarImages.Clear();
        generatedCarNumberLabels.Clear();
        generatedDoorStatusImages.Clear();
        generatedCurrentBarBackgrounds.Clear();
        generatedCurrentBarFills.Clear();
        generatedCurrentNumberLabels.Clear();
        generatedBrakePressureLabels.Clear();
        pendingVisualSnapshots.Clear();
        displayedMotorCurrentsA.Clear();
        displayedBrakePressuresKPa.Clear();
        nextTrainStateSampleTime = 0f;
        int childCount = formationDisplayRoot != null ? formationDisplayRoot.childCount : 0;
        for (int i = 0; i < CarCount; i++)
        {
            if (i < childCount)
            {
                Image image = formationDisplayRoot.GetChild(i).GetComponent<Image>();
                generatedCarImages.Add(image);
                TextMeshProUGUI carNumberLabel = formationDisplayRoot.GetChild(i).Find("CarNumber")?.GetComponent<TextMeshProUGUI>();
                if (carNumberLabel == null && image != null)
                {
                    carNumberLabel = CreateCarNumberLabel(image.rectTransform, i + 1, i == 0);
                }

                generatedCarNumberLabels.Add(carNumberLabel);

                Image doorStatusImage = formationDisplayRoot.GetChild(i).Find("DoorStatus")?.GetComponent<Image>();
                if (doorStatusImage == null && image != null)
                {
                    doorStatusImage = CreateDoorStatusImage(image.rectTransform, i == 0);
                }

                generatedDoorStatusImages.Add(doorStatusImage);

                Transform currentBar = formationDisplayRoot.GetChild(i).Find("CurrentBar");
                if (currentBar == null && image != null)
                {
                    CreateCurrentBar(image.rectTransform, i == 0);
                    generatedBrakePressureLabels.Add(CreateBrakePressureNumberLabel(image.rectTransform, i == 0));
                }
                else
                {
                    Image background = currentBar != null ? currentBar.GetComponent<Image>() : null;
                    Image fill = currentBar != null ? currentBar.Find("Fill")?.GetComponent<Image>() : null;
                    TextMeshProUGUI numberLabel = formationDisplayRoot.GetChild(i).Find("CurrentNumber")?.GetComponent<TextMeshProUGUI>();
                    if (numberLabel == null && image != null)
                    {
                        numberLabel = CreateCurrentNumberLabel(image.rectTransform, i == 0);
                    }

                    TextMeshProUGUI pressureLabel = formationDisplayRoot.GetChild(i).Find("BrakePressureNumber")?.GetComponent<TextMeshProUGUI>();
                    if (pressureLabel == null && image != null)
                    {
                        pressureLabel = CreateBrakePressureNumberLabel(image.rectTransform, i == 0);
                    }

                    generatedCurrentBarBackgrounds.Add(background);
                    generatedCurrentBarFills.Add(fill);
                    generatedCurrentNumberLabels.Add(numberLabel);
                    generatedBrakePressureLabels.Add(pressureLabel);
                }
            }
            else
            {
                generatedCarImages.Add(null);
                generatedCarNumberLabels.Add(null);
                generatedDoorStatusImages.Add(null);
                generatedCurrentBarBackgrounds.Add(null);
                generatedCurrentBarFills.Add(null);
                generatedCurrentNumberLabels.Add(null);
                generatedBrakePressureLabels.Add(null);
            }
        }
    }

    [ContextMenu("Clear Generated Train Formation Display")]
    /// <summary>
    /// 役割: ClearGenerated の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    public void ClearGenerated()
    {
        generatedCarImages.Clear();
        generatedCarNumberLabels.Clear();
        generatedDoorStatusImages.Clear();
        generatedCurrentBarBackgrounds.Clear();
        generatedCurrentBarFills.Clear();
        generatedCurrentNumberLabels.Clear();
        generatedBrakePressureLabels.Clear();
        pendingVisualSnapshots.Clear();
        displayedMotorCurrentsA.Clear();
        displayedBrakePressuresKPa.Clear();
        nextTrainStateSampleTime = 0f;
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

    /// <summary>
    /// 役割: TryGetCarTypeAt の処理を実行します。
    /// </summary>
    /// <param name="carIndex">carIndex を指定します。</param>
    /// <param name="carType">carType を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private bool TryGetCarTypeAt(int carIndex, out CarType carType)
    {
        if (consistDefinition == null)
        {
            carType = CarType.Trailer;
            return false;
        }

        return consistDefinition.TryGetCarType(carIndex, out carType);
    }

    /// <summary>
    /// 役割: GetCarTypeAtOrDefault の処理を実行します。
    /// </summary>
    /// <param name="carIndex">carIndex を指定します。</param>
    /// <param name="fallback">fallback を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private CarType GetCarTypeAtOrDefault(int carIndex, CarType fallback = CarType.Trailer)
    {
        return TryGetCarTypeAt(carIndex, out CarType carType) ? carType : fallback;
    }

    /// <summary>
    /// 役割: TryGetCarMotorCurrentA の処理を実行します。
    /// </summary>
    /// <param name="carIndex">carIndex を指定します。</param>
    /// <param name="motorCurrentA">motorCurrentA を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private bool TryGetCarMotorCurrentA(int carIndex, out float motorCurrentA)
    {
        motorCurrentA = 0f;

        if (train == null)
        {
            return false;
        }

        if (TryGetCarVvvfSignedMotorCurrentA(carIndex, out motorCurrentA))
        {
            return true;
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

        motorCurrentA = GetSignedCarCurrentA(state);
        return true;
    }

    private bool IsCarInTractionOrRegen(int carIndex)
    {
        return GetCarDriveDirection(carIndex) != 0f;
    }

    private float GetCarDriveDirection(int carIndex)
    {
        float currentThresholdA = Mathf.Max(0f, currentDirectionThresholdA);
        if (TryGetCarMotorCurrentA(carIndex, out float signedCurrentA) &&
            Mathf.Abs(signedCurrentA) > currentThresholdA)
        {
            return Mathf.Sign(signedCurrentA);
        }

        if (TryGetCarVvvfTractionForceN(carIndex, out float vvvfTractionForceN) &&
            Mathf.Abs(vvvfTractionForceN) > 1f)
        {
            return Mathf.Sign(vvvfTractionForceN);
        }

        return 0f;
    }

    private bool TryGetCarVvvfSignedMotorCurrentA(int carIndex, out float signedCurrentA)
    {
        signedCurrentA = 0f;

        if (train == null || carIndex < 0)
        {
            return false;
        }

        VVVFController[] vvvfControllers = train.VVVFControllers;
        if (vvvfControllers == null || vvvfControllers.Length == 0)
        {
            return false;
        }

        bool found = false;
        for (int i = 0; i < vvvfControllers.Length; i++)
        {
            VVVFController vvvf = vvvfControllers[i];
            if (vvvf == null || vvvf.AssignedCarIndex != carIndex)
            {
                continue;
            }

            MotorModel[] motors = vvvf.Motors;
            if (motors == null)
            {
                found = true;
                continue;
            }

            for (int j = 0; j < motors.Length; j++)
            {
                signedCurrentA += GetSignedMotorCurrentA(motors[j]);
            }
            found = true;
        }

        return found;
    }

    private static float GetSignedMotorCurrentA(MotorModel motor)
    {
        if (motor == null)
        {
            return 0f;
        }

        float currentA = Mathf.Max(0f, motor.MotorCurrentRmsA);
        if (currentA <= 0f)
        {
            return 0f;
        }

        float signSource = Mathf.Abs(motor.InputActivePowerW) > 0.01f
            ? motor.InputActivePowerW
            : motor.MotorTorqueNm;
        if (Mathf.Abs(signSource) <= 0.0001f)
        {
            return 0f;
        }

        return Mathf.Sign(signSource) * currentA;
    }

    private static float GetSignedCarCurrentA(CarTractionState state)
    {
        if (state == null)
        {
            return 0f;
        }

        float currentA = Mathf.Max(0f, state.motorCurrentA);
        if (currentA <= 0f)
        {
            return 0f;
        }

        if (Mathf.Abs(state.tractionForceN) <= 0.0001f)
        {
            return currentA;
        }

        return Mathf.Sign(state.tractionForceN) * currentA;
    }

    private bool TryGetCarVvvfTractionForceN(int carIndex, out float tractionForceN)
    {
        tractionForceN = 0f;

        if (train == null || carIndex < 0)
        {
            return false;
        }

        VVVFController[] vvvfControllers = train.VVVFControllers;
        if (vvvfControllers == null || vvvfControllers.Length == 0)
        {
            return false;
        }

        bool found = false;
        for (int i = 0; i < vvvfControllers.Length; i++)
        {
            VVVFController vvvf = vvvfControllers[i];
            if (vvvf == null || vvvf.AssignedCarIndex != carIndex)
            {
                continue;
            }

            tractionForceN += vvvf.TotalMotorTractionForceN;
            found = true;
        }

        return found;
    }

    /// <summary>
    /// 役割: TryGetCarBrakePressureKPa の処理を実行します。
    /// </summary>
    /// <param name="carIndex">carIndex を指定します。</param>
    /// <param name="pressureKPa">pressureKPa を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private bool TryGetCarBrakePressureKPa(int carIndex, out float pressureKPa)
    {
        pressureKPa = 0f;

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

        pressureKPa = state.bcPressureKPa;
        return true;
    }

    /// <summary>
    /// 役割: TryGetCarRegenForceN の処理を実行します。
    /// </summary>
    /// <param name="carIndex">carIndex を指定します。</param>
    /// <param name="regenForceN">regenForceN を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private bool TryGetCarRegenForceN(int carIndex, out float regenForceN)
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
