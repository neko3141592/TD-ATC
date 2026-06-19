using TMPro;
using UnityEngine;

public class SimpleTrainStatusHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainController train;
    [SerializeField] private ATCController atc;
    [SerializeField] private TMP_Text reverserText;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text gradientText;
    [SerializeField] private TMP_Text notchText;
    [SerializeField] private TMP_Text atcText;
    [SerializeField] private TMP_Text patternApproachText;
    [SerializeField] private TMP_Text orpText;
    [SerializeField] private TMP_Text atcBrakeText;

    [Header("Notch Colors")]
    [SerializeField] private Color powerNotchColor = new Color(0.2f, 0.8f, 1f);
    [SerializeField] private Color brakeNotchColor = new Color(1f, 0.75f, 0.2f);
    [SerializeField] private Color emergencyNotchColor = new Color(1f, 0.2f, 0.2f);
    [SerializeField] private Color neutralNotchColor = Color.white;

    [Header("Indicator Colors")]
    [SerializeField] private Color indicatorOnColor = Color.white;
    [SerializeField] private Color indicatorOffColor = new Color(0.35f, 0.35f, 0.35f);

    [Header("ATC Colors")]
    [SerializeField] private Color atcGreenColor = Color.white;
    [SerializeField] private Color atcRedColor = Color.white;

    private void Reset()
    {
        train = FindAnyObjectByType<TrainController>();
        atc = FindAnyObjectByType<ATCController>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();

        if (train == null)
        {
            SetText(reverserText, "--");
            SetText(speedText, "--.- km/h");
            SetText(gradientText, "--.-‰");
            SetNotchText("--", neutralNotchColor);
            UpdateAtcDisplay();
            return;
        }

        SetText(reverserText, FormatReverser(train.Reverser));
        SetText(speedText, $"{train.SpeedKmH:0.0}");
        SetText(gradientText, $"{train.CurrentGradientPermille:+0.0;-0.0;0.0}");
        UpdateAtcDisplay();
        SetNotchText(
            FormatNotch(train.ManualPowerNotch, train.ManualBrakeNotch, train.EmergencyBrakeNotch),
            GetNotchColor(train.ManualPowerNotch, train.ManualBrakeNotch, train.EmergencyBrakeNotch)
        );
    }

    private void ResolveReferences()
    {
        if (train == null)
        {
            train = FindAnyObjectByType<TrainController>();
        }

        if (atc == null)
        {
            atc = FindAnyObjectByType<ATCController>();
        }
    }

    private static string FormatReverser(TrainController.ReverserPosition reverser)
    {
        return reverser switch
        {
            TrainController.ReverserPosition.Forward => "F",
            TrainController.ReverserPosition.Neutral => "N",
            TrainController.ReverserPosition.Reverse => "R",
            _ => "--"
        };
    }

    private static string FormatNotch(int powerNotch, int brakeNotch, int emergencyBrakeNotch)
    {
        if (brakeNotch >= emergencyBrakeNotch)
        {
            return "EB";
        }

        if (brakeNotch > 0)
        {
            return $"B{brakeNotch}";
        }

        if (powerNotch > 0)
        {
            return $"P{powerNotch}";
        }

        return "N";
    }

    private Color GetNotchColor(int powerNotch, int brakeNotch, int emergencyBrakeNotch)
    {
        if (brakeNotch >= emergencyBrakeNotch)
        {
            return emergencyNotchColor;
        }

        if (brakeNotch > 0)
        {
            return brakeNotchColor;
        }

        if (powerNotch > 0)
        {
            return powerNotchColor;
        }

        return neutralNotchColor;
    }

    private void SetNotchText(string value, Color color)
    {
        if (notchText == null)
        {
            return;
        }

        notchText.text = value;
        notchText.color = color;
    }

    private void UpdateAtcDisplay()
    {
        if (atc == null)
        {
            SetText(atcText, "--");
            SetIndicator(patternApproachText, false);
            SetIndicator(orpText, false);
            SetIndicator(atcBrakeText, false);
            return;
        }

        SetText(atcText, $"{Mathf.RoundToInt(atc.CurrentPatternAllowSpeedKmH)}");
        atcText.color = 
        atc.CurrentSignalAspect == ATCSignalAspect.Green ? atcGreenColor : atcRedColor;
            
        SetIndicator(patternApproachText, atc.IsPatternApproaching);
        SetIndicator(orpText, atc.CurrentAtcStateLabel == "ORP");
        SetIndicator(atcBrakeText, atc.IsAtcServiceBrakeActive || atc.IsAtcEmergencyBrakeActive || atc.IsAtcBrakeLatched);
    }

    private void SetIndicator(TMP_Text text, bool isOn)
    {
        if (text != null)
        {
            text.color = isOn ? indicatorOnColor : indicatorOffColor;
        }
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }
}
