using System.Collections.Generic;
using UnityEngine;

public class NextStationResolver : MonoBehaviour
{
    // 毎フレームの経路探索で余計な GC を出さないよう、前方・後方のトレース結果バッファを使い回します。
    private readonly List<TrackTraceSegment> aheadTraceSegments = new();
    private readonly List<TrackTraceSegment> behindTraceSegments = new();

    /// <summary>
    /// 役割: TryGetNextStopStation の処理を取得を試みます。
    /// </summary>
    /// <param name="graph">graph を指定します。</param>
    /// <param name="service">service を指定します。</param>
    /// <param name="currentStopIndex">currentStopIndex を指定します。</param>
    /// <param name="currentEdgeId">currentEdgeId を指定します。</param>
    /// <param name="distanceOnEdgeM">distanceOnEdgeM を指定します。</param>
    /// <param name="lookaheadDistanceM">lookaheadDistanceM を指定します。</param>
    /// <param name="resolvedStopIndex">出力結果を受け取る resolvedStopIndex です。</param>
    /// <param name="station">出力結果を受け取る station です。</param>
    /// <param name="distanceToStopM">出力結果を受け取る distanceToStopM です。</param>
    /// <returns>処理が成功した場合は true、それ以外は false を返します。</returns>

    public bool TryGetNextStopStation(
        TrackGraph graph,
        TrainServiceDefinition service,
        int currentStopIndex,
        string currentEdgeId,
        float distanceOnEdgeM,
        float lookaheadDistanceM,
        out int resolvedStopIndex,
        out StationData station,
        out float distanceToStopM
    )
    {
        resolvedStopIndex = -1;
        station = null;
        distanceToStopM = 0f;

        // この resolver は運転中の HUD や停車判定から直接呼ばれるため、依存参照はすべて有効である必要があります。
        if (graph == null ||
            graph.stations == null ||
            service == null ||
            service.stops == null ||
            string.IsNullOrEmpty(currentEdgeId) ||
            lookaheadDistanceM < 0f)
        {
            return false;
        }

        // すでに完了した停車を飛ばすため、現在のサービス停車インデックスから探索を始めます。
        int startIndex = Mathf.Max(0, currentStopIndex);
        if (startIndex >= service.stops.Count)
        {
            return false;
        }

        bool hasAheadTrace = TrackRouteTracer.TryTraceAhead(
            graph,
            currentEdgeId,
            distanceOnEdgeM,
            lookaheadDistanceM,
            aheadTraceSegments
        );
        bool hasBehindTrace = TrackRouteTracer.TryTraceBehind(
            graph,
            currentEdgeId,
            distanceOnEdgeM,
            lookaheadDistanceM,
            behindTraceSegments
        );

        if (!hasAheadTrace && !hasBehindTrace)
        {
            return false;
        }

        // ダイヤ順に停車定義をたどり、現在トレースした経路上に存在する最初の停車駅を返します。
        for (int i = startIndex; i < service.stops.Count; i++)
        {
            ServiceStop serviceStop = service.stops[i];
            if (serviceStop == null ||
                !serviceStop.stopHere ||
                string.IsNullOrEmpty(serviceStop.stationId))
            {
                continue;
            }


            string searchStationId = serviceStop.stationId;
            StationData candidateStation = FindStation(graph, searchStationId);
            if (candidateStation == null || string.IsNullOrEmpty(candidateStation.edgeId))
            {
                return false;
            }

            if (hasAheadTrace &&
                TryGetDistanceInTrace(aheadTraceSegments, candidateStation, isBehindTrace: false, out distanceToStopM))
            {
                resolvedStopIndex = i;
                station = candidateStation;
                return true;
            }

            if (hasBehindTrace &&
                TryGetDistanceInTrace(behindTraceSegments, candidateStation, isBehindTrace: true, out distanceToStopM))
            {
                resolvedStopIndex = i;
                station = candidateStation;
                // 負の距離は、トレースした経路上で列車がすでに停止位置を通り過ぎていることを表します。
                distanceToStopM = -distanceToStopM;
                return true;
            }

        }

        resolvedStopIndex = -1;
        station = null;
        distanceToStopM = 0f;
        return false;
    }

    /// <summary>
    /// 役割: TryGetDistanceInTrace の処理を取得を試みます。
    /// </summary>
    /// <param name="traceSegments">traceSegments を指定します。</param>
    /// <param name="station">station を指定します。</param>
    /// <param name="isBehindTrace">isBehindTrace を指定します。</param>
    /// <param name="distanceToStopM">出力結果を受け取る distanceToStopM です。</param>
    /// <returns>処理が成功した場合は true、それ以外は false を返します。</returns>

    private static bool TryGetDistanceInTrace(
        List<TrackTraceSegment> traceSegments,
        StationData station,
        bool isBehindTrace,
        out float distanceToStopM
    )
    {
        // 駅を含むトレース区間を見つけ、その区間内の位置を列車からの距離に変換します。
        for (int i = 0; i < traceSegments.Count; i++)
        {
            TrackTraceSegment segment = traceSegments[i];

            if (segment.edgeId != station.edgeId ||
                station.distanceFromEdgeStart < segment.startDistanceOnEdgeM ||
                station.distanceFromEdgeStart > segment.endDistanceOnEdgeM)
            {
                continue;
            }

            if (isBehindTrace)
            {
                distanceToStopM = segment.startDistanceFromOriginM + segment.endDistanceOnEdgeM - station.distanceFromEdgeStart;
            }
            else
            {
                distanceToStopM = segment.startDistanceFromOriginM + station.distanceFromEdgeStart - segment.startDistanceOnEdgeM;
            }

            return true;
        }

        distanceToStopM = 0f;
        return false;
    }

    /// <summary>
    /// 役割: FindStation の処理を検索します。
    /// </summary>
    /// <param name="graph">graph を指定します。</param>
    /// <param name="stationId">stationId を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private static StationData FindStation(TrackGraph graph, string stationId)
    {
        // 現状の TrackGraph.stations は件数が少ないため、線形探索で十分でありデータ構造も単純に保てます。
        for (int i = 0; i < graph.stations.Count; i++)
        {
            StationData station = graph.stations[i];
            if (station != null && station.stationId == stationId)
            {
                return station;
            }
        }

        return null;
    }
}
