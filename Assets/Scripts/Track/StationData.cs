using System;
using UnityEngine;

[Serializable]
public class StationData
{
    public string stationId;
    public string stationName;
    public string edgeId;
    public float distanceFromEdgeStart; // エッジの始点からの位置 (m)
    public float stopMarginM = 5f;      // 停止位置許容範囲 (m)
    
    // 3Dモデルなどの参照をここに追加可能
}
