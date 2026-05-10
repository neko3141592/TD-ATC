using UnityEngine;

public class VVVFRpmDebugProbe : MonoBehaviour
{
    [SerializeField] private TrainController train;
    [SerializeField, Min(0.05f)] private float logIntervalS = 0.5f;

    private float logTimer;

    private void Awake()
    {
        if (train == null)
        {
            train = GetComponent<TrainController>();
        }
    }

    private void Update()
    {
        if (train == null || train.Spec == null)
        {
            return;
        }

        TrainSpec spec = train.Spec;
        float wheelRpm = VVVFMath.GetWheelRpm(train.SpeedMS, spec.wheelRadiusM);
        float motorRpm = VVVFMath.GetMotorRpm(wheelRpm, spec.gearRatio);

        logTimer += Time.deltaTime;
        if (logTimer < logIntervalS)
        {
            return;
        }

        logTimer = 0f;
        Debug.Log(
            $"VVVF RPM | speed={train.SpeedKmH:F1} km/h, " +
            $"wheel={wheelRpm:F1} rpm, motor={motorRpm:F1} rpm, " +
            $"wheelRadius={spec.wheelRadiusM:F3} m, gearRatio={spec.gearRatio:F2}",
            this
        );
    }
}
