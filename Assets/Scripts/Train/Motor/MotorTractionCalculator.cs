using UnityEngine;

public static class MotorTractionCalculator
{
    public static float GetWheelTorqueNm(
        float motorTorqueNm,
        float gearRatio,
        float drivelineEfficiency
    )
    {
        return motorTorqueNm * gearRatio * drivelineEfficiency;
    }

    public static float GetTractionForceN(
        float motorTorqueNm,
        float gearRatio,
        float wheelRadiusM,
        float drivelineEfficiency
    )
    {
        float wheelTorqueNm = GetWheelTorqueNm(
            motorTorqueNm,
            gearRatio,
            drivelineEfficiency
        );

        return wheelTorqueNm / Mathf.Max(0.01f, wheelRadiusM);
    }

    public static float GetTotalTractionForceN(
        MotorModel[] motors,
        TrainSpec trainSpec
    )
    {
        if (motors == null || trainSpec == null)
        {
            return 0f;
        }

        float totalForceN = 0f;

        for (int i = 0; i < motors.Length; i++)
        {
            MotorModel motor = motors[i];
            if (motor == null)
            {
                continue;
            }

            totalForceN += GetTractionForceN(
                motor.MotorTorqueNm,
                trainSpec.gearRatio,
                trainSpec.wheelRadiusM,
                trainSpec.drivelineEfficiency
            );
        }

        return totalForceN;
    }
}
