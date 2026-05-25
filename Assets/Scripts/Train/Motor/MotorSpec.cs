using UnityEngine;

[CreateAssetMenu(fileName = "MotorSpec", menuName = "Train/Motor/Motor Spec")]
public class MotorSpec : ScriptableObject
{
    [Header("Rated Values")]
    [Min(0f)]
    public float ratedPowerW = 140000f;
    [Min(0f)]
    public float ratedLineVoltageV = 1050f;
    [Min(0f)]
    public float ratedCurrentA = 108f;
    [Min(0.01f)]
    public float ratedFrequencyHz = 80f;
    [Min(0.01f)]
    public float ratedRpm = 2380f;

    [Header("Motor Geometry")]
    [Min(2)]
    public int poleCount = 4;

    [Header("Characteristics")]
    [Range(0f, 1f)] public float efficiency = 0.945f;
    [Range(0f, 1f)] public float powerFactor = 0.755f;
    [Range(0f, 0.2f)] public float ratedSlipRatio = 0.007f;


    [Header("Equivalent Circuit at Rated Frequency")]
    [Min(0f)] public float statorResistanceOhm = 0.12f;      // R1
    [Min(0f)] public float statorReactanceOhm = 0.60f;       // X1
    [Min(0f)] public float magnetizingReactanceOhm = 10.0f;  // Xm
    [Min(0f)] public float rotorResistanceOhm = 0.045f;      // R2
    [Min(0f)] public float rotorReactanceOhm = 0.80f;        // X2
}
