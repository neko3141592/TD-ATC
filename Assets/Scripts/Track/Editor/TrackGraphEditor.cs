using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrackGraph))]
public class TrackGraphEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8);

        if (GUILayout.Button("Create Simple Test Track"))
        {
            var graph = (TrackGraph)target;
            graph.nodes.Clear();
            graph.edges.Clear();
            graph.turnoutStates.Clear();
            graph.stations.Clear(); // 駅データもリセット

            TrackBuilder builder = new TrackBuilder(graph);

            // スタート
            builder.Start(Vector3.zero, Quaternion.identity);

            // 1. 直線 1000m
            builder.AddStraight(2360f);
            
            // ★ スタートの直線内に駅を設置（出発地点から50mの位置に駅の中心(停止目標など)を置く）
            builder.AddStation("ST_Start", "始発駅", offsetMFromNode: 50f);
            
            builder.PutNode("N1");

            // 2. 右カーブ (緩和曲線40m + R=200, 90度 + 緩和曲線40m)
            float r = 450f;
            float curveL = (2f * Mathf.PI * r) * (90f / 360f);
            float clothoidL = 40f;
            
            builder.AddClothoidIn(clothoidL, r);
            builder.AddCurve(curveL, r);
            builder.AddClothoidOut(clothoidL, r);
            builder.PutNode("N2");

            // 3. 直線 100m
            builder.AddStraight(400f);
            builder.PutNode("N3");

            // 4. 左カーブ (緩和曲線40m + R=-200, 90度 + 緩和曲線40m)
            r = 600f;
            curveL = (2f * Mathf.PI * r) * (70f / 360f);
            clothoidL = 40f;
            builder.AddClothoidIn(clothoidL, -r);
            builder.AddCurve(curveL, -r);
            builder.AddClothoidOut(clothoidL, -r);
            builder.PutNode("N4");

            // 5. 最後に直線 2500m
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
            
            // ★ ゴールの直線内にも駅を設置（この区間の始まりから50mの地点）
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
            
            // 円形コースを作る (半径100m)
            float radius = 100f;
            float curveLength = (2f * Mathf.PI * radius) / 4f; // 90度分の弧長 (約157m)
        
            var nodes = new TrackNode[4];
            for(int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                // 円周上の座標 (x: sin, z: cos)
                // Y軸回転を90度ずつずらす
                nodes[i] = new TrackNode { 
                    nodeId = $"N{i:000}", 
                    worldPosition = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * radius, 0, Mathf.Cos(angle * Mathf.Deg2Rad) * radius), 
                    worldRotation = Quaternion.Euler(0, angle + 90f, 0),
                    outgoingEdgeIds = new List<string>()
                };
                graph.nodes.Add(nodes[i]);
            }
            
            // 4つのエッジを繋ぐ (0->1, 1->2, 2->3, 3->0)
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
                
                // 90度の右カーブ
                edge.mathCurves.Add(new TrackCurveData { 
                    trackCurveType = TrackCurveType.Curve, 
                    lengthM = curveLength,
                    radiusM = radius 
                });
                
                graph.edges.Add(edge);
                nodes[i].outgoingEdgeIds.Add(edgeId); // 次のエッジを登録（これがないと途中で止まる）
            }

            // ノードのタイプ（分岐など）を更新する
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

            // 1. スタートして分岐点まで (助走区間を長めに確保：200m)
            builder.Start(Vector3.zero, Quaternion.identity);
            builder.AddStraight(200f);
            TrackNode pointStart = builder.PutNode("Point_Start");

            // 2. 本線 (ズバッと長く直線500mを通す) + 出口分岐点
            builder.AddStraight(500f);
            TrackNode pointMerge = builder.PutNode("Point_Merge");

            // 3. 本線のその先 (合流後も余裕をもって走れるよう300m追加)
            builder.AddStraight(300f);
            builder.PutNode("End_Station");

            // 4. 待避線の作成 (Point_Start から Point_Merge へ繋ぐ)
            builder.StartFrom(pointStart);
            
            float trL = 30f;  // 緩和曲線を長くして乗り心地を向上
            float trR = 250f; // 現実の10番分岐器(45km/h制限)相当のゆるやかなカーブ
            float diagonalL = 30f; // ★ここの数値を増やすと、待避線の横幅（線路間隔）が広がります！

            // 右へ分岐 (緩和曲線を組み込んだ分岐)
            builder.AddClothoidInOut(trL, trR);

            if (diagonalL > 0f) builder.AddStraight(diagonalL); // 横幅を稼ぐための斜めの直線

            builder.AddClothoidInOut(trL, -trR);
            
            // 本線に対する前進距離(Z成分)を計算して残りの直線を埋める
            float curveAdvanceZ = Vector3.Dot(builder.currentPos - pointStart.worldPosition, pointStart.worldRotation * Vector3.forward);
            builder.AddStraight(500f - curveAdvanceZ * 2f);
            
            // 左へS字カーブで本線に戻る
            builder.AddClothoidInOut(trL, -trR);

            if (diagonalL > 0f) builder.AddStraight(diagonalL); // 戻りも同じ長さの斜め直線を挟む

            builder.AddClothoidInOut(trL, trR);

            // ゴール(Point_Merge) に合流！
            builder.ConnectToNode(pointMerge);

            // 最後に、グラフを更新して「分岐」として登録させる
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

            // スタート位置
            builder.Start(Vector3.zero, Quaternion.identity);
            builder.AddStraight(550f);
            
            // --- 待避線1 (駅) ---
            TrackNode station1Start = builder.PutNode("Station1_Start");
            
            // 本線 (通過線) 500m
            builder.AddStraight(550f);
            TrackNode station1End = builder.PutNode("Station1_End");

            // 待避線1 (緩和曲線を組み込んだ分岐)
            float trL = 30f;  // 緩和曲線を長くして乗り心地を向上
            float trR = 200f; // 現実の10番分岐器(45km/h制限)相当のゆるやかなカーブ
            float diagonalL = 30f; // ★待避線の横幅（線路間隔）を広げるパラメータ
            
            builder.StartFrom(station1Start);
            // 分岐: 緩和曲線(直線→曲率1/R) → (曲率1/R→直線) で平行な角度に戻す
            builder.AddClothoidInOut(trL, trR);
            if (diagonalL > 0f) builder.AddStraight(diagonalL); // 斜めの直線で横幅を稼ぐ
            builder.AddClothoidInOut(trL, -trR);

            // 本線に対する前進距離(Z成分)を計算して残りの直線を埋める
            float curveAdvanceZ1 = Vector3.Dot(builder.currentPos - station1Start.worldPosition, station1Start.worldRotation * Vector3.forward);
            builder.AddStraight(600f - curveAdvanceZ1 * 2f);

            // 合流: 分岐の逆の操作
            builder.AddClothoidInOut(trL, -trR);
            if (diagonalL > 0f) builder.AddStraight(diagonalL); // 合流側も同じように斜めの直線を挟む
            builder.AddClothoidInOut(trL, trR);

            // 本線に再合流 (分岐側の制限速度を45km/hに設定)
            builder.ConnectToNode(station1End, 45f);

            // --- 曲線区間 (クロソイドのテスト) ---
            builder.StartFrom(station1End);
            builder.AddStraight(300f);

            // 右カーブ (R=400m)
            builder.AddClothoidIn(40f, 400f); // 緩和曲線
            builder.AddCurve(300f, 400f);     // 本円
            builder.AddClothoidOut(40f, 400f); // 緩和曲線

            builder.AddStraight(400f);

            // 左急カーブ (R=-200m) 制限速度 60km/h のノードを設ける
            builder.PutNode(speedLimitKmH: 60f);
            builder.AddClothoidIn(50f, -200f);
            builder.AddCurve(200f, -200f);
            builder.AddClothoidOut(50f, -200f);
            builder.PutNode(); // 制限速度解除

            builder.AddStraight(500f);

            // --- 待避線2 (駅) ---
            TrackNode station2Start = builder.PutNode("Station2_Start");
            
            // 本線 500m
            builder.AddStraight(500f);
            TrackNode station2End = builder.PutNode("Station2_End");

            // 待避線2 (緩和曲線つき)
            builder.StartFrom(station2Start);
            builder.AddClothoidInOut(trL, trR);
            if (diagonalL > 0f) builder.AddStraight(diagonalL); // 横幅拡張
            builder.AddClothoidInOut(trL, -trR);

            float curveAdvanceZ2 = Vector3.Dot(builder.currentPos - station2Start.worldPosition, station2Start.worldRotation * Vector3.forward);
            builder.AddStraight(500f - curveAdvanceZ2 * 2f);

            builder.AddClothoidInOut(trL, -trR);
            if (diagonalL > 0f) builder.AddStraight(diagonalL); // 横幅拡張
            builder.AddClothoidInOut(trL, trR);

            builder.ConnectToNode(station2End, 45f);

            // --- 終端部 ---
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

