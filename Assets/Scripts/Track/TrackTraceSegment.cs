using UnityEngine;

public struct TrackTraceSegment
{
    public string edgeId;
    public float startDistanceOnEdgeM;
    public float endDistanceOnEdgeM;
    public float startDistanceFromOriginM;
    public float endDistanceFromOriginM;

    public float LengthM => Mathf.Max(0f, endDistanceOnEdgeM - startDistanceOnEdgeM);
}