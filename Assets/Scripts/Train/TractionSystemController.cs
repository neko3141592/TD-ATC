using System.Collections.Generic;
using UnityEngine;

public class TractionSystemController : MonoBehaviour
{
    [SerializeField] private TrainSpec trainSpec;
    [SerializeField] private ConsistDefinition consistDefinition;

    private readonly List<CarTractionState> carTractionStates = new List<CarTractionState>();
    public IReadOnlyList<CarTractionState> CarTractionStates => carTractionStates;
    public ConsistDefinition ConsistDefinition => consistDefinition;

    public float CurrentTotalTractionForceN { get; private set; } = 0f;
    public float CurrentConsistMassKg { get; private set; } = 0f;

    /// <summary>
    /// 役割: Awake の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void Awake()
    {
        if (trainSpec == null)
        {
            Debug.LogError("TrainSpec is not assigned.", this);
        }

        InitializeCarTractionStates();
        CurrentConsistMassKg = GetTotalConsistMassKg();
    }

    /// <summary>
    /// 役割: OnValidate の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void OnValidate()
    {
        InitializeCarTractionStates();
        CurrentConsistMassKg = GetTotalConsistMassKg();
    }

    /// <summary>
    /// 役割: UpdateTraction の処理を実行します。
    /// </summary>
    /// <param name="powerNotch">powerNotch を指定します。</param>
    /// <param name="speedMS">speedMS を指定します。</param>
    /// <param name="externalForceN">externalForceN を指定します。</param>
    /// <remarks>返り値はありません。</remarks>
    public void UpdateTraction(int powerNotch, float speedMS, float externalForceN)
    {
        EnsureCarTractionStateCount();
        CurrentConsistMassKg = GetTotalConsistMassKg();

        ResetCurrentTractionForces();
        if (trainSpec == null || powerNotch <= 0)
        {
            return;
        }

        float safeMassKg = CurrentConsistMassKg > 0f ? CurrentConsistMassKg : Mathf.Max(1f, trainSpec.massKg);
        float safeExternalForceN = Mathf.Max(0f, externalForceN);
        bool hasConsist = consistDefinition != null && consistDefinition.HasCars;
        int totalMotorCount = hasConsist ? consistDefinition.GetTotalMotorCount() : -1;

        // 編成定義があり、M車が1両もないなら力行なし
        if (hasConsist && totalMotorCount <= 0)
        {
            CurrentTotalTractionForceN = 0f;
            return;
        }

        // 総力行力を作る（1基あたり出力/トルク * 総モータ基数を反映）
        float targetTotalTractionForceN = trainSpec.GetTractionDemandForceN(
            powerNotch,
            speedMS,
            safeMassKg,
            safeExternalForceN,
            totalMotorCount
        );
        if (targetTotalTractionForceN <= 0f)
        {
            return;
        }

        // 編成未設定時は従来互換（総力のみ利用）
        if (!hasConsist)
        {
            CurrentTotalTractionForceN = targetTotalTractionForceN;
            return;
        }

        // 「単純分割」: M車のmotorCount比で総力行力を配る（T車は0）
        float distributedTractionForceN = 0f;
        int count = Mathf.Min(carTractionStates.Count, consistDefinition.CarCount);
        for (int i = 0; i < count; i++)
        {
            CarTractionState state = carTractionStates[i];
            CarSpec carSpec = GetCarSpec(i);

            if (state == null)
            {
                continue;
            }

            if (carSpec == null)
            {
                state.Reset();
                continue;
            }

            if (carSpec.carType != CarType.Motor || carSpec.motorCount <= 0)
            {
                state.tractionForceN = 0f;
                continue;
            }

            float carShare = carSpec.motorCount / (float)totalMotorCount;
            state.tractionForceN = targetTotalTractionForceN * carShare;
            distributedTractionForceN += state.tractionForceN;
        }

        CurrentTotalTractionForceN = distributedTractionForceN;
    }

    /// <summary>
    /// 役割: InitializeCarTractionStates の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void InitializeCarTractionStates()
    {
        carTractionStates.Clear();
        int target = consistDefinition != null ? consistDefinition.CarCount : 0;
        for (int i = 0; i < target; i++)
        {
            carTractionStates.Add(new CarTractionState());
        }

        ResetNullCarStates();
    }

    /// <summary>
    /// 役割: EnsureCarTractionStateCount の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void EnsureCarTractionStateCount()
    {
        int target = consistDefinition != null ? consistDefinition.CarCount : 0;

        while (carTractionStates.Count < target)
        {
            carTractionStates.Add(new CarTractionState());
        }

        while (carTractionStates.Count > target)
        {
            carTractionStates.RemoveAt(carTractionStates.Count - 1);
        }

        ResetNullCarStates();
        CurrentConsistMassKg = GetTotalConsistMassKg();
    }

    /// <summary>
    /// 役割: ResetNullCarStates の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ResetNullCarStates()
    {
        if (consistDefinition == null || !consistDefinition.HasCars)
        {
            return;
        }

        int count = Mathf.Min(carTractionStates.Count, consistDefinition.CarCount);
        for (int i = 0; i < count; i++)
        {
            if (GetCarSpec(i) == null)
            {
                carTractionStates[i].Reset();
            }
        }
    }

    /// <summary>
    /// 役割: ResetCurrentTractionForces の処理を実行します。
    /// </summary>
    /// <remarks>返り値はありません。</remarks>
    private void ResetCurrentTractionForces()
    {
        for (int i = 0; i < carTractionStates.Count; i++)
        {
            CarTractionState state = carTractionStates[i];
            if (state != null)
            {
                state.tractionForceN = 0f;
            }
        }

        CurrentTotalTractionForceN = 0f;
    }

    /// <summary>
    /// 役割: GetTotalConsistMassKg の処理を実行します。
    /// </summary>
    /// <returns>処理結果を返します。</returns>
    private float GetTotalConsistMassKg()
    {
        float fallbackMassKg = trainSpec != null ? trainSpec.massKg : 1f;
        if (consistDefinition == null)
        {
            return Mathf.Max(1f, fallbackMassKg);
        }

        return consistDefinition.GetTotalMassKgOrFallback(fallbackMassKg);
    }

    /// <summary>
    /// 役割: GetCarSpec の処理を実行します。
    /// </summary>
    /// <param name="index">index を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    private CarSpec GetCarSpec(int index)
    {
        return consistDefinition != null ? consistDefinition.GetCar(index) : null;
    }
}
