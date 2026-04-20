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

    public TrackNode FindNode(string id) =>
        string.IsNullOrEmpty(id) || nodes == null ? null : nodes.Find(n => n != null && n.nodeId == id);

    public TrackEdge FindEdge(string id) =>
        string.IsNullOrEmpty(id) || edges == null ? null : edges.Find(e => e != null && e.edgeId == id);

    public TurnoutState FindTurnoutState(string junctionId) =>
        string.IsNullOrEmpty(junctionId) || turnoutStates == null
            ? null
            : turnoutStates.Find(t => t != null && t.junctionId == junctionId);

    [Header("Generator Source")]
    [SerializeField, Min(0.001f)]
    private float nodeMergeDistanceM = 0.05f;
    [SerializeField]
    private bool generateReverseEdge = false;


    public float NodeMergeDistanceM => nodeMergeDistanceM;
    public bool GenerateReverseEdge => generateReverseEdge;

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
        ValidateNodeOutgoingEdges(errors, edgeById);
        ValidateTurnouts(errors, edgeById);

        return errors.Count == 0;
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

    public string ResolvePreviousEdgeId(string nodeId, string outgoingEdgeId = null)
    {
        if (string.IsNullOrEmpty(nodeId) || edges == null || edges.Count == 0)
        {
            return null;
        }

        string nextNodeId = null;
        if (!string.IsNullOrEmpty(outgoingEdgeId))
        {
            TrackEdge outgoingEdge = FindEdge(outgoingEdgeId);
            if (outgoingEdge != null)
            {
                nextNodeId = outgoingEdge.toNodeId;
            }
        }

        string fallbackEdgeId = null;
        for (int i = 0; i < edges.Count; i++)
        {
            TrackEdge candidate = edges[i];
            if (candidate == null || candidate.toNodeId != nodeId)
            {
                continue;
            }

            if (fallbackEdgeId == null)
            {
                fallbackEdgeId = candidate.edgeId;
            }

            if (string.IsNullOrEmpty(nextNodeId) || candidate.fromNodeId != nextNodeId)
            {
                return candidate.edgeId;
            }
        }

        return fallbackEdgeId;
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

        // If the incoming edge is unknown, use the first configured outgoing edge.
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

        // Prefer an edge that does not immediately send the train back to the previous node.
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

        // If every candidate loops back, fall back to the first option.
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
        // Snapshot existing turnout state objects so valid selections can be preserved across regeneration.
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

        // Rebuild the turnout list from the current node set.
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

            // If the stored selection is no longer valid, replace it with the default route.
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

            if (string.IsNullOrEmpty(edge.fromNodeId))
            {
                errors.Add($"Edge '{edge.edgeId}' has an empty fromNodeId.");
            }
            else if (!nodeById.ContainsKey(edge.fromNodeId))
            {
                errors.Add($"Edge '{edge.edgeId}' references missing fromNodeId '{edge.fromNodeId}'.");
            }

            if (string.IsNullOrEmpty(edge.toNodeId))
            {
                errors.Add($"Edge '{edge.edgeId}' has an empty toNodeId.");
            }
            else if (!nodeById.ContainsKey(edge.toNodeId))
            {
                errors.Add($"Edge '{edge.edgeId}' references missing toNodeId '{edge.toNodeId}'.");
            }

            if (nodeById.TryGetValue(edge.fromNodeId, out TrackNode fromNode))
            {
                if (fromNode.outgoingEdgeIds == null || !fromNode.outgoingEdgeIds.Contains(edge.edgeId))
                {
                    errors.Add($"Edge '{edge.edgeId}' is not listed in from node '{edge.fromNodeId}' outgoingEdgeIds.");
                }
            }

            ValidateEdgeCurveLengths(errors, edge);
        }
    }

    private void ValidateEdgeCurveLengths(List<string> errors, TrackEdge edge)
    {
        if (edge.mathCurves == null)
        {
            errors.Add($"Edge '{edge.edgeId}' has null mathCurves.");
            return;
        }

        if (edge.mathCurves.Count == 0)
        {
            if (edge.lengthM > EdgeLengthToleranceM)
            {
                errors.Add($"Edge '{edge.edgeId}' has lengthM {edge.lengthM:0.###} but no mathCurves.");
            }

            return;
        }

        float curveLengthSumM = 0f;
        for (int i = 0; i < edge.mathCurves.Count; i++)
        {
            TrackCurveData curve = edge.mathCurves[i];
            if (curve == null)
            {
                errors.Add($"Edge '{edge.edgeId}' mathCurves[{i}] is null.");
                continue;
            }

            if (curve.lengthM < 0f)
            {
                errors.Add($"Edge '{edge.edgeId}' mathCurves[{i}] has a negative lengthM ({curve.lengthM:0.###}).");
            }

            curveLengthSumM += curve.lengthM;
        }

        if (Mathf.Abs(edge.lengthM - curveLengthSumM) > EdgeLengthToleranceM)
        {
            errors.Add(
                $"Edge '{edge.edgeId}' lengthM ({edge.lengthM:0.###}) differs from mathCurves sum ({curveLengthSumM:0.###}) by more than {EdgeLengthToleranceM:0.###}m."
            );
        }
    }

    private void ValidateNodeOutgoingEdges(List<string> errors, Dictionary<string, TrackEdge> edgeById)
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

            if (node.outgoingEdgeIds == null)
            {
                errors.Add($"Node '{node.nodeId}' has null outgoingEdgeIds.");
                continue;
            }

            if (node.outgoingEdgeIds.Count >= 2 && node.trackNodeType != TrackNodeType.Junction)
            {
                errors.Add($"Node '{node.nodeId}' has {node.outgoingEdgeIds.Count} outgoing edges but is not marked as Junction.");
            }

            var outgoingIds = new HashSet<string>();
            for (int j = 0; j < node.outgoingEdgeIds.Count; j++)
            {
                string outgoingEdgeId = node.outgoingEdgeIds[j];
                if (string.IsNullOrEmpty(outgoingEdgeId))
                {
                    errors.Add($"Node '{node.nodeId}' outgoingEdgeIds[{j}] is empty.");
                    continue;
                }

                if (!outgoingIds.Add(outgoingEdgeId))
                {
                    errors.Add($"Node '{node.nodeId}' has duplicate outgoing edge '{outgoingEdgeId}'.");
                }

                if (!edgeById.TryGetValue(outgoingEdgeId, out TrackEdge outgoingEdge))
                {
                    errors.Add($"Node '{node.nodeId}' outgoingEdgeIds[{j}] references missing edge '{outgoingEdgeId}'.");
                    continue;
                }

                if (outgoingEdge.fromNodeId != node.nodeId)
                {
                    errors.Add(
                        $"Node '{node.nodeId}' outgoing edge '{outgoingEdgeId}' starts from '{outgoingEdge.fromNodeId}', not this node."
                    );
                }
            }
        }
    }

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

                if (!string.IsNullOrEmpty(state.selectedOutgoingEdgeId) &&
                    !edgeById.ContainsKey(state.selectedOutgoingEdgeId))
                {
                    errors.Add(
                        $"TurnoutState '{state.junctionId}' selects missing edge '{state.selectedOutgoingEdgeId}'."
                    );
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

                int outgoingCount = node.outgoingEdgeIds != null ? node.outgoingEdgeIds.Count : 0;
                if (outgoingCount < 2)
                {
                    errors.Add($"Junction node '{node.nodeId}' has fewer than 2 outgoing edges.");
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

                if (string.IsNullOrEmpty(state.selectedOutgoingEdgeId))
                {
                    errors.Add($"TurnoutState '{state.junctionId}' has an empty selectedOutgoingEdgeId.");
                    continue;
                }

                if (node.outgoingEdgeIds == null || !node.outgoingEdgeIds.Contains(state.selectedOutgoingEdgeId))
                {
                    errors.Add(
                        $"TurnoutState '{state.junctionId}' selects edge '{state.selectedOutgoingEdgeId}' that is not outgoing from node '{node.nodeId}'."
                    );
                }
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
}
