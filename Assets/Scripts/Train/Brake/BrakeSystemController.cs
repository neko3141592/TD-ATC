using System.Collections.Generic;
using UnityEngine;

public class BrakeSystemController : MonoBehaviour, ITimsDataSource
{
    [Header("References")]
    [SerializeField] private TimsSystem tims;
    [SerializeField] private TrainController train;
    [SerializeField] private TrainSpec trainSpec;
    [SerializeField] private TimsCarTerminal terminal;
    [SerializeField] private BrakeCylinder[] brakeCylinders;

    [Header("Rolling Prevention")]
    [SerializeField] private bool isRollingPreventionActive = false;

    [Header("Regen Release")]
    [SerializeField] private bool regenRelease = false;

    [Header("Gradient Start")]
    [SerializeField] private bool gradientStart = false;

    private readonly List<CarBrakeState> carBrakeStates = new();

    private float targetRegenForceN;
    private float targetAirForceN;
    private float targetBCPressureKPa;
    private float airCapForceN;
    private bool emergencyBrakeCommand;

    public IReadOnlyList<CarBrakeState> CarBrakeStates => carBrakeStates;
    public ConsistDefinition ConsistDefinition => GetConsistDefinition();
    public float TransmissionIntervalSeconds => 0.05f;
    public bool IsRollingPreventionActive => isRollingPreventionActive;
    public bool IsRegenReleased => regenRelease;
    public bool IsGradientStart => gradientStart && train != null && train.SpeedMS <= 1f;
    public float CurrentBCPressureKPa { get; private set; }
    public float CurrentRegenForceN { get; private set; }
    public float CurrentAirForceN { get; private set; }
    public float TotalBrakeForceN { get; private set; }
    public float CurrentTargetBCPressureKPa { get; private set; }
    public float CurrentRegenDecelMS2 { get; private set; }
    public float CurrentAirDecelMS2 { get; private set; }
    public float TotalBrakeDecelMS2 { get; private set; }
    public float CurrentConsistMassKg { get; private set; }

    private void Awake()
    {
        ResolveReferences();
        RefreshBrakeStateList();
    }

    private void Update()
    {
        UpdateBrakeFromTims();
    }

    private void OnValidate()
    {
        ResolveBrakeCylinders();
    }

    private void ResolveReferences()
    {
        if (tims == null)
        {
            tims = GetComponentInParent<TimsSystem>();
        }

        if (terminal == null)
        {
            terminal = GetComponentInParent<TimsCarTerminal>();
        }

        if (train == null)
        {
            train = GetComponentInParent<TrainController>();
        }

        if (trainSpec == null && train != null)
        {
            trainSpec = train.Spec;
        }

        ResolveBrakeCylinders();
    }

    private void ResolveBrakeCylinders()
    {
        if (brakeCylinders == null || brakeCylinders.Length == 0)
        {
            brakeCylinders = GetComponentsInChildren<BrakeCylinder>(true);
        }
    }

    private void UpdateBrakeFromTims()
    {
        ResolveReferences();
        RefreshBrakeStateList();

        if (tims == null || terminal == null)
        {
            ResetOutputs();
            return;
        }

        int carIndex = terminal.CarIndex;
        TimsDataBus masterBus = tims.MasterBus;

        emergencyBrakeCommand =
            masterBus.TryGetBool(new TimsTagKey("Brake", "EmergencyBrake"), out bool emergency) &&
            emergency;

        targetRegenForceN = GetMasterFloatArrayValue(new TimsTagKey("Brake", "TargetRegenForcesN"), carIndex);
        targetAirForceN = GetMasterFloatArrayValue(new TimsTagKey("Brake", "TargetAirForcesN"), carIndex);

        CarSpec carSpec = GetLocalCarSpec();
        float speedMS = train != null ? train.SpeedMS : 0f;

        targetBCPressureKPa = emergencyBrakeCommand
            ? GetEmergencyTargetBCPressureKPa(carSpec)
            : GetTargetBCPressureKPa(carSpec, speedMS, targetAirForceN);

        ApplyTargetBCPressure(targetBCPressureKPa);
        RefreshOutputs(carSpec, speedMS);
    }

    private float GetMasterFloatArrayValue(TimsTagKey key, int index)
    {
        if (index < 0 || tims == null || !tims.MasterBus.TryGetFloatArray(key, out float[] values))
        {
            return 0f;
        }

        return index < values.Length ? Mathf.Max(0f, values[index]) : 0f;
    }

    private ConsistDefinition GetConsistDefinition()
    {
        if (tims == null)
        {
            tims = GetComponentInParent<TimsSystem>();
        }

        return tims != null ? tims.ConsistDefinition : null;
    }

    private CarSpec GetLocalCarSpec()
    {
        ConsistDefinition definition = GetConsistDefinition();
        if (definition == null || terminal == null)
        {
            return null;
        }

        return definition.GetCar(terminal.CarIndex);
    }

    private float GetTargetBCPressureKPa(CarSpec carSpec, float speedMS, float targetAirForceN)
    {
        return GetTargetBCPressureKPaFromBrakeCylinders(targetAirForceN);
    }

    private float GetEmergencyTargetBCPressureKPa(CarSpec carSpec)
    {
        float maxPressureKPa = 0f;
        if (brakeCylinders == null)
        {
            return 0f;
        }

        for (int i = 0; i < brakeCylinders.Length; i++)
        {
            BrakeCylinder cylinder = brakeCylinders[i];
            if (cylinder != null && cylinder.Spec != null)
            {
                maxPressureKPa = Mathf.Max(maxPressureKPa, cylinder.Spec.MaxPressureKPa);
            }
        }

        return maxPressureKPa;
    }

    private void ApplyTargetBCPressure(float pressureKPa)
    {
        if (brakeCylinders == null)
        {
            return;
        }

        for (int i = 0; i < brakeCylinders.Length; i++)
        {
            if (brakeCylinders[i] != null)
            {
                brakeCylinders[i].SetTargetPressureKPa(pressureKPa);
            }
        }
    }

    private void RefreshOutputs(CarSpec carSpec, float speedMS)
    {
        CurrentRegenForceN = GetLocalBusFloat(new TimsTagKey("BrakeSystem", "RegenForcekN")) * 1000f;
        CurrentAirForceN = GetTotalBrakeCylinderForceN();
        TotalBrakeForceN = CurrentRegenForceN + CurrentAirForceN;
        CurrentBCPressureKPa = GetMaxBrakeCylinderPressureKPa();
        CurrentTargetBCPressureKPa = targetBCPressureKPa;
        airCapForceN = GetAirBrakeCapForceN(carSpec, speedMS);
        CurrentConsistMassKg = GetTotalConsistMassKg();

        float safeMassKg = Mathf.Max(1f, CurrentConsistMassKg);
        CurrentRegenDecelMS2 = CurrentRegenForceN / safeMassKg;
        CurrentAirDecelMS2 = CurrentAirForceN / safeMassKg;
        TotalBrakeDecelMS2 = TotalBrakeForceN / safeMassKg;

        UpdateLocalBrakeState(carSpec);
    }

    private float GetLocalBusFloat(TimsTagKey key)
    {
        return terminal != null &&
               terminal.LocalBus.TryGetFloat(key, out float value)
            ? value
            : 0f;
    }

    private float GetTotalBrakeCylinderForceN()
    {
        float total = 0f;
        if (brakeCylinders == null)
        {
            return total;
        }

        for (int i = 0; i < brakeCylinders.Length; i++)
        {
            if (brakeCylinders[i] != null)
            {
                total += Mathf.Max(0f, brakeCylinders[i].BrakeForceN);
            }
        }

        return total;
    }

    private float GetMaxBrakeCylinderPressureKPa()
    {
        float max = 0f;
        if (brakeCylinders == null)
        {
            return max;
        }

        for (int i = 0; i < brakeCylinders.Length; i++)
        {
            if (brakeCylinders[i] != null)
            {
                max = Mathf.Max(max, brakeCylinders[i].CurrentPressureKPa);
            }
        }

        return max;
    }

    private float GetAirBrakeCapForceN(CarSpec carSpec, float speedMS)
    {
        float cap = 0f;
        if (brakeCylinders == null)
        {
            return cap;
        }

        for (int i = 0; i < brakeCylinders.Length; i++)
        {
            BrakeCylinder cylinder = brakeCylinders[i];
            if (cylinder != null && cylinder.Spec != null)
            {
                cap += cylinder.CalculateBrakeForceN(cylinder.Spec.maxPressurePa);
            }
        }

        return cap;
    }

    private float GetTargetBCPressureKPaFromBrakeCylinders(float targetAirForceN)
    {
        float safeTargetAirForceN = Mathf.Max(0f, targetAirForceN);
        if (safeTargetAirForceN <= 0f || brakeCylinders == null)
        {
            return 0f;
        }

        float forcePerKPa = 0f;
        float maxPressureKPa = 0f;
        for (int i = 0; i < brakeCylinders.Length; i++)
        {
            BrakeCylinder cylinder = brakeCylinders[i];
            if (cylinder == null || cylinder.Spec == null)
            {
                continue;
            }

            forcePerKPa += cylinder.CalculateBrakeForceN(1000f);
            maxPressureKPa = Mathf.Max(maxPressureKPa, cylinder.Spec.MaxPressureKPa);
        }

        if (forcePerKPa <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp(safeTargetAirForceN / forcePerKPa, 0f, maxPressureKPa);
    }

    private float GetTotalConsistMassKg()
    {
        ConsistDefinition definition = GetConsistDefinition();
        float fallbackMassKg = trainSpec != null ? trainSpec.massKg : 1f;
        return definition != null
            ? definition.GetTotalMassKgOrFallback(fallbackMassKg)
            : Mathf.Max(1f, fallbackMassKg);
    }

    private void RefreshBrakeStateList()
    {
        if (terminal == null)
        {
            carBrakeStates.Clear();
            return;
        }

        while (carBrakeStates.Count <= terminal.CarIndex)
        {
            carBrakeStates.Add(new CarBrakeState());
        }
    }

    private void UpdateLocalBrakeState(CarSpec carSpec)
    {
        if (terminal == null || terminal.CarIndex < 0 || terminal.CarIndex >= carBrakeStates.Count)
        {
            return;
        }

        CarBrakeState state = carBrakeStates[terminal.CarIndex];
        state.bcPressureKPa = CurrentBCPressureKPa;
        state.regenForceN = CurrentRegenForceN;
        state.airForceN = CurrentAirForceN;
        state.regenCapN = GetLocalBusFloat(new TimsTagKey("BrakeSystem", "RegenCapN"));
        state.airCapN = airCapForceN;
        state.airForcePerKPa = GetAirForcePerKPa();
        state.maxBCPressureKPa = GetMaxBrakeCylinderSpecPressureKPa();

    }

    public void WriteTimsData(TimsCarTerminal terminal)
    {
        if (terminal == null)
        {
            return;
        }

        TimsDataBus localBus = terminal.LocalBus;
        localBus.SetBool(new TimsTagKey("BrakeSystem", "EmergencyBrake"), emergencyBrakeCommand);
        if (HasVvvfEquipment(GetCarSpecForTerminal(terminal)))
        {
            localBus.SetFloat(new TimsTagKey("BrakeSystem", "TargetRegenForcekN"), targetRegenForceN / 1000f);
        }
        else
        {
            localBus.Remove(new TimsTagKey("BrakeSystem", "TargetRegenForcekN"));
        }

        localBus.SetFloat(new TimsTagKey("BrakeSystem", "TargetAirForceN"), targetAirForceN);
        localBus.SetFloat(new TimsTagKey("BrakeSystem", "TargetBCPressureKPa"), targetBCPressureKPa);
        localBus.SetFloat(new TimsTagKey("BrakeSystem", "BCPressureKPa"), CurrentBCPressureKPa);
        localBus.SetFloat(new TimsTagKey("BrakeSystem", "AirForceN"), CurrentAirForceN);
        localBus.SetFloat(new TimsTagKey("BrakeSystem", "AirCapN"), airCapForceN);
        localBus.SetFloat(new TimsTagKey("BrakeSystem", "AirForcePerKPa"), GetAirForcePerKPa());
        localBus.SetFloat(new TimsTagKey("BrakeSystem", "MaxBCPressureKPa"), GetMaxBrakeCylinderSpecPressureKPa());
    }

    private CarSpec GetCarSpecForTerminal(TimsCarTerminal targetTerminal)
    {
        ConsistDefinition definition = GetConsistDefinition();
        if (definition == null || targetTerminal == null)
        {
            return null;
        }

        return definition.GetCar(targetTerminal.CarIndex);
    }

    private static bool HasVvvfEquipment(CarSpec carSpec)
    {
        return carSpec != null &&
               carSpec.carType == CarType.Motor &&
               carSpec.motorCount > 0 &&
               carSpec.vvvfPrefab != null;
    }

    private void ResetOutputs()
    {
        targetRegenForceN = 0f;
        targetAirForceN = 0f;
        targetBCPressureKPa = 0f;
        airCapForceN = 0f;
        emergencyBrakeCommand = false;
        CurrentBCPressureKPa = 0f;
        CurrentRegenForceN = 0f;
        CurrentAirForceN = 0f;
        TotalBrakeForceN = 0f;
        CurrentTargetBCPressureKPa = 0f;
        CurrentRegenDecelMS2 = 0f;
        CurrentAirDecelMS2 = 0f;
        TotalBrakeDecelMS2 = 0f;
        CurrentConsistMassKg = 0f;
    }

    private float GetAirForcePerKPa()
    {
        float forcePerKPa = 0f;
        if (brakeCylinders == null)
        {
            return forcePerKPa;
        }

        for (int i = 0; i < brakeCylinders.Length; i++)
        {
            BrakeCylinder cylinder = brakeCylinders[i];
            if (cylinder != null && cylinder.Spec != null)
            {
                forcePerKPa += cylinder.CalculateBrakeForceN(1000f);
            }
        }

        return forcePerKPa;
    }

    private float GetMaxBrakeCylinderSpecPressureKPa()
    {
        float maxPressureKPa = 0f;
        if (brakeCylinders == null)
        {
            return maxPressureKPa;
        }

        for (int i = 0; i < brakeCylinders.Length; i++)
        {
            BrakeCylinder cylinder = brakeCylinders[i];
            if (cylinder != null && cylinder.Spec != null)
            {
                maxPressureKPa = Mathf.Max(maxPressureKPa, cylinder.Spec.MaxPressureKPa);
            }
        }

        return maxPressureKPa;
    }
}
