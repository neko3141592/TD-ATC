using UnityEngine;

public partial class TrainController
{
    /// <summary>
    /// 役割: SyncCarTrackStatesWithConsist の処理を同期します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void SyncCarTrackStatesWithConsist()
    {
        ConsistDefinition resolvedConsistDefinition = ResolveConsistDefinition();
        EnsureCarTrackStateCount(GetTargetCarCount(resolvedConsistDefinition));
        RefreshCarOffsets(resolvedConsistDefinition);
    }

    /// <summary>
    /// 役割: UpdateCarTrackStates の処理を更新します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void UpdateCarTrackStates()
    {
        for (int i = 0; i < carTrackStates.Count; i++)
        {
            UpdateCarTrackState(i);
        }
    }

    /// <summary>
    /// 役割: UpdateCarTrackState の処理を更新します。
    /// </summary>
    /// <param name="index">index を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void UpdateCarTrackState(int index)
    {
        CarTrackState state = carTrackStates[index];
        if (state == null)
        {
            return;
        }

        if (!TryGetPositionBehind(state.offsetFromHeadM, out string edgeId, out float distOnEdge))
        {
            return;
        }

        state.edgeId = edgeId;
        state.distanceOnEdgeM = distOnEdge;

        if (resolver.TryResolvePose(trackGraph, edgeId, distOnEdge, out Vector3 carPos, out Vector3 carTan))
        {
            state.position = carPos;
            state.tangent = carTan;
        }
    }

    /// <summary>
    /// 役割: EnsureCarTrackStateCount の処理を必要な状態を保証します。
    /// </summary>
    /// <param name="targetCarCount">targetCarCount を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void EnsureCarTrackStateCount(int targetCarCount)
    {
        while (carTrackStates.Count < targetCarCount)
        {
            carTrackStates.Add(new CarTrackState());
        }

        while (carTrackStates.Count > targetCarCount)
        {
            carTrackStates.RemoveAt(carTrackStates.Count - 1);
        }
    }

    /// <summary>
    /// 役割: RefreshCarOffsets の処理を再計算します。
    /// </summary>
    /// <param name="resolvedConsistDefinition">resolvedConsistDefinition を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    private void RefreshCarOffsets(ConsistDefinition resolvedConsistDefinition)
    {
        float accumulatedOffsetM = 0f;
        float previousCarLengthM = GetCarLengthM(resolvedConsistDefinition, 0);

        for (int i = 0; i < carTrackStates.Count; i++)
        {
            CarTrackState state = GetOrCreateCarTrackState(i);
            state.carIndex = i;
            if (i == 0)
            {
                state.offsetFromHeadM = 0f;
                previousCarLengthM = GetCarLengthM(resolvedConsistDefinition, i);
                continue;
            }

            float currentCarLengthM = GetCarLengthM(resolvedConsistDefinition, i);
            // 先頭中心基準での各車中心位置: 前車と現車の「半車長ずつ」の和を積み上げる。
            accumulatedOffsetM += 0.5f * (previousCarLengthM + currentCarLengthM);
            state.offsetFromHeadM = accumulatedOffsetM;
            previousCarLengthM = currentCarLengthM;
        }
    }

    /// <summary>
    /// 役割: GetOrCreateCarTrackState の処理を取得します。
    /// </summary>
    /// <param name="index">index を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private CarTrackState GetOrCreateCarTrackState(int index)
    {
        CarTrackState state = carTrackStates[index];
        if (state == null)
        {
            state = new CarTrackState();
            carTrackStates[index] = state;
        }

        return state;
    }

    /// <summary>
    /// 役割: ResolveConsistDefinition の処理を解決します。
    /// </summary>
    /// <returns>処理結果を返します。</returns>
    private ConsistDefinition ResolveConsistDefinition()
    {
        if (consistDefinition != null && consistDefinition.HasCars)
        {
            return consistDefinition;
        }

        if (brakeSystem != null && brakeSystem.ConsistDefinition != null && brakeSystem.ConsistDefinition.HasCars)
        {
            return brakeSystem.ConsistDefinition;
        }

        if (tractionSystem != null && tractionSystem.ConsistDefinition != null && tractionSystem.ConsistDefinition.HasCars)
        {
            return tractionSystem.ConsistDefinition;
        }

        return consistDefinition;
    }

    /// <summary>
    /// 役割: GetTargetCarCount の処理を取得します。
    /// </summary>
    /// <param name="resolvedConsistDefinition">resolvedConsistDefinition を指定します。</param>
    /// <returns>計算または参照した値を返します。</returns>
    private int GetTargetCarCount(ConsistDefinition resolvedConsistDefinition)
    {
        if (resolvedConsistDefinition != null && resolvedConsistDefinition.HasCars)
        {
            return resolvedConsistDefinition.CarCount;
        }

        return Mathf.Max(1, carTrackStates.Count);
    }

    /// <summary>
    /// 役割: GetCarLengthM の処理を取得します。
    /// </summary>
    /// <param name="resolvedConsistDefinition">resolvedConsistDefinition を指定します。</param>
    /// <param name="index">index を指定します。</param>
    /// <returns>計算または参照した値を返します。</returns>
    private float GetCarLengthM(ConsistDefinition resolvedConsistDefinition, int index)
    {
        if (resolvedConsistDefinition != null &&
            resolvedConsistDefinition.TryGetCar(index, out CarSpec carSpec) &&
            carSpec != null)
        {
            return Mathf.Max(1f, carSpec.lengthM);
        }

        return Mathf.Max(1f, defaultCarLengthM);
    }
}
