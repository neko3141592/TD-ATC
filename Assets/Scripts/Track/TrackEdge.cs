using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class TrackEdge
{
    public string edgeId; // Unique edge identifier.
    public string fromNodeId; // Start node ID.
    public string toNodeId; // End node ID.

    // Core movement data for this edge.
    [Min(0f)] public float lengthM;
    [Min(0f)] public float speedLimitMS = 33.33f;

    [Header("Rail Data")]
    public List<TrackCurveData> mathCurves = new List<TrackCurveData>();
}
