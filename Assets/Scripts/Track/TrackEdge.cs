using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class TrackEdge
{
    public string edgeId; // エッジを一意に識別する ID です。
    public string fromNodeId; // 始点ノード ID です。
    public string toNodeId; // 終点ノード ID です。
    public string blockId; // このエッジが属する閉塞 ID です。

    // このエッジ上を走行するための基本データです。
    [Min(0f)] public float lengthM;
    [Min(0f)] public float speedLimitMS = 33.33f;

    [Header("Rail Data")]
    public List<TrackCurveData> mathCurves = new List<TrackCurveData>();
}
