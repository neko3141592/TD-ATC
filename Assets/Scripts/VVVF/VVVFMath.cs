using UnityEngine;

public class VVVFMath : MonoBehaviour
{
    public static float GetWheelRpm(float speedMS, float wheelRadiusM)
    {
        float safeWheelRadiusM = Mathf.Max(0.01f, wheelRadiusM);
        float wheelAngularSpeedRadS = speedMS / safeWheelRadiusM;
        return wheelAngularSpeedRadS * 60f / (2f * Mathf.PI);
    }

    public static float GetMotorRpm(float wheelRpm, float gearRatio)
    {
        return wheelRpm * gearRatio;
    }
}
