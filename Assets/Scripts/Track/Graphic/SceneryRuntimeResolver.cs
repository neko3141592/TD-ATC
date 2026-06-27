using UnityEngine;

public static class SceneryRuntimeResolver
{
    private const float minMagnitude = 0.001f;
    private static readonly TrackRuntimeResolver resolver = new TrackRuntimeResolver();

    public static bool TryResolveFrame(
        TrackGraph graph,
        SceneryAnchor anchor,
        float distanceM, 
        out SceneryFrame frame
    )
    {

        Vector3 currentPosition = Vector3.zero;
        Vector3 currentTangent = Vector3.forward;

        frame = new SceneryFrame
        {
            position = currentPosition,
            forward = currentTangent,
            right = Vector3.right,
            up = Vector3.up,
            rotation = Quaternion.identity
        };

        if (graph == null || anchor == null)
        {
            return false;
        }   

        if (anchor.kind == SceneryAnchorKind.Geometry)
        {
            TrackGeometry geometry = graph.FindGeometry(anchor.id);
            if (geometry == null)
            {
                return false;
            }

            float resolvedDistanceM = ResolveDistance(anchor, distanceM);
            if (!resolver.TryResolveGeometryPose(graph, geometry.geometryId, resolvedDistanceM, out var position, out var tangent, out _))
            {
                return false;
            }

            currentPosition = position;
            currentTangent = tangent;
        } 
        else
        {
            TrackEdge edge = graph.FindEdge(anchor.id);
            if (edge == null)
            {
                return false;
            }

            float resolvedDistanceM = ResolveDistance(anchor, distanceM);
            if (!resolver.TryResolvePose(graph, edge.edgeId, resolvedDistanceM, out var position, out var tangent, out _))
            {
                return false;
            }
            currentPosition = position;
            currentTangent = tangent;
        }

        frame = FromTrackPose(currentPosition, currentTangent);
        return true;
    }

    private static float ResolveDistance(SceneryAnchor anchor, float distanceM)
    {
        if (anchor == null || anchor.endDistanceM <= anchor.startDistanceM)
        {
            return distanceM;
        }

        return Mathf.Clamp(distanceM, anchor.startDistanceM, anchor.endDistanceM);
    }
    
    public static SceneryFrame FromTrackPose(Vector3 position, Vector3 tangent)
    {

        // tangentが小さい場合には、ワールドforwardを使う
        Vector3 forward = tangent.sqrMagnitude > minMagnitude
        ? tangent.normalized
        : Vector3.forward;

        Vector3 up = Vector3.up;
        Vector3 right = Vector3.Cross(up, forward);

        if (right.sqrMagnitude < minMagnitude)
        {
            right = Vector3.right;
        } 
        else
        {
            right = right.normalized;
        }

        return new SceneryFrame
        {
            position = position,
            forward = forward,
            right = right,
            up = up,
            rotation = Quaternion.LookRotation(forward, up)
        };
    }
}
