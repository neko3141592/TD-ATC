using System.Collections.Generic;
using UnityEngine;

public class TractionSystemController : MonoBehaviour
{
    [Header("References")]
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

    public void ApplyExternalTractionForce(float totalTractionForceN)
    {
        EnsureCarTractionStateCount();
        CurrentConsistMassKg = GetTotalConsistMassKg();

        ResetCurrentTractionForces();

        float safeTotalTractionForceN = Mathf.Max(0f, totalTractionForceN);
        if (safeTotalTractionForceN <= 0f)
        {
            return;
        }

        bool hasConsist = consistDefinition != null && consistDefinition.HasCars;
        if (!hasConsist)
        {
            CurrentTotalTractionForceN = safeTotalTractionForceN;
            return;
        }

        int totalMotorCount = consistDefinition.GetTotalMotorCount();
        if (totalMotorCount <= 0)
        {
            return;
        }

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
            state.tractionForceN = safeTotalTractionForceN * carShare;
            distributedTractionForceN += state.tractionForceN;
        }

        CurrentTotalTractionForceN = distributedTractionForceN;
    }

    public void ClearTractionOutputs()
    {
        EnsureCarTractionStateCount();
        ResetCurrentTractionForces();
    }

    public void ApplyExternalMotorCurrents(VVVFController[] vvvfControllers)
    {
        EnsureCarTractionStateCount();
        ResetCurrentMotorCurrents();

        if (vvvfControllers == null || consistDefinition == null || !consistDefinition.HasCars)
        {
            return;
        }

        int carIndex = 0;
        int remainingMotorSlotsInCar = 0;
        CarTractionState currentState = null;

        foreach (VVVFController vvvf in vvvfControllers)
        {
            if (vvvf == null || vvvf.MotorCount <= 0)
            {
                continue;
            }

            float vvvfCurrentA = GetTotalMotorCurrentA(vvvf);
            int remainingVvvfMotors = vvvf.MotorCount;

            while (remainingVvvfMotors > 0)
            {
                if (currentState == null || remainingMotorSlotsInCar <= 0)
                {
                    if (!TryMoveToNextMotorCar(ref carIndex, out currentState, out remainingMotorSlotsInCar))
                    {
                        return;
                    }
                }

                int assignedMotors = Mathf.Min(remainingVvvfMotors, remainingMotorSlotsInCar);
                float assignedRatio = assignedMotors / (float)vvvf.MotorCount;
                currentState.motorCurrentA += vvvfCurrentA * assignedRatio;

                remainingVvvfMotors -= assignedMotors;
                remainingMotorSlotsInCar -= assignedMotors;
            }
        }
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
                state.motorCurrentA = 0f;
            }
        }

        CurrentTotalTractionForceN = 0f;
    }

    private void ResetCurrentMotorCurrents()
    {
        for (int i = 0; i < carTractionStates.Count; i++)
        {
            CarTractionState state = carTractionStates[i];
            if (state != null)
            {
                state.motorCurrentA = 0f;
            }
        }
    }

    private float GetTotalMotorCurrentA(VVVFController vvvf)
    {
        MotorModel[] motors = vvvf.Motors;
        if (motors == null)
        {
            return 0f;
        }

        float totalCurrentA = 0f;
        for (int i = 0; i < motors.Length; i++)
        {
            MotorModel motor = motors[i];
            if (motor != null)
            {
                totalCurrentA += Mathf.Max(0f, motor.MotorCurrentRmsA);
            }
        }

        return totalCurrentA;
    }

    private bool TryMoveToNextMotorCar(ref int carIndex, out CarTractionState state, out int motorSlots)
    {
        state = null;
        motorSlots = 0;

        if (consistDefinition == null || !consistDefinition.HasCars)
        {
            return false;
        }

        while (carIndex < consistDefinition.CarCount)
        {
            int index = carIndex;
            carIndex++;

            CarSpec carSpec = GetCarSpec(index);
            if (carSpec == null || carSpec.carType != CarType.Motor || carSpec.motorCount <= 0)
            {
                continue;
            }

            if (index < 0 || index >= carTractionStates.Count)
            {
                continue;
            }

            state = carTractionStates[index];
            motorSlots = carSpec.motorCount;
            return state != null;
        }

        return false;
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
