using UnityEngine;

[CreateAssetMenu(fileName = "VVVFSpec", menuName = "Train/VVVF/VVVF Spec")]

public class VVVFSpec : ScriptableObject
{
    [Header("Control")]
    [Min(0f)] public float launchFrequency = 0.5f;
    [Range(0f, 0.5f)] public float launchVoltageBoostRatio = 0.12f;
    [Min(0f)] public float slipFrequencyControlRateHzPerSec = 0.4f;
    [Min(0f)] public float voltageControlRateRatioPerSec = 0.4f;
    [Min(0f)] public float torqueDeadbandNm = 5;
    [Min(0f)] public float maxSlipFrequencyHz = 8;

}