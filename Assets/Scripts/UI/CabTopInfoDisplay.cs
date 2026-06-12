using System;
using System.Globalization;
using TMPro;
using UnityEngine;

public class CabTopInfoDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainController train;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text distanceText;

    [Header("Clock")]
    [SerializeField] private bool useSystemClock = true;
    [SerializeField, Range(0, 23)] private int startHour = 12;
    [SerializeField, Range(0, 59)] private int startMinute = 0;
    [SerializeField, Range(0, 59)] private int startSecond = 0;
    [SerializeField, Min(0f)] private float simulatedClockScale = 1f;

    [Header("Display")]
    [SerializeField, Min(0.02f)] private float updateIntervalSeconds = 0.1f;
    [SerializeField] private bool clampReverseDistanceToZero = true;

    private float nextUpdateTime;
    private float simulatedClockStartRealtime;

    private void OnEnable()
    {
        ResolveReferences();
        simulatedClockStartRealtime = Time.realtimeSinceStartup;
        nextUpdateTime = 0f;
        UpdateDisplay();
    }

    private void Update()
    {
        ResolveReferences();

        if (Time.unscaledTime < nextUpdateTime)
        {
            return;
        }

        nextUpdateTime = Time.unscaledTime + Mathf.Max(0.02f, updateIntervalSeconds);
        UpdateDisplay();
    }

    private void ResolveReferences()
    {
        train = CabReferenceResolver.ResolveTrain(this, train);

        if (timeText == null)
        {
            timeText = FindText("Time");
        }

        if (speedText == null)
        {
            speedText = FindText("Speed/Speed", "Speed");
        }

        if (distanceText == null)
        {
            distanceText = FindText("Dist/Dist", "Dist");
        }
    }

    private TMP_Text FindText(params string[] namesOrPaths)
    {
        foreach (string nameOrPath in namesOrPaths)
        {
            Transform target = transform.Find(nameOrPath);
            if (target != null && target.TryGetComponent(out TMP_Text text))
            {
                return text;
            }
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (string nameOrPath in namesOrPaths)
        {
            string targetName = GetLastPathName(nameOrPath);
            foreach (TMP_Text text in texts)
            {
                if (text != null && text.name == targetName)
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static string GetLastPathName(string path)
    {
        int slashIndex = path.LastIndexOf('/');
        return slashIndex >= 0 ? path.Substring(slashIndex + 1) : path;
    }

    private void UpdateDisplay()
    {
        if (timeText != null)
        {
            timeText.text = FormatTimeText();
        }

        if (speedText != null)
        {
            float speedKmH = train != null ? train.SpeedKmH : 0f;
            speedText.text = Mathf.RoundToInt(Mathf.Max(0f, speedKmH)).ToString("0", CultureInfo.InvariantCulture);
        }

        if (distanceText != null)
        {
            float distanceKm = train != null ? train.DistanceM / 1000f : 0f;
            if (clampReverseDistanceToZero)
            {
                distanceKm = Mathf.Max(0f, distanceKm);
            }

            distanceText.text = ToMonitorPunctuation(distanceKm.ToString("0.0", CultureInfo.InvariantCulture));
        }
    }

    private string FormatTimeText()
    {
        if (useSystemClock)
        {
            DateTime now = DateTime.Now;
            return $"{now:HH}：{now:mm}：{now:ss}";
        }

        float elapsedSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - simulatedClockStartRealtime);
        int totalSeconds = Mathf.FloorToInt(GetStartSeconds() + elapsedSeconds * Mathf.Max(0f, simulatedClockScale));
        totalSeconds = Mod(totalSeconds, 24 * 60 * 60);

        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds / 60) % 60;
        int seconds = totalSeconds % 60;
        return $"{hours:00}：{minutes:00}：{seconds:00}";
    }

    private int GetStartSeconds()
    {
        return startHour * 3600 + startMinute * 60 + startSecond;
    }

    private static int Mod(int value, int divisor)
    {
        int result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private static string ToMonitorPunctuation(string text)
    {
        return string.IsNullOrEmpty(text)
            ? string.Empty
            : text.Replace(':', '：').Replace('.', '．');
    }
}
