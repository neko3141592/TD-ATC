using UnityEngine;

public class EBController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NotchManager notchManager;
    [SerializeField] private TrainController train;

    [Header("Judgement")]
    [SerializeField, Min(0f)] private float criterionTimeS = 60f;
    [SerializeField, Min(0f)] private float resetLimitS = 5f;
    [SerializeField, Min(0f)] private float driveThresholdKmH = 15f;


    [Header("Runtime Status")]
    [SerializeField] private bool isNoOperation = false;
    [SerializeField] private bool isEBHolding = false;
    [SerializeField, Min(0f)] private float noOperationTimeS = 0f;
    [SerializeField, Min(0f)] private float noResetTimeS = 0f;

    private int previousPowerNotch;
    private int previousBrakeNotch;
    private bool hasPreviousNotchState = false;

    public bool IsNoOperation => isNoOperation;
    public bool IsEBHolding => isEBHolding;
    public float NoOperationTimeS => noOperationTimeS;

    public bool IsActive => isNoOperation || isEBHolding;

    void Awake()
    {
        ResolveReferences();
        CacheCurrentNotchState();
    }

    void Update()
    {
        ResolveReferences();

        if (notchManager == null)
        {
            ResetJudgement();
            return;
        }

        if (HasDriverOperation())
        {
            ResetNoOperationTimer();
            CacheCurrentNotchState();
            return;
        }

        noOperationTimeS += Time.deltaTime;
        isNoOperation = noOperationTimeS >= criterionTimeS;
        ActivateEBHolding();
    }

    public void ResetEBHolding()
    {
        isEBHolding = false;
        ResetNoOperationTimer();
        CacheCurrentNotchState();
    }

    private void ResolveReferences()
    {
        if (notchManager == null)
        {
            notchManager = GetComponent<NotchManager>();
        }

        if (notchManager == null)
        {
            notchManager = GetComponentInParent<NotchManager>();
        }

        if (train == null)
        {
            train = GetComponentInParent<TrainController>();
        }
    }

    private bool HasDriverOperation()
    {
        if (train.SpeedKmH < driveThresholdKmH)
        {
            return true;
        }
        if (!hasPreviousNotchState)
        {
            return false;
        }

        return notchManager.ManualPowerNotch != previousPowerNotch ||
               notchManager.ManualBrakeNotch != previousBrakeNotch;
    }

    private void CacheCurrentNotchState()
    {
        if (notchManager == null)
        {
            hasPreviousNotchState = false;
            previousPowerNotch = 0;
            previousBrakeNotch = 0;
            return;
        }

        previousPowerNotch = notchManager.ManualPowerNotch;
        previousBrakeNotch = notchManager.ManualBrakeNotch;
        hasPreviousNotchState = true;
    }

    private void ResetNoOperationTimer()
    {
        noOperationTimeS = 0f;
        isNoOperation = false;
    }

    private void ResetJudgement()
    {
        isEBHolding = false;
        ResetNoOperationTimer();
        CacheCurrentNotchState();
    }

    private void ActivateEBHolding()
    {
        if (!isNoOperation)
        {
            noResetTimeS = 0;
            return;
        }

        noResetTimeS += Time.deltaTime;

        if (noResetTimeS >= resetLimitS)
        {
            isEBHolding = true;
        }
    }
}
