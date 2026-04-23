using System;
using UnityEngine;

[Serializable]
public class StationData
{
    public string stationId;
    public string stationName;
    public string edgeId;
    public float distanceFromEdgeStart; // エッジ始点からの距離[m]です。
    public float stopMarginM = 5f;      // 停止目標位置まわりの許容誤差[m]です。
    
    // 将来的には駅モデルやマーカーなどの追加参照をここに持たせられます。
}
