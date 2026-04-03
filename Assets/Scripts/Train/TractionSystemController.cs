using System.Collections.Generic;
using UnityEngine;

public class TractionSystemController : MonoBehaviour
{
    [SerializeField] private TrainSpec trainSpec;
    [SerializeField] private ConsistDefinition consistDefinition;

    private readonly List<CarTractionState> carTractionStates = new List<CarTractionState>();
    public IReadOnlyList<CarTractionState> CarTractionStates => carTractionStates;

    public float CurrentTotalTractionForceN { get; private set; } = 0f;
    public float CurrentConsistMassKg { get; private set; } = 0f;

    private void Awake()
    {
        if (trainSpec == null)
        {
            Debug.LogError("TrainSpec is not assigned.", this);
        }

        InitializeCarTractionStates();
        CurrentConsistMassKg = GetTotalConsistMassKg();
    }

    private void OnValidate()
    {
        InitializeCarTractionStates();
        CurrentConsistMassKg = GetTotalConsistMassKg();
    }

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
        bool hasConsist = consistDefinition != null && consistDefinition.cars != null && consistDefinition.cars.Count > 0;
        int totalMotorCount = hasConsist ? GetTotalMotorCount() : -1;

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
        int count = Mathf.Min(carTractionStates.Count, consistDefinition.cars.Count);
        for (int i = 0; i < count; i++)
        {
            CarTractionState state = carTractionStates[i];
            CarSpec carSpec = consistDefinition.cars[i];

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

    private void InitializeCarTractionStates()
    {
        carTractionStates.Clear();
        int target = consistDefinition?.cars?.Count ?? 0;
        for (int i = 0; i < target; i++)
        {
            carTractionStates.Add(new CarTractionState());
        }

        ResetNullCarStates();
    }

    private void EnsureCarTractionStateCount()
    {
        int target = consistDefinition?.cars?.Count ?? 0;

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

    private void ResetNullCarStates()
    {
        if (consistDefinition == null || consistDefinition.cars == null)
        {
            return;
        }

        int count = Mathf.Min(carTractionStates.Count, consistDefinition.cars.Count);
        for (int i = 0; i < count; i++)
        {
            if (consistDefinition.cars[i] == null)
            {
                carTractionStates[i].Reset();
            }
        }
    }

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

    private float GetTotalConsistMassKg()
    {
        if (consistDefinition == null || consistDefinition.cars == null || consistDefinition.cars.Count == 0)
        {
            return Mathf.Max(1f, trainSpec != null ? trainSpec.massKg : 1f);
        }

        float totalMassKg = 0f;
        for (int i = 0; i < consistDefinition.cars.Count; i++)
        {
            CarSpec carSpec = consistDefinition.cars[i];
            if (carSpec == null)
            {
                continue;
            }

            totalMassKg += Mathf.Max(1f, carSpec.massKg);
        }

        if (totalMassKg <= 0f)
        {
            totalMassKg = Mathf.Max(1f, trainSpec != null ? trainSpec.massKg : 1f);
        }

        return totalMassKg;
    }

    private int GetTotalMotorCount()
    {
        if (consistDefinition == null || consistDefinition.cars == null)
        {
            return 0;
        }

        int totalMotorCount = 0;
        for (int i = 0; i < consistDefinition.cars.Count; i++)
        {
            CarSpec carSpec = consistDefinition.cars[i];
            if (carSpec == null || carSpec.carType != CarType.Motor)
            {
                continue;
            }

            totalMotorCount += Mathf.Max(0, carSpec.motorCount);
        }

        return totalMotorCount;
    }
}
