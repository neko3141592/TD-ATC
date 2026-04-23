using UnityEngine;

[System.Serializable]
public class CarTractionState
{
    [Tooltip("現在の力行力[N]")]
    public float tractionForceN = 0f;

    /// <summary>
    /// 役割: Reset の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    public void Reset()
    {
        tractionForceN = 0f;
    }
}
