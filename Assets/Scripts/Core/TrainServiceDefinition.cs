using System;
using UnityEngine;
using System.Collections.Generic;

// ServiceType identifies the operating pattern of a train service.
public enum ServiceType
{
    Local,
    SemiExpress,
    Express,
    RapidExpress,
    Depot,
    TestRun,
}

[Serializable]
public class ServiceStop
{
    // stationId must match StationData.stationId in the active TrackGraph.
    public string stationId;
    // stopHere lets one service pass a station while another service stops there.
    public bool stopHere = true;
    // dwellSeconds is reserved for future door/dwell gameplay and does not drive runtime logic yet.
    public float dwellSeconds = 30f;
}

[CreateAssetMenu(fileName = "TrainServiceDefinition", menuName = "Train/Service Definition")]
public class TrainServiceDefinition : ScriptableObject
{
    // serviceName is a human-readable label for editor setup and future HUD/session screens.
    public string serviceName;
    public ServiceType serviceType;
    // stops is ordered in running direction and acts as the canonical stop/pass sequence.
    public List<ServiceStop> stops = new List<ServiceStop>();
}
