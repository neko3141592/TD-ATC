using UnityEngine;

public class LoadWeightController : MonoBehaviour, ITimsDataSource
{
    [SerializeField] private TimsSystem tims;
    [SerializeField] private TimsCarTerminal terminal;
    [SerializeField, Min(1f)] private float fallbackMassKg = 35000f;
    [SerializeField, Min(1f)] private float averagePassengerMassKg = 60f;
    [SerializeField, Range(0f, 2f)] private float minPassengerRate = 0f;
    [SerializeField, Range(0f, 2f)] private float maxPassengerRate = 1.2f;
    [SerializeField] private bool randomizeOnAwake = true;
    [SerializeField, Min(0)] private int passengerCount;

    public float TransmissionIntervalSeconds => 0.5f;
    public int CarIndex
    {
        get
        {
            ResolveReferences();
            return terminal != null ? terminal.CarIndex : -1;
        }
    }

    public int PassengerCount => passengerCount;
    public float PassengerMassKg => passengerCount * averagePassengerMassKg;
    public float MassKg => GetBaseMassKg() + PassengerMassKg;

    private void Awake()
    {
        ResolveReferences();

        if (randomizeOnAwake)
        {
            RandomizePassengerCount();
        }
    }

    private void OnValidate()
    {
        fallbackMassKg = Mathf.Max(1f, fallbackMassKg);
        averagePassengerMassKg = Mathf.Max(1f, averagePassengerMassKg);
        maxPassengerRate = Mathf.Max(minPassengerRate, maxPassengerRate);
        passengerCount = Mathf.Max(0, passengerCount);
    }

    [ContextMenu("Randomize Passenger Count")]
    public void RandomizePassengerCount()
    {
        CarSpec carSpec = GetCarSpec();
        int capacity = carSpec != null ? Mathf.Max(0, carSpec.capacity) : 150;
        int min = Mathf.RoundToInt(capacity * minPassengerRate);
        int max = Mathf.RoundToInt(capacity * maxPassengerRate);
        passengerCount = Random.Range(Mathf.Min(min, max), Mathf.Max(min, max) + 1);
    }

    public void WriteTimsData(TimsCarTerminal targetTerminal)
    {
        if (targetTerminal == null)
        {
            return;
        }

        TimsDataBus localBus = targetTerminal.LocalBus;
        localBus.SetInt(new TimsTagKey("Load", "PassengerCount"), passengerCount);
        localBus.SetFloat(new TimsTagKey("Load", "PassengerMassKg"), PassengerMassKg);
        localBus.SetFloat(new TimsTagKey("Load", "Mass"), MassKg);
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
    }

    private float GetBaseMassKg()
    {
        CarSpec carSpec = GetCarSpec();
        return carSpec != null ? Mathf.Max(1f, carSpec.massKg) : fallbackMassKg;
    }

    private CarSpec GetCarSpec()
    {
        ResolveReferences();

        if (tims == null ||
            tims.ConsistDefinition == null ||
            terminal == null)
        {
            return null;
        }

        return tims.ConsistDefinition.GetCar(terminal.CarIndex);
    }
}
