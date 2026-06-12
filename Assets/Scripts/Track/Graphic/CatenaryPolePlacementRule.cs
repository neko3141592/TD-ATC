using UnityEngine;

[System.Serializable]
public class CatenaryPolePlacementRule
{
    public string edgeId;
    public float startDistanceM;
    public float endDistanceM;

    public GameObject prefab;
    public float spacingM = 40f;
    public float sideOffsetM = 3f;
    public float heightOffsetM = 0f;
    public Vector3 rotationOffsetEuler;
}