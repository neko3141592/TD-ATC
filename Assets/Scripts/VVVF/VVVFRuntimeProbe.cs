using System;
using UnityEngine;

public class VVVFRuntimeProbe : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainController train;

    [Header("Three Phase Debug")]
    [SerializeField, Min(0f)] private float frequencyHz = 1f;
    [SerializeField, Min(0f)] private float phaseVoltagePeakV = 1f;

    public float FrequencyHz => frequencyHz;
    public float PhaseRad => phaseRad;

    public float WheelRpm { get; private set; }
    public float MotorRpm { get; private set; }

    public float UPhaseV { get; private set; }
    public float VPhaseV { get; private set; }
    public float WPhaseV { get; private set; }

    public float UVLineV { get; private set; }
    public float VWLineV { get; private set; }
    public float WULineV { get; private set; }

    public float PhaseSumV => UPhaseV + VPhaseV + WPhaseV;

    private float phaseRad;



    private void Update()
    {
        if (train == null || train.Spec == null)
        {
            ResetValues();
            return;
        }

        TrainSpec spec = train.Spec;

        WheelRpm = VVVFMath.GetWheelRpm(train.SpeedMS, spec.wheelRadiusM);

        phaseRad += 2f * Mathf.PI * FrequencyHz * Time.deltaTime;
        phaseRad = Mathf.Repeat(phaseRad, 2f * Mathf.PI);

        float u = Mathf.Sin(phaseRad);
        float v = Mathf.Sin(phaseRad + 2f * Mathf.PI / 3f);
        float w = Mathf.Sin(phaseRad - 2f * Mathf.PI / 3f);

        UPhaseV = phaseVoltagePeakV * u;
        VPhaseV = phaseVoltagePeakV * v;
        WPhaseV = phaseVoltagePeakV * w;

        UVLineV = UPhaseV - VPhaseV;
        VWLineV = VPhaseV - WPhaseV;
        WULineV = WPhaseV - UPhaseV;

    }

    private void ResetValues()
    {
        WheelRpm = 0f;
        MotorRpm = 0f;
        UPhaseV = 0f;
        VPhaseV = 0f;
        WPhaseV = 0f;
        UVLineV = 0f;
        VWLineV = 0f;
        WULineV = 0f;
    }
    
}