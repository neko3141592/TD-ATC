using System;
using UnityEngine;

[Serializable]
public class SceneryLinearPrefabRule
{
    public string name;
    public SceneryAnchor anchor;

    public GameObject prefab;

    public float spacingM = 2f;
    public float offsetM = 0f;
    public float heightOffsetM = 0f;
    

    public Vector3 rotationOffsetEuler;
}