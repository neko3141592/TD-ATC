using System.Collections.Generic;
using UnityEngine;

public class TrackRuntimeResolver
{
    private const float OffsetTangentSampleDistanceM = 0.25f;

    public static void CalculateStraight(float lengthM, out float x, out float z, out float angleDegree)
    {
        x = 0f;
        z = lengthM;
        angleDegree = 0f;
    }

    public static void CalculateCircularCurve(float lengthM, float radiusM, out float x, out float z, out float angleDegree)
    {
        if (Mathf.Abs(radiusM) < 0.001f)
        {
            CalculateStraight(lengthM, out x, out z, out angleDegree);
            return;
        }

        float theta = lengthM / radiusM;
        x = radiusM * (1f - Mathf.Cos(theta));
        z = radiusM * Mathf.Sin(theta);
        angleDegree = theta * Mathf.Rad2Deg;
    }

    public static void CalculateCubicTransitionIn(
        float lengthM,
        float totalLengthM,
        float radiusM,
        out float x,
        out float z,
        out float angleDegree)
    {
        if (Mathf.Abs(radiusM) < 0.001f || totalLengthM < 0.001f)
        {
            CalculateStraight(lengthM, out x, out z, out angleDegree);
            return;
        }

        float theta = (lengthM * lengthM) / (2f * totalLengthM * radiusM);
        x = (lengthM * lengthM * lengthM) / (6f * totalLengthM * radiusM);
        z = lengthM;
        angleDegree = theta * Mathf.Rad2Deg;
    }

    public static void CalculateCubicTransitionOut(
        float lengthM,
        float totalLengthM,
        float radiusM,
        out float x,
        out float z,
        out float angleDegree)
    {
        if (Mathf.Abs(radiusM) < 0.001f || totalLengthM < 0.001f)
        {
            CalculateStraight(lengthM, out x, out z, out angleDegree);
            return;
        }

        float theta = lengthM / radiusM - (lengthM * lengthM) / (2f * radiusM * totalLengthM);
        x = (lengthM * lengthM) / (2f * radiusM) - (lengthM * lengthM * lengthM) / (6f * radiusM * totalLengthM);
        z = lengthM;
        angleDegree = theta * Mathf.Rad2Deg;
    }

    public bool TryResolvePose(
        TrackGraph graph,
        string edgeId,
        float distanceOnEdgeM,
        out Vector3 position,
        out Vector3 tangent)
    {
        return TryResolvePose(graph, edgeId, distanceOnEdgeM, out position, out tangent, out _);
    }

    public bool TryResolvePose(
        TrackGraph graph,
        string edgeId,
        float distanceOnEdgeM,
        out Vector3 position,
        out Vector3 tangent,
        out Quaternion rotation)
    {
        position = Vector3.zero;
        tangent = Vector3.forward;
        rotation = Quaternion.identity;

        if (graph == null || string.IsNullOrEmpty(edgeId))
        {
            return false;
        }

        TrackEdge edge = graph.FindEdge(edgeId);
        return TryResolveOffsetEdgePose(graph, edge, distanceOnEdgeM, out position, out tangent, out rotation);
    }

    public bool TryResolveGeometryPose(
        TrackGraph graph,
        string geometryId,
        float distanceM,
        out Vector3 position,
        out Vector3 tangent,
        out Quaternion rotation)
    {
        position = Vector3.zero;
        tangent = Vector3.forward;
        rotation = Quaternion.identity;

        if (graph == null || string.IsNullOrEmpty(geometryId))
        {
            return false;
        }

        TrackGeometry geometry = graph.FindGeometry(geometryId);
        if (geometry == null)
        {
            return false;
        }

        float clampedDistanceM = Mathf.Clamp(distanceM, 0f, Mathf.Max(0f, geometry.lengthM));
        return TryResolveNativeGeometryPose(
            geometry,
            clampedDistanceM,
            out position,
            out tangent,
            out rotation
        );
    }

    public bool TryGetGradientPermille(
        TrackGraph graph,
        string edgeId,
        float distanceOnEdgeM,
        out float gradientPermille)
    {
        gradientPermille = 0f;

        if (!TryGetOffsetEdgeBaseGeometryAndDistance(graph, edgeId, distanceOnEdgeM, out TrackGeometry baseGeometry, out float baseDistanceM))
        {
            return false;
        }

        gradientPermille = TrackGradientUtility.GetGradientPermilleAt(baseGeometry.verticalSegments, baseDistanceM);
        return true;
    }

    public bool TryGetCantMm(
        TrackGraph graph,
        string edgeId,
        float distanceOnEdgeM,
        out float cantMm)
    {
        cantMm = 0f;

        if (!TryGetOffsetEdgeBaseGeometryAndDistance(graph, edgeId, distanceOnEdgeM, out TrackGeometry baseGeometry, out float baseDistanceM))
        {
            return false;
        }

        cantMm = TrackGradientUtility.GetCantMmAt(baseGeometry.cantSegments, baseDistanceM);
        return true;
    }

    public bool TryResolveOffsetEdgePose(
        TrackGraph graph,
        TrackEdge edge,
        float distanceOnEdgeM,
        out Vector3 position,
        out Vector3 tangent,
        out Quaternion rotation)
    {
        position = Vector3.zero;
        tangent = Vector3.forward;
        rotation = Quaternion.identity;

        if (!IsOffsetEdge(edge))
        {
            return false;
        }

        float edgeLengthM = Mathf.Max(0f, edge.lengthM);
        float clampedDistanceM = Mathf.Clamp(distanceOnEdgeM, 0f, edgeLengthM);

        if (!TryResolveOffsetEdgePosition(graph, edge, clampedDistanceM, out position, out Quaternion baseRotation))
        {
            return false;
        }

        if (IsConstantOffset(edge.offsetSegments))
        {
            tangent = baseRotation * Vector3.forward;
            rotation = baseRotation;
            return true;
        }

        float sample0 = Mathf.Max(0f, clampedDistanceM - OffsetTangentSampleDistanceM);
        float sample1 = Mathf.Min(edgeLengthM, clampedDistanceM + OffsetTangentSampleDistanceM);
        if (sample1 - sample0 < 0.001f)
        {
            tangent = baseRotation * Vector3.forward;
            rotation = baseRotation;
            return true;
        }

        if (!TryResolveOffsetEdgePosition(graph, edge, sample0, out Vector3 p0, out _) ||
            !TryResolveOffsetEdgePosition(graph, edge, sample1, out Vector3 p1, out _))
        {
            return false;
        }

        Vector3 delta = p1 - p0;
        if (delta.sqrMagnitude < 0.000001f)
        {
            tangent = baseRotation * Vector3.forward;
            rotation = baseRotation;
            return true;
        }

        tangent = delta.normalized;
        Vector3 up = baseRotation * Vector3.up;
        rotation = Quaternion.LookRotation(tangent, up.sqrMagnitude > 0.001f ? up.normalized : Vector3.up);
        return true;
    }

    public bool TryResolveOffsetEdgePosition(
        TrackGraph graph,
        TrackEdge edge,
        float distanceOnEdgeM,
        out Vector3 position,
        out Quaternion baseRotation)
    {
        position = Vector3.zero;
        baseRotation = Quaternion.identity;

        if (!TryGetOffsetEdgeBaseGeometryAndDistance(graph, edge, distanceOnEdgeM, out _, out float baseDistanceM))
        {
            return false;
        }

        float offsetM = TrackOffsetUtility.EvaluateOffsetAtBaseDistance(edge.offsetSegments, baseDistanceM);
        if (!TryResolveGeometryPose(
            graph,
            edge.baseGeometryId,
            baseDistanceM,
            out Vector3 basePosition,
            out _,
            out baseRotation))
        {
            return false;
        }

        position = basePosition + baseRotation * Vector3.right * offsetM;
        return true;
    }

    private bool TryResolveNativeGeometryPose(
        TrackGeometry geometry,
        float distanceM,
        out Vector3 position,
        out Vector3 tangent,
        out Quaternion rotation)
    {
        position = Vector3.zero;
        tangent = Vector3.forward;
        rotation = Quaternion.identity;

        Vector3 currentPos = geometry.originPosition;
        Quaternion currentRot = GetPlanRotation(geometry.originRotation);

        if (!TryResolveHorizontalPosition(geometry.horizontalSegments, distanceM, ref currentPos, ref currentRot))
        {
            return false;
        }

        float heightM = TrackGradientUtility.GetVerticalHeightAt(geometry.verticalSegments, distanceM);
        float currentPermille = TrackGradientUtility.GetGradientPermilleAt(geometry.verticalSegments, distanceM);

        position = currentPos;
        position.y = geometry.originPosition.y + heightM;

        float pitchDegree = -Mathf.Atan(currentPermille / 1000f) * Mathf.Rad2Deg;
        float cantMm = TrackGradientUtility.GetCantMmAt(geometry.cantSegments, distanceM);
        float rollDegree = Mathf.Atan2(cantMm / 1000f, Mathf.Max(0.001f, geometry.gaugeM)) * Mathf.Rad2Deg;
        rotation = currentRot * Quaternion.Euler(pitchDegree, 0f, rollDegree);
        tangent = rotation * Vector3.forward;
        return true;
    }

    private bool TryGetOffsetEdgeBaseGeometryAndDistance(
        TrackGraph graph,
        string edgeId,
        float distanceOnEdgeM,
        out TrackGeometry baseGeometry,
        out float baseDistanceM)
    {
        baseGeometry = null;
        baseDistanceM = 0f;

        if (graph == null || string.IsNullOrEmpty(edgeId))
        {
            return false;
        }

        return TryGetOffsetEdgeBaseGeometryAndDistance(
            graph,
            graph.FindEdge(edgeId),
            distanceOnEdgeM,
            out baseGeometry,
            out baseDistanceM
        );
    }

    private bool TryGetOffsetEdgeBaseGeometryAndDistance(
        TrackGraph graph,
        TrackEdge edge,
        float distanceOnEdgeM,
        out TrackGeometry baseGeometry,
        out float baseDistanceM)
    {
        baseGeometry = null;
        baseDistanceM = 0f;

        if (graph == null ||
            !IsOffsetEdge(edge) ||
            edge.offsetDistanceMap == null)
        {
            return false;
        }

        baseGeometry = graph.FindGeometry(edge.baseGeometryId);
        if (baseGeometry == null)
        {
            return false;
        }

        float clampedDistanceM = Mathf.Clamp(distanceOnEdgeM, 0f, Mathf.Max(0f, edge.lengthM));
        baseDistanceM = edge.offsetDistanceMap.SampleBaseDistance(clampedDistanceM);
        baseDistanceM = Mathf.Clamp(baseDistanceM, 0f, Mathf.Max(0f, baseGeometry.lengthM));
        return true;
    }

    private static bool IsOffsetEdge(TrackEdge edge)
    {
        return edge != null && !string.IsNullOrEmpty(edge.baseGeometryId);
    }

    private static bool IsConstantOffset(List<TrackOffsetSegment> offsetSegments)
    {
        if (offsetSegments == null || offsetSegments.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < offsetSegments.Count; i++)
        {
            TrackOffsetSegment segment = offsetSegments[i];
            if (segment == null || segment.curveType != TrackOffsetCurveType.Constant)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveHorizontalPosition(
        List<TrackHorizontalSegment> segments,
        float distanceM,
        ref Vector3 currentPos,
        ref Quaternion currentRot)
    {
        if (segments == null || segments.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < segments.Count; i++)
        {
            TrackHorizontalSegment segment = segments[i];
            if (segment == null)
            {
                continue;
            }

            float segmentStartM = Mathf.Max(0f, segment.startDistanceM);
            float segmentLengthM = Mathf.Max(0f, segment.lengthM);
            float segmentEndM = segmentStartM + segmentLengthM;

            if (distanceM <= segmentStartM)
            {
                break;
            }

            float localDistanceM = Mathf.Min(distanceM, segmentEndM) - segmentStartM;
            if (localDistanceM <= 0f)
            {
                continue;
            }

            CalculateHorizontal(
                segment.trackCurveType,
                localDistanceM,
                segmentLengthM,
                segment.radiusM,
                out float localX,
                out float localZ,
                out float angleDegree
            );
            currentPos += currentRot * new Vector3(localX, 0f, localZ);
            currentRot *= Quaternion.Euler(0f, angleDegree, 0f);

            if (distanceM <= segmentEndM)
            {
                return true;
            }
        }

        return true;
    }

    private static void CalculateHorizontal(
        TrackCurveType type,
        float localDistanceM,
        float segmentLengthM,
        float radiusM,
        out float localX,
        out float localZ,
        out float angleDegree)
    {
        switch (type)
        {
            case TrackCurveType.Curve:
                CalculateCircularCurve(localDistanceM, radiusM, out localX, out localZ, out angleDegree);
                break;
            case TrackCurveType.TransitionIn:
                CalculateCubicTransitionIn(localDistanceM, segmentLengthM, radiusM, out localX, out localZ, out angleDegree);
                break;
            case TrackCurveType.TransitionOut:
                CalculateCubicTransitionOut(localDistanceM, segmentLengthM, radiusM, out localX, out localZ, out angleDegree);
                break;
            default:
                CalculateStraight(localDistanceM, out localX, out localZ, out angleDegree);
                break;
        }
    }

    private static Quaternion GetPlanRotation(Quaternion worldRotation)
    {
        Vector3 forwardXZ = worldRotation * Vector3.forward;
        forwardXZ.y = 0f;
        return forwardXZ.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forwardXZ.normalized) : Quaternion.identity;
    }
}
