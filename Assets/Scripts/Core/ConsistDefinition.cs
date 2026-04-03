using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ConsistDefinition", menuName = "Train/Consist Definition")]
public class ConsistDefinition : ScriptableObject
{
    [Tooltip("編成順（先頭 -> 最後尾）でCarSpecを並べる")]
    public List<CarSpec> cars = new List<CarSpec>();

    public int CarCount => cars != null ? cars.Count : 0;

    public float GetTotalMassKg()
    {
        if (cars == null)
        {
            return 0f;
        }

        float totalMass = 0f;
        for (int i = 0; i < cars.Count; i++)
        {
            if (cars[i] != null)
            {
                totalMass += Mathf.Max(0f, cars[i].massKg);
            }
        }

        return totalMass;
    }

    public int GetTotalMotorCount()
    {
        if (cars == null)
        {
            return 0;
        }

        int totalMotors = 0;
        for (int i = 0; i < cars.Count; i++)
        {
            if (cars[i] != null)
            {
                totalMotors += Mathf.Max(0, cars[i].motorCount);
            }
        }

        return totalMotors;
    }
}
