using UnityEngine;

[System.Serializable]
public class CarBrakeForceCommand
{
    [Tooltip("目標ブレーキ力[N]")]
    public float targetBrakeForceN = 0f;

    [Tooltip("目標回生ブレーキ力[N]")]
    public float targetRegenForceN = 0f;

    [Tooltip("目標空気ブレーキ力[N]")]
    public float targetAirForceN = 0f;

    [Tooltip("非常ブレーキ指令")]
    public bool isEmergency = false;

    public void Reset()
    {
        targetBrakeForceN = 0f;
        targetRegenForceN = 0f;
        targetAirForceN = 0f;
        isEmergency = false;
    }
}
