using System.Collections.Generic;
using UnityEngine;

public class TimsBrakeController : MonoBehaviour, ITimsMasterDataSource
{
    [SerializeField] private TimsSystem tims;
    [SerializeField] private TrainController train;

    // 車両ごとのブレーキ状態
    private List<CarBrakeState> carBrakeStates = new();

    // 車両ごとの目標ブレーキ力
    private List<float> targetCarBrakeForcesN = new();

    // 車両ごとの要求ブレーキ力
    private List<CarBrakeForceCommand> carBrakeForceCommands = new();

    // 車両ごとの重量
    private List<float> carMasses = new();

    private float targetTotalBrakeForceN = 0f;
    private float actualRegenTotalForceN = 0f;
    private List<float> targetRegenForcesN = new();
    private List<float> targetAirForcesN = new();
    private List<float> minimumAirForcesN = new();

    private int brakeStep = 0;

    private bool isEmergency = true;

    public float TotalMassKg => GetTotalMassKg();


    private void Awake()
    {
        ResolveReferences();
        InitializeCarBrakeStates();
        InitializeCarBrakeForceCommands();
    }

    private void Update()
    {
        // 非常ブレーキが緩解できるか判断
        if (!CanReleaseEmergencyBrake())
        {
            isEmergency = true;   
            if (tims != null)
            {
                tims.MasterBus.SetBool(new TimsTagKey("Brake", "EmergencyBrake"), true);
            }

            return;

        }

        isEmergency = false;
        tims.MasterBus.SetBool(new TimsTagKey("Brake", "EmergencyBrake"), false);
        EnsureCarDataLists();



        // 車両ごとのブレーキ状態を取得
        GetBrakeStates();

        // 現在の受信ノッチを取得
        GetCurrentNotch();

        // 現在の車両重量を取得
        GetCurrentMass();

        // ブレーキ状態を元に要求ブレーキ力を計算
        CalculateBrakeForces();

        // ブレーキ司令を書き換え
        CommandBrakeForces();
    }

    private void ResolveReferences()
    {
        if (tims == null)
        {
            tims = GetComponent<TimsSystem>();   
        }

        if (train == null)
        {
            train = GetComponent<TrainController>();
        }
    }


    private void InitializeCarBrakeStates()
    {
        carBrakeStates.Clear();
        int target = tims != null && tims.ConsistDefinition != null
            ? tims.ConsistDefinition.CarCount
            : 0;
        for (int i = 0; i < target; i++)
        {
            carBrakeStates.Add(new CarBrakeState());
        }
    }

    private void InitializeCarBrakeForceCommands()
    {
        carBrakeForceCommands.Clear();
        int target = tims != null && tims.ConsistDefinition != null
            ? tims.ConsistDefinition.CarCount
            : 0;
        for (int i = 0; i < target; i++)
        {
            carBrakeForceCommands.Add(new CarBrakeForceCommand());
        }
    }

    private bool CanReleaseEmergencyBrake()
    {
        return tims != null && tims.ConsistDefinition != null && tims.ControlConfig != null && tims.Terminals != null;
    }

    private void EnsureCarDataLists()
    {
        int target = tims.ConsistDefinition.CarCount;

        while (carBrakeStates.Count < target)
        {
            carBrakeStates.Add(new CarBrakeState());
        }

        while (carBrakeForceCommands.Count < target)
        {
            carBrakeForceCommands.Add(new CarBrakeForceCommand());
        }

        EnsureFloatListSize(targetRegenForcesN, target);
        EnsureFloatListSize(targetAirForcesN, target);
        EnsureFloatListSize(minimumAirForcesN, target);
        EnsureFloatListSize(targetCarBrakeForcesN, target);
    }

    private void EnsureFloatListSize(List<float> values, int target)
    {
        while (values.Count < target)
        {
            values.Add(0f);
        }

        while (values.Count > target)
        {
            values.RemoveAt(values.Count - 1);
        }
    }


    private void GetBrakeStates()
    {
        if (tims == null)
        {
            return;
        }

        List<TimsCarTerminal> terminals = tims.Terminals;

        foreach (TimsCarTerminal terminal in terminals)
        {
            if (terminal == null ||
                terminal.CarIndex < 0 ||
                terminal.CarIndex >= carBrakeStates.Count)
            {
                continue;
            }

            TimsDataBus localBus = terminal.LocalBus;
            carBrakeStates[terminal.CarIndex] = GetCarBrakeState(localBus);
        }
    }

    private static CarBrakeState GetCarBrakeState(TimsDataBus localBus)
    {
        CarBrakeState currentState = new();

        // 回生ブレーキ力
        if (localBus.TryGetFloat(new TimsTagKey("BrakeSystem", "RegenForcekN"), out float regenForcekN))
        {
            currentState.regenForceN = regenForcekN * 1000f;
        }

        // 空気ブレーキ力
        if (localBus.TryGetFloat(new TimsTagKey("BrakeSystem", "AirForceN"), out float airForceN))
        {
            currentState.airForceN = airForceN;
        }

        // BC圧
        if (localBus.TryGetFloat(new TimsTagKey("BrakeSystem", "BCPressureKPa"), out float bcPressureKPa))
        {
            currentState.bcPressureKPa = bcPressureKPa;
        }

        // 回生上限
        if (localBus.TryGetFloat(new TimsTagKey("BrakeSystem", "RegenCapN"), out float regenCapN))
        {
            currentState.regenCapN = regenCapN;
        }

        // 空制上限
        if (localBus.TryGetFloat(new TimsTagKey("BrakeSystem", "AirCapN"), out float airCapN))
        {
            currentState.airCapN = airCapN;
        }

        // BC圧1kPaあたりの空気ブレーキ力
        if (localBus.TryGetFloat(new TimsTagKey("BrakeSystem", "AirForcePerKPa"), out float airForcePerKPa))
        {
            currentState.airForcePerKPa = airForcePerKPa;
        }

        // 最大BC圧
        if (localBus.TryGetFloat(new TimsTagKey("BrakeSystem", "MaxBCPressureKPa"), out float maxBCPressureKPa))
        {
            currentState.maxBCPressureKPa = maxBCPressureKPa;
        }

        return currentState;
    }

    private void GetCurrentNotch()
    {
        TimsDataBus masterBus = tims.MasterBus;

        if (masterBus.TryGetInt(new TimsTagKey("Notch", "BrakeStep"), out int receivedBrakeStep))
        {
            brakeStep = receivedBrakeStep;
        }
    }

    private void GetCurrentMass()
    {
        if (tims == null || tims.ConsistDefinition == null)
        {
            return;
        }

        carMasses = tims.CollectFloatFromCars(new TimsTagKey("Load", "Mass"), out List<bool> founds);

        for (int i = 0; i < carMasses.Count; i++)
        {
            if (!founds[i])
            {
                carMasses[i] = tims.ConsistDefinition.cars[i].massKg;
            }
        }
    }

    private float GetTotalMassKg()
    {
        float total = 0f;

        for (int i = 0; i < carMasses.Count; i++)
        {
            total += carMasses[i];
        }

        return total;
    }

    private void CalculateRegenPattern(float remainingTargetBrakeForceN)
    {
        EnsureFloatListSize(targetRegenForcesN, carMasses.Count);

        float regenTotalMassKg = 0f;
        for (int i = 0; i < carMasses.Count; i++)
        {
            if (!IsVvvfMotorCar(i))
            {
                continue;
            }

            regenTotalMassKg += carMasses[i];
        }

        for (int i = 0; i < carMasses.Count; i++)
        {
            if (!IsVvvfMotorCar(i) || regenTotalMassKg <= 0f)
            {
                targetRegenForcesN[i] = 0f;
                continue;
            }

            targetRegenForcesN[i] = Mathf.Max(0f, carMasses[i]) / regenTotalMassKg * remainingTargetBrakeForceN;
        }
    }

    private bool IsVvvfMotorCar(int carIndex)
    {
        if (tims == null || tims.ConsistDefinition == null)
        {
            return false;
        }

        CarSpec carSpec = tims.ConsistDefinition.GetCar(carIndex);
        return carSpec != null &&
            carSpec.carType == CarType.Motor &&
            carSpec.motorCount > 0 &&
            carSpec.vvvfPrefab != null;
    }

    private void CalculateBrakeForces()
    {
        int subStepCount = tims.ControlConfig.brakeSubstepCount;
        List<float> decelerationsMS2 = tims.ControlConfig.BrakeTargetDecelerationsMS2;

        float targetDecelerationMS2 = TimsBrakeHelper.GetBrakeDecelerationFromStep(
            brakeStep,
            subStepCount,
            decelerationsMS2
        );

        targetTotalBrakeForceN = TotalMassKg * targetDecelerationMS2;
        float minimumAirTotalForceN = CalculateMinimumAirBrakeForces();
        float remainingTargetBrakeForceN = Mathf.Max(0f, targetTotalBrakeForceN - minimumAirTotalForceN);


        // 回生PTN計算
        CalculateRegenPattern(remainingTargetBrakeForceN);

        // 最低込め圧を除いた追加ブレーキ目標を質量比で計算
        CalculateTargetCarBrakeForcesN(remainingTargetBrakeForceN);

        CalculateAirBrakeForces();
        UpdateBrakeForceCommands();
    }

    private void CalculateTargetCarBrakeForcesN(float targetBrakeForceN)
    {
        EnsureFloatListSize(targetCarBrakeForcesN, carMasses.Count);
        float totalMassKg = Mathf.Max(1f, TotalMassKg);

        for (int i = 0; i < carMasses.Count; i++)
        {
            targetCarBrakeForcesN[i] = Mathf.Max(0f, carMasses[i]) / totalMassKg * targetBrakeForceN;
        }
    }

    private float CalculateMinimumAirBrakeForces()
    {
        EnsureFloatListSize(minimumAirForcesN, carBrakeStates.Count);

        float total = 0f;
        float baseMinimumPressureKPa = brakeStep > 0
            ? Mathf.Max(0f, tims.ControlConfig.minimumServiceBrakePressureKPa)
            : 0f;
        float minMassKg = GetMinimumCarMassKg();
        float maxLoadScale = Mathf.Max(1f, tims.ControlConfig.minimumServiceBrakePressureLoadScaleMax);

        for (int i = 0; i < carBrakeStates.Count; i++)
        {
            CarBrakeState state = carBrakeStates[i];
            float maxPressureKPa = state != null ? Mathf.Max(0f, state.maxBCPressureKPa) : 0f;
            float forcePerKPa = state != null ? Mathf.Max(0f, state.airForcePerKPa) : 0f;
            float loadScale = minMassKg > 0f && i < carMasses.Count
                ? Mathf.Clamp(Mathf.Max(0f, carMasses[i]) / minMassKg, 1f, maxLoadScale)
                : 1f;
            float minimumPressureKPa = baseMinimumPressureKPa * loadScale;
            float pressureKPa = maxPressureKPa > 0f
                ? Mathf.Min(minimumPressureKPa, maxPressureKPa)
                : minimumPressureKPa;

            minimumAirForcesN[i] = pressureKPa * forcePerKPa;
            total += minimumAirForcesN[i];
        }

        return total;
    }

    private float GetMinimumCarMassKg()
    {
        float minMassKg = float.MaxValue;

        for (int i = 0; i < carMasses.Count; i++)
        {
            float massKg = Mathf.Max(0f, carMasses[i]);
            if (massKg > 0f)
            {
                minMassKg = Mathf.Min(minMassKg, massKg);
            }
        }

        return minMassKg < float.MaxValue ? minMassKg : 0f;
    }

    private void CalculateAirBrakeForces()
    {
        EnsureFloatListSize(targetAirForcesN, carBrakeStates.Count);

        float regenSurplusForceN = 0f;
        actualRegenTotalForceN = 0f;
        for (int i = 0; i < carBrakeStates.Count; i++)
        {
            CarBrakeState state = carBrakeStates[i];
            float actualRegenForceN = state != null ? Mathf.Max(0f, state.regenForceN) : 0f;
            float targetBrakeForceN = GetTargetCarBrakeForceN(i);

            actualRegenTotalForceN += actualRegenForceN;

            if (IsVvvfMotorCar(i))
            {
                if (actualRegenForceN >= targetBrakeForceN)
                {
                    regenSurplusForceN += actualRegenForceN - targetBrakeForceN;
                    targetAirForcesN[i] = 0f;
                }
                else
                {
                    targetAirForcesN[i] = ClampAdditionalAirForceN(i, targetBrakeForceN - actualRegenForceN);
                }
            }
            else
            {
                targetAirForcesN[i] = ClampAdditionalAirForceN(i, targetBrakeForceN);
            }
        }

        ReduceTrailerAirBrakeByRegenSurplus(regenSurplusForceN);
    }

    private void ReduceTrailerAirBrakeByRegenSurplus(float regenSurplusForceN)
    {
        if (regenSurplusForceN <= 0f)
        {
            return;
        }

        List<float> trailerReductionCaps = new();
        for (int i = 0; i < targetAirForcesN.Count; i++)
        {
            trailerReductionCaps.Add(IsTrailerCar(i) ? targetAirForcesN[i] : 0f);
        }

        List<float> reductions = TimsBrakeHelper.AllocateEvenlyWithSaturation(
            trailerReductionCaps,
            regenSurplusForceN
        );

        for (int i = 0; i < targetAirForcesN.Count; i++)
        {
            targetAirForcesN[i] = Mathf.Max(0f, targetAirForcesN[i] - reductions[i]);
        }
    }

    private float ClampAdditionalAirForceN(int carIndex, float additionalAirTargetN)
    {
        float minimumAirForceN = GetMinimumAirForceN(carIndex);
        float airCapN = GetAirCapForceN(carIndex);
        float additionalAirCapN = Mathf.Max(0f, airCapN - minimumAirForceN);

        return additionalAirCapN > 0f
            ? Mathf.Clamp(additionalAirTargetN, 0f, additionalAirCapN)
            : Mathf.Max(0f, additionalAirTargetN);
    }

    private float GetTargetCarBrakeForceN(int carIndex)
    {
        return carIndex >= 0 && carIndex < targetCarBrakeForcesN.Count
            ? Mathf.Max(0f, targetCarBrakeForcesN[carIndex])
            : 0f;
    }

    private float GetMinimumAirForceN(int carIndex)
    {
        return carIndex >= 0 && carIndex < minimumAirForcesN.Count
            ? Mathf.Max(0f, minimumAirForcesN[carIndex])
            : 0f;
    }

    private float GetAirCapForceN(int carIndex)
    {
        if (carIndex < 0 || carIndex >= carBrakeStates.Count || carBrakeStates[carIndex] == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, carBrakeStates[carIndex].airCapN);
    }

    private bool IsTrailerCar(int carIndex)
    {
        if (tims == null || tims.ConsistDefinition == null)
        {
            return false;
        }

        CarSpec carSpec = tims.ConsistDefinition.GetCar(carIndex);
        return carSpec != null && carSpec.carType == CarType.Trailer;
    }

    private void UpdateBrakeForceCommands()
    {
        for (int i = 0; i < carBrakeForceCommands.Count; i++)
        {
            CarBrakeForceCommand command = carBrakeForceCommands[i];
            float targetRegenForceN = i < targetRegenForcesN.Count ? targetRegenForcesN[i] : 0f;
            float targetAirForceN = i < targetAirForcesN.Count ? targetAirForcesN[i] : 0f;
            float minimumAirForceN = i < minimumAirForcesN.Count ? minimumAirForcesN[i] : 0f;

            command.targetRegenForceN = targetRegenForceN;
            command.targetAirForceN = targetAirForceN + minimumAirForceN;
            command.targetBrakeForceN = GetTargetCarBrakeForceN(i);
            command.isEmergency = false;
        }
    }

    private void CommandBrakeForces()
    {
        WriteTimsMasterData(tims);
    }

    public void WriteTimsMasterData(TimsSystem tims)
    {
        if (tims == null)
        {
            return;
        }

        TimsDataBus masterBus = tims.MasterBus;
        masterBus.SetBool(new TimsTagKey("Brake", "EmergencyBrake"), isEmergency);
        masterBus.SetFloatArray(new TimsTagKey("Brake", "TargetBrakeForcesN"), BuildTargetBrakeForceArray());
        masterBus.SetFloatArray(new TimsTagKey("Brake", "TargetRegenForcesN"), BuildTargetRegenForceArray());
        masterBus.SetFloatArray(new TimsTagKey("Brake", "TargetAirForcesN"), BuildTargetAirForceArray());
    }

    private float[] BuildTargetBrakeForceArray()
    {
        float[] values = new float[carBrakeForceCommands.Count];

        for (int i = 0; i < carBrakeForceCommands.Count; i++)
        {
            values[i] = carBrakeForceCommands[i].targetBrakeForceN;
        }

        return values;
    }

    private float[] BuildTargetRegenForceArray()
    {
        float[] values = new float[carBrakeForceCommands.Count];

        for (int i = 0; i < carBrakeForceCommands.Count; i++)
        {
            values[i] = carBrakeForceCommands[i].targetRegenForceN;
        }

        return values;
    }

    private float[] BuildTargetAirForceArray()
    {
        float[] values = new float[carBrakeForceCommands.Count];

        for (int i = 0; i < carBrakeForceCommands.Count; i++)
        {
            values[i] = carBrakeForceCommands[i].targetAirForceN;
        }

        return values;
    }
}
