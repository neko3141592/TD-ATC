using System;
using System.Collections.Generic;
using UnityEngine;

public static class TrackJsonCompiler
{
    public static void CompileInto(TrackGraph graph, TrackLayoutJson layout)
    {
        if (graph == null)
        {
            Debug.LogError("Cannot compile track JSON. TrackGraph is null.");
            return;
        }

        if (layout == null)
        {
            Debug.LogError("Cannot compile track JSON. TrackLayoutJson is null.", graph);
            return;
        }

        ClearGraphData(graph);
        CompileGeometries(graph, layout.geometries);
        CompileTracks(graph, layout.tracks, 0.05f, 0.005f);
        
        ApplyConnections(graph, layout.connections);

        graph.UpdateNodeTypesAndJunctionIds();
        graph.SyncTurnoutStates();

        var errors = new List<string>();
        if (!graph.ValidateGraph(errors))
        {
            Debug.LogError("Compiled TrackGraph has validation errors:\n- " + string.Join("\n- ", errors), graph);
        }
    }

    public static void CompileGeometries(TrackGraph graph, List<TrackGeometryJson> geometries)
    {
        if (graph == null || geometries == null)
        {
            return;
        }

        var usedGeometryIds = new HashSet<string>();
        for (int i = 0; i < geometries.Count; i++)
        {
            TrackGeometryJson geometryJson = geometries[i];
            if (geometryJson == null)
            {
                Debug.LogWarning($"geometries[{i}] is null. Skipped.", graph);
                continue;
            }

            string geometryId = string.IsNullOrEmpty(geometryJson.name)
                ? $"GEO_{i}"
                : geometryJson.name;

            if (!usedGeometryIds.Add(geometryId))
            {
                Debug.LogWarning($"Duplicate geometry id '{geometryId}'. Skipped.", graph);
                continue;
            }

            Vector3 origin = ToVector3(geometryJson.origin);
            Quaternion rotation = Quaternion.Euler(0f, geometryJson.yawDeg, 0f);
            List<TrackHorizontalSegment> horizontalSegments = ToHorizontalSegments(geometryJson.segments);
            float lengthM = SumSegmentLengths(horizontalSegments);
            if (lengthM <= 0f)
            {
                Debug.LogWarning($"Geometry '{geometryId}' has no positive-length horizontal segments. Skipped.", graph);
                continue;
            }

            graph.geometries.Add(CreateCenterGeometry(
                geometryId,
                origin,
                rotation,
                horizontalSegments,
                ToVerticalSegments(geometryJson.vertical, lengthM),
                ToCantSegments(geometryJson.cant, lengthM)
            ));
        }
    }

    public static void CompileTracks(
        TrackGraph graph,
        List<TrackJson> tracks,
        float sampleIntervalM = 0.1f,
        float integrationStepM = 0.01f)
    {
        if (graph == null || tracks == null)
        {
            return;
        }

        var usedEdgeIds = new HashSet<string>();
        for (int i = 0; i < tracks.Count; i++)
        {
            TrackJson trackJson = tracks[i];
            if (trackJson == null)
            {
                Debug.LogWarning($"tracks[{i}] is null. Skipped.", graph);
                continue;
            }

            string edgeId = string.IsNullOrEmpty(trackJson.name)
                ? $"TRACK_{i}"
                : trackJson.name;

            if (!usedEdgeIds.Add(edgeId))
            {
                Debug.LogWarning($"Duplicate track name '{edgeId}'. Skipped.", graph);
                continue;
            }

            string startNodeId = $"{edgeId}_start";
            string endNodeId = $"{edgeId}_end";
            List<TrackOffsetSegment> offsetSegments = ToOffsetSegments(trackJson.offset);
            float speedLimitKmH = trackJson.speedLimitKmH > 0f ? trackJson.speedLimitKmH : 0f;

            AddOffsetEdgeFromCenterGeometry(
                graph,
                edgeId,
                startNodeId,
                endNodeId,
                trackJson.baseCenterLineId,
                offsetSegments,
                speedLimitKmH,
                sampleIntervalM,
                integrationStepM
            );
        }
    }

    private static Vector3 ToVector3(TrackVector3Json source)
    {
        return source == null
            ? Vector3.zero
            : new Vector3(source.x, source.y, source.z);
    }

    private static List<TrackHorizontalSegment> ToHorizontalSegments(List<TrackHorizontalSegmentJson> source)
    {
        var horizontalSegments = new List<TrackHorizontalSegment>();
        if (source == null)
        {
            return horizontalSegments;
        }

        float startDistanceM = 0f;
        for (int i = 0; i < source.Count; i++)
        {
            TrackHorizontalSegmentJson sourceSegment = source[i];
            if (sourceSegment == null)
            {
                continue;
            }

            TrackHorizontalSegment segment = ToHorizontalSegment(sourceSegment, startDistanceM);
            if (segment == null || segment.lengthM <= 0f)
            {
                continue;
            }

            horizontalSegments.Add(segment);
            startDistanceM += segment.lengthM;
        }

        return horizontalSegments;
    }

    private static TrackHorizontalSegment ToHorizontalSegment(
        TrackHorizontalSegmentJson source,
        float startDistanceM)
    {
        if (source == null)
        {
            return null;
        }

        return new TrackHorizontalSegment
        {
            startDistanceM = startDistanceM,
            lengthM = Mathf.Max(0f, source.lengthM),
            trackCurveType = ToTrackCurveType(source.type),
            radiusM = source.radiusM
        };
    }

    private static TrackCurveType ToTrackCurveType(string type)
    {
        if (string.Equals(type, "straight", StringComparison.OrdinalIgnoreCase))
        {
            return TrackCurveType.Straight;
        }

        if (string.Equals(type, "curve", StringComparison.OrdinalIgnoreCase))
        {
            return TrackCurveType.Curve;
        }

        if (string.Equals(type, "transitionIn", StringComparison.OrdinalIgnoreCase))
        {
            return TrackCurveType.TransitionIn;
        }

        if (string.Equals(type, "transitionOut", StringComparison.OrdinalIgnoreCase))
        {
            return TrackCurveType.TransitionOut;
        }

        Debug.LogWarning($"Unknown horizontal segment type '{type}'. Fallback to straight.");
        return TrackCurveType.Straight;
    }

    private static List<TrackVerticalSegment> ToVerticalSegments(List<TrackVerticalSegmentJson> source, float geometryLengthM)
    {
        var segments = new List<TrackVerticalSegment>();
        if (source != null)
        {
            for (int i = 0; i < source.Count; i++)
            {
                TrackVerticalSegmentJson sourceSegment = source[i];
                if (sourceSegment == null)
                {
                    continue;
                }

                float lengthM = Mathf.Max(0f, sourceSegment.lengthM);
                if (lengthM <= 0f)
                {
                    continue;
                }

                segments.Add(new TrackVerticalSegment
                {
                    startDistanceM = Mathf.Max(0f, sourceSegment.startM),
                    lengthM = lengthM,
                    startGradientPermille = sourceSegment.startPermille,
                    endGradientPermille = sourceSegment.endPermille
                });
            }
        }

        if (segments.Count == 0)
        {
            segments.Add(new TrackVerticalSegment
            {
                startDistanceM = 0f,
                lengthM = Mathf.Max(0f, geometryLengthM),
                startGradientPermille = 0f,
                endGradientPermille = 0f
            });
        }

        return segments;
    }

    private static List<TrackCantSegment> ToCantSegments(List<TrackCantSegmentJson> source, float geometryLengthM)
    {
        var segments = new List<TrackCantSegment>();
        if (source != null)
        {
            for (int i = 0; i < source.Count; i++)
            {
                TrackCantSegmentJson sourceSegment = source[i];
                if (sourceSegment == null)
                {
                    continue;
                }

                float lengthM = Mathf.Max(0f, sourceSegment.lengthM);
                if (lengthM <= 0f)
                {
                    continue;
                }

                segments.Add(new TrackCantSegment
                {
                    startDistanceM = Mathf.Max(0f, sourceSegment.startM),
                    lengthM = lengthM,
                    startCantMm = sourceSegment.startMm,
                    endCantMm = sourceSegment.endMm
                });
            }
        }

        if (segments.Count == 0)
        {
            segments.Add(new TrackCantSegment
            {
                startDistanceM = 0f,
                lengthM = Mathf.Max(0f, geometryLengthM),
                startCantMm = 0f,
                endCantMm = 0f
            });
        }

        return segments;
    }

    private static TrackOffsetCurveType ToTrackOffsetCurveType(string type)
    {
        if (string.Equals(type, "constant", StringComparison.OrdinalIgnoreCase))
        {
            return TrackOffsetCurveType.Constant;
        }

        if (string.Equals(type, "linear", StringComparison.OrdinalIgnoreCase))
        {
            return TrackOffsetCurveType.Linear;
        }

        if (string.Equals(type, "cubic", StringComparison.OrdinalIgnoreCase))
        {
            return TrackOffsetCurveType.Cubic;
        }

        Debug.LogWarning($"Unknown track offset curve type '{type}'. Fallback to constant.");
        return TrackOffsetCurveType.Constant;
    }

    private static List<TrackOffsetSegment> ToOffsetSegments(List<TrackOffsetSegmentJson> source)
    {
        var segments = new List<TrackOffsetSegment>();
        if (source == null)
        {
            return segments;
        }

        for (int i = 0; i < source.Count; i++)
        {
            TrackOffsetSegmentJson sourceSegment = source[i];
            if (sourceSegment == null)
            {
                continue;
            }

            float baseLengthM = Mathf.Max(0f, sourceSegment.lengthM);
            if (baseLengthM <= 0f)
            {
                continue;
            }

            TrackOffsetCurveType curveType = ToTrackOffsetCurveType(sourceSegment.type);
            float startOffsetM = sourceSegment.startOffsetM;
            float endOffsetM = sourceSegment.endOffsetM;
            if (curveType == TrackOffsetCurveType.Constant)
            {
                startOffsetM = sourceSegment.offsetM;
                endOffsetM = sourceSegment.offsetM;
            }

            segments.Add(new TrackOffsetSegment
            {
                curveType = curveType,
                startBaseDistanceM = Mathf.Max(0f, sourceSegment.startBaseM),
                baseLengthM = baseLengthM,
                startOffsetM = startOffsetM,
                endOffsetM = endOffsetM,
            });
        }

        segments.Sort((a, b) => a.startBaseDistanceM.CompareTo(b.startBaseDistanceM));
        return segments;
    }

    private static void ClearGraphData(TrackGraph graph)
    {
        if (graph == null)
        {
            return;
        }

        graph.nodes ??= new List<TrackNode>();
        graph.edges ??= new List<TrackEdge>();
        graph.geometries ??= new List<TrackGeometry>();
        graph.turnoutStates ??= new List<TurnoutState>();
        graph.turnoutConnections ??= new List<TurnoutConnection>();
        graph.stations ??= new List<StationData>();

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
        List<TrackHorizontalSegment> horizontalSegments,
        List<TrackVerticalSegment> verticalSegments,
        List<TrackCantSegment> cantSegments)
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
            verticalSegments = verticalSegments ?? ToVerticalSegments(null, lengthM),
            cantSegments = cantSegments ?? ToCantSegments(null, lengthM)
        };
    }

    private static void AddOffsetEdgeFromCenterGeometry(
        TrackGraph graph,
        string edgeId,
        string startNodeId,
        string endNodeId,
        string baseGeometryId,
        List<TrackOffsetSegment> segments,
        float speedLimitKmH,
        float sampleIntervalM,
        float integrationStepM)
    {
        if (graph == null || string.IsNullOrEmpty(edgeId) || string.IsNullOrEmpty(baseGeometryId))
        {
            Debug.LogWarning($"Cannot create offset edge. edgeId='{edgeId}', baseGeometryId='{baseGeometryId}'.", graph);
            return;
        }

        TrackGeometry baseGeometry = graph.FindGeometry(baseGeometryId);
        if (baseGeometry == null)
        {
            Debug.LogError($"Cannot create offset edge '{edgeId}'. Missing base geometry '{baseGeometryId}'.", graph);
            return;
        }

        if (segments == null || segments.Count == 0)
        {
            Debug.LogWarning($"Offset edge '{edgeId}' has no offset segments. Fallback to centerline offset 0m.", graph);
            segments = CreateConstantOffsetSegments(baseGeometry.lengthM, 0f);
        }

        TrackRuntimeResolver resolver = new TrackRuntimeResolver();
        TrackOffsetDistanceMap distanceMap = TrackOffsetDistanceMapBuilder.Build(
            graph,
            resolver,
            baseGeometryId,
            segments,
            sampleIntervalM,
            integrationStepM
        );

        if (distanceMap == null)
        {
            Debug.LogError($"Cannot create offset edge '{edgeId}'. Distance map is null.", graph);
            return;
        }

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
            offsetSegments = segments,
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

    private static List<TrackOffsetSegment> CreateConstantOffsetSegments(float baseLengthM, float offsetM)
    {
        return new List<TrackOffsetSegment>
        {
            new TrackOffsetSegment
            {
                startBaseDistanceM = 0f,
                baseLengthM = Mathf.Max(0f, baseLengthM),
                startOffsetM = offsetM,
                endOffsetM = offsetM,
                curveType = TrackOffsetCurveType.Constant
            }
        };
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

    private static void ApplyConnections(TrackGraph graph, List<TrackConnectionJson> connections)
    {
        if (graph == null || connections == null || connections.Count == 0)
        {
            RebuildNodeConnections(graph);
            return;
        }

        var endpointNodeIdsToRemove = new HashSet<string>();

        for (int i = 0; i < connections.Count; i++)
        {
            TrackConnectionJson connection = connections[i];
            if (connection == null || connection.points == null || connection.points.Count == 0)
            {
                continue;
            }

            string connectionNodeId = string.IsNullOrEmpty(connection.name)
                ? $"CONNECTION_{i}"
                : connection.name;

            TrackNode connectionNode = null;
            for (int j = 0; j < connection.points.Count; j++)
            {
                TrackPointJson point = connection.points[j];
                if (point == null || string.IsNullOrEmpty(point.trackName))
                {
                    continue;
                }

                TrackEdge edge = graph.FindEdge(point.trackName);
                if (edge == null)
                {
                    Debug.LogWarning($"Connection '{connectionNodeId}' references missing track '{point.trackName}'.", graph);
                    continue;
                }

                if (!TryGetEndpoint(point.end, out bool isStart))
                {
                    Debug.LogWarning($"Connection '{connectionNodeId}' has invalid end '{point.end}' for track '{point.trackName}'.", graph);
                    continue;
                }

                string oldNodeId = isStart ? edge.nodeAId : edge.nodeBId;
                TrackNode oldNode = graph.FindNode(oldNodeId);
                if (oldNode == null)
                {
                    Debug.LogWarning($"Connection '{connectionNodeId}' cannot find endpoint node '{oldNodeId}'.", graph);
                    continue;
                }

                connectionNode ??= CreateNode(connectionNodeId, oldNode.worldPosition, oldNode.worldRotation);

                if (isStart)
                {
                    edge.nodeAId = connectionNodeId;
                }
                else
                {
                    edge.nodeBId = connectionNodeId;
                }

                if (!string.Equals(oldNodeId, connectionNodeId, StringComparison.Ordinal))
                {
                    endpointNodeIdsToRemove.Add(oldNodeId);
                }
            }

            if (connectionNode == null)
            {
                continue;
            }

            graph.nodes.RemoveAll(n => n != null && n.nodeId == connectionNodeId);
            graph.nodes.Add(connectionNode);
        }

        graph.nodes.RemoveAll(n =>
            n != null &&
            endpointNodeIdsToRemove.Contains(n.nodeId) &&
            !IsNodeReferencedByAnyEdge(graph, n.nodeId)
        );

        RebuildNodeConnections(graph);
    }

    private static bool TryGetEndpoint(string end, out bool isStart)
    {
        isStart = false;
        if (string.IsNullOrEmpty(end))
        {
            return false;
        }

        if (string.Equals(end, "start", StringComparison.OrdinalIgnoreCase))
        {
            isStart = true;
            return true;
        }

        if (string.Equals(end, "end", StringComparison.OrdinalIgnoreCase))
        {
            isStart = false;
            return true;
        }

        return false;
    }

    private static bool IsNodeReferencedByAnyEdge(TrackGraph graph, string nodeId)
    {
        if (graph == null || graph.edges == null || string.IsNullOrEmpty(nodeId))
        {
            return false;
        }

        for (int i = 0; i < graph.edges.Count; i++)
        {
            TrackEdge edge = graph.edges[i];
            if (edge == null)
            {
                continue;
            }

            if (edge.nodeAId == nodeId || edge.nodeBId == nodeId)
            {
                return true;
            }
        }

        return false;
    }

    private static void RebuildNodeConnections(TrackGraph graph)
    {
        if (graph == null || graph.nodes == null || graph.edges == null)
        {
            return;
        }

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            TrackNode node = graph.nodes[i];
            if (node != null)
            {
                node.connectedEdgeIds = new List<string>();
            }
        }

        for (int i = 0; i < graph.edges.Count; i++)
        {
            TrackEdge edge = graph.edges[i];
            if (edge == null || string.IsNullOrEmpty(edge.edgeId))
            {
                continue;
            }

            AddConnectedEdgeId(graph.FindNode(edge.nodeAId), edge.edgeId);
            AddConnectedEdgeId(graph.FindNode(edge.nodeBId), edge.edgeId);
        }
    }

    private static void AddConnectedEdgeId(TrackNode node, string edgeId)
    {
        if (node == null || string.IsNullOrEmpty(edgeId))
        {
            return;
        }

        node.connectedEdgeIds ??= new List<string>();
        if (!node.connectedEdgeIds.Contains(edgeId))
        {
            node.connectedEdgeIds.Add(edgeId);
        }
    }
}
