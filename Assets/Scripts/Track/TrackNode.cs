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
    public string nodeId; // ノードを一意に識別する ID です。
    public TrackNodeType trackNodeType = TrackNodeType.Normal; // 路線グラフ上でのノード種別です。
    public string junctionId; // 分岐ノードとして扱うときの転てつ識別子です。
    public Vector3 worldPosition;
    public Quaternion worldRotation = Quaternion.identity; // ローカル前方の基準になるワールド向きです。
    public List<string> outgoingEdgeIds = new(); // このノードから進める出線エッジ一覧です。
}
