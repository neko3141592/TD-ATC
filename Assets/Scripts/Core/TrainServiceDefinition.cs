using System;
using UnityEngine;
using System.Collections.Generic;

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
    public string stationId;
    public bool stopHere = true;
    public float dwellSeconds = 30f;
}

[CreateAssetMenu(fileName = "TrainServiceDefinition", menuName = "Train/Service Definition")]
public class TrainServiceDefinition : ScriptableObject
{
    public string serviceName;
    public ServiceType serviceType;
    public List<ServiceStop> stops = new List<ServiceStop>();
}