using UnityEngine;

[System.Serializable]
public class CarBrakeState
{
    [Tooltip("現在の回生ブレーキ力[N]")]
    public float regenForceN = 0f;

    [Tooltip("現在の空気ブレーキ力[N]")]
    public float airForceN = 0f;

    [Tooltip("現在のBC圧[kPa]")]
    public float bcPressureKPa = 0f;

    [Tooltip("現在の回生上限")]
    public float regenCapN = 0f;

    [Tooltip("現在の空制上限")]
    public float airCapN = 0f;

    [Tooltip("BC圧1kPaあたりの空気ブレーキ力[N/kPa]")]
    public float airForcePerKPa = 0f;

    [Tooltip("最大BC圧[kPa]")]
    public float maxBCPressureKPa = 0f;

    [Tooltip("ブレーキ操作継続中フラグ（回生失効ラッチ用）")]
    public bool regenBrakeApplicationActive = false;

    [Tooltip("現在のブレーキ操作で回生を使用できるか")]
    public bool regenLatchedForCurrentBrake = false;

    public void Reset()
    {
        regenForceN = 0f;
        airForceN = 0f;
        bcPressureKPa = 0f;
        airForcePerKPa = 0f;
        maxBCPressureKPa = 0f;
        regenBrakeApplicationActive = false;
        regenLatchedForCurrentBrake = false;
    }
}
