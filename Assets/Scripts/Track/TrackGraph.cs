using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "TrackGraph", menuName = "Train/Track Graph")]
public class TrackGraph : ScriptableObject
{
    public List<TrackNode> nodes = new();
    public List<TrackEdge> edges = new();
    public List<TurnoutState> turnoutStates = new();

    public TrackNode FindNode(string id) => nodes.Find(n => n.nodeId == id);
    public TrackEdge FindEdge(string id) => edges.Find(e => e.edgeId == id);
    public TurnoutState FindTurnoutState(string junctionId) => turnoutStates.Find(t => t.junctionId == junctionId);

    [Header("Generator Source")]
    [SerializeField, Min(0.001f)]
    private float nodeMergeDistanceM = 0.05f;
    [SerializeField]
    private bool generateReverseEdge = false;


    public float NodeMergeDistanceM => nodeMergeDistanceM;
    public bool GenerateReverseEdge => generateReverseEdge;



    private TrackNode FindOrCreateNode(Vector3 p, float mergeDist)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            TrackNode n = nodes[i];
            if (Vector3.Distance(n.worldPosition, p) <= mergeDist)
            {
                return n;
            }
        }

        // ノードが見つからない場合は新規作成
        var newNode = new TrackNode
        {
            nodeId = $"N{nodes.Count + 1:000}",
            worldPosition = p
        };
        nodes.Add(newNode);
        return newNode;
    }

    private void AddEdge(
        TrackNode fromNode,
        TrackNode toNode,
        float lengthM
        )
    {
        string edgeId = $"E{edges.Count + 1:000}";
        var edge = new TrackEdge
        {
            edgeId = edgeId,
            fromNodeId = fromNode.nodeId,
            toNodeId = toNode.nodeId,
            lengthM = lengthM,
            blockId = edgeId,
        };

        edge.mathCurves.Add(new TrackCurveData
        {
            trackCurveType = TrackCurveType.Straight,
            lengthM = lengthM,
            radiusM = 0f
        });

        edges.Add(edge);

        if (fromNode.outgoingEdgeIds == null)
        {
            fromNode.outgoingEdgeIds = new List<string>();
        }

        if (!fromNode.outgoingEdgeIds.Contains(edgeId))
        {
            fromNode.outgoingEdgeIds.Add(edgeId);
        }
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null)
        {
            return string.Empty;
        }

        string path = t.name;
        Transform current = t.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    public string ResolveNextEdgeId(string nodeId, string incomingEdgeId = null)
    {
        TrackNode node = FindNode(nodeId);
        if (node == null || node.outgoingEdgeIds == null || node.outgoingEdgeIds.Count == 0)
        {
            return null;
        }

        if (node.trackNodeType == TrackNodeType.Junction && !string.IsNullOrEmpty(node.junctionId))
        {
            TurnoutState state = FindTurnoutState(node.junctionId);
            if (state != null &&
                !string.IsNullOrEmpty(state.selectedOutgoingEdgeId) &&
                node.outgoingEdgeIds.Contains(state.selectedOutgoingEdgeId))
            {
                return state.selectedOutgoingEdgeId;
            }
        }

        return GetDefaultOutgoingEdgeId(node, incomingEdgeId);
    }

    public void SetTurnoutSelectedEdge(string junctionId, string edgeId)
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

        state.selectedOutgoingEdgeId = edgeId;
    }

    private string GetDefaultOutgoingEdgeId(TrackNode node, string incomingEdgeId)
    {
        if (node == null || node.outgoingEdgeIds == null || node.outgoingEdgeIds.Count == 0)
        {
            return null;
        }

        // incomingが不明なら先頭
        if (string.IsNullOrEmpty(incomingEdgeId))
        {
            return node.outgoingEdgeIds[0];
        }

        TrackEdge incomingEdge = FindEdge(incomingEdgeId);
        if (incomingEdge == null)
        {
            return node.outgoingEdgeIds[0];
        }

        string previousNodeId = incomingEdge.fromNodeId;

        // 可能なら直前ノードへ折り返すエッジを避ける
        for (int i = 0; i < node.outgoingEdgeIds.Count; i++)
        {
            string candidateId = node.outgoingEdgeIds[i];
            TrackEdge candidate = FindEdge(candidateId);
            if (candidate == null)
            {
                continue;
            }

            if (candidate.toNodeId != previousNodeId)
            {
                return candidateId;
            }
        }

        // 全候補が折り返しなら先頭を返す
        return node.outgoingEdgeIds[0];
    }

    public void UpdateNodeTypesAndJunctionIds()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            TrackNode node = nodes[i];
            int outCount = node.outgoingEdgeIds != null ? node.outgoingEdgeIds.Count : 0;

            if (outCount >= 2)
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

    public void SyncTurnoutStates()
    {
        // 既存stateは一旦マップ化
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

        // nodesから再生成
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

            // 選択edgeが不正ならデフォルトを入れる
            if (string.IsNullOrEmpty(state.selectedOutgoingEdgeId) ||
                node.outgoingEdgeIds == null ||
                !node.outgoingEdgeIds.Contains(state.selectedOutgoingEdgeId))
            {
                state.selectedOutgoingEdgeId = GetDefaultOutgoingEdgeId(node, null);
            }

            newStates.Add(state);
        }

        turnoutStates = newStates;
    }

}
