using UnityEngine;

public partial class TrainController
{
    /// <summary>
    /// 現在の線路上座標から先頭車の姿勢を解決し、編成各車の線路上位置も更新します。
    /// </summary>
    void MoveTrain()
    {
        SyncCarTrackStatesWithConsist();
        EnsureRuntimeResolver();

        if (!TryResolveHeadPose(out Vector3 pos, out Vector3 tan, out Quaternion rot))
        {
            return;
        }

        UpdateCurrentTrackProfileStatus();
        ApplyHeadPose(pos, tan, rot);

        UpdateCarTrackStates();
    }

    /// <summary>
    /// edge 端を越えたぶんの距離を、接続先 edge へ繰り越します。
    /// </summary>
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
            EdgeTravelDirection movementDirection = GetMovementDirection();
            if (!TrackGraphUndirectedHelpers.HasReachedExit(currentEdge, distanceOnEdgeM, movementDirection))
            {
                break;
            }

            string exitNodeId = TrackGraphUndirectedHelpers.GetExitNodeId(currentEdge, movementDirection);
            float remainDistanceM = TrackGraphUndirectedHelpers.GetOvershootDistance(
                currentEdge,
                distanceOnEdgeM,
                movementDirection
            );

            string nextEdgeId = TrackGraphUndirectedHelpers.ResolveConnectedEdge(trackGraph, exitNodeId, currentEdgeId);

            if (string.IsNullOrEmpty(nextEdgeId))
            {
                StopAtCurrentEdgeExit(currentEdge, movementDirection);
                break;
            }

            TrackEdge newEdge = trackGraph.FindEdge(nextEdgeId);
            if (newEdge == null)
            {
                StopAtCurrentEdgeExit(currentEdge, movementDirection);
                Debug.LogWarning(
                    $"{nameof(TrainController)} on {name}: resolved next edge '{nextEdgeId}' was not found. Stopping at end of edge '{currentEdgeId}'.",
                    this
                );
                break;
            }

            currentEdgeId = nextEdgeId;
            EdgeTravelDirection nextMovementDirection = TrackGraphUndirectedHelpers.GetTravelDirectionFromNode(newEdge, exitNodeId);
            CurrentDirection = reverserPosition == ReverserPosition.Reverse
                ? TrackGraphUndirectedHelpers.GetOppositeDirection(nextMovementDirection)
                : nextMovementDirection;

            float entryDistanceM = TrackGraphUndirectedHelpers.GetEntryDistanceOnEdge(newEdge, exitNodeId);
            distanceOnEdgeM = nextMovementDirection == EdgeTravelDirection.AtoB
                ? entryDistanceM + remainDistanceM
                : entryDistanceM - remainDistanceM;

            SetCurrentActiveEdge(newEdge);
        }

        if (guard >= maxTransitionsPerFrame)
        {
            Debug.LogWarning($"{nameof(TrainController)} on {name}: edge transition loop reached guard limit.", this);
        }
    }

    private void StopAtCurrentEdgeExit(TrackEdge edge, EdgeTravelDirection movementDirection)
    {
        distanceOnEdgeM = TrackGraphUndirectedHelpers.ClampDistanceAtExit(edge, movementDirection);
        speedMS = 0f;
        currentAccelerationMS2 = 0f;
    }

    private EdgeTravelDirection GetMovementDirection()
    {
        return reverserPosition == ReverserPosition.Reverse
            ? TrackGraphUndirectedHelpers.GetOppositeDirection(CurrentDirection)
            : CurrentDirection;
    }

    /// <summary>
    /// 先頭位置から指定距離だけ後方の線路上位置を求めます。
    /// </summary>
    /// <param name="offsetM">offsetM を指定します。</param>
    /// <param name="edgeId">出力結果を受け取る edgeId です。</param>
    /// <param name="distOnEdge">出力結果を受け取る distOnEdge です。</param>
    /// <returns>処理が成功した場合は true、それ以外は false を返します。</returns>
    public bool TryGetPositionBehind(float offsetM, out string edgeId, out float distOnEdge)
    {
        edgeId = currentEdgeId;
        distOnEdge = distanceOnEdgeM;

        if (trackGraph == null || string.IsNullOrEmpty(currentEdgeId))
        {
            return false;
        }

        float safeOffsetM = Mathf.Max(0f, offsetM);
        if (safeOffsetM <= Mathf.Epsilon)
        {
            return true;
        }

        if (!TrackRouteTracer.TryTraceBehind(
                trackGraph,
                currentEdgeId,
                distanceOnEdgeM,
                safeOffsetM,
                positionBehindSegments,
                CurrentDirection
            ))
        {
            return false;
        }

        if (positionBehindSegments.Count == 0)
        {
            return true;
        }

        TrackTraceSegment lastSegment = positionBehindSegments[positionBehindSegments.Count - 1];
        edgeId = lastSegment.edgeId;
        distOnEdge = lastSegment.direction == EdgeTravelDirection.AtoB
            ? lastSegment.endDistanceOnEdgeM
            : lastSegment.startDistanceOnEdgeM;

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
    /// 線路形状を解決する runtime resolver を遅延生成します。
    /// </summary>
    private void EnsureRuntimeResolver()
    {
        if (resolver == null)
        {
            resolver = new TrackRuntimeResolver();
        }
    }

    /// <summary>
    /// 起動時の edge・距離・方向を整合させます。
    /// startNodeId が指定されている場合は、その node から currentEdgeId へ入ったものとして初期化します。
    /// </summary>
    private void InitializeTrackState()
    {
        bool shouldInitializeFromNode = !string.IsNullOrEmpty(startNodeId);
        if (trackGraph != null && string.IsNullOrEmpty(currentEdgeId) && trackGraph.edges != null && trackGraph.edges.Count > 0)
        {
            currentEdgeId = trackGraph.edges[0].edgeId;
            distanceOnEdgeM = 0f;
            shouldInitializeFromNode = true;
        }

        activeEdges.Clear();

        if (trackGraph == null || string.IsNullOrEmpty(currentEdgeId))
        {
            return;
        }

        TrackEdge initialEdge = trackGraph.FindEdge(currentEdgeId);
        if (initialEdge != null)
        {
            if (shouldInitializeFromNode)
            {
                InitializeDirectionFromStartNode(initialEdge);
            }

            activeEdges.Add(initialEdge);
        }
    }

    private void InitializeDirectionFromStartNode(TrackEdge initialEdge)
    {
        string entryNodeId = startNodeId;
        if (string.IsNullOrEmpty(entryNodeId))
        {
            entryNodeId = TrackGraphUndirectedHelpers.GetNodeAId(initialEdge);
        }

        if (!TrackGraphUndirectedHelpers.IsEdgeConnectedToNode(initialEdge, entryNodeId))
        {
            Debug.LogWarning(
                $"{nameof(TrainController)} on {name}: startNodeId '{entryNodeId}' is not connected to start edge '{initialEdge.edgeId}'. Falling back to nodeA.",
                this
            );
            entryNodeId = TrackGraphUndirectedHelpers.GetNodeAId(initialEdge);
        }

        CurrentDirection = TrackGraphUndirectedHelpers.GetTravelDirectionFromNode(initialEdge, entryNodeId);
        distanceOnEdgeM = TrackGraphUndirectedHelpers.GetEntryDistanceOnEdge(initialEdge, entryNodeId);
    }

    /// <summary>
    /// 現在 edge と edge 内距離から、先頭車のワールド姿勢を解決します。
    /// </summary>
    /// <param name="pos">出力結果を受け取る pos です。</param>
    /// <param name="tan">出力結果を受け取る tan です。</param>
    /// <returns>処理が成功した場合は true、それ以外は false を返します。</returns>
    private bool TryResolveHeadPose(out Vector3 pos, out Vector3 tan, out Quaternion rot)
    {
        pos = default;
        tan = default;
        rot = Quaternion.identity;

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

        if (!resolver.TryResolvePose(trackGraph, currentEdgeId, distanceOnEdgeM, out pos, out tan, out rot))
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
    /// 解決済みのワールド姿勢を列車 GameObject に反映します。
    /// </summary>
    /// <param name="pos">pos を指定します。</param>
    /// <param name="tan">tan を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void ApplyHeadPose(Vector3 pos, Vector3 tan, Quaternion rot)
    {
        transform.position = pos;
        if (tan.sqrMagnitude > 0.000001f)
        {
            transform.rotation = rot;
        }
    }

    private void UpdateCurrentTrackProfileStatus()
    {
        currentGradientPermille = 0f;
        currentCantMm = 0f;

        if (resolver == null || trackGraph == null || string.IsNullOrEmpty(currentEdgeId))
        {
            return;
        }

        resolver.TryGetGradientPermille(trackGraph, currentEdgeId, distanceOnEdgeM, out currentGradientPermille);
        resolver.TryGetCantMm(trackGraph, currentEdgeId, distanceOnEdgeM, out currentCantMm);
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
    /// 編成後端までの描画・在線判定に必要な過去 edge を保持します。
    /// </summary>
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
            string backNodeId = GetBackNodeId(oldestTrackedEdge);
            if (oldestTrackedEdge == null || string.IsNullOrEmpty(backNodeId))
            {
                break;
            }

            string previousEdgeId = trackGraph.ResolvePreviousEdgeId(backNodeId, oldestTrackedEdge.edgeId);
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

    private string GetBackNodeId(TrackEdge edge)
    {
        if (edge == null)
        {
            return null;
        }

        return CurrentDirection == EdgeTravelDirection.AtoB
            ? TrackGraphUndirectedHelpers.GetNodeAId(edge)
            : TrackGraphUndirectedHelpers.GetNodeBId(edge);
    }

    /// <summary>
    /// 先頭が入った edge を履歴の先頭に置きます。
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
