using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrackGraph))]
public class TrackGraphEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8);

        if (GUILayout.Button("Validate Graph"))
        {
            ValidateGraph();
        }

        GUILayout.Space(8);

        if (GUILayout.Button("Create 2km Double Straight Course"))
        {
            CreateDoubleStraight2kmCourse();
        }

        if (GUILayout.Button("Create Cubic Main Curve Test Course"))
        {
            CreateCubicMainCurveTestCourse();
        }
    }

    private void ValidateGraph()
    {
        var graph = (TrackGraph)target;
        var errors = new List<string>();
        if (graph.ValidateGraph(errors))
        {
            Debug.Log($"TrackGraph validation passed. nodes={graph.nodes.Count}, edges={graph.edges.Count}", graph);
            return;
        }

        Debug.LogError("TrackGraph validation failed:\n- " + string.Join("\n- ", errors), graph);
    }

    private void CreateDoubleStraight2kmCourse()
    {
        var graph = (TrackGraph)target;
        Undo.RecordObject(graph, "Create 2km Double Straight Course");

        ClearGraphData(graph);

        const float edgeLengthM = 1000f;
        const float totalLengthM = 2000f;
        const float trackCenterSpacingM = 3.8f;
        const float speedLimitKmH = 90f;

        graph.nodes.Add(CreateNode("DS1_Main_Start", new Vector3(0f, 0f, 0f), Quaternion.identity, "E001"));
        graph.nodes.Add(CreateNode("DS1_Main_Mid", new Vector3(0f, 0f, edgeLengthM), Quaternion.identity, "E001", "E003"));
        graph.nodes.Add(CreateNode("DS1_Main_End", new Vector3(0f, 0f, totalLengthM), Quaternion.identity, "E003"));
        graph.nodes.Add(CreateNode("DS1_Parallel_Start", new Vector3(trackCenterSpacingM, 0f, 0f), Quaternion.identity, "E002"));
        graph.nodes.Add(CreateNode("DS1_Parallel_Mid", new Vector3(trackCenterSpacingM, 0f, edgeLengthM), Quaternion.identity, "E002", "E004"));
        graph.nodes.Add(CreateNode("DS1_Parallel_End", new Vector3(trackCenterSpacingM, 0f, totalLengthM), Quaternion.identity, "E004"));

        AddBuiltEdge(graph, CreateStraightEdge("E001", "DS1_Main_Start", "DS1_Main_Mid", edgeLengthM, speedLimitKmH));
        AddBuiltEdge(graph, CreateStraightEdge("E003", "DS1_Main_Mid", "DS1_Main_End", edgeLengthM, speedLimitKmH));
        AddBuiltEdge(graph, CreateStraightEdge("E002", "DS1_Parallel_Start", "DS1_Parallel_Mid", edgeLengthM, speedLimitKmH));
        AddBuiltEdge(graph, CreateStraightEdge("E004", "DS1_Parallel_Mid", "DS1_Parallel_End", edgeLengthM, speedLimitKmH));

        graph.stations.Add(new StationData
        {
            stationId = "ST_Start",
            stationName = "Start",
            edgeId = "E001",
            distanceFromEdgeStart = 50f,
            stopMarginM = 5f
        });
        graph.stations.Add(new StationData
        {
            stationId = "ST_End",
            stationName = "End",
            edgeId = "E003",
            distanceFromEdgeStart = 950f,
            stopMarginM = 5f
        });

        graph.UpdateNodeTypesAndJunctionIds();
        graph.SyncTurnoutStates();

        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
        Debug.Log($"Created 2km double straight course. main=E001->E003, parallel=E002->E004, spacing={trackCenterSpacingM}m.", graph);
    }

    private void CreateCubicMainCurveTestCourse()
    {
        var graph = (TrackGraph)target;
        Undo.RecordObject(graph, "Create Cubic Main Curve Test Course");

        ClearGraphData(graph);

        Vector3 startPosition = Vector3.zero;
        Quaternion startRotation = Quaternion.identity;
        string currentNodeId = "CurveTest_N000";
        graph.nodes.Add(CreateNode(currentNodeId, startPosition, startRotation, "E001"));

        Vector3 currentPosition = startPosition;
        Quaternion currentRotation = startRotation;
        float routeLengthM = 0f;

        AddRouteEdge(
            graph,
            "E001",
            ref currentNodeId,
            "CurveTest_N001",
            ref currentPosition,
            ref currentRotation,
            CreateStraightSegments(1200f),
            110f,
            "E002"
        );
        routeLengthM += 1200f;

        AddRouteEdge(
            graph,
            "E002",
            ref currentNodeId,
            "CurveTest_N002",
            ref currentPosition,
            ref currentRotation,
            CreateCurveSegments(110f, 400f, 360f, 110f),
            75f,
            "E003"
        );
        routeLengthM += 580f;

        AddRouteEdge(
            graph,
            "E003",
            ref currentNodeId,
            "CurveTest_N003",
            ref currentPosition,
            ref currentRotation,
            CreateStraightSegments(1800f),
            110f,
            "E004"
        );
        routeLengthM += 1800f;

        AddRouteEdge(
            graph,
            "E004",
            ref currentNodeId,
            "CurveTest_N004",
            ref currentPosition,
            ref currentRotation,
            CreateCurveSegments(90f, -300f, 260f, 90f),
            50f,
            "E005"
        );
        routeLengthM += 440f;

        AddRouteEdge(
            graph,
            "E005",
            ref currentNodeId,
            "CurveTest_N005",
            ref currentPosition,
            ref currentRotation,
            CreateStraightSegments(1500f),
            100f,
            "E006"
        );
        routeLengthM += 1500f;

        AddRouteEdge(
            graph,
            "E006",
            ref currentNodeId,
            "CurveTest_N006",
            ref currentPosition,
            ref currentRotation,
            CreateCurveSegments(110f, 700f, 390f, 110f),
            110f,
            "E007"
        );
        routeLengthM += 610f;

        AddRouteEdge(
            graph,
            "E007",
            ref currentNodeId,
            "CurveTest_N007",
            ref currentPosition,
            ref currentRotation,
            CreateStraightSegments(2200f),
            110f,
            null
        );
        routeLengthM += 2200f;

        AddRouteEdge(
            graph,
            "E008",
            ref currentNodeId,
            "CurveTest_N008",
            ref currentPosition,
            ref currentRotation,
            CreateCurveSegments(90f, 900f, 450f, 90f),
            95f,
            null
        );

        AddRouteEdge(
            graph,
            "E009",
            ref currentNodeId,
            "CurveTest_N009",
            ref currentPosition,
            ref currentRotation,
            CreateStraightSegments(1050f),
            110f,
            null
        );

        graph.stations.Add(new StationData
        {
            stationId = "ST_Start",
            stationName = "Start",
            edgeId = "E001",
            distanceFromEdgeStart = 50f,
            stopMarginM = 5f
        });


        graph.stations.Add(new StationData
        {
            stationId = "ST_End",
            stationName = "End",
            edgeId = "E007",
            distanceFromEdgeStart = 2150f,
            stopMarginM = 5f
        });

    

        graph.UpdateNodeTypesAndJunctionIds();
        graph.SyncTurnoutStates();

        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
        Debug.Log($"Created cubic main curve test course. length={routeLengthM:0.#}m, edges={graph.edges.Count}.", graph);
    }

    private static void ClearGraphData(TrackGraph graph)
    {
        graph.nodes.Clear();
        graph.edges.Clear();
        graph.geometries.Clear();
        graph.turnoutStates.Clear();
        graph.turnoutConnections.Clear();
        graph.stations.Clear();
    }

    private static TrackNode CreateNode(string nodeId, Vector3 worldPosition, Quaternion worldRotation, params string[] connectedEdgeIds)
    {
        TrackNode node = new TrackNode
        {
            nodeId = nodeId,
            trackNodeType = TrackNodeType.Normal,
            junctionId = string.Empty,
            worldPosition = worldPosition,
            worldRotation = worldRotation,
            connectedEdgeIds = new List<string>()
        };

        if (connectedEdgeIds == null)
        {
            return node;
        }

        for (int i = 0; i < connectedEdgeIds.Length; i++)
        {
            if (!string.IsNullOrEmpty(connectedEdgeIds[i]))
            {
                node.connectedEdgeIds.Add(connectedEdgeIds[i]);
            }
        }

        return node;
    }

    private static TrackEdgeBuildResult CreateStraightEdge(
        string edgeId,
        string nodeAId,
        string nodeBId,
        float lengthM,
        float speedLimitKmH)
    {
        var horizontalSegments = new List<TrackHorizontalSegment>
        {
            new TrackHorizontalSegment
            {
                startDistanceM = 0f,
                lengthM = lengthM,
                trackCurveType = TrackCurveType.Straight,
                radiusM = 0f
            }
        };

        return CreateEdgeFromHorizontalSegments(edgeId, nodeAId, nodeBId, horizontalSegments, speedLimitKmH);
    }

    private static List<TrackHorizontalSegment> CreateStraightSegments(float lengthM)
    {
        return new List<TrackHorizontalSegment>
        {
            new TrackHorizontalSegment
            {
                lengthM = lengthM,
                trackCurveType = TrackCurveType.Straight,
                radiusM = 0f
            }
        };
    }

    private static List<TrackHorizontalSegment> CreateCurveSegments(
        float transitionInLengthM,
        float radiusM,
        float curveLengthM,
        float transitionOutLengthM)
    {
        return new List<TrackHorizontalSegment>
        {
            new TrackHorizontalSegment
            {
                lengthM = transitionInLengthM,
                trackCurveType = TrackCurveType.TransitionIn,
                radiusM = radiusM
            },
            new TrackHorizontalSegment
            {
                lengthM = curveLengthM,
                trackCurveType = TrackCurveType.Curve,
                radiusM = radiusM
            },
            new TrackHorizontalSegment
            {
                lengthM = transitionOutLengthM,
                trackCurveType = TrackCurveType.TransitionOut,
                radiusM = radiusM
            }
        };
    }

    private static void AddRouteEdge(
        TrackGraph graph,
        string edgeId,
        ref string currentNodeId,
        string nextNodeId,
        ref Vector3 currentPosition,
        ref Quaternion currentRotation,
        List<TrackHorizontalSegment> horizontalSegments,
        float speedLimitKmH,
        string nextEdgeId)
    {
        List<TrackHorizontalSegment> normalizedSegments = NormalizeHorizontalSegments(horizontalSegments);
        CalculateHorizontalEndPose(normalizedSegments, currentPosition, currentRotation, out Vector3 nextPosition, out Quaternion nextRotation);

        graph.nodes.Add(CreateNode(nextNodeId, nextPosition, nextRotation, string.IsNullOrEmpty(nextEdgeId) ? new[] { edgeId } : new[] { edgeId, nextEdgeId }));
        AddBuiltEdge(graph, CreateEdgeFromHorizontalSegments(edgeId, currentNodeId, nextNodeId, normalizedSegments, speedLimitKmH));

        currentNodeId = nextNodeId;
        currentPosition = nextPosition;
        currentRotation = nextRotation;
    }

    private static void AddBuiltEdge(TrackGraph graph, TrackEdgeBuildResult result)
    {
        graph.edges.Add(result.edge);
        graph.geometries.Add(result.geometry);
    }

    // エッジどジオメトリの返り値をまとめるstruct
    private readonly struct TrackEdgeBuildResult
    {
        public readonly TrackEdge edge;
        public readonly TrackGeometry geometry;
        public TrackEdgeBuildResult(TrackEdge edge, TrackGeometry geometry)
        {
            this.edge = edge;
            this.geometry = geometry;
        }
    }

    private static TrackEdgeBuildResult CreateEdgeFromHorizontalSegments(
        string edgeId,
        string nodeAId,
        string nodeBId,
        List<TrackHorizontalSegment> horizontalSegments,
        float speedLimitKmH)
    {
        List<TrackHorizontalSegment> normalizedSegments = NormalizeHorizontalSegments(horizontalSegments);
        float lengthM = SumSegmentLengths(normalizedSegments);
        string geometryId = $"{edgeId}_Geo";

        TrackEdge edge = new TrackEdge
        {
            edgeId = edgeId,
            nodeAId = nodeAId,
            nodeBId = nodeBId,
            geometryId = geometryId,
            blockSections = CreateSingleBlockSection(edgeId, lengthM),
            lengthM = lengthM,
            speedLimitMS = speedLimitKmH / 3.6f,
        };

        TrackGeometry geometry = new TrackGeometry
        {
            geometryId = geometryId,
            lengthM = lengthM,
            gaugeM = edge.gaugeM,
            horizontalSegments = normalizedSegments,
            verticalSegments = new List<TrackVerticalSegment>
            {
                new TrackVerticalSegment
                {
                    startDistanceM = 0f,
                    lengthM = lengthM,
                    startGradientPermille = 0f,
                    endGradientPermille = 0f
                }
            },
            cantSegments = new List<TrackCantSegment>
            {
                new TrackCantSegment
                {
                    startDistanceM = 0f,
                    lengthM = lengthM,
                    startCantMm = 0f,
                    endCantMm = 0f
                }
            }
        };

        return new TrackEdgeBuildResult(edge, geometry);
    }

    // セグメントの開始位置を自動決定する;
    private static List<TrackHorizontalSegment> NormalizeHorizontalSegments(List<TrackHorizontalSegment> sourceSegments)
    {
        var result = new List<TrackHorizontalSegment>();

        // 渡されたセグメントがnullではないか確認
        if (sourceSegments == null)
        {
            return result;
        }

        // 現在見ている位置
        float cursorM = 0f;

        for (int i = 0; i < sourceSegments.Count; i++)
        {
            TrackHorizontalSegment source = sourceSegments[i];
            if (source == null)
            {
                continue;
            }

            result.Add(new TrackHorizontalSegment
            {
                // 開始位置を決定
                startDistanceM = cursorM,
                lengthM = Mathf.Max(0f, source.lengthM),
                trackCurveType = source.trackCurveType,
                radiusM = source.radiusM
            });

            // 現在見ている位置を更新
            cursorM += Mathf.Max(0f, source.lengthM);
        }

        return result;
    }

    // セグメントの距離の和を求める
    private static float SumSegmentLengths(List<TrackHorizontalSegment> segments)
    {
        if (segments == null)
        {
            return 0f;
        }

        float lengthM = 0f;
        for (int i = 0; i < segments.Count; i++)
        {
            TrackHorizontalSegment segment = segments[i];
            if (segment != null)
            {
                lengthM += Mathf.Max(0f, segment.lengthM);
            }
        }

        return lengthM;
    }

    private static void CalculateHorizontalEndPose(
        List<TrackHorizontalSegment> segments,
        Vector3 startPosition,
        Quaternion startRotation,
        out Vector3 endPosition,
        out Quaternion endRotation)
    {
        endPosition = startPosition;
        endRotation = startRotation;

        if (segments == null)
        {
            return;
        }

        for (int i = 0; i < segments.Count; i++)
        {
            TrackHorizontalSegment segment = segments[i];
            if (segment == null)
            {
                continue;
            }

            float segmentLengthM = Mathf.Max(0f, segment.lengthM);
            CalculateHorizontalSegmentEnd(
                segment.trackCurveType,
                segmentLengthM,
                segment.radiusM,
                out float localX,
                out float localZ,
                out float angleDegree
            );

            endPosition += endRotation * new Vector3(localX, 0f, localZ);
            endRotation *= Quaternion.Euler(0f, angleDegree, 0f);
        }
    }

    private static void CalculateHorizontalSegmentEnd(
        TrackCurveType curveType,
        float segmentLengthM,
        float radiusM,
        out float localX,
        out float localZ,
        out float angleDegree)
    {
        switch (curveType)
        {
            case TrackCurveType.Curve:
                TrackRuntimeResolver.CalculateCircularCurve(segmentLengthM, radiusM, out localX, out localZ, out angleDegree);
                break;
            case TrackCurveType.TransitionIn:
                TrackRuntimeResolver.CalculateCubicTransitionIn(segmentLengthM, segmentLengthM, radiusM, out localX, out localZ, out angleDegree);
                break;
            case TrackCurveType.TransitionOut:
                TrackRuntimeResolver.CalculateCubicTransitionOut(segmentLengthM, segmentLengthM, radiusM, out localX, out localZ, out angleDegree);
                break;
            default:
                TrackRuntimeResolver.CalculateStraight(segmentLengthM, out localX, out localZ, out angleDegree);
                break;
        }
    }

    private static List<BlockSection> CreateSingleBlockSection(string edgeId, float lengthM)
    {
        return new List<BlockSection>
        {
            new BlockSection
            {
                blockId = $"{edgeId}_B000",
                startDistanceM = 0f,
                endDistanceM = Mathf.Max(0f, lengthM)
            }
        };
    }
}
