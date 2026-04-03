using System;

[Serializable]
public class SpeedBlock
{
    public float startDistanceM; // ブロックの開始位置 (m)
    public float endDistanceM;   // ブロックの終了位置 (m)
    public float limitSpeedKmH;   // ブロック内の速度制限 (km/h)

    public bool Contains(float distanceM)
    {
        float start = Math.Min(startDistanceM, endDistanceM);
        float end = Math.Max(startDistanceM, endDistanceM);
        return distanceM >= start && distanceM < end;
    }
}
