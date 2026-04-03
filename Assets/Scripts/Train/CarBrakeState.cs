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

    [Tooltip("ブレーキ操作継続中フラグ（回生失効ラッチ用）")]
    public bool regenBrakeApplicationActive = false;

    [Tooltip("現在のブレーキ操作で回生を使用できるか")]
    public bool regenLatchedForCurrentBrake = false;

    [Tooltip("回生揺らぎノイズのシード値")]
    public float regenNoiseSeed = 0f;

    [Tooltip("回生揺らぎノイズの経過時間")]
    public float regenNoiseTime = 0f;

    public void Reset()
    {
        regenForceN = 0f;
        airForceN = 0f;
        bcPressureKPa = 0f;
        regenBrakeApplicationActive = false;
        regenLatchedForCurrentBrake = false;
        regenNoiseTime = 0f;
    }
}
