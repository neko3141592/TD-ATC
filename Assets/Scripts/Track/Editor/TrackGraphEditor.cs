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

        if (GUILayout.Button("Recalculate Node Heights From Vertical Profiles"))
        {
            var graph = (TrackGraph)target;
            Undo.RecordObject(graph, "Recalculate Node Heights");
            int updatedCount = graph.RecalculateNodeHeightsFromVerticalProfiles();
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            Debug.Log($"Recalculated node heights from vertical profiles. updatedNodes={updatedCount}", graph);
        }

        if (GUILayout.Button("Apply Demo 25permille Grade To First Edge"))
        {
            var graph = (TrackGraph)target;
            Undo.RecordObject(graph, "Apply Demo Vertical Profile");
            bool applied = graph.ApplyDemoVerticalProfileToFirstEdge();
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            Debug.Log(applied
                ? "Applied demo vertical profile to first edge: 0 -> 25 -> 0 permille."
                : "Could not apply demo vertical profile. TrackGraph has no usable first edge.",
                graph);
        }

        GUILayout.Space(8);

        if (GUILayout.Button("Create TASC 1km Test Track"))
        {
            CreateTascTestTrack();
        }

        if (GUILayout.Button("Create Grade 2km Test Track"))
        {
            CreateGradeTestTrack();
        }

        if (GUILayout.Button("Create 10km Single Track Course"))
        {
            CreateSingleTrack10kmCourse();
        }

        if (GUILayout.Button("Create 20km Grade Station Junction Course"))
        {
            CreateGradeStationJunction20kmCourse();
        }
    }

    private struct CourseSectionResult
    {
        public string mainEdgeId;
        public string sidingEdgeId;
        public float lengthM;
    }

    /// <summary>
    /// 役割: 曲線、約10駅、主要駅の待避線を含む、約10kmの単線テストコースを生成します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void CreateSingleTrack10kmCourse()
    {
        var graph = (TrackGraph)target;
        graph.nodes.Clear();
        graph.edges.Clear();
        graph.turnoutStates.Clear();
        graph.stations.Clear();

        // プレイヤー列車の試験用として、0mから10km方向へ進む単線を生成します。
        // 既存の LocalTestService.asset と接続しやすいよう、主要な stationId は従来名を維持します。
        TrackBuilder builder = new TrackBuilder(graph);
        builder.Start(Vector3.zero, Quaternion.identity);

        AddPlainSection(builder, 650f, "MAIN_00650", "ST_Start", "Start", 250f, 105f);
        AddCurvedSection(builder, 850f, "MAIN_01500", "ST_East", "East", 430f, 105f, 520f, 260f, 1f);
        AddMajorStationWithPassingLoop(builder, graph, "MAIN_Mid", "ST_Mid", "Mid", 900f, 90f, 45f, -1f);
        AddCurvedSection(builder, 850f, "MAIN_03250", "ST_Pass", "Pass", 420f, 95f, -460f, 250f, -1f);
        AddMajorStationWithPassingLoop(builder, graph, "MAIN_Center", "ST_Center", "Center", 950f, 85f, 40f, 1f);
        AddCurvedSection(builder, 1000f, "MAIN_05200", "ST_Park", "Park", 520f, 90f, 620f, 320f, 1f);
        AddMajorStationWithPassingLoop(builder, graph, "MAIN_Yard", "ST_Yard", "Yard", 1100f, 80f, 35f, -1f);
        AddCurvedSection(builder, 1100f, "MAIN_07400", "ST_Hillside", "Hillside", 560f, 85f, -700f, 360f, -1f);
        AddMajorStationWithPassingLoop(builder, graph, "MAIN_Harbor", "ST_Harbor", "Harbor", 1050f, 75f, 35f, 1f);
        AddCurvedSection(builder, 1550f, "MAIN_10000", "ST_End", "End", 1300f, 100f, 900f, 420f, 1f);

        AssignSequentialBlockIds(graph);

        graph.UpdateNodeTypesAndJunctionIds();
        graph.SyncTurnoutStates();

        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
        Debug.Log($"Created 10km single track course. nodes={graph.nodes.Count}, edges={graph.edges.Count}, stations={graph.stations.Count}", graph);
    }

    /// <summary>
    /// 役割: 駅間700〜1500m、現実的な勾配・駅・分岐を含む約20kmのテストコースを生成します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void CreateGradeStationJunction20kmCourse()
    {
        var graph = (TrackGraph)target;
        graph.nodes.Clear();
        graph.edges.Clear();
        graph.turnoutStates.Clear();
        graph.stations.Clear();

        const float targetLengthM = 20000f;
        const float minStationIntervalM = 700f;
        const float maxStationIntervalM = 1500f;
        const int seed = 20260501;

        var random = new System.Random(seed);
        var intervals = BuildRandomStationIntervals(
            random,
            targetLengthM,
            minStationIntervalM,
            maxStationIntervalM
        );

        TrackBuilder builder = new TrackBuilder(graph);
        builder.Start(Vector3.zero, Quaternion.identity);

        float routeDistanceM = 0f;
        float currentGradientPermille = 0f;
        bool hasAssignedMidStation = false;

        for (int i = 0; i < intervals.Count; i++)
        {
            float sectionLengthM = intervals[i];
            routeDistanceM += sectionLengthM;

            bool isFinalStation = i == intervals.Count - 1;
            bool shouldAddPassingLoop = Is20kmPassingLoopSection(i, intervals.Count);
            bool nextSectionIsPassingLoop = Is20kmPassingLoopSection(i + 1, intervals.Count);
            bool shouldAddCurve = !shouldAddPassingLoop && random.NextDouble() < 0.28;
            float speedLimitKmH = shouldAddPassingLoop
                ? RandomRange(random, 70f, 95f)
                : RandomRange(random, 85f, 115f);
            string stationId = ResolveGeneratedStationId(i + 1, routeDistanceM, targetLengthM, isFinalStation, ref hasAssignedMidStation);
            string stationName = ResolveGeneratedStationName(stationId, i + 1);
            string nodeId = isFinalStation
                ? "G20_End"
                : $"G20_{routeDistanceM:00000}";

            CourseSectionResult sectionResult;
            if (shouldAddPassingLoop)
            {
                sectionResult = AddPassingLoopSectionWithStationNearEnd(
                    builder,
                    graph,
                    $"G20_Loop_{i + 1:00}",
                    stationId,
                    stationName,
                    sectionLengthM,
                    speedLimitKmH,
                    RandomRange(random, 45f, 60f),
                    random.NextDouble() < 0.5 ? -1f : 1f
                );
            }
            else if (shouldAddCurve)
            {
                sectionResult = AddCurvedSectionWithStationNearEnd(
                    builder,
                    graph,
                    sectionLengthM,
                    nodeId,
                    stationId,
                    stationName,
                    speedLimitKmH,
                    RandomRange(random, 900f, 1800f),
                    RandomRange(random, 80f, Mathf.Min(240f, sectionLengthM * 0.25f)),
                    random.NextDouble() < 0.5 ? -1f : 1f
                );
            }
            else
            {
                sectionResult = AddPlainSectionWithStationNearEnd(
                    builder,
                    graph,
                    sectionLengthM,
                    nodeId,
                    stationId,
                    stationName,
                    speedLimitKmH
                );
            }

            float nextGradientPermille = 0f;
            TrackEdge mainEdge = graph.FindEdge(sectionResult.mainEdgeId);
            if (shouldAddPassingLoop)
            {
                ApplyFlatGradeProfile(mainEdge);
            }
            else
            {
                // 分岐区間に入る直前は必ず0‰へ戻し、分岐途中で勾配が出ないようにします。
                nextGradientPermille = (isFinalStation || nextSectionIsPassingLoop)
                    ? 0f
                    : RandomRealisticGradePermille(random);
                ApplyReadableGradeProfile(mainEdge, currentGradientPermille, nextGradientPermille);
            }

            // 分岐側は本線既定なので通常走行では使いません。将来の分岐試験用に水平線形だけ残し、縦断は平坦扱いにします。
            TrackEdge sidingEdge = graph.FindEdge(sectionResult.sidingEdgeId);
            if (sidingEdge != null)
            {
                ApplyFlatGradeProfile(sidingEdge);
                sidingEdge.cantSegments.Clear();
            }

            currentGradientPermille = shouldAddPassingLoop ? 0f : nextGradientPermille;
        }

        if (graph.edges.Count > 0)
        {
            graph.stations.Insert(0, new StationData
            {
                stationId = "ST_Start",
                stationName = "Start",
                edgeId = graph.edges[0].edgeId,
                distanceFromEdgeStart = Mathf.Min(50f, graph.edges[0].lengthM * 0.25f)
            });
        }

        AssignSequentialBlockIds(graph);
        graph.RecalculateNodeHeightsFromVerticalProfiles();
        graph.UpdateNodeTypesAndJunctionIds();
        graph.SyncTurnoutStates();

        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
        Debug.Log(
            $"Created 20km grade/station/junction course. seed={seed}, intervals={intervals.Count}, nodes={graph.nodes.Count}, edges={graph.edges.Count}, stations={graph.stations.Count}",
            graph
        );
    }

    private static bool Is20kmPassingLoopSection(int sectionIndex, int sectionCount)
    {
        if (sectionIndex < 0 || sectionIndex >= sectionCount - 1)
        {
            return false;
        }

        return (sectionIndex + 1) % 4 == 0;
    }

    private static List<float> BuildRandomStationIntervals(
        System.Random random,
        float targetLengthM,
        float minIntervalM,
        float maxIntervalM)
    {
        var intervals = new List<float>();
        float accumulatedM = 0f;

        while (accumulatedM < targetLengthM - 0.001f)
        {
            float remainingM = targetLengthM - accumulatedM;
            if (remainingM <= maxIntervalM)
            {
                if (remainingM < minIntervalM && intervals.Count > 0)
                {
                    intervals[intervals.Count - 1] += remainingM;
                }
                else
                {
                    intervals.Add(remainingM);
                }

                break;
            }

            float maxAllowedIntervalM = Mathf.Min(maxIntervalM, remainingM - minIntervalM);
            float nextIntervalM = RandomRange(random, minIntervalM, maxAllowedIntervalM);
            intervals.Add(nextIntervalM);
            accumulatedM += nextIntervalM;
        }

        return intervals;
    }

    private static CourseSectionResult AddPlainSectionWithStationNearEnd(
        TrackBuilder builder,
        TrackGraph graph,
        float lengthM,
        string nodeId,
        string stationId,
        string stationName,
        float speedLimitKmH)
    {
        builder.AddStraight(lengthM);
        builder.PutNode(nodeId, speedLimitKmH);
        string edgeId = graph.edges.Count > 0 ? graph.edges[graph.edges.Count - 1].edgeId : null;
        builder.AddStation(stationId, stationName, Mathf.Clamp(lengthM - 80f, 50f, lengthM));
        return new CourseSectionResult
        {
            mainEdgeId = edgeId,
            lengthM = lengthM
        };
    }

    private static CourseSectionResult AddCurvedSectionWithStationNearEnd(
        TrackBuilder builder,
        TrackGraph graph,
        float lengthM,
        string nodeId,
        string stationId,
        string stationName,
        float speedLimitKmH,
        float radiusM,
        float curveLengthM,
        float curveSign)
    {
        float transitionLengthM = 80f;
        float safeCurveLengthM = Mathf.Max(0f, curveLengthM);
        float requiredCurveLengthM = (transitionLengthM * 2f) + safeCurveLengthM;
        float straightBudgetM = Mathf.Max(0f, lengthM - requiredCurveLengthM);
        float firstStraightM = straightBudgetM * 0.5f;
        float lastStraightM = straightBudgetM - firstStraightM;
        float signedRadiusM = Mathf.Abs(radiusM) * (curveSign >= 0f ? 1f : -1f);

        if (firstStraightM > 0f)
        {
            builder.AddStraight(firstStraightM);
        }

        builder.AddClothoidIn(transitionLengthM, signedRadiusM);
        if (safeCurveLengthM > 0f)
        {
            builder.AddCurve(safeCurveLengthM, signedRadiusM);
        }
        builder.AddClothoidOut(transitionLengthM, signedRadiusM);

        if (lastStraightM > 0f)
        {
            builder.AddStraight(lastStraightM);
        }

        builder.PutNode(nodeId, speedLimitKmH);
        string edgeId = graph.edges.Count > 0 ? graph.edges[graph.edges.Count - 1].edgeId : null;
        builder.AddStation(stationId, stationName, Mathf.Clamp(lengthM - 80f, 50f, lengthM));
        return new CourseSectionResult
        {
            mainEdgeId = edgeId,
            lengthM = lengthM
        };
    }

    private static CourseSectionResult AddPassingLoopSectionWithStationNearEnd(
        TrackBuilder builder,
        TrackGraph graph,
        string prefix,
        string stationId,
        string stationName,
        float mainLengthM,
        float mainSpeedLimitKmH,
        float sidingSpeedLimitKmH,
        float sideSign)
    {
        TrackNode startNode = builder.LastNode;
        if (startNode == null)
        {
            startNode = builder.PutNode($"{prefix}_Start", mainSpeedLimitKmH);
        }

        int sidingEdgeIndex = graph.edges.Count;
        TrackNode endNode = CreatePassingSidingEndNode(builder, startNode, $"{prefix}_End", mainLengthM, sidingSpeedLimitKmH, sideSign);
        string sidingEdgeId = sidingEdgeIndex >= 0 && sidingEdgeIndex < graph.edges.Count ? graph.edges[sidingEdgeIndex].edgeId : null;

        builder.StartFrom(startNode);
        builder.AddStraight(mainLengthM);
        int mainEdgeIndex = graph.edges.Count;
        builder.ConnectToNode(endNode, mainSpeedLimitKmH);
        string mainEdgeId = graph.edges[mainEdgeIndex].edgeId;
        builder.AddStation(stationId, stationName, Mathf.Clamp(mainLengthM - 80f, 50f, mainLengthM));

        graph.SetTurnoutSelectedEdge(startNode.nodeId, mainEdgeId);
        builder.StartFrom(endNode);

        return new CourseSectionResult
        {
            mainEdgeId = mainEdgeId,
            sidingEdgeId = sidingEdgeId,
            lengthM = mainLengthM
        };
    }

    private static void ApplyReadableGradeProfile(TrackEdge edge, float startGradientPermille, float endGradientPermille)
    {
        if (edge == null)
        {
            return;
        }

        float lengthM = Mathf.Max(0f, edge.lengthM);
        if (lengthM <= 0.001f)
        {
            edge.verticalSegments.Clear();
            return;
        }

        float transitionLengthM = Mathf.Min(220f, lengthM * 0.35f);
        float constantLengthM = Mathf.Max(0f, lengthM - transitionLengthM);
        edge.verticalSegments = new List<TrackVerticalSegment>
        {
            new TrackVerticalSegment
            {
                startDistanceM = 0f,
                lengthM = transitionLengthM,
                startGradientPermille = startGradientPermille,
                endGradientPermille = endGradientPermille
            }
        };

        if (constantLengthM > 0.001f)
        {
            edge.verticalSegments.Add(new TrackVerticalSegment
            {
                startDistanceM = transitionLengthM,
                lengthM = constantLengthM,
                startGradientPermille = endGradientPermille,
                endGradientPermille = endGradientPermille
            });
        }
    }

    private static void ApplyFlatGradeProfile(TrackEdge edge)
    {
        if (edge == null)
        {
            return;
        }

        float lengthM = Mathf.Max(0f, edge.lengthM);
        if (lengthM <= 0.001f)
        {
            edge.verticalSegments.Clear();
            return;
        }

        edge.verticalSegments = new List<TrackVerticalSegment>
        {
            new TrackVerticalSegment
            {
                startDistanceM = 0f,
                lengthM = lengthM,
                startGradientPermille = 0f,
                endGradientPermille = 0f
            }
        };
    }

    private static float RandomRealisticGradePermille(System.Random random)
    {
        if (random.NextDouble() < 0.7)
        {
            return 0f;
        }

        float[] magnitudes = { 10f, 12f, 15f, 18f, 20f, 22f, 25f };
        float sign = random.NextDouble() < 0.5 ? -1f : 1f;
        return magnitudes[random.Next(magnitudes.Length)] * sign;
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        if (max <= min)
        {
            return min;
        }

        return min + ((float)random.NextDouble() * (max - min));
    }

    private static string ResolveGeneratedStationId(
        int stationNumber,
        float routeDistanceM,
        float targetLengthM,
        bool isFinalStation,
        ref bool hasAssignedMidStation)
    {
        if (isFinalStation)
        {
            return "ST_End";
        }

        if (!hasAssignedMidStation && routeDistanceM >= targetLengthM * 0.5f)
        {
            hasAssignedMidStation = true;
            return "ST_Mid";
        }

        return $"ST_G20_{stationNumber:00}";
    }

    private static string ResolveGeneratedStationName(string stationId, int stationNumber)
    {
        switch (stationId)
        {
            case "ST_Mid":
                return "Mid";
            case "ST_End":
                return "End";
            default:
                return $"G20-{stationNumber:00}";
        }
    }

    /// <summary>
    /// 役割: 単純な本線区間を追加し、必要ならその区間上に駅を配置します。
    /// </summary>
    /// <param name="builder">線路を追加する TrackBuilder を指定します。</param>
    /// <param name="lengthM">区間長[m]を指定します。</param>
    /// <param name="nodeId">区間終端ノードIDを指定します。</param>
    /// <param name="stationId">駅を置く場合の stationId を指定します。</param>
    /// <param name="stationName">駅を置く場合の表示名を指定します。</param>
    /// <param name="stationOffsetM">駅を置く場合のエッジ始点からの距離[m]を指定します。</param>
    /// <param name="speedLimitKmH">区間の速度制限[km/h]を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private static void AddPlainSection(
        TrackBuilder builder,
        float lengthM,
        string nodeId,
        string stationId,
        string stationName,
        float stationOffsetM,
        float speedLimitKmH
    )
    {
        builder.AddStraight(lengthM);
        builder.PutNode(nodeId, speedLimitKmH);

        if (!string.IsNullOrEmpty(stationId))
        {
            builder.AddStation(stationId, stationName, Mathf.Clamp(stationOffsetM, 0f, lengthM));
        }
    }

    /// <summary>
    /// 役割: 曲線を含む単線区間を追加し、必要ならその区間上に駅を配置します。
    /// </summary>
    /// <param name="builder">線路を追加する TrackBuilder を指定します。</param>
    /// <param name="lengthM">区間長[m]を指定します。</param>
    /// <param name="nodeId">区間終端ノードIDを指定します。</param>
    /// <param name="stationId">駅を置く場合の stationId を指定します。</param>
    /// <param name="stationName">駅を置く場合の表示名を指定します。</param>
    /// <param name="stationOffsetM">駅を置く場合のエッジ始点からの距離[m]を指定します。</param>
    /// <param name="speedLimitKmH">区間の速度制限[km/h]を指定します。</param>
    /// <param name="radiusM">曲線半径[m]を指定します。正負で曲がる向きを指定します。</param>
    /// <param name="curveLengthM">円曲線部分の長さ[m]を指定します。</param>
    /// <param name="curveSign">曲線を出す向きを指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private static void AddCurvedSection(
        TrackBuilder builder,
        float lengthM,
        string nodeId,
        string stationId,
        string stationName,
        float stationOffsetM,
        float speedLimitKmH,
        float radiusM,
        float curveLengthM,
        float curveSign
    )
    {
        float transitionLengthM = 80f;
        float safeCurveLengthM = Mathf.Max(0f, curveLengthM);
        float requiredCurveLengthM = (transitionLengthM * 2f) + safeCurveLengthM;
        float straightBudgetM = Mathf.Max(0f, lengthM - requiredCurveLengthM);
        float firstStraightM = straightBudgetM * 0.5f;
        float lastStraightM = straightBudgetM - firstStraightM;
        float signedRadiusM = Mathf.Abs(radiusM) * (curveSign >= 0f ? 1f : -1f);

        if (firstStraightM > 0f)
        {
            builder.AddStraight(firstStraightM);
        }

        builder.AddClothoidIn(transitionLengthM, signedRadiusM);
        if (safeCurveLengthM > 0f)
        {
            builder.AddCurve(safeCurveLengthM, signedRadiusM);
        }
        builder.AddClothoidOut(transitionLengthM, signedRadiusM);

        if (lastStraightM > 0f)
        {
            builder.AddStraight(lastStraightM);
        }

        builder.PutNode(nodeId, speedLimitKmH);

        if (!string.IsNullOrEmpty(stationId))
        {
            builder.AddStation(stationId, stationName, Mathf.Clamp(stationOffsetM, 0f, lengthM));
        }
    }

    /// <summary>
    /// 役割: 本線と待避線を持つ主要駅ユニットを追加します。
    /// </summary>
    /// <param name="builder">線路を追加する TrackBuilder を指定します。</param>
    /// <param name="graph">分岐選択を後で本線側へ固定する TrackGraph を指定します。</param>
    /// <param name="prefix">生成するノードIDの接頭辞を指定します。</param>
    /// <param name="stationId">駅の stationId を指定します。</param>
    /// <param name="stationName">駅の表示名を指定します。</param>
    /// <param name="mainLengthM">主要駅ユニットの本線長[m]を指定します。</param>
    /// <param name="mainSpeedLimitKmH">本線側の速度制限[km/h]を指定します。</param>
    /// <param name="sidingSpeedLimitKmH">待避線側の速度制限[km/h]を指定します。</param>
    /// <param name="sideSign">待避線を出す向きを指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private static void AddMajorStationWithPassingLoop(
        TrackBuilder builder,
        TrackGraph graph,
        string prefix,
        string stationId,
        string stationName,
        float mainLengthM,
        float mainSpeedLimitKmH,
        float sidingSpeedLimitKmH,
        float sideSign
    )
    {
        TrackNode startNode = builder.LastNode;
        if (startNode == null)
        {
            startNode = builder.PutNode($"{prefix}_Start", mainSpeedLimitKmH);
        }

        TrackNode endNode = CreatePassingSidingEndNode(builder, startNode, $"{prefix}_End", mainLengthM, sidingSpeedLimitKmH, sideSign);

        builder.StartFrom(startNode);
        builder.AddStraight(mainLengthM);
        int mainEdgeIndex = graph.edges.Count;
        builder.ConnectToNode(endNode, mainSpeedLimitKmH);
        string mainEdgeId = graph.edges[mainEdgeIndex].edgeId;
        builder.AddStation(stationId, stationName, mainLengthM * 0.5f);

        // 分岐の既定進路は本線にします。待避線を試したい場合は Inspector の turnoutStates で切り替えます。
        graph.SetTurnoutSelectedEdge(startNode.nodeId, mainEdgeId);
        builder.StartFrom(endNode);
    }

    /// <summary>
    /// 役割: 待避線側を先に生成し、その終点ノードを返します。
    /// </summary>
    /// <param name="builder">線路を追加する TrackBuilder を指定します。</param>
    /// <param name="startNode">待避線の分岐開始ノードを指定します。</param>
    /// <param name="endNodeId">待避線終点ノードIDを指定します。</param>
    /// <param name="mainLengthM">本線と同じ前進距離[m]を指定します。</param>
    /// <param name="speedLimitKmH">待避線側の速度制限[km/h]を指定します。</param>
    /// <param name="sideSign">待避線を出す向きを指定します。</param>
    /// <returns>待避線の終点ノードを返します。</returns>
    private static TrackNode CreatePassingSidingEndNode(
        TrackBuilder builder,
        TrackNode startNode,
        string endNodeId,
        float mainLengthM,
        float speedLimitKmH,
        float sideSign
    )
    {
        float direction = sideSign >= 0f ? 1f : -1f;
        float transitionLengthM = 35f;
        float radiusM = 240f * direction;
        float diagonalLengthM = 35f;

        builder.StartFrom(startNode);
        builder.AddClothoidInOut(transitionLengthM, radiusM);
        builder.AddStraight(diagonalLengthM);
        builder.AddClothoidInOut(transitionLengthM, -radiusM);

        float forwardAdvanceM = Vector3.Dot(
            builder.currentPos - startNode.worldPosition,
            startNode.worldRotation * Vector3.forward
        );
        float middleStraightM = Mathf.Max(80f, mainLengthM - forwardAdvanceM * 2f);
        builder.AddStraight(middleStraightM);

        builder.AddClothoidInOut(transitionLengthM, -radiusM);
        builder.AddStraight(diagonalLengthM);
        builder.AddClothoidInOut(transitionLengthM, radiusM);
        return builder.PutNode(endNodeId, speedLimitKmH);
    }

    /// <summary>
    /// 役割: 在線管理で使いやすいように、全エッジへ連番の blockId を割り当てます。
    /// </summary>
    /// <param name="graph">blockId を割り当てる TrackGraph を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private static void AssignSequentialBlockIds(TrackGraph graph)
    {
        for (int i = 0; i < graph.edges.Count; i++)
        {
            TrackEdge edge = graph.edges[i];
            if (edge == null)
            {
                continue;
            }

            edge.blockId = $"B{i + 1:000}";
        }
    }

    /// <summary>
    /// 役割: 勾配・縦曲線・勾配抵抗の確認に使う2km直線コースを生成します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void CreateGradeTestTrack()
    {
        var graph = (TrackGraph)target;
        graph.nodes.Clear();
        graph.edges.Clear();
        graph.turnoutStates.Clear();
        graph.stations.Clear();

        TrackBuilder builder = new TrackBuilder(graph);
        builder.Start(Vector3.zero, Quaternion.identity);
        builder.AddStraight(2000f);
        builder.PutNode("GRADE_Test_End", 100f);

        if (graph.edges.Count > 0)
        {
            TrackEdge edge = graph.edges[0];
            edge.blockId = "B_GRADE_TEST";
            edge.speedLimitMS = 100f / 3.6f;
            edge.verticalSegments = new List<TrackVerticalSegment>
            {
                // 0-200m: 平坦
                new TrackVerticalSegment
                {
                    startDistanceM = 0f,
                    lengthM = 200f,
                    startGradientPermille = 0f,
                    endGradientPermille = 0f
                },
                // 200-400m: 0 -> +35‰ の縦曲線
                new TrackVerticalSegment
                {
                    startDistanceM = 200f,
                    lengthM = 200f,
                    startGradientPermille = 0f,
                    endGradientPermille = 35f
                },
                // 400-800m: +35‰ 上り一定
                new TrackVerticalSegment
                {
                    startDistanceM = 400f,
                    lengthM = 400f,
                    startGradientPermille = 35f,
                    endGradientPermille = 35f
                },
                // 800-1000m: +35 -> 0‰ の縦曲線
                new TrackVerticalSegment
                {
                    startDistanceM = 800f,
                    lengthM = 200f,
                    startGradientPermille = 35f,
                    endGradientPermille = 0f
                },
                // 1000-1200m: 平坦
                new TrackVerticalSegment
                {
                    startDistanceM = 1000f,
                    lengthM = 200f,
                    startGradientPermille = 0f,
                    endGradientPermille = 0f
                },
                // 1200-1400m: 0 -> -35‰ の縦曲線
                new TrackVerticalSegment
                {
                    startDistanceM = 1200f,
                    lengthM = 200f,
                    startGradientPermille = 0f,
                    endGradientPermille = -35f
                },
                // 1400-1800m: -35‰ 下り一定
                new TrackVerticalSegment
                {
                    startDistanceM = 1400f,
                    lengthM = 400f,
                    startGradientPermille = -35f,
                    endGradientPermille = -35f
                },
                // 1800-2000m: -35 -> 0‰ の縦曲線
                new TrackVerticalSegment
                {
                    startDistanceM = 1800f,
                    lengthM = 200f,
                    startGradientPermille = -35f,
                    endGradientPermille = 0f
                }
            };
        }

        builder.AddStation("ST_Start", "Grade Test Start", offsetMFromNode: 50f);
        builder.AddStation("ST_Mid", "Grade Test Summit", offsetMFromNode: 1000f);
        builder.AddStation("ST_End", "Grade Test End", offsetMFromNode: 1950f);

        graph.RecalculateNodeHeightsFromVerticalProfiles();
        graph.UpdateNodeTypesAndJunctionIds();
        graph.SyncTurnoutStates();

        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
        Debug.Log("Created Grade 2km test track. Profile: flat, +35permille climb, flat, -35permille descent.", graph);
    }

    /// <summary>
    /// 役割: TASC の停止パターン確認だけに使う約1kmの単純な直線線路を生成します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void CreateTascTestTrack()
    {
        var graph = (TrackGraph)target;
        graph.nodes.Clear();
        graph.edges.Clear();
        graph.turnoutStates.Clear();
        graph.stations.Clear();

        TrackBuilder builder = new TrackBuilder(graph);

        // TASC の挙動を見やすくするため、分岐や曲線を入れない 1km 直線にします。
        builder.Start(Vector3.zero, Quaternion.identity);
        builder.AddStraight(1000f);
        builder.PutNode("TASC_Test_End");

        // 停止目標を終端の少し手前に置き、過走しても線路上に余裕が残るようにします。
        // 既存の LocalTestService.asset に合わせます。
        // ST_Start は通過扱いなので、最初の停車対象になる ST_Mid をTASC確認用の停止駅にします。
        builder.AddStation("ST_Start", "TASC Test Start", offsetMFromNode: 50f);
        builder.AddStation("ST_Mid", "TASC Test Stop", offsetMFromNode: 950f);
        builder.AddStation("ST_End", "TASC Test End", offsetMFromNode: 990f);

        if (graph.edges.Count > 0)
        {
            TrackEdge edge = graph.edges[0];
            edge.blockId = "B_TASC_TEST";
            edge.speedLimitMS = 100f / 3.6f;
            edge.fromNodeId = graph.nodes[0].nodeId;
            edge.toNodeId = "TASC_Test_End";

            graph.nodes[0].outgoingEdgeIds.Clear();
            graph.nodes[0].outgoingEdgeIds.Add(edge.edgeId);
            for (int i = 0; i < graph.stations.Count; i++)
            {
                graph.stations[i].edgeId = edge.edgeId;
            }
        }

        graph.UpdateNodeTypesAndJunctionIds();
        graph.SyncTurnoutStates();

        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
        Debug.Log($"Created TASC 1km test track. ST_Mid stop station is at 950m on {graph.edges[0].edgeId}.", graph);
    }
}
