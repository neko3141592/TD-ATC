using UnityEngine;

public partial class TrainController
{
    /// <summary>
    /// 役割: MoveTrain の処理を移動処理を行います。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    void MoveTrain()
    {
        SyncCarTrackStatesWithConsist();
        EnsureRuntimeResolver();

        if (!TryResolveHeadPose(out Vector3 pos, out Vector3 tan))
        {
            return;
        }

        ApplyHeadPose(pos, tan);

        float requiredHistoryLengthM = GetRequiredHistoryLengthM();
        EnsureActiveEdgeHistory(requiredHistoryLengthM);
        TrimActiveEdgeHistory(requiredHistoryLengthM);
        UpdateCarTrackStates();
    }

    /// <summary>
    /// 役割: AdvanceEdgeTransitionIfNeeded の処理を進めます。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void AdvanceEdgeTransitionIfNeeded()
    {
        if (trackGraph == null || string.IsNullOrEmpty(currentEdgeId))
        {
            return;
        }

        const int maxTransitionsPerFrame = 256;
        int guard = 0;

        while (guard < maxTransitionsPerFrame)
        {
            guard++;

            TrackEdge currentEdge = trackGraph.FindEdge(currentEdgeId);
            if (currentEdge == null)
            {
                break;
            }

            float edgeLengthM = Mathf.Max(0f, currentEdge.lengthM);
            if (distanceOnEdgeM <= edgeLengthM)
            {
                break;
            }

            // 現エッジを超過した分を次エッジへ繰り越す。
            float remainDistanceM = distanceOnEdgeM - edgeLengthM;
            string nextEdgeId = trackGraph.ResolveNextEdgeId(currentEdge.toNodeId, currentEdgeId);

            if (string.IsNullOrEmpty(nextEdgeId))
            {
                distanceOnEdgeM = edgeLengthM;
                speedMS = 0f;
                currentAccelerationMS2 = 0f;
                break;
            }

            TrackEdge newEdge = trackGraph.FindEdge(nextEdgeId);
            if (newEdge == null)
            {
                distanceOnEdgeM = edgeLengthM;
                speedMS = 0f;
                currentAccelerationMS2 = 0f;
                Debug.LogWarning(
                    $"{nameof(TrainController)} on {name}: resolved next edge '{nextEdgeId}' was not found. Stopping at end of edge '{currentEdgeId}'.",
                    this
                );
                break;
            }

            currentEdgeId = nextEdgeId;
            distanceOnEdgeM = remainDistanceM;
            SetCurrentActiveEdge(newEdge);
        }

        if (guard >= maxTransitionsPerFrame)
        {
            Debug.LogWarning($"{nameof(TrainController)} on {name}: edge transition loop reached guard limit.", this);
        }
    }

    /// <summary>
    /// 役割: TryGetPositionBehind の処理を取得を試みます。
    /// </summary>
    /// <param name="offsetM">offsetM を指定します。</param>
    /// <param name="edgeId">出力結果を受け取る edgeId です。</param>
    /// <param name="distOnEdge">出力結果を受け取る distOnEdge です。</param>
    /// <returns>処理が成功した場合は true、それ以外は false を返します。</returns>
    public bool TryGetPositionBehind(float offsetM, out string edgeId, out float distOnEdge)
    {
        edgeId = currentEdgeId;
        distOnEdge = distanceOnEdgeM;

        if (activeEdges.Count == 0)
        {
            return false;
        }

        float currentOffset = offsetM;

        for (int i = 0; i < activeEdges.Count; i++)
        {
            TrackEdge edge = activeEdges[i];
            if (edge == null)
            {
                continue;
            }

            // i=0 は「先頭の現在位置まで」しか巻き戻れないので distanceOnEdgeM を使う。
            float currentEdgeLength = (i == 0) ? distanceOnEdgeM : edge.lengthM;

            if (currentOffset <= currentEdgeLength)
            {
                edgeId = edge.edgeId;
                distOnEdge = currentEdgeLength - currentOffset;
                return true;
            }

            currentOffset -= currentEdgeLength;
        }

        edgeId = activeEdges[activeEdges.Count - 1].edgeId;
        distOnEdge = 0f;
        return true;
    }

    /// <summary>
    /// 先頭基準点より前に実際の車体がどれだけ張り出しているかを返します。
    /// TrainController の位置は先頭車の中心として扱っているため、
    /// 先頭車長の半分が前方張り出し量になります。
    /// </summary>
    public float GetHeadForwardExtentM()
    {
        ConsistDefinition consist = ResolveConsistDefinition();
        return 0.5f * GetCarLengthM(consist, 0);
    }

    /// <summary>
    /// 先頭基準点から実際の最後尾端までの距離を返します。
    /// carTrackStates には最後尾車の中心位置までのオフセットが入っているため、
    /// そこに最後尾車長の半分を足すと編成の実際の後端位置になります。
    /// </summary>
    public float GetTailEndOffsetFromHeadM()
    {
        SyncCarTrackStatesWithConsist();

        if (carTrackStates == null || carTrackStates.Count == 0)
        {
            return 0f;
        }

        ConsistDefinition consist = ResolveConsistDefinition();
        int tailIndex = carTrackStates.Count - 1;
        CarTrackState tailState = carTrackStates[tailIndex];
        float tailCarLengthM = GetCarLengthM(consist, tailIndex);

        return tailState.offsetFromHeadM + 0.5f * tailCarLengthM;
    }

    /// <summary>
    /// 編成の実際の最後尾端が線路上のどこにあるかを解決します。
    /// これは、最後尾が完全に抜けるまで前の閉塞を保持したい
    /// 閉塞在線管理のような仕組みで使う想定です。
    /// </summary>
    public bool TryGetTailEndTrackPosition(out string edgeId, out float distanceOnEdgeM)
    {
        float tailOffsetM = GetTailEndOffsetFromHeadM();
        return TryGetPositionBehind(tailOffsetM, out edgeId, out distanceOnEdgeM);
    }

    /// <summary>
    /// 役割: EnsureRuntimeResolver の処理を必要な状態を保証します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void EnsureRuntimeResolver()
    {
        if (resolver == null)
        {
            resolver = new TrackRuntimeResolver();
        }
    }

    /// <summary>
    /// 役割: InitializeTrackState の処理を初期化します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void InitializeTrackState()
    {
        if (trackGraph != null && string.IsNullOrEmpty(currentEdgeId) && trackGraph.edges != null && trackGraph.edges.Count > 0)
        {
            currentEdgeId = trackGraph.edges[0].edgeId;
            distanceOnEdgeM = 0f;
        }

        activeEdges.Clear();

        if (trackGraph == null || string.IsNullOrEmpty(currentEdgeId))
        {
            return;
        }

        TrackEdge initialEdge = trackGraph.FindEdge(currentEdgeId);
        if (initialEdge != null)
        {
            activeEdges.Add(initialEdge);
        }
    }

    /// <summary>
    /// 役割: TryResolveHeadPose の処理を行います。
    /// </summary>
    /// <param name="pos">出力結果を受け取る pos です。</param>
    /// <param name="tan">出力結果を受け取る tan です。</param>
    /// <returns>処理が成功した場合は true、それ以外は false を返します。</returns>
    private bool TryResolveHeadPose(out Vector3 pos, out Vector3 tan)
    {
        pos = default;
        tan = default;

        if (trackGraph == null)
        {
            Debug.LogError($"{nameof(TrainController)} on {name}: TrackGraph is not assigned.", this);
            return false;
        }

        if (string.IsNullOrEmpty(currentEdgeId))
        {
            Debug.LogError($"{nameof(TrainController)} on {name}: currentEdgeId is empty.", this);
            return false;
        }

        if (!resolver.TryResolvePose(trackGraph, currentEdgeId, distanceOnEdgeM, out pos, out tan))
        {
            Debug.LogError(
                $"{nameof(TrainController)} on {name}: failed to resolve pose. edgeId={currentEdgeId}, distanceOnEdgeM={distanceOnEdgeM:0.###}",
                this
            );
            return false;
        }

        return true;
    }

    /// <summary>
    /// 役割: ApplyHeadPose の処理を適用します。
    /// </summary>
    /// <param name="pos">pos を指定します。</param>
    /// <param name="tan">tan を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ApplyHeadPose(Vector3 pos, Vector3 tan)
    {
        transform.position = pos;
        if (tan.sqrMagnitude > 0.000001f)
        {
            transform.rotation = Quaternion.LookRotation(tan);
        }
    }

    /// <summary>
    /// 役割: GetRequiredHistoryLengthM の処理を取得します。
    /// </summary>
    /// <returns>計算または参照した値を返します。</returns>
    private float GetRequiredHistoryLengthM()
    {
        if (carTrackStates == null || carTrackStates.Count == 0)
        {
            return 0f;
        }

        CarTrackState tailState = carTrackStates[carTrackStates.Count - 1];
        if (tailState == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, tailState.offsetFromHeadM);
    }

    /// <summary>
    /// 役割: EnsureActiveEdgeHistory の処理を必要な状態を保証します。
    /// </summary>
    /// <param name="requiredOffsetM">requiredOffsetM を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void EnsureActiveEdgeHistory(float requiredOffsetM)
    {
        if (trackGraph == null || activeEdges.Count == 0 || requiredOffsetM <= 0f)
        {
            return;
        }

        float coveredDistanceM = GetTrackedHistoryLengthM();
        const int maxBackfillEdges = 512;
        int guard = 0;
        while (coveredDistanceM < requiredOffsetM && guard < maxBackfillEdges)
        {
            guard++;

            TrackEdge oldestTrackedEdge = activeEdges[activeEdges.Count - 1];
            if (oldestTrackedEdge == null || string.IsNullOrEmpty(oldestTrackedEdge.fromNodeId))
            {
                break;
            }

            string previousEdgeId = trackGraph.ResolvePreviousEdgeId(oldestTrackedEdge.fromNodeId, oldestTrackedEdge.edgeId);
            if (string.IsNullOrEmpty(previousEdgeId))
            {
                break;
            }

            TrackEdge previousEdge = trackGraph.FindEdge(previousEdgeId);
            if (previousEdge == null)
            {
                break;
            }

            float previousEdgeLengthM = Mathf.Max(0f, previousEdge.lengthM);
            if (previousEdgeLengthM <= Mathf.Epsilon)
            {
                break;
            }

            activeEdges.Add(previousEdge);
            coveredDistanceM += previousEdgeLengthM;
        }

        if (guard >= maxBackfillEdges)
        {
            Debug.LogWarning($"{nameof(TrainController)} on {name}: active edge history backfill reached guard limit.", this);
        }
    }

    /// <summary>
    /// 役割: SetCurrentActiveEdge の処理を設定します。
    /// </summary>
    /// <param name="currentEdge">currentEdge を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void SetCurrentActiveEdge(TrackEdge currentEdge)
    {
        if (currentEdge == null)
        {
            return;
        }

        if (activeEdges.Count > 0 && activeEdges[0] != null && activeEdges[0].edgeId == currentEdge.edgeId)
        {
            activeEdges[0] = currentEdge;
            return;
        }

        activeEdges.Insert(0, currentEdge);
    }

    /// <summary>
    /// 役割: TrimActiveEdgeHistory の処理を不要分を削減します。
    /// </summary>
    /// <param name="requiredOffsetM">requiredOffsetM を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void TrimActiveEdgeHistory(float requiredOffsetM)
    {
        if (activeEdges.Count <= 1)
        {
            return;
        }

        float coveredDistanceM = Mathf.Max(0f, distanceOnEdgeM);
        int keepCount = 1;
        while (keepCount < activeEdges.Count && coveredDistanceM < requiredOffsetM)
        {
            TrackEdge trackedEdge = activeEdges[keepCount];
            if (trackedEdge != null)
            {
                coveredDistanceM += Mathf.Max(0f, trackedEdge.lengthM);
            }

            keepCount++;
        }

        if (keepCount < activeEdges.Count)
        {
            activeEdges.RemoveRange(keepCount, activeEdges.Count - keepCount);
        }
    }

    /// <summary>
    /// 役割: GetTrackedHistoryLengthM の処理を取得します。
    /// </summary>
    /// <returns>計算または参照した値を返します。</returns>
    private float GetTrackedHistoryLengthM()
    {
        float coveredDistanceM = Mathf.Max(0f, distanceOnEdgeM);
        for (int i = 1; i < activeEdges.Count; i++)
        {
            TrackEdge trackedEdge = activeEdges[i];
            if (trackedEdge != null)
            {
                coveredDistanceM += Mathf.Max(0f, trackedEdge.lengthM);
            }
        }

        return coveredDistanceM;
    }
}
