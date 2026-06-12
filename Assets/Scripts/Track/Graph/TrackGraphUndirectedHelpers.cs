using System.Collections.Generic;
using UnityEngine;

public static class TrackGraphUndirectedHelpers
{
    public static string GetNodeAId(TrackEdge edge)
    {
        if (edge == null)
        {
            return null;
        }

        return edge.nodeAId;
    }

    public static string GetNodeBId(TrackEdge edge)
    {
        if (edge == null)
        {
            return null;
        }

        return edge.nodeBId;
    }

    public static string GetOtherNodeId(TrackEdge edge, string nodeId)
    {
        string nodeAId = GetNodeAId(edge);
        string nodeBId = GetNodeBId(edge);

        if (nodeId == nodeAId)
        {
            return nodeBId;
        }

        if (nodeId == nodeBId)
        {
            return nodeAId;
        }

        return null;
    }

    public static bool IsEdgeConnectedToNode(TrackEdge edge, string nodeId)
    {
        if (edge == null || string.IsNullOrEmpty(nodeId))
        {
            return false;
        }

        return GetNodeAId(edge) == nodeId || GetNodeBId(edge) == nodeId;
    }

    public static List<string> GetConnectedEdgeIds(TrackNode node)
    {
        if (node == null)
        {
            return null;
        }

        return node.connectedEdgeIds;
    }

    public static float GetEntryDistanceOnEdge(TrackEdge edge, string entryNodeId)
    {
        if (edge == null)
        {
            return 0f;
        }

        if (entryNodeId == GetNodeAId(edge))
        {
            return 0f;
        }

        if (entryNodeId == GetNodeBId(edge))
        {
            return Mathf.Max(edge.lengthM, 0f);
        }

        return 0f;
    }

    public static EdgeTravelDirection GetTravelDirectionFromNode(TrackEdge edge, string entryNodeId)
    {
        if (edge == null)
        {
            return EdgeTravelDirection.AtoB;
        }

        if (entryNodeId == GetNodeAId(edge))
        {
            return EdgeTravelDirection.AtoB;
        }

        if (entryNodeId == GetNodeBId(edge))
        {
            return EdgeTravelDirection.BtoA;
        }

        return EdgeTravelDirection.AtoB;
    }

    public static EdgeTravelDirection GetOppositeDirection(EdgeTravelDirection direction)
    {
        return direction == EdgeTravelDirection.AtoB
            ? EdgeTravelDirection.BtoA
            : EdgeTravelDirection.AtoB;
    }

    public static string GetExitNodeId(TrackEdge edge, EdgeTravelDirection direction)
    {
        return direction == EdgeTravelDirection.AtoB
            ? GetNodeBId(edge)
            : GetNodeAId(edge);
    }

    public static float GetDistanceToExit(TrackEdge edge, float distanceOnEdgeM, EdgeTravelDirection direction)
    {
        float edgeLengthM = edge != null ? Mathf.Max(0f, edge.lengthM) : 0f;
        return direction == EdgeTravelDirection.AtoB
            ? Mathf.Max(0f, edgeLengthM - distanceOnEdgeM)
            : Mathf.Max(0f, distanceOnEdgeM);
    }

    public static bool HasReachedExit(TrackEdge edge, float distanceOnEdgeM, EdgeTravelDirection direction)
    {
        float edgeLengthM = edge != null ? Mathf.Max(0f, edge.lengthM) : 0f;
        return direction == EdgeTravelDirection.AtoB
            ? distanceOnEdgeM > edgeLengthM
            : distanceOnEdgeM < 0f;
    }

    public static float GetOvershootDistance(TrackEdge edge, float distanceOnEdgeM, EdgeTravelDirection direction)
    {
        float edgeLengthM = edge != null ? Mathf.Max(0f, edge.lengthM) : 0f;
        return direction == EdgeTravelDirection.AtoB
            ? Mathf.Max(0f, distanceOnEdgeM - edgeLengthM)
            : Mathf.Max(0f, -distanceOnEdgeM);
    }

    public static float ClampDistanceAtExit(TrackEdge edge, EdgeTravelDirection direction)
    {
        return direction == EdgeTravelDirection.AtoB
            ? (edge != null ? Mathf.Max(0f, edge.lengthM) : 0f)
            : 0f;
    }

    public static string ResolveConnectedEdge(TrackGraph graph, string nodeId, string incomingEdgeId)
    {
        if (graph == null || string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(incomingEdgeId))
        {
            return null;
        }

        TrackNode node = graph.FindNode(nodeId);
        if (node == null)
        {
            return null;
        }

        if (node.trackNodeType == TrackNodeType.Junction)
        {
            return ResolveTurnoutConnection(graph, nodeId, incomingEdgeId);
        }

        string connectedEdgeId = FindConnectedEdgeFromNodeList(graph, node, nodeId, incomingEdgeId);
        if (!string.IsNullOrEmpty(connectedEdgeId))
        {
            return connectedEdgeId;
        }

        return FindConnectedEdgeByScanningGraph(graph, nodeId, incomingEdgeId);
    }

    private static string FindConnectedEdgeFromNodeList(
        TrackGraph graph,
        TrackNode node,
        string nodeId,
        string incomingEdgeId)
    {
        List<string> connectedEdgeIds = GetConnectedEdgeIds(node);
        if (connectedEdgeIds == null)
        {
            return null;
        }

        for (int i = 0; i < connectedEdgeIds.Count; i++)
        {
            string edgeId = connectedEdgeIds[i];
            if (string.IsNullOrEmpty(edgeId) || edgeId == incomingEdgeId)
            {
                continue;
            }

            TrackEdge edge = graph.FindEdge(edgeId);
            if (IsEdgeConnectedToNode(edge, nodeId))
            {
                return edgeId;
            }
        }

        return null;
    }

    private static string FindConnectedEdgeByScanningGraph(TrackGraph graph, string nodeId, string incomingEdgeId)
    {
        if (graph.edges == null)
        {
            return null;
        }

        for (int i = 0; i < graph.edges.Count; i++)
        {
            TrackEdge edge = graph.edges[i];
            if (edge == null || edge.edgeId == incomingEdgeId)
            {
                continue;
            }

            if (IsEdgeConnectedToNode(edge, nodeId))
            {
                return edge.edgeId;
            }
        }

        return null;
    }

    public static string ResolveTurnoutConnection(TrackGraph graph, string nodeId, string incomingEdgeId)
    {
        if (graph == null)
        {
            return null;
        }

        TrackNode currentNode = graph.FindNode(nodeId);

        if (currentNode == null || currentNode.trackNodeType != TrackNodeType.Junction)
        {
            return null;
        }

        string junctionId = string.IsNullOrEmpty(currentNode.junctionId) ? nodeId : currentNode.junctionId;
        TurnoutState turnoutState = graph.FindTurnoutState(junctionId);
        if (turnoutState == null || string.IsNullOrEmpty(turnoutState.ActiveConnectionId))
        {
            return null;
        }

        TurnoutConnection connection = graph.FindTurnoutConnection(turnoutState.ActiveConnectionId);

        if (connection == null ||
        string.IsNullOrEmpty(connection.edgeAId) ||
        string.IsNullOrEmpty(connection.edgeBId))
        {
            return null;
        }

        if (incomingEdgeId != connection.edgeAId && incomingEdgeId != connection.edgeBId)
        {
            return null;
        }

        if (connection.edgeAId == incomingEdgeId)
        {
            return connection.edgeBId;
        }

        if (connection.edgeBId == incomingEdgeId)
        {
            return connection.edgeAId;
        }

        return null;
    }
}
