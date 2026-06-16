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

        if (GUILayout.Button("Create Center Geometry Double Straight Course"))
        {
            CreateCenterGeometryDoubleStraightCourse();
        }
    }

    private void ValidateGraph()
    {
        var graph = (TrackGraph)target;
        var errors = new List<string>();
        if (graph.ValidateGraph(errors))
        {
            Debug.Log($"TrackGraph validation passed. nodes={graph.nodes.Count}, edges={graph.edges.Count}, geometries={graph.geometries.Count}", graph);
            return;
        }

        Debug.LogError("TrackGraph validation failed:\n- " + string.Join("\n- ", errors), graph);
    }

    private void CreateCenterGeometryDoubleStraightCourse()
    {
        var graph = (TrackGraph)target;
        Undo.RecordObject(graph, "Create Center Geometry Double Straight Course");

        ClearGraphData(graph);

        const string centerGeometryId = "Center_001_Geo";
        const float routeLengthM = 1000f;
        const float trackCenterSpacingM = 3.8f;
        const float sampleIntervalM = 0.1f;
        const float integrationStepM = 0.05f;
        const float speedLimitKmH = 90f;

        graph.geometries.Add(CreateCenterGeometry(
            centerGeometryId,
            Vector3.zero,
            Quaternion.identity,
            CreateStraightSegments(routeLengthM)
        ));

        AddOffsetEdgeFromCenterGeometry(
            graph,
            "Up_001",
            "Up_001_Start",
            "Up_001_End",
            centerGeometryId,
            -trackCenterSpacingM * 0.5f,
            speedLimitKmH,
            sampleIntervalM,
            integrationStepM
        );

        AddOffsetEdgeFromCenterGeometry(
            graph,
            "Down_001",
            "Down_001_Start",
            "Down_001_End",
            centerGeometryId,
            trackCenterSpacingM * 0.5f,
            speedLimitKmH,
            sampleIntervalM,
            integrationStepM
        );

        graph.stations.Add(new StationData
        {
            stationId = "ST_Start",
            stationName = "Start",
            edgeId = "Up_001",
            distanceFromEdgeStart = 50f,
            stopMarginM = 5f
        });

        graph.stations.Add(new StationData
        {
            stationId = "ST_End",
            stationName = "End",
            edgeId = "Up_001",
            distanceFromEdgeStart = 950f,
            stopMarginM = 5f
        });

        graph.UpdateNodeTypesAndJunctionIds();
        graph.SyncTurnoutStates();

        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
        Debug.Log($"Created center geometry double straight course. centerGeometry={centerGeometryId}, edges={graph.edges.Count}, spacing={trackCenterSpacingM}m.", graph);
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

    private static TrackGeometry CreateCenterGeometry(
        string geometryId,
        Vector3 originPosition,
        Quaternion originRotation,
        List<TrackHorizontalSegment> horizontalSegments)
    {
        List<TrackHorizontalSegment> normalizedSegments = NormalizeHorizontalSegments(horizontalSegments);
        float lengthM = SumSegmentLengths(normalizedSegments);

        return new TrackGeometry
        {
            geometryId = geometryId,
            lengthM = lengthM,
            originPosition = originPosition,
            originRotation = originRotation,
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
    }

    private static void AddOffsetEdgeFromCenterGeometry(
        TrackGraph graph,
        string edgeId,
        string startNodeId,
        string endNodeId,
        string baseGeometryId,
        float offsetM,
        float speedLimitKmH,
        float sampleIntervalM,
        float integrationStepM)
    {
        if (graph == null || string.IsNullOrEmpty(edgeId) || string.IsNullOrEmpty(baseGeometryId))
        {
            return;
        }

        TrackGeometry baseGeometry = graph.FindGeometry(baseGeometryId);
        if (baseGeometry == null)
        {
            Debug.LogError($"Cannot create offset edge '{edgeId}'. Missing base geometry '{baseGeometryId}'.", graph);
            return;
        }

        var offsetSegments = new List<TrackOffsetSegment>
        {
            new TrackOffsetSegment
            {
                startBaseDistanceM = 0f,
                baseLengthM = baseGeometry.lengthM,
                startOffsetM = offsetM,
                endOffsetM = offsetM,
                curveType = TrackOffsetCurveType.Constant
            }
        };

        TrackRuntimeResolver resolver = new TrackRuntimeResolver();
        TrackOffsetDistanceMap distanceMap = TrackOffsetDistanceMapBuilder.Build(
            graph,
            resolver,
            baseGeometryId,
            offsetSegments,
            sampleIntervalM,
            integrationStepM
        );

        float offsetLengthM = distanceMap.OffsetLengthM;
        if (offsetLengthM <= 0f)
        {
            Debug.LogError($"Cannot create offset edge '{edgeId}'. Distance map is empty.", graph);
            return;
        }

        TrackEdge edge = new TrackEdge
        {
            edgeId = edgeId,
            nodeAId = startNodeId,
            nodeBId = endNodeId,
            baseGeometryId = baseGeometryId,
            offsetSegments = offsetSegments,
            offsetDistanceMap = distanceMap,
            blockSections = CreateSingleBlockSection(edgeId, offsetLengthM),
            lengthM = offsetLengthM,
            speedLimitMS = speedLimitKmH / 3.6f,
            gaugeM = baseGeometry.gaugeM
        };

        if (!resolver.TryResolveOffsetEdgePose(
            graph,
            edge,
            0f,
            out Vector3 startPosition,
            out _,
            out Quaternion startRotation))
        {
            Debug.LogError($"Cannot resolve start pose for offset edge '{edgeId}'.", graph);
            return;
        }

        if (!resolver.TryResolveOffsetEdgePose(
            graph,
            edge,
            offsetLengthM,
            out Vector3 endPosition,
            out _,
            out Quaternion endRotation))
        {
            Debug.LogError($"Cannot resolve end pose for offset edge '{edgeId}'.", graph);
            return;
        }

        graph.nodes.Add(CreateNode(startNodeId, startPosition, startRotation, edgeId));
        graph.nodes.Add(CreateNode(endNodeId, endPosition, endRotation, edgeId));
        graph.edges.Add(edge);
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

    private static List<TrackHorizontalSegment> NormalizeHorizontalSegments(List<TrackHorizontalSegment> sourceSegments)
    {
        var result = new List<TrackHorizontalSegment>();
        if (sourceSegments == null)
        {
            return result;
        }

        float cursorM = 0f;
        for (int i = 0; i < sourceSegments.Count; i++)
        {
            TrackHorizontalSegment source = sourceSegments[i];
            if (source == null)
            {
                continue;
            }

            float lengthM = Mathf.Max(0f, source.lengthM);
            result.Add(new TrackHorizontalSegment
            {
                startDistanceM = cursorM,
                lengthM = lengthM,
                trackCurveType = source.trackCurveType,
                radiusM = source.radiusM
            });

            cursorM += lengthM;
        }

        return result;
    }

    private static float SumSegmentLengths(List<TrackHorizontalSegment> segments)
    {
        if (segments == null)
        {
            return 0f;
        }

        float lengthM = 0f;
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i] != null)
            {
                lengthM += Mathf.Max(0f, segments[i].lengthM);
            }
        }

        return lengthM;
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
