using System;
using System.Collections.Generic;
using UnityEngine;

public class TimsSystem : MonoBehaviour
{
    private readonly List<ITimsDataSource> dataSources = new();
    private readonly Dictionary<ITimsDataSource, float> nextTransmissionTimes = new();

    private readonly List<TimsCarTerminal> terminals = new();

    private readonly TimsDataBus masterBus = new();

    public List<TimsCarTerminal> Terminals => terminals;
    public TimsDataBus MasterBus => masterBus;
    public ConsistDefinition ConsistDefinition => consistDefinition;

    public TimsControlConfig ControlConfig => controlConfig;


    [SerializeField] private ConsistDefinition consistDefinition;
    [SerializeField] private TimsControlConfig controlConfig;

    private void Awake()
    {
        ResolveReferences();
        InitTerminals();
        CollectTerminals();
        RefreshDataSources();
    }

    private void Start()
    {
        RefreshDataSources();
    }

    private void LateUpdate()
    {
        for (int i = 0; i < dataSources.Count; i++)
        {
            ITimsDataSource source = dataSources[i];
            if (source is not MonoBehaviour behaviour)
            {
                continue;
            }

            TimsCarTerminal terminal = behaviour.GetComponentInParent<TimsCarTerminal>();
            if (terminal == null)
            {
                continue;
            }

            if (!ShouldTransmit(source))
            {
                continue;
            }

            source.WriteTimsData(terminal);
            ScheduleNextTransmission(source);
        }
    }

    private bool ShouldTransmit(ITimsDataSource source)
    {
        if (!nextTransmissionTimes.TryGetValue(source, out float nextTime))
        {
            return true;
        }

        return Time.time >= nextTime;
    }

    private void ScheduleNextTransmission(ITimsDataSource source)
    {
        float interval = Mathf.Max(0.001f, source.TransmissionIntervalSeconds);
        nextTransmissionTimes[source] = Time.time + interval;
    }

    private void ResolveReferences()
    {
    }

    private void InitTerminals()
    {
        terminals.Clear();

        if (consistDefinition == null)
        {
            return;
        }        

        for (int i = 0; i < consistDefinition.CarCount; i++)
        {
            terminals.Add(null);
        }
    }

    private void CollectTerminals()
    {
        if (consistDefinition == null)
        {
            return;
        }


        TimsCarTerminal[] founds = GetComponentsInChildren<TimsCarTerminal>(true);

        foreach (TimsCarTerminal found in founds)
        {
            if (found.CarIndex < 0 || found.CarIndex >= consistDefinition.CarCount)
            {
                continue;
            }

            terminals[found.CarIndex] = found;
        }

    }

    public void RefreshDataSources()
    {
        dataSources.Clear();
        nextTransmissionTimes.Clear();

        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ITimsDataSource source)
            {
                dataSources.Add(source);
            }
        }
    }

    public List<float> CollectFloatFromCars(TimsTagKey key, out List<bool> founds)
    {
        List<float> values = new();
        founds = new List<bool>();

        for (int i = 0; i < terminals.Count; i++)
        {
            float value = 0f;
            bool found = false;
            TimsCarTerminal terminal = terminals[i];
            if (terminal != null && terminal.LocalBus.TryGetFloat(key, out float localValue))
            {
                value = localValue;
                found = true;
            }

            values.Add(value);
            founds.Add(found);
        }

        return values;
    }
}
