using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BlockSection
{
    public string blockId;
    [Min(0f)] public float startDistanceM;
    [Min(0f)] public float endDistanceM;
}


[Serializable]
public class TrackEdge
{

    public string edgeId; // エッジを一意に識別する ID です。
    public string physicalId;
    public string nodeAId; // 片側ノード ID です。
    public string nodeBId; // もう片側のノード ID です。
    public string geometryId; //　エッジのジオメトリID

    public List<BlockSection> blockSections = new();

    // このエッジ上を走行するための基本データです。
    [Min(0f)] public float lengthM;
    [Min(0f)] public float speedLimitMS = 33.33f;
    [Min(0.001f)] public float gaugeM = 1.067f;


    // 旧方式の走行セグメント
    [Header("Rail Data")]
    [HideInInspector] public List<TrackHorizontalSegment> horizontalSegments = new List<TrackHorizontalSegment>();
    [HideInInspector] public List<TrackVerticalSegment> verticalSegments = new List<TrackVerticalSegment>();
    [HideInInspector] public List<TrackCantSegment> cantSegments = new List<TrackCantSegment>();
}
