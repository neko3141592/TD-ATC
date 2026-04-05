using UnityEngine;
public class TrackRuntimeResolver
{
    // ====== 【新しい数学エンジン部分】 ======

    public static void CalculateStraight(float L, out float x, out float z, out float angleDegree) {
        x = 0f;
        z = L;
        angleDegree = 0f;
    }
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
        position = Vector3.zero;
        tangent = Vector3.forward;

        if (graph == null || string.IsNullOrEmpty(edgeId)) return false;

        TrackEdge edge = graph.FindEdge(edgeId);
        if (edge == null) return false;

        TrackNode fromNode = graph.FindNode(edge.fromNodeId);
        if (fromNode == null) return false;

        // 1. スタート地点（ノード）のワールド座標と向きを基準にする
        Vector3 currentPos = fromNode.worldPosition;
        Quaternion currentRot = fromNode.worldRotation;

        float remainingDist = Mathf.Max(0f, distanceOnEdgeM);

        // 2. エッジが持っている「カーブレシピ（mathCurves）」を順番に計算していく
        if (edge.mathCurves == null || edge.mathCurves.Count == 0)
        {
            Debug.LogError($"TrackEdge {edgeId} に有効なカーブレシピ(mathCurves)が設定されていません。データが壊れている可能性があります。");
            return false;
        }

        for (int i = 0; i < edge.mathCurves.Count; i++)
        {
            TrackCurveData curve = edge.mathCurves[i];

            if (remainingDist > curve.lengthM)
            {
                // 電車はこのカーブ区間を「通り過ぎた（完了した）」ので、全長の姿を計算して足す
                CalculateLocalAndAdd(curve.lengthM, curve.lengthM, curve.trackCurveType, curve.radiusM, ref currentPos, ref currentRot);
                remainingDist -= curve.lengthM; // 残りの距離を減らして、次のカーブへ進む
            }
            else
            {
                // 電車は「現在このカーブ区間の途中（残りの距離）」にいる！
                CalculateLocalAndAdd(remainingDist, curve.lengthM, curve.trackCurveType, curve.radiusM, ref currentPos, ref currentRot);
                remainingDist = 0f; // 計算完了
                break; 
            }
        }

        // もしカーブの合計距離よりも長く進んでしまった場合は、強制的に最後の向きのまま真っ直ぐ進ませる
        if (remainingDist > 0.001f)
        {
            CalculateLocalAndAdd(remainingDist, remainingDist, TrackCurveType.Straight, 0f, ref currentPos, ref currentRot);
        }

        // 3. 最終的な計算結果をセットする
        position = currentPos;
        tangent = currentRot * Vector3.forward; // 向いている角度の正面方向を「接線(タンジェント)」として返す
        return true;
    }

    // --- 計算を合体させる魔法の補助関数 ---
    private void CalculateLocalAndAdd(float l, float totalL, TrackCurveType type, float R, ref Vector3 currentPos, ref Quaternion currentRot)
    {
        float localX, localZ, angleDegree;

        if (type == TrackCurveType.Straight) {
            CalculateStraight(l, out localX, out localZ, out angleDegree);
        } else if (type == TrackCurveType.Curve) {
            CalculateCircularCurve(l, R, out localX, out localZ, out angleDegree);
        } else if (type == TrackCurveType.TransitionIn) {
            CalculateClothoidIn(l, totalL, R, out localX, out localZ, out angleDegree);
        } else if (type == TrackCurveType.TransitionOut) {
            CalculateClothoidOut(l, totalL, R, out localX, out localZ, out angleDegree);
        } else {
            CalculateStraight(l, out localX, out localZ, out angleDegree);
        }

        // 計算して出たローカルの移動量を、今の「基準の向き」に合わせて足し算する！
        Vector3 offset = new Vector3(localX, 0f, localZ);
        currentPos += currentRot * offset;

        // 角度も、今の「基準の向き」に足して回転させる
        currentRot *= Quaternion.Euler(0f, angleDegree, 0f);
    }
}
