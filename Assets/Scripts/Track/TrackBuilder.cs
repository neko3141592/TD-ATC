// Assets/Scripts/Track/TrackBuilder.cs
using System.Collections.Generic;
using UnityEngine;

public class TrackBuilder
{
    private TrackGraph targetGraph;

    public Vector3 currentPos { get; private set; }
    public Quaternion currentRot { get; private set; }

    private TrackNode lastNode;
    private List<TrackCurveData> currentCurves = new List<TrackCurveData>();
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
        currentCurves.Add(new TrackCurveData
        {
            trackCurveType = TrackCurveType.Straight,
            lengthM = lengthM,
            radiusM = 0f
        });
        currentEdgeLength += lengthM;
        currentPos += currentRot * Vector3.forward * lengthM;
    }

    public void AddCurve(float lengthM, float radiusM)
    {
        currentCurves.Add(new TrackCurveData
        {
            trackCurveType = TrackCurveType.Curve,
            lengthM = lengthM,
            radiusM = radiusM
        });
        currentEdgeLength += lengthM;
        
        TrackRuntimeResolver.CalculateCircularCurve(lengthM, radiusM, out float localX, out float localZ, out float angleDegree);
        currentPos += currentRot * new Vector3(localX, 0f, localZ);
        currentRot *= Quaternion.Euler(0f, angleDegree, 0f);
    }

    public void AddClothoidIn(float lengthM, float radiusM)
    {
        currentCurves.Add(new TrackCurveData
        {
            trackCurveType = TrackCurveType.TransitionIn,
            lengthM = lengthM,
            radiusM = radiusM
        });
        currentEdgeLength += lengthM;
        
        TrackRuntimeResolver.CalculateClothoidIn(lengthM, lengthM, radiusM, out float localX, out float localZ, out float angleDegree);
        currentPos += currentRot * new Vector3(localX, 0f, localZ);
        currentRot *= Quaternion.Euler(0f, angleDegree, 0f);
    }

    public void AddClothoidOut(float lengthM, float radiusM)
    {
        currentCurves.Add(new TrackCurveData
        {
            trackCurveType = TrackCurveType.TransitionOut,
            lengthM = lengthM,
            radiusM = radiusM
        });
        currentEdgeLength += lengthM;
        
        TrackRuntimeResolver.CalculateClothoidOut(lengthM, lengthM, radiusM, out float localX, out float localZ, out float angleDegree);
        currentPos += currentRot * new Vector3(localX, 0f, localZ);
        currentRot *= Quaternion.Euler(0f, angleDegree, 0f);
    }

    public void AddClothoidInOut(float lengthM, float radiusM)
    {
        AddClothoidIn(lengthM, radiusM);
        AddClothoidOut(lengthM, radiusM);
    }

    public TrackNode PutNode(string optionalNodeId = null, float speedLimitKmH = -1f)
    {
        // 1. 今のペン先に「新しいノード」を置く
        TrackNode newNode = CreateNode(currentPos, currentRot, optionalNodeId);

        // 以下、ConnectToNodeと共通の処理
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
}