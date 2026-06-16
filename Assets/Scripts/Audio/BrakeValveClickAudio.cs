
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BrakeValveClickAudio : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainController train;
    [SerializeField] private AudioSource audioSource;

    [Header("Clips")]
    [SerializeField] private AudioClip valveClickClip;

    [Header("Pressure Tracking")]
    [SerializeField, Min(0.05f)] private float pressureSampleWindowSec = 1f;
    [SerializeField, Min(0f)] private float pressureRateThresholdKPaPerSec = 8f;
    [SerializeField, Min(0.001f)] private float minClickIntervalSec = 0.035f;
    [SerializeField, Min(0.001f)] private float maxClickIntervalSec = 0.12f;
    [SerializeField, Min(0f)] private float maxPressureRateForFastClickKPaPerSec = 120f;

    [Header("Randomization")]
    [SerializeField, Range(0f, 1f)] private float minVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float maxVolume = 0.9f;
    [SerializeField] private float minPitch = 0.9f;
    [SerializeField] private float maxPitch = 1.12f;

    private struct PressureSample
    {
        public float time;
        public float pressureKPa;
    }

    private readonly List<List<PressureSample>> pressureHistories = new List<List<PressureSample>>();
    private float clickTimer;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (train == null)
        {
            train = GetComponentInParent<TrainController>();
        }

        audioSource.playOnAwake = false;
    }

    private void Update()
    {
        if (train == null)
        {
            return;
        }

        UpdateValveClicks();
    }

    private void UpdateValveClicks()
    {
        IReadOnlyList<CarBrakeState> states = train.CurrentCarBrakeStates;
        if (states == null || states.Count == 0)
        {
            pressureHistories.Clear();
            return;
        }

        EnsurePressureHistoryCount(states.Count);

        float now = Time.time;
        float safeWindowSec = Mathf.Max(0.05f, pressureSampleWindowSec);
        float maxPressureRate = 0f;

        for (int i = 0; i < states.Count; i++)
        {
            CarBrakeState state = states[i];
            float currentPressure = state != null ? Mathf.Max(0f, state.bcPressureKPa) : 0f;
            List<PressureSample> history = pressureHistories[i];

            history.Add(new PressureSample
            {
                time = now,
                pressureKPa = currentPressure
            });

            TrimPressureHistory(history, now - safeWindowSec);

            PressureSample oldSample = history.Count > 0 ? history[0] : new PressureSample
            {
                time = now,
                pressureKPa = currentPressure
            };

            float elapsed = Mathf.Max(0.0001f, now - oldSample.time);
            float pressureRate = Mathf.Abs(currentPressure - oldSample.pressureKPa) / elapsed;

            if (pressureRate > maxPressureRate)
            {
                maxPressureRate = pressureRate;
            }
        }

        bool isAdjusting = maxPressureRate >= pressureRateThresholdKPaPerSec;
        if (!isAdjusting || valveClickClip == null)
        {
            clickTimer = 0f;
            return;
        }

        clickTimer -= Time.deltaTime;
        if (clickTimer > 0f)
        {
            return;
        }

        float rate01 = Mathf.Clamp01(maxPressureRate / Mathf.Max(1f, maxPressureRateForFastClickKPaPerSec));
        float interval = Mathf.Lerp(maxClickIntervalSec, minClickIntervalSec, rate01);
        float volume = Mathf.Lerp(minVolume, maxVolume, rate01);

        PlayClick(valveClickClip, volume);
        clickTimer = interval;
    }

    private void EnsurePressureHistoryCount(int count)
    {
        while (pressureHistories.Count < count)
        {
            pressureHistories.Add(new List<PressureSample>());
        }

        while (pressureHistories.Count > count)
        {
            pressureHistories.RemoveAt(pressureHistories.Count - 1);
        }
    }

    private void TrimPressureHistory(List<PressureSample> history, float oldestTime)
    {
        while (history.Count > 1 && history[1].time <= oldestTime)
        {
            history.RemoveAt(0);
        }
    }

    private void PlayClick(AudioClip clip, float volume)
    {
        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
