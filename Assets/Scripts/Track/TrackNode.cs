using System;
using System.Collections.Generic;
using UnityEngine;

public enum TrackNodeType
{
    Normal, 
    Junction,
    Boundary,
    Station
}

[Serializable]
public class TrackNode
{
    public string nodeId; // Unique node identifier.
    public TrackNodeType trackNodeType = TrackNodeType.Normal; // Node role in the route graph.
    public string junctionId; // Turnout/junction identifier when this node behaves as a branch.
    public Vector3 worldPosition;
    public Quaternion worldRotation = Quaternion.identity; // World-facing direction used as the local forward basis.
    public List<string> outgoingEdgeIds = new(); // Edges that can be taken from this node.
}
