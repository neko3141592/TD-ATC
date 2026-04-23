using UnityEngine;
using System;

public enum TrackCurveType
{
    Straight,
    Curve,
    TransitionIn,  // 直線から円曲線へつなぐ緩和曲線です。
    TransitionOut  // 円曲線から直線へ戻す緩和曲線です。
}

[Serializable]
public class TrackCurveData
{
    public TrackCurveType trackCurveType;
    
    [Min(0f)] 
    public float gradientPermille = 0f;
    public float lengthM = 100f; // 区間長[m]です。
    
    public float radiusM = 500f; // 半径[m]です。正なら右曲線、負なら左曲線です。
}
