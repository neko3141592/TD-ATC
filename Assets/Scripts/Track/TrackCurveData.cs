using UnityEngine;
using System;

public enum TrackCurveType
{
    Straight,
    Curve,
    TransitionIn,  // 直線 → 円曲線
    TransitionOut  // 円曲線 → 直線
}

[Serializable]
public class TrackCurveData
{
    public TrackCurveType trackCurveType;
    
    [Min(0f)] 
    public float gradientPermille = 0f;
    public float lengthM = 100f; // カーブ区間の長さ(m)
    
    public float radiusM = 500f; // 半径(m)。右カーブはプラス、左カーブはマイナス
}