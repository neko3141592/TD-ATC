using System.Collections.Generic;
using UnityEngine;

public class BlockOccupancyManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrackGraph trackGraph;
    [SerializeField] private List<TrainController> trains = new();
    [SerializeField] private bool autoCollectTrains = true;

    [Header("Trace Settings")]
    [SerializeField, Min(0f)] private float lookaheadDistanceM = 3000f;

    // blockId -> Set<trainId>
    private Dictionary<string, HashSet<string>> occupiedTrainsByBlock = new();
    // trainId -> Set<blockId>
    private Dictionary<string, HashSet<string>> occupiedBlocksByTrain = new();

    // トレース結果を使い回して、毎フレームの不要な確保を避けます。
    private readonly List<TrackTraceSegment> behindSegments = new();
    private readonly List<TrackTraceSegment> aheadSegments = new();
    private readonly List<string> sortedBlockBuffer = new();
    private readonly List<TrainController> activeTrains = new();

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

        CollectActiveTrains(activeTrains);
        for (int i = 0; i < activeTrains.Count; i++)
        {
            RebuildSingleTrainOccupancy(activeTrains[i], nextOccupiedTrainsByBlock, nextOccupiedBlocksByTrain);
        }

        occupiedTrainsByBlock = nextOccupiedTrainsByBlock;
        occupiedBlocksByTrain = nextOccupiedBlocksByTrain;
    }

    private void CollectActiveTrains(List<TrainController> results)
    {
        results.Clear();

        for (int i = 0; i < trains.Count; i++)
        {
            AddTrainIfValid(trains[i], results);
        }

        if (!autoCollectTrains)
        {
            return;
        }

        TrainController[] foundTrains = Object.FindObjectsByType<TrainController>();
        for (int i = 0; i < foundTrains.Length; i++)
        {
            AddTrainIfValid(foundTrains[i], results);
        }
    }

    private void AddTrainIfValid(TrainController targetTrain, List<TrainController> results)
    {
        if (targetTrain == null || !targetTrain.isActiveAndEnabled || results.Contains(targetTrain))
        {
            return;
        }

        results.Add(targetTrain);
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

        TrackEdge currentEdge = activeTrackGraph.FindEdge(targetTrain.CurrentEdgeId);
        string currentBlockId = GetBlockIdAt(currentEdge, targetTrain.DistanceOnEdgeM);
        if (!string.IsNullOrEmpty(currentBlockId))
        {
            results.Add(currentBlockId);
        }

        float tailOffsetM = Mathf.Max(0f, targetTrain.GetTailEndOffsetFromHeadM());
        if (tailOffsetM > 0f &&
            TrackRouteTracer.TryTraceBehind(
                activeTrackGraph,
                targetTrain.CurrentEdgeId,
                targetTrain.DistanceOnEdgeM,
                tailOffsetM,
                behindSegments,
                targetTrain.CurrentDirection
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
                aheadSegments,
                targetTrain.CurrentDirection
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
            TrackTraceSegment segment = segments[i];
            TrackEdge edge = graph.FindEdge(segment.edgeId);
            CollectBlockIdsInRange(
                edge,
                segment.startDistanceOnEdgeM,
                segment.endDistanceOnEdgeM,
                results
            );
        }
    }

    private string GetBlockIdAt(TrackEdge edge, float distanceM)
    {
        if (edge == null) {
            return null;
        }

        if (edge.blockSections != null && edge.blockSections.Count > 0)
        {
            float clampedDistanceM = Mathf.Clamp(distanceM, 0f, Mathf.Max(0f, edge.lengthM));

            for (int i = 0; i < edge.blockSections.Count; i++)
            {
                BlockSection currentSection = edge.blockSections[i];
                if (currentSection == null || string.IsNullOrEmpty(currentSection.blockId))
                {
                    continue;
                }

                bool isLastSection = i == edge.blockSections.Count - 1;

                if (isLastSection)
                {
                    if (clampedDistanceM >= currentSection.startDistanceM && clampedDistanceM <= currentSection.endDistanceM)
                    {
                        return currentSection.blockId;
                    }
                } 
                else
                {
                    if (clampedDistanceM >= currentSection.startDistanceM && clampedDistanceM < currentSection.endDistanceM)
                    {
                        return currentSection.blockId;
                    }
                }
            }
        }

        return null;
    }

    private bool CollectBlockIdsInRange(TrackEdge edge, float startDistanceM, float endDistanceM, HashSet<string> results)
    {
        if (edge == null || results == null)
        {
            return false;
        }

        if (edge.blockSections == null || edge.blockSections.Count == 0)
        {
            Debug.LogWarning($"{nameof(BlockOccupancyManager)} on {name}: edge '{edge.edgeId}' does not have blockSections.", this);
            return false;
        }

        float edgeLengthM = Mathf.Max(0f, edge.lengthM);
        float clampedStartDistanceM = Mathf.Clamp(Mathf.Min(startDistanceM, endDistanceM), 0f, edgeLengthM);
        float clampedEndDistanceM = Mathf.Clamp(Mathf.Max(startDistanceM, endDistanceM), 0f, edgeLengthM);
        bool added = false;

        for (int i = 0; i < edge.blockSections.Count; i++)
        {
            BlockSection currentSection = edge.blockSections[i];
            if (currentSection == null || string.IsNullOrEmpty(currentSection.blockId))
            {
                continue;
            }

            if (IsOverlap(currentSection.startDistanceM, currentSection.endDistanceM, clampedStartDistanceM, clampedEndDistanceM))
            {
                results.Add(currentSection.blockId);
                added = true;
            }
        }

        return added;
    }

    private bool IsOverlap(float a, float b, float c, float d)
    {
        return Mathf.Max(a, c) < Mathf.Min(b, d);
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
    /// <param name="nextBlockOnly">true の場合は、現在閉塞の次にある閉塞だけを検査します。</param>
    /// <returns>自列車以外が在線している前方 block を見つけた場合は true、それ以外は false を返します。</returns>
    public bool TryFindFirstOccupiedBlockAhead(
        TrainController targetTrain,
        out string occupiedBlockId,
        out float distanceToBlockM,
        bool nextBlockOnly = false
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

        TrackEdge currentEdge = activeTrackGraph.FindEdge(targetTrain.CurrentEdgeId);
        string currentBlockId = GetBlockIdAt(currentEdge, targetTrain.DistanceOnEdgeM);

        if (!TrackRouteTracer.TryTraceAhead(
                activeTrackGraph,
                targetTrain.CurrentEdgeId,
                targetTrain.DistanceOnEdgeM,
                lookaheadDistanceM,
                aheadSegments,
                targetTrain.CurrentDirection
            ))
        {
            return false;
        }

        HashSet<string> visitedBlocks = new();
        for (int i = 0; i < aheadSegments.Count; i++)
        {
            TrackTraceSegment segment = aheadSegments[i];
            TrackEdge edge = activeTrackGraph.FindEdge(segment.edgeId);

            if (edge == null)
            {
                continue;
            }

            if (edge.blockSections == null || edge.blockSections.Count == 0)
            {
                Debug.LogWarning($"{nameof(BlockOccupancyManager)} on {name}: edge '{edge.edgeId}' does not have blockSections.", this);
                continue;
            }

            int sectionStartIndex = segment.direction == EdgeTravelDirection.AtoB
                ? 0
                : edge.blockSections.Count - 1;
            int sectionEndIndex = segment.direction == EdgeTravelDirection.AtoB
                ? edge.blockSections.Count
                : -1;
            int sectionStep = segment.direction == EdgeTravelDirection.AtoB ? 1 : -1;

            for (int j = sectionStartIndex; j != sectionEndIndex; j += sectionStep)
            {
                BlockSection currentSection = edge.blockSections[j];
                if (currentSection == null || string.IsNullOrEmpty(currentSection.blockId))
                {
                    continue;
                }

                if (!IsOverlap(
                        currentSection.startDistanceM,
                        currentSection.endDistanceM,
                        segment.startDistanceOnEdgeM,
                        segment.endDistanceOnEdgeM))
                {
                    continue;
                }

                if (!visitedBlocks.Add(currentSection.blockId))
                {
                    continue;
                }

                float overlapStartM = Mathf.Max(
                    currentSection.startDistanceM,
                    segment.startDistanceOnEdgeM
                );
                float overlapEndM = Mathf.Min(
                    currentSection.endDistanceM,
                    segment.endDistanceOnEdgeM
                );

                if (nextBlockOnly && !string.IsNullOrEmpty(currentBlockId) && currentSection.blockId == currentBlockId)
                {
                    continue;
                }

                float blockDistanceFromTrainM =
                    segment.direction == EdgeTravelDirection.AtoB
                        ? segment.startDistanceFromOriginM + (overlapStartM - segment.startDistanceOnEdgeM)
                        : segment.startDistanceFromOriginM + (segment.endDistanceOnEdgeM - overlapEndM);

                if (nextBlockOnly)
                {
                    if (IsBlockOccupiedByOtherTrain(currentSection.blockId, targetTrain.TrainId))
                    {
                        occupiedBlockId = currentSection.blockId;
                        distanceToBlockM = blockDistanceFromTrainM;
                        return true;
                    }

                    occupiedBlockId = null;
                    distanceToBlockM = 0f;
                    return false;
                }

                if (!IsBlockOccupiedByOtherTrain(currentSection.blockId, targetTrain.TrainId))
                {
                    continue;
                }

                occupiedBlockId = currentSection.blockId;
                distanceToBlockM = blockDistanceFromTrainM;
                return true;
            }
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
