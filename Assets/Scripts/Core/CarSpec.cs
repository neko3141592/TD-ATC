using UnityEngine;

public enum CarRole
{
    Intermediate,
    Cab
}

public enum CarType
{
    Motor,
    Trailer
}

public enum CabEnd
{
    None,
    Front,
    Rear
}

[CreateAssetMenu(fileName = "CarSpec", menuName = "Train/Core/Car Spec")]
public class CarSpec : ScriptableObject
{
    [Header("Prefab")]
    public GameObject carPrefab;

    [Header("Role")]
    public CarRole carRole = CarRole.Intermediate;
    public CabEnd cabEnd = CabEnd.None;

    [Header("Identity")]
    public CarType carType = CarType.Trailer;

    [Header("Geometry")]
    [Min(1f)] public float lengthM = 20f;

    [Header("Mass")]
    [Min(1f)] public float massKg = 35000f;
    [Min(0f)] public int capacity = 150;

    [Header("Traction")]
    [Min(0)] public int motorCount = 0;
    public VVVFController vvvfPrefab;
    [Min(0)] public int vvvfUnitCount = 0;

    [Header("Regen Brake")]
    [Min(0f)] public float maxRegenDecelMS2 = 1.1f;

    [Header("Air Brake")]
    [Min(0f)] public float bcCylinderAreaM2 = 0.01f;
    [Min(1)] public int bcCylinderCount = 4;
    [Min(0f)] public float brakeLeverageRatio = 6.0f;
    [Range(0f, 1f)] public float brakeMechanicalEfficiency = 0.9f;
    [Min(0f)] public float bcMaxPressureKPa = 380f;

    /// <summary>
    /// 役割: OnValidate の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void OnValidate()
    {

        lengthM = Mathf.Max(1f, lengthM);
        massKg = Mathf.Max(1f, massKg);
        motorCount = Mathf.Max(0, motorCount);
        maxRegenDecelMS2 = Mathf.Max(0f, maxRegenDecelMS2);
        bcCylinderAreaM2 = Mathf.Max(0f, bcCylinderAreaM2);
        bcCylinderCount = Mathf.Max(1, bcCylinderCount);
        brakeLeverageRatio = Mathf.Max(0f, brakeLeverageRatio);
        brakeMechanicalEfficiency = Mathf.Clamp01(brakeMechanicalEfficiency);
        bcMaxPressureKPa = Mathf.Max(0f, bcMaxPressureKPa);

        // T車は駆動モータなしをデフォルトに寄せる
        if (carType == CarType.Trailer)
        {
            motorCount = 0;
            vvvfUnitCount = 0;
        }
        else if (motorCount > 0 && vvvfPrefab != null && vvvfUnitCount <= 0)
        {
            vvvfUnitCount = 1;
        }

        vvvfUnitCount = Mathf.Max(0, vvvfUnitCount);
        if (carRole != CarRole.Cab)
        {
            cabEnd = CabEnd.None;
        }
        else if (cabEnd == CabEnd.None)
        {
            cabEnd = CabEnd.Front;
        }
    }
}
