using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CurrentMeterGraphUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainController train;
    [SerializeField] private RectTransform graphRoot;
    [SerializeField] private Image currentFill;
    [SerializeField] private TMP_Text currentText;
    [SerializeField] private TMP_Text unitText;
    [SerializeField] private Image overloadLamp;

    [Header("Current Source")]
    [SerializeField] private bool useAverageVvvfUnitCurrent = true;
    [SerializeField] private bool fallbackToCarTractionCurrent = true;
    [SerializeField] private bool useTotalMotorCurrent = true;
    [SerializeField, Min(0)] private int carIndex = 0;
    [SerializeField] private bool useAbsoluteCurrent = true;
    [SerializeField, Min(0f)] private float currentDirectionThresholdA = 0.5f;

    [Header("Scale (A)")]
    [SerializeField] private float minCurrentA = 0f;
    [SerializeField] private float maxCurrentA = 2000f;
    [SerializeField] private float warningCurrentA = 1800f;
    [SerializeField, Min(0.1f)] private float displayStepA = 2f;
    [SerializeField, Min(0f)] private float minimumFillHeight = 2f;

    [Header("Random Update Lag")]
    [SerializeField] private bool enableRandomLag = false;
    [SerializeField, Min(0f)] private float minUpdateLagSec = 0.03f;
    [SerializeField, Min(0f)] private float maxUpdateLagSec = 0.10f;

    [Header("Color")]
    [SerializeField] private Color fillColor = new Color(0.34f, 1f, 1f, 0.95f);
    [SerializeField] private Color regenFillColor = new Color(0.55f, 0.8f, 1f, 0.95f);
    [SerializeField] private Color warningFillColor = new Color(1f, 0.35f, 0.25f, 0.95f);
    [SerializeField, Min(0f)] private float regenForceThresholdN = 1f;

    private const string FillObjectName = "CurrentGraph_Fill";
    private const string ValueObjectName = "CurrentGraph_Value";
    private const string UnitObjectName = "CurrentGraph_Unit";
    private const string WarningLampObjectName = "CurrentGraph_WarningLamp";

    private float latestCurrentA = 0f;
    private float sampledCurrentA = 0f;
    private float displayedCurrentA = 0f;
    private float nextSampleTime = 0f;
    private bool isRegenerating = false;

    private void Reset()
    {
        graphRoot = transform as RectTransform;
    }

    private void OnEnable()
    {
        ResolveReferences();
        latestCurrentA = 0f;
        sampledCurrentA = 0f;
        displayedCurrentA = 0f;
        nextSampleTime = Time.time;
        isRegenerating = false;
        ScheduleNextSampleTime();
        UpdateVisuals(0f, false);
    }

    private void Update()
    {
        ResolveReferences();

        float targetCurrentA = ReadCurrentA();
        UpdateSampledCurrent(targetCurrentA);

        float minCurrent = Mathf.Min(minCurrentA, maxCurrentA);
        float maxCurrent = Mathf.Max(minCurrentA, maxCurrentA);
        isRegenerating = IsRegenerating(sampledCurrentA);
        float displayCurrentA = useAbsoluteCurrent ? Mathf.Abs(sampledCurrentA) : sampledCurrentA;
        float clampedTargetCurrentA = Mathf.Clamp(displayCurrentA, minCurrent, maxCurrent);
        displayedCurrentA = clampedTargetCurrentA;

        UpdateVisuals(displayedCurrentA, isRegenerating);
    }

    private void ResolveReferences()
    {
        train = CabReferenceResolver.ResolveTrain(this, train);

        if (graphRoot == null)
        {
            graphRoot = transform as RectTransform;
        }

        if (graphRoot == null)
        {
            return;
        }

        if (currentFill == null)
        {
            currentFill = FindChildComponent<Image>(graphRoot, FillObjectName);
        }

        if (currentText == null)
        {
            currentText = FindChildComponent<TMP_Text>(graphRoot, ValueObjectName);
        }

        if (unitText == null)
        {
            unitText = FindChildComponent<TMP_Text>(graphRoot, UnitObjectName);
        }

        if (overloadLamp == null)
        {
            overloadLamp = FindChildComponent<Image>(graphRoot, WarningLampObjectName);
        }
    }

    private float ReadCurrentA()
    {
        if (train == null)
        {
            return 0f;
        }

        if (useAverageVvvfUnitCurrent && TryReadAverageVvvfUnitCurrentA(out float averageVvvfUnitCurrentA))
        {
            return averageVvvfUnitCurrentA;
        }

        if (!fallbackToCarTractionCurrent)
        {
            return 0f;
        }

        IReadOnlyList<CarTractionState> states = train.CurrentCarTractionStates;
        if (states == null || states.Count == 0)
        {
            return 0f;
        }

        if (useTotalMotorCurrent)
        {
            float totalCurrentA = 0f;
            for (int i = 0; i < states.Count; i++)
            {
                CarTractionState state = states[i];
                if (state != null)
                {
                    totalCurrentA += GetSignedCarCurrentA(state);
                }
            }

            return totalCurrentA;
        }

        if (carIndex < 0 || carIndex >= states.Count || states[carIndex] == null)
        {
            return 0f;
        }

        return GetSignedCarCurrentA(states[carIndex]);
    }

    private bool TryReadAverageVvvfUnitCurrentA(out float averageCurrentA)
    {
        averageCurrentA = 0f;

        VVVFController[] vvvfControllers = train != null ? train.VVVFControllers : null;
        if (vvvfControllers == null || vvvfControllers.Length == 0)
        {
            return false;
        }

        float totalUnitCurrentA = 0f;
        int activeUnitCount = 0;
        for (int i = 0; i < vvvfControllers.Length; i++)
        {
            VVVFController vvvf = vvvfControllers[i];
            if (vvvf == null || vvvf.MotorCount <= 0)
            {
                continue;
            }

            MotorModel[] motors = vvvf.Motors;
            float unitCurrentA = 0f;
            if (motors != null)
            {
                for (int j = 0; j < motors.Length; j++)
                {
                    MotorModel motor = motors[j];
                    if (motor != null)
                    {
                        unitCurrentA += GetSignedMotorCurrentA(motor);
                    }
                }
            }

            totalUnitCurrentA += unitCurrentA;
            activeUnitCount++;
        }

        if (activeUnitCount <= 0)
        {
            return false;
        }

        averageCurrentA = totalUnitCurrentA / activeUnitCount;
        return true;
    }

    private float GetSignedMotorCurrentA(MotorModel motor)
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

    private bool IsRegenerating(float signedCurrentA)
    {
        float thresholdA = Mathf.Max(0f, currentDirectionThresholdA);
        if (signedCurrentA < -thresholdA)
        {
            return true;
        }

        if (signedCurrentA > thresholdA)
        {
            return false;
        }

        return train != null && train.CurrentRegenBrakeForceN > regenForceThresholdN;
    }

    private void UpdateSampledCurrent(float currentA)
    {
        latestCurrentA = currentA;

        if (!enableRandomLag)
        {
            sampledCurrentA = latestCurrentA;
            nextSampleTime = Time.time;
            return;
        }

        if (Time.time < nextSampleTime)
        {
            return;
        }

        sampledCurrentA = latestCurrentA;
        ScheduleNextSampleTime();
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

    private void UpdateVisuals(float currentA, bool regenActive)
    {
        float minCurrent = Mathf.Min(minCurrentA, maxCurrentA);
        float maxCurrent = Mathf.Max(minCurrentA, maxCurrentA);
        float step = Mathf.Max(0.1f, displayStepA);
        float quantizedCurrentA = Mathf.Round(currentA / step) * step;
        float ratio = Mathf.InverseLerp(minCurrent, maxCurrent, quantizedCurrentA);
        bool isWarning = currentA >= warningCurrentA;

        if (currentFill != null)
        {
            RectTransform fillRect = currentFill.rectTransform;
            RectTransform parentRect = fillRect.parent as RectTransform;
            float parentHeight = parentRect != null ? parentRect.rect.height : fillRect.sizeDelta.y;
            float fillHeight = Mathf.Max(currentA > minCurrent + 0.5f ? minimumFillHeight : 0f, parentHeight * Mathf.Clamp01(ratio));

            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 0f);
            fillRect.pivot = new Vector2(0.5f, 0f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(0f, fillHeight);
            currentFill.color = isWarning ? warningFillColor : regenActive ? regenFillColor : fillColor;
            currentFill.enabled = fillHeight > 0f;
        }

        if (currentText != null)
        {
            currentText.text = $"{Mathf.RoundToInt(Mathf.Max(0f, quantizedCurrentA)):0}";
            currentText.alignment = TextAlignmentOptions.Right;
        }

        if (unitText != null)
        {
            unitText.text = "A";
        }

        if (overloadLamp != null)
        {
            overloadLamp.enabled = isWarning;
        }
    }

    private static T FindChildComponent<T>(Transform root, string objectName) where T : Component
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == objectName && child.TryGetComponent(out T component))
            {
                return component;
            }

            T nested = FindChildComponent<T>(child, objectName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
