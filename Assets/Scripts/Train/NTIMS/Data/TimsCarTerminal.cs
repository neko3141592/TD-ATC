using UnityEngine;
using System;

public class TimsCarTerminal : MonoBehaviour
{
    [SerializeField] private int carIndex;

    private readonly TimsDataBus localBus = new(); 

    public int CarIndex => carIndex;
    public TimsDataBus LocalBus => localBus;

    public void Configure(int index)
    {
        carIndex = index;
    }
}