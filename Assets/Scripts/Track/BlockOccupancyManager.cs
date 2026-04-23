using UnityEngine;
using System.Collections.Generic;


public class BlockOccupancyManager : MonoBehaviour
{
    private Dictionary<string, HashSet<string>> occupiedTrainsByBlock = new();

    private Dictionary<string, HashSet<string>> occupiedBlocksByTrain = new();

}