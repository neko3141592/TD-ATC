using System.Collections.Generic;
using UnityEngine;

public class BlockOccupancyManager : MonoBehaviour
{
    [SerializeField] private TrackGraph trackGraph;
    [SerializeField] private TrainController train;
    [SerializeField] private List<TrainController> additionalTrains = new();

    // blockId -> Set<trainId>
    private Dictionary<string, HashSet<string>> occupiedTrainsByBlock = new();
    // trainId -> Set<blockId>
    private Dictionary<string, HashSet<string>> occupiedBlocksByTrain = new();

    // トレース結果を使い回して、毎フレームの不要な確保を避けます。
    private readonly List<TrackTraceSegment> behindSegments = new();
    private readonly List<TrackTraceSegment> aheadSegments = new();

    public IReadOnlyDictionary<string, HashSet<string>> OccupiedTrainsByBlock => occupiedTrainsByBlock;
    public IReadOnlyDictionary<string, HashSet<string>> OccupiedBlocksByTrain => occupiedBlocksByTrain;

    /// <summary>
    /// 役割: 毎フレームの更新処理を行います。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Update()
    {
        RebuildOccupancy();
    }

    /// <summary>
    /// 役割: 登録されている列車一覧から在線辞書を再構築します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void RebuildOccupancy()
    {
        Dictionary<string, HashSet<string>> nextOccupiedTrainsByBlock = new();
        Dictionary<string, HashSet<string>> nextOccupiedBlocksByTrain = new();

        RebuildSingleTrainOccupancy(train, nextOccupiedTrainsByBlock, nextOccupiedBlocksByTrain);

        for (int i = 0; i < additionalTrains.Count; i++)
        {
            TrainController additionalTrain = additionalTrains[i];
            if (additionalTrain == train)
            {
                continue;
            }

            RebuildSingleTrainOccupancy(additionalTrain, nextOccupiedTrainsByBlock, nextOccupiedBlocksByTrain);
        }

        occupiedTrainsByBlock = nextOccupiedTrainsByBlock;
        occupiedBlocksByTrain = nextOccupiedBlocksByTrain;
    }

    /// <summary>
    /// 役割: 1 編成ぶんの在線情報を辞書へ反映します。
    /// </summary>
    /// <param name="targetTrain">在線情報を更新する列車を指定します。</param>
    /// <param name="trainsByBlock">blockId ごとの在線列車辞書を指定します。</param>
    /// <param name="blocksByTrain">trainId ごとの在線閉塞辞書を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void RebuildSingleTrainOccupancy(
        TrainController targetTrain,
        Dictionary<string, HashSet<string>> trainsByBlock,
        Dictionary<string, HashSet<string>> blocksByTrain
    )
    {
        if (targetTrain == null)
        {
            return;
        }

        HashSet<string> occupiedBlocks = new();
        if (!CollectOccupiedBlocks(targetTrain, occupiedBlocks))
        {
            return;
        }

        blocksByTrain[targetTrain.TrainId] = occupiedBlocks;

        foreach (string occupiedBlock in occupiedBlocks)
        {
            if (!trainsByBlock.TryGetValue(occupiedBlock, out HashSet<string> trainsInBlock))
            {
                trainsInBlock = new HashSet<string>();
                trainsByBlock[occupiedBlock] = trainsInBlock;
            }

            trainsInBlock.Add(targetTrain.TrainId);
        }
    }

    /// <summary>
    /// 役割: 先頭から最後尾までにかかっている blockId を収集します。
    /// </summary>
    /// <param name="targetTrain">在線区間を調べる列車を指定します。</param>
    /// <param name="results">収集した blockId を受け取る集合です。</param>
    /// <returns>1 つ以上の blockId を収集できた場合は true、それ以外は false を返します。</returns>
    private bool CollectOccupiedBlocks(TrainController targetTrain, HashSet<string> results)
    {
        results.Clear();

        if (targetTrain == null)
        {
            Debug.LogError($"{nameof(BlockOccupancyManager)} on {name}: train is null.", this);
            return false;
        }

        TrackGraph activeTrackGraph = trackGraph != null ? trackGraph : targetTrain.Graph;
        if (activeTrackGraph == null)
        {
            Debug.LogError($"{nameof(BlockOccupancyManager)} on {name}: trackGraph is null.", this);
            return false;
        }

        if (string.IsNullOrEmpty(targetTrain.CurrentEdgeId))
        {
            return false;
        }

        // 先頭車中心が乗っているエッジの block を必ず含めます。
        AddBlockForEdge(activeTrackGraph, targetTrain.CurrentEdgeId, results);

        // 最後尾端まで後方をたどり、またいでいる block を追加します。
        float tailOffsetM = Mathf.Max(0f, targetTrain.GetTailEndOffsetFromHeadM());
        if (tailOffsetM > 0f &&
            TrackRouteTracer.TryTraceBehind(
                activeTrackGraph,
                targetTrain.CurrentEdgeId,
                targetTrain.DistanceOnEdgeM,
                tailOffsetM,
                behindSegments
            ))
        {
            AddBlocksFromSegments(activeTrackGraph, behindSegments, results);
        }

        // 先頭車中心より前に張り出しているぶんも block 集計へ含めます。
        float headForwardExtentM = Mathf.Max(0f, targetTrain.GetHeadForwardExtentM());
        if (headForwardExtentM > 0f &&
            TrackRouteTracer.TryTraceAhead(
                activeTrackGraph,
                targetTrain.CurrentEdgeId,
                targetTrain.DistanceOnEdgeM,
                headForwardExtentM,
                aheadSegments
            ))
        {
            AddBlocksFromSegments(activeTrackGraph, aheadSegments, results);
        }

        return results.Count > 0;
    }

    /// <summary>
    /// 役割: トレース区間に含まれる各エッジから blockId を抽出して集合へ追加します。
    /// </summary>
    /// <param name="graph">エッジ検索に使う TrackGraph を指定します。</param>
    /// <param name="segments">blockId を拾いたいトレース区間一覧を指定します。</param>
    /// <param name="results">抽出した blockId を受け取る集合です。</param>
    /// <remarks>返り値はありません。</remarks>
    private void AddBlocksFromSegments(TrackGraph graph, List<TrackTraceSegment> segments, HashSet<string> results)
    {
        for (int i = 0; i < segments.Count; i++)
        {
            AddBlockForEdge(graph, segments[i].edgeId, results);
        }
    }

    /// <summary>
    /// 役割: 指定エッジの blockId を 1 件だけ集合へ追加します。
    /// </summary>
    /// <param name="graph">対象エッジを検索する TrackGraph を指定します。</param>
    /// <param name="edgeId">blockId を調べる edgeId を指定します。</param>
    /// <param name="results">blockId を追加する集合です。</param>
    /// <remarks>返り値はありません。</remarks>
    private void AddBlockForEdge(TrackGraph graph, string edgeId, HashSet<string> results)
    {
        TrackEdge edge = graph.FindEdge(edgeId);
        if (edge == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(edge.blockId))
        {
            Debug.LogWarning($"{nameof(BlockOccupancyManager)} on {name}: edge '{edgeId}' does not have a blockId.", this);
            return;
        }

        results.Add(edge.blockId);
    }
}
