using System.Collections.Generic;
using UnityEngine;

public class BlockOccupancyManager : MonoBehaviour
{
    [SerializeField] private TrackGraph trackGraph;
    [SerializeField] private TrainController train;
    [SerializeField] private List<TrainController> additionalTrains = new();
    [SerializeField, Min(0f)] private float lookaheadDistanceM = 3000f;

    // blockId -> Set<trainId>
    private Dictionary<string, HashSet<string>> occupiedTrainsByBlock = new();
    // trainId -> Set<blockId>
    private Dictionary<string, HashSet<string>> occupiedBlocksByTrain = new();

    // トレース結果を使い回して、毎フレームの不要な確保を避けます。
    private readonly List<TrackTraceSegment> behindSegments = new();
    private readonly List<TrackTraceSegment> aheadSegments = new();
    private readonly List<string> sortedBlockBuffer = new();

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

    /// <summary>
    /// 役割: 指定列車が現在占有している blockId 一覧を文字列として返します。
    /// </summary>
    /// <param name="targetTrain">占有 block を確認したい列車を指定します。</param>
    /// <returns>表示用に整形した blockId 一覧を返します。列車が未登録なら "--" を返します。</returns>
    public string GetOccupiedBlocksLabel(TrainController targetTrain)
    {
        if (targetTrain == null || string.IsNullOrEmpty(targetTrain.TrainId))
        {
            return "--";
        }

        if (!occupiedBlocksByTrain.TryGetValue(targetTrain.TrainId, out HashSet<string> occupiedBlocks) ||
            occupiedBlocks == null ||
            occupiedBlocks.Count == 0)
        {
            return "--";
        }

        sortedBlockBuffer.Clear();
        foreach (string blockId in occupiedBlocks)
        {
            sortedBlockBuffer.Add(blockId);
        }

        sortedBlockBuffer.Sort(System.StringComparer.Ordinal);
        return string.Join(", ", sortedBlockBuffer);
    }

    /// <summary>
    /// 役割: 指定列車の前方で最初に在線している block を検索します。
    /// </summary>
    /// <param name="targetTrain">前方在線を調べる自列車を指定します。</param>
    /// <param name="occupiedBlockId">最初に見つかった在線 blockId を受け取ります。</param>
    /// <param name="distanceToBlockM">自列車先頭基準点からその block までの距離[m]を受け取ります。</param>
    /// <returns>自列車以外が在線している前方 block を見つけた場合は true、それ以外は false を返します。</returns>
    public bool TryFindFirstOccupiedBlockAhead(
        TrainController targetTrain,
        out string occupiedBlockId,
        out float distanceToBlockM
    )
    {
        occupiedBlockId = null;
        distanceToBlockM = 0f;

        if (targetTrain == null)
        {
            return false;
        }

        TrackGraph activeTrackGraph = trackGraph != null ? trackGraph : targetTrain.Graph;
        if (activeTrackGraph == null || string.IsNullOrEmpty(targetTrain.CurrentEdgeId))
        {
            return false;
        }

        if (!TrackRouteTracer.TryTraceAhead(
                activeTrackGraph,
                targetTrain.CurrentEdgeId,
                targetTrain.DistanceOnEdgeM,
                lookaheadDistanceM,
                aheadSegments
            ))
        {
            return false;
        }

        HashSet<string> visitedBlocks = new();
        for (int i = 0; i < aheadSegments.Count; i++)
        {
            TrackTraceSegment segment = aheadSegments[i];
            TrackEdge edge = activeTrackGraph.FindEdge(segment.edgeId);
            if (edge == null || string.IsNullOrEmpty(edge.blockId))
            {
                continue;
            }

            if (!visitedBlocks.Add(edge.blockId))
            {
                continue;
            }

            if (!IsBlockOccupiedByOtherTrain(edge.blockId, targetTrain.TrainId))
            {
                continue;
            }

            occupiedBlockId = edge.blockId;
            distanceToBlockM = segment.startDistanceFromOriginM;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 役割: 指定 block に自列車以外の列車が在線しているか判定します。
    /// </summary>
    /// <param name="blockId">在線有無を確認する blockId を指定します。</param>
    /// <param name="selfTrainId">除外したい自列車の trainId を指定します。</param>
    /// <returns>自列車以外が在線していれば true、それ以外は false を返します。</returns>
    private bool IsBlockOccupiedByOtherTrain(string blockId, string selfTrainId)
    {
        if (!occupiedTrainsByBlock.TryGetValue(blockId, out HashSet<string> trainsInBlock) ||
            trainsInBlock == null)
        {
            return false;
        }

        foreach (string trainId in trainsInBlock)
        {
            if (trainId != selfTrainId)
            {
                return true;
            }
        }

        return false;
    }
}
