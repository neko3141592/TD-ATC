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
        position = Vector3.zero;
        tangent = Vector3.forward;

        if (graph == null || string.IsNullOrEmpty(edgeId)) return false;

        TrackEdge edge = graph.FindEdge(edgeId);
        if (edge == null) return false;

        TrackNode fromNode = graph.FindNode(edge.fromNodeId);
        if (fromNode == null) return false;

        // 1. スタート地点（ノード）のワールド座標と向きを基準にする
        Vector3 currentPos = fromNode.worldPosition;
        // Nodeの水平方向の向きだけを抽出してベースにする
        Vector3 forwardXZ = fromNode.worldRotation * Vector3.forward;
        forwardXZ.y = 0;
        Quaternion currentRot = forwardXZ.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forwardXZ.normalized) : Quaternion.identity;

        float remainingDist = Mathf.Max(0f, distanceOnEdgeM);
        float currentPermille = 0f;

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
                CalculateHorizontalAndAltitude(curve.lengthM, curve.lengthM, curve.trackCurveType, curve.radiusM, curve.gradientPermille, ref currentPos, ref currentRot);
                remainingDist -= curve.lengthM; 
            }
            else
            {
                CalculateHorizontalAndAltitude(remainingDist, curve.lengthM, curve.trackCurveType, curve.radiusM, curve.gradientPermille, ref currentPos, ref currentRot);
                currentPermille = curve.gradientPermille;
                remainingDist = 0f; 
                break; 
            }
        }

        if (remainingDist > 0.001f)
        {
            CalculateHorizontalAndAltitude(remainingDist, remainingDist, TrackCurveType.Straight, 0f, 0f, ref currentPos, ref currentRot);
            currentPermille = 0f;
        }

        // 3. 最終的な計算結果をセットする
        position = currentPos;
        
        // 接線（タンジェント）に対して、「現在乗っている勾配」のぶんだけピッチを適用する
        float pitchDegree = -Mathf.Atan(currentPermille / 1000f) * Mathf.Rad2Deg;
        tangent = currentRot * Quaternion.Euler(pitchDegree, 0f, 0f) * Vector3.forward;

        return true;
    }

    // --- 計算を合体させる魔法の補助関数 ---
    /// <summary>
    /// 役割: CalculateHorizontalAndAltitude の処理を実行します。
    /// </summary>
    /// <param name="l">l を指定します。</param>
    /// <param name="totalL">totalL を指定します。</param>
    /// <param name="type">type を指定します。</param>
    /// <param name="R">R を指定します。</param>
    /// <param name="permille">permille を指定します。</param>
    /// <param name="currentPos">currentPos を指定します。</param>
    /// <param name="currentRot">currentRot を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void CalculateHorizontalAndAltitude(float l, float totalL, TrackCurveType type, float R, float permille, ref Vector3 currentPos, ref Quaternion currentRot)
    {
        float pitchRad = Mathf.Atan(permille / 1000f);
        
        // 斜辺(l) から、高さ(Y)と平面上の進み(horizontalL)を分解
        float localY = l * Mathf.Sin(pitchRad);
        float horizontalL = l * Mathf.Cos(pitchRad);
        float horizontalTotalL = totalL * Mathf.Cos(pitchRad);

        float localX, localZ, angleDegree;

        // カーブ計算には分解した水平距離(horizontalL)を使う
        if (type == TrackCurveType.Straight) {
            CalculateStraight(horizontalL, out localX, out localZ, out angleDegree);
        } else if (type == TrackCurveType.Curve) {
            CalculateCircularCurve(horizontalL, R, out localX, out localZ, out angleDegree);
        } else if (type == TrackCurveType.TransitionIn) {
            CalculateClothoidIn(horizontalL, horizontalTotalL, R, out localX, out localZ, out angleDegree);
        } else if (type == TrackCurveType.TransitionOut) {
            CalculateClothoidOut(horizontalL, horizontalTotalL, R, out localX, out localZ, out angleDegree);
        } else {
            CalculateStraight(horizontalL, out localX, out localZ, out angleDegree);
        }

        // 水平面での移動ベクトルを作り、現在の向いている方向(currentRot)に添わせて足す
        Vector3 horizontalOffset = new Vector3(localX, 0f, localZ);
        currentPos += currentRot * horizontalOffset;
        
        // 高さは単純にワールドのY座標に足す！（ピッチが累積して破綻するのを防ぐため）
        currentPos.y += localY;

        // Y軸の回転（ヨー角＝左右の曲がり）だけを累積させる
        currentRot *= Quaternion.Euler(0f, angleDegree, 0f);
    }
}
