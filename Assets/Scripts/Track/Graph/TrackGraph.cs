using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TrackGraph", menuName = "Train/Track Graph")]
public class TrackGraph : ScriptableObject
{
    private const float EdgeLengthToleranceM = 0.05f;

    public List<TrackNode> nodes = new();
    public List<TrackEdge> edges = new();
    public List<TurnoutState> turnoutStates = new();
    public List<StationData> stations = new();

    public List<TurnoutConnection> turnoutConnections = new();

    public List<TrackGeometry> geometries = new();

    /// <summary>
    /// 役割: FindNode の処理を検索します。
    /// </summary>
    /// <param name="id">id を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    public TrackNode FindNode(string id) =>
        string.IsNullOrEmpty(id) || nodes == null ? null : nodes.Find(n => n != null && n.nodeId == id);

    /// <summary>
    /// 役割: FindEdge の処理を検索します。
    /// </summary>
    /// <param name="id">id を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    public TrackEdge FindEdge(string id) =>
        string.IsNullOrEmpty(id) || edges == null ? null : edges.Find(e => e != null && e.edgeId == id);

    /// <summary>
    /// 役割: FindTurnoutState の処理を検索します。
    /// </summary>
    /// <param name="junctionId">junctionId を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    public TurnoutState FindTurnoutState(string junctionId) =>
        string.IsNullOrEmpty(junctionId) || turnoutStates == null
            ? null
            : turnoutStates.Find(t => t != null && t.junctionId == junctionId);

    public TurnoutConnection FindTurnoutConnection(string connectionId) => 
        string.IsNullOrEmpty(connectionId) || turnoutConnections == null
            ? null
            : turnoutConnections.Find(t => t != null && t.connectionId == connectionId);

    public TrackGeometry FindGeometry(string id) =>
    string.IsNullOrEmpty(id) || geometries == null
        ? null
        : geometries.Find(g => g != null && g.geometryId == id);
    

    [Header("Generator Source")]
    [SerializeField, Min(0.001f)]
    private float nodeMergeDistanceM = 0.05f;


    public float NodeMergeDistanceM => nodeMergeDistanceM;

    /// <summary>
    /// 役割: ValidateGraph の処理を検証します。
    /// </summary>
    /// <param name="errors">errors を指定します。</param>
    /// <returns>処理が成功した場合は true、それ以外は false を返します。</returns>
    public bool ValidateGraph(List<string> errors)
    {
        if (errors == null)
        {
            return false;
        }

        errors.Clear();

        var nodeIds = new HashSet<string>();
        var nodeById = new Dictionary<string, TrackNode>();
        if (nodes == null)
        {
            errors.Add("TrackGraph.nodes is null.");
        }
        else
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TrackNode node = nodes[i];
                if (node == null)
                {
                    errors.Add($"nodes[{i}] is null.");
                    continue;
                }

                if (string.IsNullOrEmpty(node.nodeId))
                {
                    errors.Add($"nodes[{i}] has an empty nodeId.");
                    continue;
                }

                if (!nodeIds.Add(node.nodeId))
                {
                    errors.Add($"Duplicate nodeId '{node.nodeId}'.");
                    continue;
                }

                nodeById.Add(node.nodeId, node);
            }
        }

        var edgeIds = new HashSet<string>();
        var edgeById = new Dictionary<string, TrackEdge>();
        if (edges == null)
        {
            errors.Add("TrackGraph.edges is null.");
        }
        else
        {
            for (int i = 0; i < edges.Count; i++)
            {
                TrackEdge edge = edges[i];
                if (edge == null)
                {
                    errors.Add($"edges[{i}] is null.");
                    continue;
                }

                if (string.IsNullOrEmpty(edge.edgeId))
                {
                    errors.Add($"edges[{i}] has an empty edgeId.");
                    continue;
                }

                if (!edgeIds.Add(edge.edgeId))
                {
                    errors.Add($"Duplicate edgeId '{edge.edgeId}'.");
                    continue;
                }

                edgeById.Add(edge.edgeId, edge);
            }
        }

        ValidateEdges(errors, nodeById);
        ValidateNodeConnections(errors, edgeById);
        ValidateTurnouts(errors, edgeById);

        return errors.Count == 0;
    }

    /// <summary>
    /// 役割: ResolveNextEdgeId の処理を解決します。
    /// </summary>
    /// <param name="nodeId">nodeId を指定します。</param>
    /// <param name="incomingEdgeId">incomingEdgeId を指定します。</param>
    /// <returns>文字列結果を返します。</returns>
    public string ResolveNextEdgeId(string nodeId, string incomingEdgeId = null)
    {
        return ResolveConnectedEdgeId(nodeId, incomingEdgeId);
    }

    public string ResolveConnectedEdgeId(string nodeId, string incomingEdgeId = null)
    {
        return TrackGraphUndirectedHelpers.ResolveConnectedEdge(this, nodeId, incomingEdgeId);
    }

    /// <summary>
    /// 役割: ResolvePreviousEdgeId の処理を解決します。
    /// </summary>
    /// <param name="nodeId">nodeId を指定します。</param>
    /// <param name="outgoingEdgeId">outgoingEdgeId を指定します。</param>
    /// <returns>文字列結果を返します。</returns>
    public string ResolvePreviousEdgeId(string nodeId, string outgoingEdgeId = null)
    {
        return ResolveConnectedEdgeId(nodeId, outgoingEdgeId);
    }

    /// <summary>
    /// 役割: 分岐器の通常位と反位の接続を設定します。
    /// </summary>
    /// <param name="junctionId">junctionId を指定します。</param>
    /// <param name="normalConnectionId">通常位で使う接続 ID を指定します。</param>
    /// <param name="reverseConnectionId">反位で使う接続 ID を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void SetTurnoutConnections(
        string junctionId,
        string normalConnectionId,
        string reverseConnectionId,
        TurnoutPosition selectedPosition = TurnoutPosition.Normal)
    {
        if (string.IsNullOrEmpty(junctionId))
        {
            return;
        }

        TurnoutState state = FindTurnoutState(junctionId);
        if (state == null)
        {
            state = new TurnoutState { junctionId = junctionId };
            turnoutStates.Add(state);
        }

        state.normalConnectionId = normalConnectionId;
        state.reverseConnectionId = reverseConnectionId;
        state.selectedPosition = selectedPosition;
    }

    /// <summary>
    /// 役割: UpdateNodeTypesAndJunctionIds の処理を更新します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    public void UpdateNodeTypesAndJunctionIds()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            TrackNode node = nodes[i];
            int connectedCount = node.connectedEdgeIds != null ? node.connectedEdgeIds.Count : 0;

            if (connectedCount >= 3)
            {
                node.trackNodeType = TrackNodeType.Junction;
                if (string.IsNullOrEmpty(node.junctionId))
                {
                    node.junctionId = node.nodeId;
                }
            }
            else if (node.trackNodeType == TrackNodeType.Junction)
            {
                node.trackNodeType = TrackNodeType.Normal;
                node.junctionId = string.Empty;
            }
        }
    }

    /// <summary>
    /// 役割: SyncTurnoutStates の処理を同期します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    public void SyncTurnoutStates()
    {
        // 再生成後も有効な分岐選択を引き継げるよう、既存の分岐状態を一度退避します。
        var stateByJunction = new Dictionary<string, TurnoutState>();
        for (int i = 0; i < turnoutStates.Count; i++)
        {
            TurnoutState state = turnoutStates[i];
            if (state == null || string.IsNullOrEmpty(state.junctionId))
            {
                continue;
            }

            if (!stateByJunction.ContainsKey(state.junctionId))
            {
                stateByJunction.Add(state.junctionId, state);
            }
        }

        // 現在のノード構成から分岐状態リストを組み直します。
        var newStates = new List<TurnoutState>();
        for (int i = 0; i < nodes.Count; i++)
        {
            TrackNode node = nodes[i];
            if (node.trackNodeType != TrackNodeType.Junction || string.IsNullOrEmpty(node.junctionId))
            {
                continue;
            }

            TurnoutState state;
            if (!stateByJunction.TryGetValue(node.junctionId, out state) || state == null)
            {
                state = new TurnoutState { junctionId = node.junctionId };
            }

            newStates.Add(state);
        }

        turnoutStates = newStates;
    }

    /// <summary>
    /// 役割: ValidateEdges の処理を検証します。
    /// </summary>
    /// <param name="errors">errors を指定します。</param>
    /// <param name="nodeById">nodeById を指定します。</param>
    /// <remarks>返り値はありません。</remarks>

    private void ValidateEdges(
        List<string> errors,
        Dictionary<string, TrackNode> nodeById
    )
    {
        if (edges == null)
        {
            return;
        }

        for (int i = 0; i < edges.Count; i++)
        {
            TrackEdge edge = edges[i];
            if (edge == null || string.IsNullOrEmpty(edge.edgeId))
            {
                continue;
            }

            if (edge.lengthM < 0f)
            {
                errors.Add($"Edge '{edge.edgeId}' has a negative lengthM ({edge.lengthM:0.###}).");
            }

            if (string.IsNullOrEmpty(edge.nodeAId))
            {
                errors.Add($"Edge '{edge.edgeId}' has an empty nodeAId.");
            }
            else if (!nodeById.ContainsKey(edge.nodeAId))
            {
                errors.Add($"Edge '{edge.edgeId}' references missing nodeAId '{edge.nodeAId}'.");
            }

            if (string.IsNullOrEmpty(edge.nodeBId))
            {
                errors.Add($"Edge '{edge.edgeId}' has an empty nodeBId.");
            }
            else if (!nodeById.ContainsKey(edge.nodeBId))
            {
                errors.Add($"Edge '{edge.edgeId}' references missing nodeBId '{edge.nodeBId}'.");
            }

            if (nodeById.TryGetValue(edge.nodeAId, out TrackNode nodeA))
            {
                if (nodeA.connectedEdgeIds == null || !nodeA.connectedEdgeIds.Contains(edge.edgeId))
                {
                    errors.Add($"Edge '{edge.edgeId}' is not listed in nodeA '{edge.nodeAId}' connectedEdgeIds.");
                }
            }

            if (nodeById.TryGetValue(edge.nodeBId, out TrackNode nodeB))
            {
                if (nodeB.connectedEdgeIds == null || !nodeB.connectedEdgeIds.Contains(edge.edgeId))
                {
                    errors.Add($"Edge '{edge.edgeId}' is not listed in nodeB '{edge.nodeBId}' connectedEdgeIds.");
                }
            }

            ValidateEdgeProfiles(errors, edge, nodeById);
        }
    }

    /// <summary>
    /// 役割: ValidateEdgeProfiles の処理を検証します。
    /// </summary>
    /// <param name="errors">errors を指定します。</param>
    /// <param name="edge">edge を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ValidateEdgeProfiles(List<string> errors, TrackEdge edge, Dictionary<string, TrackNode> nodeById)
    {
        ValidateOffsetEdge(errors, edge);
    }

    private void ValidateOffsetEdge(List<string> errors, TrackEdge edge)
    {
        if (!IsOffsetEdge(edge))
        {
            errors.Add($"Edge '{edge.edgeId}' must reference a baseGeometryId.");
            return;
        }

        TrackGeometry baseGeometry = FindGeometry(edge.baseGeometryId);
        if (baseGeometry == null)
        {
            errors.Add($"Offset edge '{edge.edgeId}' references missing baseGeometryId '{edge.baseGeometryId}'.");
        }

        if (edge.offsetSegments == null)
        {
            errors.Add($"Offset edge '{edge.edgeId}' has null offsetSegments.");
        }
        else if (edge.offsetSegments.Count == 0)
        {
            errors.Add($"Offset edge '{edge.edgeId}' has no offsetSegments.");
        }

        if (edge.offsetDistanceMap == null)
        {
            errors.Add($"Offset edge '{edge.edgeId}' has null offsetDistanceMap.");
            return;
        }

        float offsetLengthM = edge.offsetDistanceMap.OffsetLengthM;
        if (offsetLengthM <= 0f)
        {
            errors.Add($"Offset edge '{edge.edgeId}' has an empty offsetDistanceMap.");
            return;
        }

        if (Mathf.Abs(edge.lengthM - offsetLengthM) > EdgeLengthToleranceM)
        {
            errors.Add(
                $"Offset edge '{edge.edgeId}' lengthM ({edge.lengthM:0.###}) differs from offsetDistanceMap length ({offsetLengthM:0.###}) by more than {EdgeLengthToleranceM:0.###}m."
            );
        }
    }

    private static bool IsOffsetEdge(TrackEdge edge)
    {
        return edge != null && !string.IsNullOrEmpty(edge.baseGeometryId);
    }

    /// <summary>
    /// 役割: ValidateNodeConnections の処理を検証します。
    /// </summary>
    /// <param name="errors">errors を指定します。</param>
    /// <param name="edgeById">edgeById を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ValidateNodeConnections(List<string> errors, Dictionary<string, TrackEdge> edgeById)
    {
        if (nodes == null)
        {
            return;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            TrackNode node = nodes[i];
            if (node == null || string.IsNullOrEmpty(node.nodeId))
            {
                continue;
            }

            if (node.connectedEdgeIds == null)
            {
                errors.Add($"Node '{node.nodeId}' has null connectedEdgeIds.");
                continue;
            }

            if (node.connectedEdgeIds.Count >= 3 && node.trackNodeType != TrackNodeType.Junction)
            {
                errors.Add($"Node '{node.nodeId}' has {node.connectedEdgeIds.Count} connected edges but is not marked as Junction.");
            }

            var connectedIds = new HashSet<string>();
            for (int j = 0; j < node.connectedEdgeIds.Count; j++)
            {
                string connectedEdgeId = node.connectedEdgeIds[j];
                if (string.IsNullOrEmpty(connectedEdgeId))
                {
                    errors.Add($"Node '{node.nodeId}' connectedEdgeIds[{j}] is empty.");
                    continue;
                }

                if (!connectedIds.Add(connectedEdgeId))
                {
                    errors.Add($"Node '{node.nodeId}' has duplicate connected edge '{connectedEdgeId}'.");
                }

                if (!edgeById.TryGetValue(connectedEdgeId, out TrackEdge connectedEdge))
                {
                    errors.Add($"Node '{node.nodeId}' connectedEdgeIds[{j}] references missing edge '{connectedEdgeId}'.");
                    continue;
                }

                if (!TrackGraphUndirectedHelpers.IsEdgeConnectedToNode(connectedEdge, node.nodeId))
                {
                    errors.Add(
                        $"Node '{node.nodeId}' connected edge '{connectedEdgeId}' does not connect to this node."
                    );
                }
            }
        }
    }

    /// <summary>
    /// 役割: ValidateTurnouts の処理を検証します。
    /// </summary>
    /// <param name="errors">errors を指定します。</param>
    /// <param name="edgeById">edgeById を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ValidateTurnouts(List<string> errors, Dictionary<string, TrackEdge> edgeById)
    {
        var stateByJunction = new Dictionary<string, TurnoutState>();
        if (turnoutStates != null)
        {
            for (int i = 0; i < turnoutStates.Count; i++)
            {
                TurnoutState state = turnoutStates[i];
                if (state == null)
                {
                    errors.Add($"turnoutStates[{i}] is null.");
                    continue;
                }

                if (string.IsNullOrEmpty(state.junctionId))
                {
                    errors.Add($"turnoutStates[{i}] has an empty junctionId.");
                    continue;
                }

                if (!stateByJunction.ContainsKey(state.junctionId))
                {
                    stateByJunction.Add(state.junctionId, state);
                }
                else
                {
                    errors.Add($"Duplicate TurnoutState for junctionId '{state.junctionId}'.");
                }

            }
        }

        var junctionIds = new HashSet<string>();
        if (nodes != null)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TrackNode node = nodes[i];
                if (node == null || node.trackNodeType != TrackNodeType.Junction)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(node.junctionId))
                {
                    errors.Add($"Junction node '{node.nodeId}' has an empty junctionId.");
                    continue;
                }

                junctionIds.Add(node.junctionId);

                int connectedCount = node.connectedEdgeIds != null ? node.connectedEdgeIds.Count : 0;
                if (connectedCount < 3)
                {
                    errors.Add($"Junction node '{node.nodeId}' has fewer than 3 connected edges.");
                }

                if (turnoutStates == null)
                {
                    errors.Add($"Junction node '{node.nodeId}' has no turnoutStates list to resolve junctionId '{node.junctionId}'.");
                    continue;
                }

                if (!stateByJunction.TryGetValue(node.junctionId, out TurnoutState state))
                {
                    errors.Add($"Junction node '{node.nodeId}' is missing TurnoutState for junctionId '{node.junctionId}'.");
                    continue;
                }

                ValidateTurnoutConnectionId(errors, edgeById, node, state.normalConnectionId, $"{state.junctionId}.normalConnectionId");
                ValidateTurnoutConnectionId(errors, edgeById, node, state.reverseConnectionId, $"{state.junctionId}.reverseConnectionId");
            }
        }

        foreach (string junctionId in stateByJunction.Keys)
        {
            if (!junctionIds.Contains(junctionId))
            {
                errors.Add($"TurnoutState '{junctionId}' does not match any Junction node.");
            }
        }
    }

    private void ValidateTurnoutConnectionId(
        List<string> errors,
        Dictionary<string, TrackEdge> edgeById,
        TrackNode node,
        string connectionId,
        string label)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            return;
        }

        TurnoutConnection connection = FindTurnoutConnection(connectionId);
        if (connection == null)
        {
            errors.Add($"Turnout connection '{label}' references missing connection '{connectionId}'.");
            return;
        }

        if (connection.nodeId != node.nodeId)
        {
            errors.Add($"Turnout connection '{connectionId}' belongs to node '{connection.nodeId}', not '{node.nodeId}'.");
        }

        ValidateTurnoutConnectionEdge(errors, edgeById, node, connection.edgeAId, connectionId, "edgeAId");
        ValidateTurnoutConnectionEdge(errors, edgeById, node, connection.edgeBId, connectionId, "edgeBId");
    }

    private void ValidateTurnoutConnectionEdge(
        List<string> errors,
        Dictionary<string, TrackEdge> edgeById,
        TrackNode node,
        string edgeId,
        string connectionId,
        string fieldName)
    {
        if (string.IsNullOrEmpty(edgeId))
        {
            errors.Add($"Turnout connection '{connectionId}' has empty {fieldName}.");
            return;
        }

        if (!edgeById.TryGetValue(edgeId, out TrackEdge edge))
        {
            errors.Add($"Turnout connection '{connectionId}' {fieldName} references missing edge '{edgeId}'.");
            return;
        }

        if (!TrackGraphUndirectedHelpers.IsEdgeConnectedToNode(edge, node.nodeId))
        {
            errors.Add($"Turnout connection '{connectionId}' {fieldName} edge '{edgeId}' does not connect to node '{node.nodeId}'.");
        }
    }
}
