using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpeedometerUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainController train;
    [SerializeField] private ATCController atcController;
    [SerializeField] private RectTransform needle;
    [SerializeField] private RectTransform atcTriangle;
    [SerializeField] private TMP_Text speedText;

    [Header("Scale (km/h)")]
    [SerializeField] private float minSpeedKmH = 0f;
    [SerializeField] private float maxSpeedKmH = 120f;

    [Header("Needle Angle (Z Euler)")]
    [SerializeField] private float minNeedleAngle = 210f;
    [SerializeField] private float maxNeedleAngle = -30f;
    [SerializeField] private float needleAngleOffset = 0f;
    [SerializeField] private bool invertDirection = false;

    [Header("ATC Triangle")]
    [SerializeField] private bool hideAtcTriangleWhenNoLimit = true;
    [SerializeField] private float atcTriangleAngleOffset = 0f;
    [SerializeField, Min(0.1f)] private float atcMarkerStepKmH = 5f;
    [SerializeField] private RectTransform atcMarkerRoot;
    [SerializeField] private Image atcMarkerTemplate;
    [SerializeField] private Sprite atcInactiveMarkerSprite;
    [SerializeField] private Sprite atcActiveMarkerSprite;
    [SerializeField] private bool generateAtcMarkersOnEnable = true;

    [Header("Motion")]
    [SerializeField] private float smoothing = 10f;

    [Header("Needle Quantize")]
    [SerializeField, Min(0.1f)] private float needleStepKmH = 1f;

    [Header("Random Update Lag")]
    [SerializeField] private bool enableRandomLag = true;
    [SerializeField, Min(0f)] private float minUpdateLagSec = 0.03f;
    [SerializeField, Min(0f)] private float maxUpdateLagSec = 0.12f;

    private float displayedSpeedKmH = 0f;
    private float sampledSpeedKmH = 0f;
    private float nextSampleTime = 0f;
    private bool hasSampledSpeed = false;
    private AtcMarker[] atcMarkers;

    private struct AtcMarker
    {
        public float speedKmH;
        public RectTransform rectTransform;
        public Image image;
    }

    /// <summary>
    /// 役割: OnEnable の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void OnEnable()
    {
        ResolveReferences();

        displayedSpeedKmH = 0f;
        sampledSpeedKmH = 0f;
        hasSampledSpeed = false;
        nextSampleTime = 0f;

        if (generateAtcMarkersOnEnable)
        {
            GenerateAtcMarkers();
        }
    }

    /// <summary>
    /// 役割: Update の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    void Update()
    {
        ResolveReferences();

        if (train != null)
        {
            SampleSpeedIfNeeded();
            float targetSpeedKmH = sampledSpeedKmH;
            float lerpFactor = 1f - Mathf.Exp(-Mathf.Max(0f, smoothing) * Time.deltaTime);
            displayedSpeedKmH = Mathf.Lerp(displayedSpeedKmH, targetSpeedKmH, lerpFactor);

            float needleStep = Mathf.Max(0.1f, needleStepKmH);
            float quantizedNeedleSpeedKmH = Mathf.Round(displayedSpeedKmH / needleStep) * needleStep;
            float speedRangeKmH = Mathf.Max(0.001f, maxSpeedKmH - minSpeedKmH);
            float t = (quantizedNeedleSpeedKmH - minSpeedKmH) / speedRangeKmH;
            if (invertDirection)
            {
                t = 1f - t;
            }

            float needleAngle = Mathf.LerpUnclamped(minNeedleAngle, maxNeedleAngle, t) + needleAngleOffset;

            if (needle != null)
            {
                needle.localEulerAngles = new Vector3(0f, 0f, needleAngle);
            }

            if (speedText != null)
            {
                int roundedSpeedKmH = Mathf.RoundToInt(displayedSpeedKmH);
                speedText.text = $"{roundedSpeedKmH:0}";
            }
        }

        UpdateAtcTriangle();
    }

    private void ResolveReferences()
    {
        train = CabReferenceResolver.ResolveTrain(this, train);
        atcController = CabReferenceResolver.ResolveTrainComponent(this, train, atcController);
    }

    /// <summary>
    /// 役割: SampleSpeedIfNeeded の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void SampleSpeedIfNeeded()
    {
        if (!hasSampledSpeed || !enableRandomLag || Time.time >= nextSampleTime)
        {
            sampledSpeedKmH = train.SpeedKmH;
            hasSampledSpeed = true;
            ScheduleNextSampleTime();
        }
    }

    /// <summary>
    /// 役割: ScheduleNextSampleTime の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ScheduleNextSampleTime()
    {
        if (!enableRandomLag)
        {
            nextSampleTime = Time.time;
            return;
        }

        float minLag = Mathf.Max(0f, minUpdateLagSec);
        float maxLag = Mathf.Max(minLag, maxUpdateLagSec);
        nextSampleTime = Time.time + Random.Range(minLag, maxLag);
    }

    [ContextMenu("Generate ATC Markers")]
    private void GenerateAtcMarkers()
    {
        Image template = ResolveAtcMarkerTemplate();
        RectTransform root = ResolveAtcMarkerRoot(template);
        if (template == null || root == null)
        {
            return;
        }

        ClearGeneratedAtcMarkers(root);

        int markerCount = Mathf.FloorToInt((maxSpeedKmH - minSpeedKmH) / Mathf.Max(0.1f, atcMarkerStepKmH)) + 1;
        markerCount = Mathf.Max(0, markerCount);
        atcMarkers = new AtcMarker[markerCount];

        template.gameObject.SetActive(false);

        for (int i = 0; i < markerCount; i++)
        {
            float markerSpeedKmH = minSpeedKmH + (i * Mathf.Max(0.1f, atcMarkerStepKmH));
            Image markerImage = Instantiate(template, root);
            markerImage.name = $"ATCMarker_{Mathf.RoundToInt(markerSpeedKmH):000}";
            markerImage.raycastTarget = false;
            markerImage.sprite = GetInactiveAtcMarkerSprite(template);
            markerImage.gameObject.SetActive(true);

            RectTransform markerTransform = markerImage.rectTransform;
            markerTransform.localScale = template.rectTransform.localScale;
            markerTransform.anchoredPosition = template.rectTransform.anchoredPosition;
            markerTransform.sizeDelta = template.rectTransform.sizeDelta;
            markerTransform.localEulerAngles = GetSpeedMarkerEuler(markerSpeedKmH);

            atcMarkers[i] = new AtcMarker
            {
                speedKmH = markerSpeedKmH,
                rectTransform = markerTransform,
                image = markerImage
            };
        }
    }

    private Image ResolveAtcMarkerTemplate()
    {
        if (atcMarkerTemplate != null)
        {
            return atcMarkerTemplate;
        }

        return atcTriangle != null ? atcTriangle.GetComponent<Image>() : null;
    }

    private RectTransform ResolveAtcMarkerRoot(Image template)
    {
        if (atcMarkerRoot != null)
        {
            return atcMarkerRoot;
        }

        if (template == null)
        {
            return null;
        }

        return template.transform.parent as RectTransform;
    }

    private void ClearGeneratedAtcMarkers(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (!child.name.StartsWith("ATCMarker_"))
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    private void UpdateAtcTriangle()
    {
        if (atcMarkers == null || atcMarkers.Length == 0)
        {
            UpdateLegacyAtcTriangle();
            return;
        }

        float patternAllowSpeedKmH = atcController != null ? atcController.CurrentPatternAllowSpeedKmH : 0f;
        bool hasLimit = atcController != null &&
            !string.IsNullOrEmpty(atcController.CurrentPatternSourceLabel) &&
            atcController.CurrentPatternSourceLabel != "--";
        bool isAtcCutOut = atcController != null && atcController.IsAtcCutOutActive;

        if (hideAtcTriangleWhenNoLimit)
        {
            SetAtcMarkersActive(hasLimit || isAtcCutOut);
        }

        if (!hasLimit)
        {
            SetAtcMarkersInactive();
            return;
        }

        float activeMarkerSpeedKmH = RoundToAtcMarkerStep(patternAllowSpeedKmH);
        for (int i = 0; i < atcMarkers.Length; i++)
        {
            AtcMarker marker = atcMarkers[i];
            if (marker.image == null)
            {
                continue;
            }

            bool isActive = Mathf.Approximately(marker.speedKmH, activeMarkerSpeedKmH);
            marker.image.sprite = isActive ? GetActiveAtcMarkerSprite() : GetInactiveAtcMarkerSprite(marker.image);
        }
    }

    private Sprite GetActiveAtcMarkerSprite()
    {
        return atcActiveMarkerSprite != null ? atcActiveMarkerSprite : GetInactiveAtcMarkerSprite(ResolveAtcMarkerTemplate());
    }

    private Sprite GetInactiveAtcMarkerSprite(Image fallbackSource)
    {
        if (atcInactiveMarkerSprite != null)
        {
            return atcInactiveMarkerSprite;
        }

        return fallbackSource != null ? fallbackSource.sprite : null;
    }

    private void UpdateLegacyAtcTriangle()
    {
        if (atcTriangle == null)
        {
            return;
        }

        float patternAllowSpeedKmH = atcController != null ? atcController.CurrentPatternAllowSpeedKmH : 0f;
        bool hasLimit = atcController != null &&
            !string.IsNullOrEmpty(atcController.CurrentPatternSourceLabel) &&
            atcController.CurrentPatternSourceLabel != "--";
        bool isAtcCutOut = atcController != null && atcController.IsAtcCutOutActive;

        if (hideAtcTriangleWhenNoLimit)
        {
            atcTriangle.gameObject.SetActive(hasLimit || isAtcCutOut);
        }

        if (!hasLimit)
        {
            return;
        }

        float clampedLimitKmH = RoundToAtcMarkerStep(patternAllowSpeedKmH);
        atcTriangle.localEulerAngles = GetSpeedMarkerEuler(clampedLimitKmH);
    }

    private void SetAtcMarkersInactive()
    {
        for (int i = 0; i < atcMarkers.Length; i++)
        {
            AtcMarker marker = atcMarkers[i];
            if (marker.image != null)
            {
                marker.image.sprite = GetInactiveAtcMarkerSprite(marker.image);
            }
        }
    }

    private void SetAtcMarkersActive(bool isActive)
    {
        for (int i = 0; i < atcMarkers.Length; i++)
        {
            if (atcMarkers[i].image != null)
            {
                atcMarkers[i].image.gameObject.SetActive(isActive);
            }
        }
    }

    private float RoundToAtcMarkerStep(float speedKmH)
    {
        float stepKmH = Mathf.Max(0.1f, atcMarkerStepKmH);
        float roundedSpeedKmH = Mathf.Round(speedKmH / stepKmH) * stepKmH;
        return Mathf.Clamp(roundedSpeedKmH, minSpeedKmH, maxSpeedKmH);
    }

    private Vector3 GetSpeedMarkerEuler(float speedKmH)
    {
        float clampedLimitKmH = Mathf.Clamp(speedKmH, minSpeedKmH, maxSpeedKmH);
        float t = Mathf.InverseLerp(minSpeedKmH, maxSpeedKmH, clampedLimitKmH);
        if (invertDirection)
        {
            t = 1f - t;
        }

        float markerAngle = Mathf.Lerp(minNeedleAngle, maxNeedleAngle, t) + needleAngleOffset + atcTriangleAngleOffset;
        return new Vector3(0f, 0f, markerAngle);
    }
}
