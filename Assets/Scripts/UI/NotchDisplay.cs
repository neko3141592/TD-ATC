using TMPro;
using UnityEngine;

public class NotchDisplay : MonoBehaviour
{

    [SerializeField] private TrainController train;
    [SerializeField] private NotchManager notchManager;
    [SerializeField] private TMP_Text notchText;

    [Header("Shadow")]
    [SerializeField] private bool enableShadow = true;
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Vector2 shadowDistance = new Vector2(2f, -2f);
    [SerializeField] private bool shadowUseGraphicAlpha = true;

    void Awake()
    {
        ResolveReferences();
        ApplyShadowSettings();
    }
    void Update ()
    {
        ResolveReferences();

        if (notchText == null)
        {
            return;
        }

        ApplyShadowSettings();

        if (train == null)
        {
            notchText.text = "--";
            return;
        }

        if (TryGetTascDisplayStep(out int tascBrakeStep))
        {
            notchText.text = $"TASC-B{tascBrakeStep}";
            return;
        }

        if (train.BrakeNotch == train.EmergencyBrakeNotch) notchText.text = $"非常";  
        else if (train.PowerNotch > 0) notchText.text = $"P{train.PowerNotch}";
        else if (train.BrakeNotch > 0) notchText.text = $"B{train.BrakeNotch}"; 
        else notchText.text = "OFF";
    }

    private void ResolveReferences()
    {
        train = CabReferenceResolver.ResolveTrain(this, train);

        if (notchManager == null)
        {
            notchManager = CabReferenceResolver.ResolveTrainComponent(this, train, notchManager);
        }

        if (notchText == null)
        {
            notchText = GetComponent<TMP_Text>();
        }
    }

    private bool TryGetTascDisplayStep(out int tascBrakeStep)
    {
        if (notchManager != null && notchManager.IsTASCBrakeSelected && notchManager.TASCBrakeStep > 0)
        {
            tascBrakeStep = notchManager.TASCBrakeStep;
            return true;
        }

        tascBrakeStep = 0;
        return false;
    }

    private void ApplyShadowSettings()
    {
        UIShadowUtility.ApplyShadow(notchText, enableShadow, shadowColor, shadowDistance, shadowUseGraphicAlpha);
    }

    private void OnValidate()
    {
        ResolveReferences();
        ApplyShadowSettings();
    }
}
