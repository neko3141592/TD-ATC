// TrackBuilder はテスト用やエディタ用に線路グラフを順番に組み立てる補助クラスです。
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

    /// <summary>
    /// 役割: TrackBuilder の処理を行います。
    /// </summary>
    /// <param name="graph">graph を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public TrackBuilder(TrackGraph graph)
    {
        targetGraph = graph;
    }
    /// <summary>
    /// 役割: Start の処理を開始状態を設定します。
    /// </summary>
    /// <param name="startPos">startPos を指定します。</param>
    /// <param name="startRot">startRot を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void Start(Vector3 startPos, Quaternion startRot)
    {
        currentPos = startPos;
        currentRot = startRot;
        lastNode = CreateNode(startPos, startRot);
    }
    
    /// <summary>
    /// 役割: StartFrom を使って必要な処理を指定ノードを起点に開始状態へ切り替えます。
    /// </summary>
    /// <param name="node">node を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void StartFrom(TrackNode node)
    {
        currentPos = node.worldPosition;
        currentRot = node.worldRotation;
        lastNode = node;
        
        currentCurves.Clear();
        currentEdgeLength = 0f;
    }

    /// <summary>
    /// 役割: AddStraight の処理を追加します。
    /// </summary>
    /// <param name="lengthM">lengthM を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void AddStraight(float lengthM)
    {
        AppendCurveSegment(TrackCurveType.Straight, lengthM, 0f);
        AdvanceCurrentPose(TrackCurveType.Straight, lengthM, 0f);
    }

    /// <summary>
    /// 役割: AddCurve の処理を追加します。
    /// </summary>
    /// <param name="lengthM">lengthM を指定します。</param>
    /// <param name="radiusM">radiusM を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void AddCurve(float lengthM, float radiusM)
    {
        AppendCurveSegment(TrackCurveType.Curve, lengthM, radiusM);
        AdvanceCurrentPose(TrackCurveType.Curve, lengthM, radiusM);
    }

    /// <summary>
    /// 役割: AddClothoidIn の処理を追加します。
    /// </summary>
    /// <param name="lengthM">lengthM を指定します。</param>
    /// <param name="radiusM">radiusM を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void AddClothoidIn(float lengthM, float radiusM)
    {
        AppendCurveSegment(TrackCurveType.TransitionIn, lengthM, radiusM);
        AdvanceCurrentPose(TrackCurveType.TransitionIn, lengthM, radiusM);
    }

    /// <summary>
    /// 役割: AddClothoidOut の処理を追加します。
    /// </summary>
    /// <param name="lengthM">lengthM を指定します。</param>
    /// <param name="radiusM">radiusM を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void AddClothoidOut(float lengthM, float radiusM)
    {
        AppendCurveSegment(TrackCurveType.TransitionOut, lengthM, radiusM);
        AdvanceCurrentPose(TrackCurveType.TransitionOut, lengthM, radiusM);
    }

    /// <summary>
    /// 役割: AddClothoidInOut の処理を追加します。
    /// </summary>
    /// <param name="lengthM">lengthM を指定します。</param>
    /// <param name="radiusM">radiusM を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void AddClothoidInOut(float lengthM, float radiusM)
    {
        AddClothoidIn(lengthM, radiusM);
        AddClothoidOut(lengthM, radiusM);
    }

    /// <summary>
    /// 役割: PutNode の処理を行います。
    /// </summary>
    /// <param name="optionalNodeId">optionalNodeId を指定します。</param>
    /// <param name="speedLimitKmH">speedLimitKmH を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    public TrackNode PutNode(string optionalNodeId = null, float speedLimitKmH = -1f)
    {
        TrackNode newNode = CreateNode(currentPos, currentRot, optionalNodeId);

        ConnectToNode(newNode, speedLimitKmH);

        return newNode;
    }

    /// <summary>
    /// 役割: ConnectToNode の処理を接続します。
    /// </summary>
    /// <param name="targetNode">targetNode を指定します。</param>
    /// <param name="speedLimitKmH">speedLimitKmH を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
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

    /// <summary>
    /// 役割: AddStation の処理を追加します。
    /// </summary>
    /// <param name="stationId">stationId を指定します。</param>
    /// <param name="stationName">stationName を指定します。</param>
    /// <param name="offsetMFromNode">offsetMFromNode を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void AddStation(string stationId, string stationName, float offsetMFromNode = 10f)
    {
        // 直前に生成したエッジ上に、エッジ始点からの距離指定で駅を配置します。
        if (targetGraph.edges.Count == 0) return;
        
        var station = new StationData
        {
            stationId = stationId,
            stationName = stationName,
            edgeId = $"E{targetGraph.edges.Count:000}", // 直前に生成したエッジです。
            distanceFromEdgeStart = offsetMFromNode
        };
        targetGraph.stations.Add(station);
    }

    /// <summary>
    /// 役割: CreateNode の処理を生成します。
    /// </summary>
    /// <param name="pos">pos を指定します。</param>
    /// <param name="rot">rot を指定します。</param>
    /// <param name="id">id を指定します。</param>
    /// <returns>処理結果を返します。</returns>
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

    /// <summary>
    /// 役割: AppendCurveSegment の処理を行います。
    /// </summary>
    /// <param name="type">type を指定します。</param>
    /// <param name="lengthM">lengthM を指定します。</param>
    /// <param name="radiusM">radiusM を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
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

    /// <summary>
    /// 役割: AdvanceCurrentPose の処理を進めます。
    /// </summary>
    /// <param name="type">type を指定します。</param>
    /// <param name="lengthM">lengthM を指定します。</param>
    /// <param name="radiusM">radiusM を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
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
