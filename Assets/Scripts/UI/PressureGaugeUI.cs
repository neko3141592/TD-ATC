using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PressureGaugeUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainController train;
    [SerializeField] private RectTransform needle;
    [SerializeField] private TMP_Text pressureText;

    [Header("Scale (kPa)")]
    [SerializeField] private float minPressureKPa = 0f;
    [SerializeField] private float maxPressureKPa = 380f;

    [Header("Needle Angle (Z Euler)")]
    [SerializeField] private float minNeedleAngle = 210f;
    [SerializeField] private float maxNeedleAngle = -30f;
    [SerializeField] private float needleAngleOffset = 0f;
    [SerializeField] private bool invertDirection = false;

    [Header("Motion")]
    [SerializeField] private float smoothing = 12f;
    [SerializeField, Min(0.1f)] private float needleStepKPa = 5f;

    [Header("Display Lag")]
    [SerializeField, Min(0f)] private float displayLagSec = 0.12f;

    [Header("Random Update Lag")]
    [SerializeField] private bool enableRandomLag = false;
    [SerializeField, Min(0f)] private float minUpdateLagSec = 0.02f;
    [SerializeField, Min(0f)] private float maxUpdateLagSec = 0.08f;

    private float displayedPressureKPa = 0f;
    private float sampledPressureKPa = 0f;
    private float laggedPressureKPa = 0f;
    private float nextSampleTime = 0f;
    private bool hasSampledPressure = false;
    private bool hasLaggedPressure = false;

    private readonly Queue<TimedPressureSample> pressureSamples = new Queue<TimedPressureSample>();

    private struct TimedPressureSample
    {
        public float time;
        public float pressureKPa;

        /// <summary>
        /// 役割: TimedPressureSample の処理を実行します。
        /// </summary>
        /// <param name="time">time を指定します。</param>
        /// <param name="pressureKPa">pressureKPa を指定します。</param>
        /// <returns>処理結果を返します。</returns>
        public TimedPressureSample(float time, float pressureKPa)
        {
            this.time = time;
            this.pressureKPa = pressureKPa;
        }
    }

    /// <summary>
    /// 役割: OnEnable の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void OnEnable()
    {
        displayedPressureKPa = 0f;
        sampledPressureKPa = 0f;
        laggedPressureKPa = 0f;
        nextSampleTime = 0f;
        hasSampledPressure = false;
        hasLaggedPressure = false;
        pressureSamples.Clear();
    }

    /// <summary>
    /// 役割: Update の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Update()
    {
        if (train == null)
        {
            return;
        }

        UpdateDisplayLag(train.CurrentBCPressureKPa);
        SamplePressureIfNeeded();

        float minPressure = Mathf.Min(minPressureKPa, maxPressureKPa);
        float maxPressure = Mathf.Max(minPressureKPa, maxPressureKPa);
        float targetPressureKPa = Mathf.Clamp(sampledPressureKPa, minPressure, maxPressure);

        float lerpFactor = 1f - Mathf.Exp(-Mathf.Max(0f, smoothing) * Time.deltaTime);
        displayedPressureKPa = Mathf.Lerp(displayedPressureKPa, targetPressureKPa, lerpFactor);

        float step = Mathf.Max(0.1f, needleStepKPa);
        float quantizedPressureKPa = Mathf.Round(displayedPressureKPa / step) * step;
        float t = Mathf.InverseLerp(minPressure, maxPressure, quantizedPressureKPa);
        if (invertDirection)
        {
            t = 1f - t;
        }

        float needleAngle = Mathf.Lerp(minNeedleAngle, maxNeedleAngle, t) + needleAngleOffset;

        if (needle != null)
        {
            needle.localEulerAngles = new Vector3(0f, 0f, needleAngle);
        }

        if (pressureText != null)
        {
            int roundedPressureKPa = Mathf.RoundToInt(displayedPressureKPa);
            pressureText.text = roundedPressureKPa.ToString();
        }
    }

    /// <summary>
    /// 役割: SamplePressureIfNeeded の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void SamplePressureIfNeeded()
    {
        if (!hasSampledPressure || !enableRandomLag || Time.time >= nextSampleTime)
        {
            sampledPressureKPa = laggedPressureKPa;
            hasSampledPressure = true;
            ScheduleNextSampleTime();
        }
    }

    /// <summary>
    /// 役割: UpdateDisplayLag の処理を実行します。
    /// </summary>
    /// <param name="currentPressureKPa">currentPressureKPa を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void UpdateDisplayLag(float currentPressureKPa)
    {
        if (!hasLaggedPressure)
        {
            laggedPressureKPa = currentPressureKPa;
            hasLaggedPressure = true;
        }

        if (displayLagSec <= 0f)
        {
            laggedPressureKPa = currentPressureKPa;
            pressureSamples.Clear();
            return;
        }

        pressureSamples.Enqueue(new TimedPressureSample(Time.time, currentPressureKPa));

        float targetTime = Time.time - displayLagSec;
        while (pressureSamples.Count > 0 && pressureSamples.Peek().time <= targetTime)
        {
            laggedPressureKPa = pressureSamples.Dequeue().pressureKPa;
        }

        // バッファ肥大化防止（通常ここまで増えない保険）
        while (pressureSamples.Count > 600)
        {
            pressureSamples.Dequeue();
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
}
