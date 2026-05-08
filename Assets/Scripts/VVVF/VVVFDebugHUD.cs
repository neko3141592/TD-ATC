using TMPro;
using UnityEngine;

public class VVVFDebugHUD : MonoBehaviour
{
    [SerializeField] private VVVFRuntimeProbe probe;
    [SerializeField] private TMP_Text targetText;

    private void Awake()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }
    }

    private void Update()
    {
        if (probe == null || targetText == null)
        {
            return;
        }

        targetText.text =
            "[VVVF]\n" +
            $"Freq: {probe.FrequencyHz:0.0} Hz\n" +
            $"Phase: {probe.PhaseRad:0.00} rad\n" +
            $"Wheel RPM: {probe.WheelRpm:0.0}\n" +
            $"Motor RPM: {probe.MotorRpm:0.0}\n" +
            $"U/V/W: {probe.UPhaseV:+0.00;-0.00;0.00} / {probe.VPhaseV:+0.00;-0.00;0.00} / {probe.WPhaseV:+0.00;-0.00;0.00}\n" +
            $"U+V+W: {probe.PhaseSumV:+0.000;-0.000;0.000}\n" +
            $"UV/VW/WU: {probe.UVLineV:+0.00;-0.00;0.00} / {probe.VWLineV:+0.00;-0.00;0.00} / {probe.WULineV:+0.00;-0.00;0.00}";
    }
}
