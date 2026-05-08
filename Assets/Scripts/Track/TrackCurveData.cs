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
public class TrackHorizontalSegment
{
    public float startDistanceM;
    public float lengthM = 100f;
    public TrackCurveType trackCurveType;
    public float radiusM = 500f;
}

[Serializable]
public class TrackVerticalSegment
{
    public float startDistanceM;
    public float lengthM = 100f;
    public float startGradientPermille;
    public float endGradientPermille;
}

[Serializable]
public class TrackCantSegment
{
    public float startDistanceM;
    public float lengthM = 100f;
    public float startCantMm;
    public float endCantMm;
}
