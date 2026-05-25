using TMPro;
using UnityEngine;

public class VVVFDebugHUD : MonoBehaviour
{
    [SerializeField] private VVVFController controller;
    [SerializeField] private TMP_Text targetText;

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponentInParent<VVVFController>();
        }

        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }
    }

    private void Update()
    {
        if (targetText == null)
        {
            return;
        }

        if (controller != null)
        {
            UpdateControllerText();
            return;
        }

        targetText.text = "[VVVF]\nController: Not Assigned";
    }

    private void UpdateControllerText()
    {
        MotorModel motor = controller.PrimaryMotor;

        targetText.text =
            "[VVVF]\n" +
            $"Freq: {controller.FrequencyHz:0.0} Hz\n" +
            $"Phase: {controller.PhaseRad:0.00} rad\n" +
            $"Wheel RPM: {controller.WheelRpm:0.0}\n" +
            $"Motor RPM: {controller.MotorRpm:0.0}\n" +
            $"Pole: {controller.PoleCount}\n" +
            $"Sync RPM: {controller.SyncRpm:0.0}\n" +
            $"Rotor Base F: {controller.RotorBaseFrequencyHz:0.0} Hz\n" +
            $"Slip F: {controller.SlipFrequencyHz:0.0} Hz\n" +
            $"Slip: {controller.SlipRatio * 100f:+0.0;-0.0;0.0}%\n" +
            $"V/f: {controller.VoltageRatio * 100f:0.0}%\n" +
            $"Line V RMS: {controller.LineVoltageRmsV:0.0} V\n" +
            $"Phase V Peak: {controller.PhaseVoltagePeakV:0.0} V\n" +
            $"Motor I: {(motor != null ? motor.MotorCurrentRmsA : 0f):0.0} A\n" +
            $"Rotor I: {(motor != null ? motor.RotorCurrentRmsA : 0f):0.0} A\n" +
            $"Target T: {controller.TargetMotorTorqueNm:0.0} Nm\n" +
            $"Motor T: {(motor != null ? motor.MotorTorqueNm : 0f):0.0} Nm\n" +
            $"Target Force: {controller.TargetTractionForceN / 1000f:0.0} kN\n" +
            $"Motor Force: {controller.TotalMotorTractionForceN / 1000f:0.0} kN\n" +
            $"P out: {(motor != null ? motor.MotorOutputPowerW / 1000f : 0f):0.0} kW\n" +
            $"S: {(motor != null ? motor.ApparentPowerVA / 1000f : 0f):0.0} kVA\n" +
            $"U/V/W: {controller.UPhaseV:+0.00;-0.00;0.00} / {controller.VPhaseV:+0.00;-0.00;0.00} / {controller.WPhaseV:+0.00;-0.00;0.00}\n" +
            $"U+V+W: {controller.PhaseSumV:+0.000;-0.000;0.000}";
    }
}
