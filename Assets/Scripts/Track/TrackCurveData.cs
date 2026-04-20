using UnityEngine;
using System;

public enum TrackCurveType
{
    Straight,
    Curve,
    TransitionIn,  // Straight to circular curve.
    TransitionOut  // Circular curve back to straight.
}

[Serializable]
public class TrackCurveData
{
    public TrackCurveType trackCurveType;
    
    [Min(0f)] 
    public float gradientPermille = 0f;
    public float lengthM = 100f; // Segment length in meters.
    
    public float radiusM = 500f; // Radius in meters. Positive turns right, negative turns left.
}
