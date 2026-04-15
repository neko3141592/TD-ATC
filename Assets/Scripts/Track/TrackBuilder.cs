// Assets/Scripts/Track/TrackBuilder.cs
using System.Collections.Generic;
using UnityEngine;


public class TrackBuilder
{
    private readonly TrackGraph targetGraph;

    public Vector3 currentPos { get; private set; }
    public Quaternion currentRot { get; private set; }

    private TrackNode lastNode;
    private readonly List<TrackCurveData> currentCurves = new List<TrackCurveData>();
    private float currentEdgeLength = 0f;

    public TrackBuilder(TrackGraph graph)
    {
        targetGraph = graph;
    }
    public void Start(Vector3 startPos, Quaternion startRot)
    {
        currentPos = startPos;
        currentRot = startRot;
        lastNode = CreateNode(startPos, startRot);
    }
    
    public void StartFrom(TrackNode node)
    {
        currentPos = node.worldPosition;
        currentRot = node.worldRotation;
        lastNode = node;
        
        currentCurves.Clear();
        currentEdgeLength = 0f;
    }

    public void AddStraight(float lengthM)
    {
        AppendCurveSegment(TrackCurveType.Straight, lengthM, 0f);
        AdvanceCurrentPose(TrackCurveType.Straight, lengthM, 0f);
    }

    public void AddCurve(float lengthM, float radiusM)
    {
        AppendCurveSegment(TrackCurveType.Curve, lengthM, radiusM);
        AdvanceCurrentPose(TrackCurveType.Curve, lengthM, radiusM);
    }

    public void AddClothoidIn(float lengthM, float radiusM)
    {
        AppendCurveSegment(TrackCurveType.TransitionIn, lengthM, radiusM);
        AdvanceCurrentPose(TrackCurveType.TransitionIn, lengthM, radiusM);
    }

    public void AddClothoidOut(float lengthM, float radiusM)
    {
        AppendCurveSegment(TrackCurveType.TransitionOut, lengthM, radiusM);
        AdvanceCurrentPose(TrackCurveType.TransitionOut, lengthM, radiusM);
    }

    public void AddClothoidInOut(float lengthM, float radiusM)
    {
        AddClothoidIn(lengthM, radiusM);
        AddClothoidOut(lengthM, radiusM);
    }

    public TrackNode PutNode(string optionalNodeId = null, float speedLimitKmH = -1f)
    {
        TrackNode newNode = CreateNode(currentPos, currentRot, optionalNodeId);

        ConnectToNode(newNode, speedLimitKmH);

        return newNode;
    }

    public void ConnectToNode(TrackNode targetNode, float speedLimitKmH = -1f)
    {
        TrackEdge newEdge = new TrackEdge
        {
            edgeId = $"E{targetGraph.edges.Count + 1:000}",
            fromNodeId = lastNode.nodeId,
            toNodeId = targetNode.nodeId,
            lengthM = currentEdgeLength
        };
        if (speedLimitKmH > 0)
        {
            newEdge.speedLimitMS = speedLimitKmH / 3.6f;
        }

        newEdge.mathCurves.AddRange(currentCurves);

        targetGraph.edges.Add(newEdge);
        if (lastNode.outgoingEdgeIds == null) lastNode.outgoingEdgeIds = new List<string>();
        lastNode.outgoingEdgeIds.Add(newEdge.edgeId);

        currentCurves.Clear();
        currentEdgeLength = 0f;
        lastNode = targetNode;
    }

    public void AddStation(string stationId, string stationName, float offsetMFromNode = 10f)
    {
        // 直近のエッジ（現在ペン先が置かれているエッジ）の途中に駅を設置
        if (targetGraph.edges.Count == 0) return;
        
        var station = new StationData
        {
            stationId = stationId,
            stationName = stationName,
            edgeId = $"E{targetGraph.edges.Count:000}", // 最後に作成されたエッジ
            distanceFromEdgeStart = offsetMFromNode
        };
        targetGraph.stations.Add(station);
    }

    private TrackNode CreateNode(Vector3 pos, Quaternion rot, string id = null)
    {
        var node = new TrackNode
        {
            nodeId = string.IsNullOrEmpty(id) ? $"N{targetGraph.nodes.Count:000}" : id,
            worldPosition = pos,
            worldRotation = rot
        };
        targetGraph.nodes.Add(node);
        return node;
    }

    private void AppendCurveSegment(TrackCurveType type, float lengthM, float radiusM)
    {
        currentCurves.Add(new TrackCurveData
        {
            trackCurveType = type,
            lengthM = lengthM,
            radiusM = radiusM
        });
        currentEdgeLength += lengthM;
    }

    private void AdvanceCurrentPose(TrackCurveType type, float lengthM, float radiusM)
    {
        float localX;
        float localZ;
        float angleDegree;

        switch (type)
        {
            case TrackCurveType.Curve:
                TrackRuntimeResolver.CalculateCircularCurve(lengthM, radiusM, out localX, out localZ, out angleDegree);
                break;
            case TrackCurveType.TransitionIn:
                TrackRuntimeResolver.CalculateClothoidIn(lengthM, lengthM, radiusM, out localX, out localZ, out angleDegree);
                break;
            case TrackCurveType.TransitionOut:
                TrackRuntimeResolver.CalculateClothoidOut(lengthM, lengthM, radiusM, out localX, out localZ, out angleDegree);
                break;
            default:
                TrackRuntimeResolver.CalculateStraight(lengthM, out localX, out localZ, out angleDegree);
                break;
        }

        currentPos += currentRot * new Vector3(localX, 0f, localZ);
        currentRot *= Quaternion.Euler(0f, angleDegree, 0f);
    }
}
