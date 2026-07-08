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
    [SerializeField] private TimsSystem tims;

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

    [Header("Traction State")]
    [SerializeField] private bool showTractionRegenState = true;

    private const int DoorCountPerCar = 4;
    private ConsistDefinition ConsistDefinition => tims != null ? tims.ConsistDefinition : null;
    private int CarCount => ConsistDefinition != null ? ConsistDefinition.CarCount : 0;

    private readonly List<Image> generatedCarImages = new List<Image>();
    private readonly List<TextMeshProUGUI> generatedCarNumberLabels = new List<TextMeshProUGUI>();
    private readonly List<Image> generatedDoorStatusImages = new List<Image>();

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
        ResolveRuntimeReferences();
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
        ResolveRuntimeReferences();
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

        if (!showTractionRegenState)
        {
            return normalMotor;
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
            generatedDoorStatusImages.Clear();
            return;
        }

        if (generatedCarImages.Count != CarCount ||
            generatedCarNumberLabels.Count != CarCount ||
            generatedDoorStatusImages.Count != CarCount ||
            HasMissingCarNumberLabels() ||
            HasMissingDoorStatusDisplays())
        {
            RebuildGeneratedImageCache();
        }

        EnsureDoorStateCache(CarCount);
        RefreshTrainStateDisplayLayout();
        ApplyDoorStatusSprites();

        ApplyVisualSnapshot(CaptureCurrentSnapshot());
    }

    private void ResolveRuntimeReferences()
    {
        if (tims == null)
        {
            tims = GetComponentInParent<TimsSystem>();
        }

        if (tims == null)
        {
            tims = FindAnyObjectByType<TimsSystem>();
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
            activeCarNumbers[i] = showTractionRegenState && IsCarInTractionOrRegen(i);
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
    /// 役割: Inspector の位置・サイズ変更を生成済み表示へ反映します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void RefreshTrainStateDisplayLayout()
    {
        int count = generatedDoorStatusImages.Count;
        for (int i = 0; i < count; i++)
        {
            Image doorStatus = i < generatedDoorStatusImages.Count ? generatedDoorStatusImages[i] : null;
            ApplyDoorStatusLayout(doorStatus);
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
            }
            else
            {
                generatedCarImages.Add(null);
                generatedCarNumberLabels.Add(null);
                generatedDoorStatusImages.Add(null);
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
        ConsistDefinition definition = ConsistDefinition;
        if (definition == null)
        {
            carType = CarType.Trailer;
            return false;
        }

        return definition.TryGetCarType(carIndex, out carType);
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

    private bool IsCarInTractionOrRegen(int carIndex)
    {
        return GetCarDriveDirection(carIndex) != 0f;
    }

    private float GetCarDriveDirection(int carIndex)
    {
        if (TryGetCarTractionForceN(carIndex, out float tractionForceN) &&
            Mathf.Abs(tractionForceN) > 1f)
        {
            return Mathf.Sign(tractionForceN);
        }

        return 0f;
    }

    private bool TryGetCarTractionForceN(int carIndex, out float tractionForceN)
    {
        tractionForceN = 0f;

        TimsDataBus localBus = GetLocalBus(carIndex);
        if (localBus == null)
        {
            return false;
        }

        return localBus.TryGetFloat(new TimsTagKey("VVVF", "TotalMotorTractionForceN"), out tractionForceN);
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
}
