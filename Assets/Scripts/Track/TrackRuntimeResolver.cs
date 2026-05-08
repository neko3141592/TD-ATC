using System.Collections.Generic;
using UnityEngine;
public class TrackRuntimeResolver
{
    // ====== 【新しい数学エンジン部分】 ======

    /// <summary>
    /// 役割: CalculateStraight の処理を実行します。
    /// </summary>
    /// <param name="L">L を指定します。</param>
    /// <param name="x">x を指定します。</param>
    /// <param name="z">z を指定します。</param>
    /// <param name="angleDegree">angleDegree を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public static void CalculateStraight(float L, out float x, out float z, out float angleDegree) {
        x = 0f;
        z = L;
        angleDegree = 0f;
    }
    /// <summary>
    /// 役割: CalculateCircularCurve の処理を実行します。
    /// </summary>
    /// <param name="L">L を指定します。</param>
    /// <param name="R">R を指定します。</param>
    /// <param name="x">x を指定します。</param>
    /// <param name="z">z を指定します。</param>
    /// <param name="angleDegree">angleDegree を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public static void CalculateCircularCurve(float L, float R, out float x, out float z, out float angleDegree)
    {
        if (Mathf.Abs(R) < 0.001f)
        {
            CalculateStraight(L, out x, out z, out angleDegree);
            return;
        }

        float theta = L / R;
        x = R * (1f - Mathf.Cos(theta));
        z = R * Mathf.Sin(theta);
        angleDegree = theta * Mathf.Rad2Deg;
    }

    // クロソイド曲線の近似計算（マクローリン展開：直線→カーブ）
    /// <summary>
    /// 役割: CalculateClothoidIn の処理を実行します。
    /// </summary>
    /// <param name="l">l を指定します。</param>
    /// <param name="totalL">totalL を指定します。</param>
    /// <param name="R">R を指定します。</param>
    /// <param name="x">x を指定します。</param>
    /// <param name="z">z を指定します。</param>
    /// <param name="angleDegree">angleDegree を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public static void CalculateClothoidIn(float l, float totalL, float R, out float x, out float z, out float angleDegree)
    {
        if (Mathf.Abs(R) < 0.001f || totalL < 0.001f)
        {
            CalculateStraight(l, out x, out z, out angleDegree);
            return;
        }

        // 半径Rへ向けて、現在の距離lでの角度
        float theta = (l * l) / (2f * R * totalL);
        angleDegree = theta * Mathf.Rad2Deg;

        // 級数展開による X と Z の計算
        float theta2 = theta * theta;
        float theta4 = theta2 * theta2;
        
        z = l * (1f - (theta2 / 10f) + (theta4 / 216f)); 
        x = l * ((theta / 3f) - (theta * theta2 / 42f)); 
    }

    // クロソイド曲線の近似計算（カーブ→直線）
    /// <summary>
    /// 役割: CalculateClothoidOut の処理を実行します。
    /// </summary>
    /// <param name="l">l を指定します。</param>
    /// <param name="totalL">totalL を指定します。</param>
    /// <param name="R">R を指定します。</param>
    /// <param name="x">x を指定します。</param>
    /// <param name="z">z を指定します。</param>
    /// <param name="angleDegree">angleDegree を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public static void CalculateClothoidOut(float l, float totalL, float R, out float x, out float z, out float angleDegree)
    {
        if (Mathf.Abs(R) < 0.001f || totalL < 0.001f)
        {
            CalculateStraight(l, out x, out z, out angleDegree);
            return;
        }

        // 曲率がRから0に変わる時の角度変化
        float theta = (l / R) - (l * l) / (2f * R * totalL);
        angleDegree = theta * Mathf.Rad2Deg;

        // In曲線を逆順に辿ることで正確な位置を算出する
        CalculateClothoidIn(totalL, totalL, R, out float endX, out float endZ, out float endAngle);
        float remainL = totalL - l;
        CalculateClothoidIn(remainL, totalL, R, out float remainX, out float remainZ, out float remainAngle);

        float dx = endX - remainX;
        float dz = endZ - remainZ;

        float phi = endAngle * Mathf.Deg2Rad;
        float sinP = Mathf.Sin(phi);
        float cosP = Mathf.Cos(phi);

        z = dz * cosP + dx * sinP;
        x = dz * sinP - dx * cosP;
    }


    // ====== 【メインエンジン：距離から座標を割り出す】 ======
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

        if (graph == null || string.IsNullOrEmpty(edgeId)) return false;

        TrackEdge edge = graph.FindEdge(edgeId);
        if (edge == null) return false;

        TrackNode fromNode = graph.FindNode(edge.fromNodeId);
        if (fromNode == null) return false;

        float clampedDistanceOnEdgeM = Mathf.Clamp(distanceOnEdgeM, 0f, Mathf.Max(0f, edge.lengthM));
        return TryResolveProfilePose(edge, fromNode, clampedDistanceOnEdgeM, out position, out tangent, out rotation);
    }

    public bool TryGetGradientPermille(
        TrackGraph graph,
        string edgeId,
        float distanceOnEdgeM,
        out float gradientPermille)
    {
        gradientPermille = 0f;

        if (graph == null || string.IsNullOrEmpty(edgeId))
        {
            return false;
        }

        TrackEdge edge = graph.FindEdge(edgeId);
        if (edge == null)
        {
            return false;
        }

        float clampedDistanceOnEdgeM = Mathf.Clamp(distanceOnEdgeM, 0f, Mathf.Max(0f, edge.lengthM));
        gradientPermille = TrackGradientUtility.GetGradientPermilleAt(edge.verticalSegments, clampedDistanceOnEdgeM);
        return true;
    }

    public bool TryGetCantMm(
        TrackGraph graph,
        string edgeId,
        float distanceOnEdgeM,
        out float cantMm)
    {
        cantMm = 0f;

        if (graph == null || string.IsNullOrEmpty(edgeId))
        {
            return false;
        }

        TrackEdge edge = graph.FindEdge(edgeId);
        if (edge == null)
        {
            return false;
        }

        float clampedDistanceOnEdgeM = Mathf.Clamp(distanceOnEdgeM, 0f, Mathf.Max(0f, edge.lengthM));
        cantMm = TrackGradientUtility.GetCantMmAt(edge.cantSegments, clampedDistanceOnEdgeM);
        return true;
    }

    private bool TryResolveProfilePose(
        TrackEdge edge,
        TrackNode fromNode,
        float distanceOnEdgeM,
        out Vector3 position,
        out Vector3 tangent,
        out Quaternion rotation)
    {
        position = Vector3.zero;
        tangent = Vector3.forward;
        rotation = Quaternion.identity;

        Vector3 currentPos = fromNode.worldPosition;
        Quaternion currentRot = GetPlanRotation(fromNode.worldRotation);

        if (!TryResolveHorizontalPosition(edge.horizontalSegments, distanceOnEdgeM, ref currentPos, ref currentRot))
        {
            return false;
        }

        float heightM = TrackGradientUtility.GetVerticalHeightAt(edge.verticalSegments, distanceOnEdgeM);
        float currentPermille = TrackGradientUtility.GetGradientPermilleAt(edge.verticalSegments, distanceOnEdgeM);

        position = currentPos;
        position.y = fromNode.worldPosition.y + heightM;

        float pitchDegree = -Mathf.Atan(currentPermille / 1000f) * Mathf.Rad2Deg;
        float cantMm = TrackGradientUtility.GetCantMmAt(edge.cantSegments, distanceOnEdgeM);
        float rollDegree = Mathf.Atan2(cantMm / 1000f, Mathf.Max(0.001f, edge.gaugeM)) * Mathf.Rad2Deg;
        rotation = currentRot * Quaternion.Euler(pitchDegree, 0f, rollDegree);
        tangent = rotation * Vector3.forward;
        return true;
    }

    private static bool TryResolveHorizontalPosition(
        List<TrackHorizontalSegment> segments,
        float distanceOnEdgeM,
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

            if (distanceOnEdgeM <= segmentStartM)
            {
                break;
            }

            float localDistanceM = Mathf.Min(distanceOnEdgeM, segmentEndM) - segmentStartM;
            if (localDistanceM <= 0f)
            {
                continue;
            }

            CalculateHorizontal(segment.trackCurveType, localDistanceM, segmentLengthM, segment.radiusM, out float localX, out float localZ, out float angleDegree);
            currentPos += currentRot * new Vector3(localX, 0f, localZ);
            currentRot *= Quaternion.Euler(0f, angleDegree, 0f);

            if (distanceOnEdgeM <= segmentEndM)
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
                CalculateClothoidIn(localDistanceM, segmentLengthM, radiusM, out localX, out localZ, out angleDegree);
                break;
            case TrackCurveType.TransitionOut:
                CalculateClothoidOut(localDistanceM, segmentLengthM, radiusM, out localX, out localZ, out angleDegree);
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
