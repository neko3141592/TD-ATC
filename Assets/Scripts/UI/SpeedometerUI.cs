using TMPro;
using UnityEngine;

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
    [SerializeField] private float atcNoLimitThresholdKmH = 0.1f;

    [Header("Motion")]
    [SerializeField] private float smoothing = 10f;

    [Header("Needle Quantize")]
    [SerializeField, Min(0.1f)] private float needleStepKmH = 0.5f;

    [Header("Random Update Lag")]
    [SerializeField] private bool enableRandomLag = true;
    [SerializeField, Min(0f)] private float minUpdateLagSec = 0.03f;
    [SerializeField, Min(0f)] private float maxUpdateLagSec = 0.12f;

    private float displayedSpeedKmH = 0f;
    private float sampledSpeedKmH = 0f;
    private float nextSampleTime = 0f;
    private bool hasSampledSpeed = false;

    private void Awake()
    {
        if (atcController == null)
        {
            atcController = FindFirstObjectByType<ATCController>();
        }
    }

    private void OnEnable()
    {
        displayedSpeedKmH = 0f;
        sampledSpeedKmH = 0f;
        hasSampledSpeed = false;
        nextSampleTime = 0f;
    }

    void Update()
    {
        if (train != null)
        {
            SampleSpeedIfNeeded();
            float targetSpeedKmH = Mathf.Clamp(sampledSpeedKmH, minSpeedKmH, maxSpeedKmH);
            float lerpFactor = 1f - Mathf.Exp(-Mathf.Max(0f, smoothing) * Time.deltaTime);
            displayedSpeedKmH = Mathf.Lerp(displayedSpeedKmH, targetSpeedKmH, lerpFactor);

            float needleStep = Mathf.Max(0.1f, needleStepKmH);
            float quantizedNeedleSpeedKmH = Mathf.Round(displayedSpeedKmH / needleStep) * needleStep;
            float t = Mathf.InverseLerp(minSpeedKmH, maxSpeedKmH, quantizedNeedleSpeedKmH);
            if (invertDirection)
            {
                t = 1f - t;
            }

            float needleAngle = Mathf.Lerp(minNeedleAngle, maxNeedleAngle, t) + needleAngleOffset;

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

    private void SampleSpeedIfNeeded()
    {
        if (!hasSampledSpeed || !enableRandomLag || Time.time >= nextSampleTime)
        {
            sampledSpeedKmH = train.SpeedKmH;
            hasSampledSpeed = true;
            ScheduleNextSampleTime();
        }
    }

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

    private void UpdateAtcTriangle()
    {
        if (atcTriangle == null)
        {
            return;
        }

        float patternAllowSpeedKmH = atcController != null ? atcController.CurrentPatternAllowSpeedKmH : 0f;
        bool hasLimit = atcController != null && patternAllowSpeedKmH > atcNoLimitThresholdKmH;

        if (hideAtcTriangleWhenNoLimit)
        {
            atcTriangle.gameObject.SetActive(hasLimit);
        }

        if (!hasLimit)
        {
            return;
        }

        float clampedLimitKmH = Mathf.Floor(Mathf.Clamp(patternAllowSpeedKmH, minSpeedKmH, maxSpeedKmH));
        float t = Mathf.InverseLerp(minSpeedKmH, maxSpeedKmH, clampedLimitKmH);
        if (invertDirection)
        {
            t = 1f - t;
        }

        float markerAngle = Mathf.Lerp(minNeedleAngle, maxNeedleAngle, t) + needleAngleOffset + atcTriangleAngleOffset;
        atcTriangle.localEulerAngles = new Vector3(0f, 0f, markerAngle);
    }
}
