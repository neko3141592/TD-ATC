using UnityEngine;

[System.Serializable]
public class CarTractionState
{
    [Tooltip("現在の力行力[N]")]
    public float tractionForceN = 0f;

    public void Reset()
    {
        tractionForceN = 0f;
    }
}
