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
    public string nodeId; // ノードのID
    public TrackNodeType trackNodeType = TrackNodeType.Normal; // ノードの種類
    public string junctionId; // 分岐器のID
    public Vector3 worldPosition;
    public Quaternion worldRotation = Quaternion.identity; // 【追加】ノードの向いている角度
    public List<string> outgoingEdgeIds = new(); // 進むことが可能な線路
}