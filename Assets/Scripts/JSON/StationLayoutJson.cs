using System;
using System.Collections.Generic;

[Serializable]
public class StationLayoutJson
{
    public string version;
    public string name;
    public List<StationJson> stations;
}

[Serializable]
public class StationJson
{
    public string id;
    public string name;
    public string type;
    public List<StationTrackJson> stationTracks;
    public List<StationStopJson> stops;
}

[Serializable]
public class StationTrackJson
{
    public string id;
    public string name;
    public string type;
    public List<string> servedEdges;
}

[Serializable]
public class StationStopJson
{
    public string id;
    public string name;
    public string edgeId;
    public List<StationStopDistanceJson> distances;
}

[Serializable]
public class StationStopDistanceJson
{
    public int consistLength;
    public float distanceOnEdgeM;
}
