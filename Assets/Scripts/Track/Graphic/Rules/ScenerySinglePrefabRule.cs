using System;
using UnityEngine;

[Serializable]
public class ScenerySinglePrefabRule
{
    public string name;
    public SceneryAnchor anchor;

    public GameObject prefab;
    public float baseOffsetM = 0f;
    public float heightOffsetM = 0f;

    public Vector3 rotationOffsetEuler;

}