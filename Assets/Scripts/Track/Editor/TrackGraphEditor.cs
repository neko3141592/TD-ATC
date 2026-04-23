using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrackGraph))]
public class TrackGraphEditor : Editor
{
    /// <summary>
    /// 役割: カスタムインスペクターを描画します。
    /// </summary>
    /// <returns>処理結果を返します。</returns>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8);

        if (GUILayout.Button("Validate Graph"))
        {
            var graph = (TrackGraph)target;
            var errors = new List<string>();
            if (graph.ValidateGraph(errors))
            {
                Debug.Log($"TrackGraph validation passed. nodes={graph.nodes.Count}, edges={graph.edges.Count}", graph);
            }
            else
            {
                Debug.LogError("TrackGraph validation failed:\n- " + string.Join("\n- ", errors), graph);
            }
        }

        GUILayout.Space(8);

        if (GUILayout.Button("Create Simple Test Track"))
        {
            var graph = (TrackGraph)target;
            graph.nodes.Clear();
            graph.edges.Clear();
            graph.turnoutStates.Clear();
            graph.stations.Clear(); // 駅データもあわせて初期化します。

            TrackBuilder builder = new TrackBuilder(graph);

            // 開始地点
            builder.Start(Vector3.zero, Quaternion.identity);

            // 1. 長い始端直線
            builder.AddStraight(2360f);
            
            // 最初の直線の始端寄りに駅を配置します。
            builder.AddStation("ST_Start", "始発駅", offsetMFromNode: 50f);
            
            builder.PutNode("N1");

            // 2. 緩和曲線付きの右曲線
            float r = 450f;
            float curveL = (2f * Mathf.PI * r) * (90f / 360f);
            float clothoidL = 40f;
            
            builder.AddClothoidIn(clothoidL, r);
            builder.AddCurve(curveL, r);
            builder.AddClothoidOut(clothoidL, r);
            builder.PutNode("N2");

            // 3. 短い直線
            builder.AddStraight(400f);
            builder.PutNode("N3");

            // 4. 緩和曲線付きの左曲線
            r = 600f;
            curveL = (2f * Mathf.PI * r) * (70f / 360f);
            clothoidL = 40f;
            builder.AddClothoidIn(clothoidL, -r);
            builder.AddCurve(curveL, -r);
            builder.AddClothoidOut(clothoidL, -r);
            builder.PutNode("N4");

            // 5. 終端側の長い直線
            builder.AddStraight(2500f);

            builder.PutNode("N5");

            r = 300f;
            curveL = (2f * Mathf.PI * r) * (40f / 360f);
            clothoidL = 60f;
            builder.AddClothoidIn(clothoidL, -r);
            builder.AddCurve(curveL, -r);
            builder.AddClothoidOut(clothoidL, -r);

            builder.PutNode("N6");

            builder.AddStraight(250f);
            
            // 終端側の直線区間に終着駅を配置します。
            builder.AddStation("ST_End", "終点駅", offsetMFromNode: 50f);
            
            builder.PutNode("End");

            graph.UpdateNodeTypesAndJunctionIds();
            graph.SyncTurnoutStates();

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
        }

        if (GUILayout.Button("Create Endless Circle Track"))
        {
            var graph = (TrackGraph)target;

            graph.nodes.Clear();
            graph.edges.Clear();
            
            // 半径 100m の円形テストコースを作成します。
            float radius = 100f;
            float curveLength = (2f * Mathf.PI * radius) / 4f; // 90 度ぶんの円弧長です。
        
            var nodes = new TrackNode[4];
            for(int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                // 円周上にノードを置き、ローカル前方が周回方向を向くよう回転を合わせます。
                nodes[i] = new TrackNode { 
                    nodeId = $"N{i:000}", 
                    worldPosition = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * radius, 0, Mathf.Cos(angle * Mathf.Deg2Rad) * radius), 
                    worldRotation = Quaternion.Euler(0, angle + 90f, 0),
                    outgoingEdgeIds = new List<string>()
                };
                graph.nodes.Add(nodes[i]);
            }
            
            // 4 本の 1/4 円エッジをつないでループを閉じます。
            for(int i = 0; i < 4; i++)
            {
                int nextIndex = (i + 1) % 4;
                string edgeId = $"E{i:000}";
                
                var edge = new TrackEdge { 
                    edgeId = edgeId, 
                    fromNodeId = nodes[i].nodeId, 
                    toNodeId = nodes[nextIndex].nodeId, 
                    lengthM = curveLength 
                };
                
                // 各エッジは 90 度の単一右曲線です。
                edge.mathCurves.Add(new TrackCurveData { 
                    trackCurveType = TrackCurveType.Curve, 
                    lengthM = curveLength,
                    radiusM = radius 
                });
                
                graph.edges.Add(edge);
                nodes[i].outgoingEdgeIds.Add(edgeId); // 列車が周回し続けられるように進路を登録します。
            }

            // グラフ再構築後にノード種別と分岐情報を更新します。
            graph.UpdateNodeTypesAndJunctionIds();
            graph.SyncTurnoutStates();

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
        }

        GUILayout.Space(8);

        if (GUILayout.Button("Create Passing Track (Turnout Demo)"))
        {
            var graph = (TrackGraph)target;
            graph.nodes.Clear();
            graph.edges.Clear();
            graph.turnoutStates.Clear();

            TrackBuilder builder = new TrackBuilder(graph);

            // 1. 始端から最初の分岐までの進入区間
            builder.Start(Vector3.zero, Quaternion.identity);
            builder.AddStraight(200f);
            TrackNode pointStart = builder.PutNode("Point_Start");

            // 2. 本線側の通過線と合流点
            builder.AddStraight(500f);
            TrackNode pointMerge = builder.PutNode("Point_Merge");

            // 3. 合流後に短い出口直線を追加
            builder.AddStraight(300f);
            builder.PutNode("End_Station");

            // 4. Point_Start から Point_Merge までの待避線を構築
            builder.StartFrom(pointStart);
            
            float trL = 30f;  // 緩和曲線を長めにして乗り心地を改善します。
            float trR = 250f; // 低速本線分岐程度の緩い分岐半径です。
            float diagonalL = 30f; // 値を大きくすると待避線の間隔が広がります。

            // 緩和曲線を使って右側へ分岐します。
            builder.AddClothoidInOut(trL, trR);

            if (diagonalL > 0f) builder.AddStraight(diagonalL); // 斜め直線で横方向の間隔を稼ぎます。

            builder.AddClothoidInOut(trL, -trR);
            
            // 本線に対する前進量を測り、残りを直線で埋めます。
            float curveAdvanceZ = Vector3.Dot(builder.currentPos - pointStart.worldPosition, pointStart.worldRotation * Vector3.forward);
            builder.AddStraight(500f - curveAdvanceZ * 2f);
            
            // 逆向きの S 字で本線へ戻します。
            builder.AddClothoidInOut(trL, -trR);

            if (diagonalL > 0f) builder.AddStraight(diagonalL); // 戻り側も同じ斜め直線を入れます。

            builder.AddClothoidInOut(trL, trR);

            // Point_Merge で本線に合流します。
            builder.ConnectToNode(pointMerge);

            // グラフ再構築後にノード種別と分岐情報を更新します。
            graph.UpdateNodeTypesAndJunctionIds();
            graph.SyncTurnoutStates();

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
        }

        GUILayout.Space(8);

        if (GUILayout.Button("Create 5km Course (with Passing Tracks & Clothoids)"))
        {
            var graph = (TrackGraph)target;
            graph.nodes.Clear();
            graph.edges.Clear();
            graph.turnoutStates.Clear();

            TrackBuilder builder = new TrackBuilder(graph);

            // 開始位置
            builder.Start(Vector3.zero, Quaternion.identity);
            builder.AddStraight(550f);
            
            // --- 待避線 1（駅構内想定） ---
            TrackNode station1Start = builder.PutNode("Station1_Start");
            
            // 本線側の通過線
            builder.AddStraight(550f);
            TrackNode station1End = builder.PutNode("Station1_End");

            // 緩和曲線ベースの待避線 1
            float trL = 30f;  // 緩和曲線を長めにして乗り心地を改善します。
            float trR = 200f; // 低速本線分岐程度の緩い分岐半径です。
            float diagonalL = 30f; // 値を大きくすると線間が広がります。
            
            builder.StartFrom(station1Start);
            // 分岐して並行な線形に落ち着かせます。
            builder.AddClothoidInOut(trL, trR);
            if (diagonalL > 0f) builder.AddStraight(diagonalL); // 斜め直線で横方向の間隔を稼ぎます。
            builder.AddClothoidInOut(trL, -trR);

            // 本線に対する前進量を測り、残りを直線で埋めます。
            float curveAdvanceZ1 = Vector3.Dot(builder.currentPos - station1Start.worldPosition, station1Start.worldRotation * Vector3.forward);
            builder.AddStraight(600f - curveAdvanceZ1 * 2f);

            // 鏡像の分岐形状で合流します。
            builder.AddClothoidInOut(trL, -trR);
            if (diagonalL > 0f) builder.AddStraight(diagonalL); // 合流側にも同じ斜め直線を入れます。
            builder.AddClothoidInOut(trL, trR);

            // 本線に再合流し、分岐側の速度制限を 45 km/h に設定します。
            builder.ConnectToNode(station1End, 45f);

            // --- 曲線区間（緩和曲線テスト） ---
            builder.StartFrom(station1End);
            builder.AddStraight(300f);

            // 右曲線（R = 400 m）
            builder.AddClothoidIn(40f, 400f); // 緩和曲線
            builder.AddCurve(300f, 400f);     // 円曲線
            builder.AddClothoidOut(40f, 400f); // 緩和曲線

            builder.AddStraight(400f);

            // よりきつい左曲線（R = -200 m）で、ノード速度制限を 60 km/h に設定します。
            builder.PutNode(speedLimitKmH: 60f);
            builder.AddClothoidIn(50f, -200f);
            builder.AddCurve(200f, -200f);
            builder.AddClothoidOut(50f, -200f);
            builder.PutNode(); // 一時的な速度制限を解除します。

            builder.AddStraight(500f);

            // --- 待避線 2（駅構内想定） ---
            TrackNode station2Start = builder.PutNode("Station2_Start");
            
            // 本線側の通過線
            builder.AddStraight(500f);
            TrackNode station2End = builder.PutNode("Station2_End");

            // 同じ分岐形状を使った待避線 2 です。
            builder.StartFrom(station2Start);
            builder.AddClothoidInOut(trL, trR);
            if (diagonalL > 0f) builder.AddStraight(diagonalL); // 横方向の間隔を広げます。
            builder.AddClothoidInOut(trL, -trR);

            float curveAdvanceZ2 = Vector3.Dot(builder.currentPos - station2Start.worldPosition, station2Start.worldRotation * Vector3.forward);
            builder.AddStraight(500f - curveAdvanceZ2 * 2f);

            builder.AddClothoidInOut(trL, -trR);
            if (diagonalL > 0f) builder.AddStraight(diagonalL); // 横方向の間隔を広げます。
            builder.AddClothoidInOut(trL, trR);

            builder.ConnectToNode(station2End, 45f);

            // --- 終端区間 ---
            builder.StartFrom(station2End);
            builder.AddStraight(1000f);
            builder.PutNode("End_Terminal");

            graph.UpdateNodeTypesAndJunctionIds();
            graph.SyncTurnoutStates();

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
        }
    }
}
