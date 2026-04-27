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

        if (GUILayout.Button("Create TASC 1km Test Track"))
        {
            CreateTascTestTrack();
        }

        if (GUILayout.Button("Create 10km Double Track Course"))
        {
            CreateDoubleTrack10kmCourse();
        }
    }

    /// <summary>
    /// 役割: 駅と分岐を多めに含む、約10kmの複線テストコースを生成します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void CreateDoubleTrack10kmCourse()
    {
        var graph = (TrackGraph)target;
        graph.nodes.Clear();
        graph.edges.Clear();
        graph.turnoutStates.Clear();
        graph.stations.Clear();

        // 上り線はプレイヤー列車の試験用として、0mから10km方向へ進みます。
        TrackBuilder upBuilder = new TrackBuilder(graph);
        upBuilder.Start(new Vector3(-2.2f, 0f, 0f), Quaternion.identity);

        AddPlainSection(upBuilder, 700f, "UP_00700", "ST_Start", "Start", 300f, 120f);
        AddPlainSection(upBuilder, 800f, "UP_01500", null, null, 0f, 120f);
        AddMajorStationWithPassingLoop(upBuilder, graph, "UP_Mid", "ST_Mid", "Mid", 900f, 120f, 45f, -1f);
        AddPlainSection(upBuilder, 900f, "UP_03300", "ST_Pass", "Pass", 450f, 110f);
        AddMajorStationWithPassingLoop(upBuilder, graph, "UP_Center", "ST_Center", "Center", 900f, 120f, 45f, -1f);
        AddPlainSection(upBuilder, 1100f, "UP_05300", "ST_Park", "Park", 650f, 100f);
        AddMajorStationWithPassingLoop(upBuilder, graph, "UP_Yard", "ST_Yard", "Yard", 900f, 100f, 45f, -1f);
        AddPlainSection(upBuilder, 1200f, "UP_07400", "ST_Harbor", "Harbor", 600f, 100f);
        AddMajorStationWithPassingLoop(upBuilder, graph, "UP_EndStation", "ST_End", "End", 900f, 90f, 45f, -1f);
        AddPlainSection(upBuilder, 1700f, "UP_End", null, null, 0f, 120f);

        // 下り線は10km側から0m側へ戻る方向に作り、見た目と経路データの両方を複線化します。
        TrackBuilder downBuilder = new TrackBuilder(graph);
        downBuilder.Start(new Vector3(2.2f, 0f, 10000f), Quaternion.Euler(0f, 180f, 0f));

        AddPlainSection(downBuilder, 700f, "DN_09300", "ST_D_End", "Down End", 300f, 120f);
        AddPlainSection(downBuilder, 800f, "DN_08500", null, null, 0f, 120f);
        AddMajorStationWithPassingLoop(downBuilder, graph, "DN_Suburb", "ST_D_Suburb", "Down Suburb", 900f, 120f, 45f, -1f);
        AddPlainSection(downBuilder, 900f, "DN_06700", "ST_D_Harbor", "Down Harbor", 450f, 110f);
        AddMajorStationWithPassingLoop(downBuilder, graph, "DN_Yard", "ST_D_Yard", "Down Yard", 900f, 120f, 45f, -1f);
        AddPlainSection(downBuilder, 1100f, "DN_04700", "ST_D_Park", "Down Park", 650f, 100f);
        AddMajorStationWithPassingLoop(downBuilder, graph, "DN_Center", "ST_D_Center", "Down Center", 900f, 100f, 45f, -1f);
        AddPlainSection(downBuilder, 1200f, "DN_02600", "ST_D_River", "Down River", 600f, 100f);
        AddMajorStationWithPassingLoop(downBuilder, graph, "DN_Town", "ST_D_Town", "Down Town", 900f, 90f, 45f, -1f);
        AddPlainSection(downBuilder, 1700f, "DN_End", "ST_D_Start", "Down Start", 1300f, 120f);

        AssignSequentialBlockIds(graph);

        graph.UpdateNodeTypesAndJunctionIds();
        graph.SyncTurnoutStates();

        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
        Debug.Log($"Created 10km double track course. nodes={graph.nodes.Count}, edges={graph.edges.Count}, stations={graph.stations.Count}", graph);
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
