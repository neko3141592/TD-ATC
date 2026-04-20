using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ConsistDefinition", menuName = "Train/Consist Definition")]
public class ConsistDefinition : ScriptableObject
{
    [Tooltip("編成順（先頭 -> 最後尾）でCarSpecを並べる")]
    public List<CarSpec> cars = new List<CarSpec>();

    public bool HasCars => cars != null && cars.Count > 0;
    public int CarCount => cars != null ? cars.Count : 0;
    public bool TryGetCar(int index, out CarSpec car)
    {
        if (!HasCars || index < 0 || index >= cars.Count)
        {
            car = null;
            return false;
        }

        car = cars[index];
        return car != null;
    }

    public CarSpec GetCar(int index)
    {
        TryGetCar(index, out CarSpec car);
        return car;
    }

    public bool TryGetCarType(int index, out CarType carType)
    {
        if (!TryGetCar(index, out CarSpec car))
        {
            carType = CarType.Trailer;
            return false;
        }

        carType = car.carType;
        return true;
    }

    private float GetTotalMassKg()
    {
        if (!HasCars)
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

    public float GetTotalMassKgOrFallback(float fallbackMassKg)
    {
        float totalMassKg = GetTotalMassKg();
        if (totalMassKg > 0f)
        {
            return totalMassKg;
        }

        return Mathf.Max(1f, fallbackMassKg);
    }

    public int GetTotalMotorCount()
    {
        if (!HasCars)
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
