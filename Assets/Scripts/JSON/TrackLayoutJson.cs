using System;
using System.Collections.Generic;

[Serializable]
public class TrackLayoutJson
{
    public string version;
    public string trackName;
    public List<TrackGeometryJson> geometries;
    public List<TrackJson> tracks;
    public List<TrackConnectionJson> connections;
}

[Serializable]
public class TrackGeometryJson
{
    public string name;
    public TrackVector3Json origin;
    public float yawDeg;
    public List<TrackHorizontalSegmentJson> segments;
    public List<TrackVerticalSegmentJson> vertical;
    public List<TrackCantSegmentJson> cant;
}

[Serializable]
public class TrackJson
{
    public string name;
    public string baseCenterLineId;
    public List<TrackOffsetSegmentJson> offset;
    public float speedLimitKmH;
}

[Serializable]
public class TrackHorizontalSegmentJson
{
    public string type;
    public float lengthM;
    public float radiusM;
}

[Serializable]
public class TrackVerticalSegmentJson
{
    public float startM;
    public float lengthM;
    public float startPermille;
    public float endPermille;
}

[Serializable]
public class TrackCantSegmentJson
{
    public float startM;
    public float lengthM;
    public float startMm;
    public float endMm;
}

[Serializable]
public class TrackOffsetSegmentJson
{
    public string type;
    public float startBaseM;
    public float lengthM;
    public float offsetM;
    public float startOffsetM;
    public float endOffsetM;
}

[Serializable]
public class TrackConnectionJson
{
    public string name;
    public List<TrackPointJson> points;
}

[Serializable]
public class TrackPointJson
{
    public string trackName;
    public string end;
}

[Serializable]
public class TrackVector3Json
{
    public float x;
    public float y;
    public float z;
}
