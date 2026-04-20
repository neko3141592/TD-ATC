using System;
using UnityEngine;

[Serializable]
public class StationData
{
    public string stationId;
    public string stationName;
    public string edgeId;
    public float distanceFromEdgeStart; // Distance from the edge start, in meters.
    public float stopMarginM = 5f;      // Allowed stopping error around the target point, in meters.
    
    // Additional references such as station models or markers can be attached here later.
}
