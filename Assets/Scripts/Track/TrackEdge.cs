using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class TrackEdge
{
    public string edgeId; // エッジのID
    public string fromNodeId; // 始点
    public string toNodeId; // 終点

    // エッジ情報
    [Min(0f)] public float lengthM;
    public string blockId;
    [Min(0f)] public float speedLimitMS = 33.33f;

    [Header("Rail Data")]
    public List<TrackCurveData> mathCurves = new List<TrackCurveData>();
}
