using System;
using UnityEngine;

public enum HeightMode
{
    GeometryRelative,
    Constant
}

[Serializable]
public class SceneryGuideLine
{
    public string id;
    public SceneryAnchor anchor;

    public float baseOffsetM = 0f;
    public float heightM = 0f;

    public HeightMode heightMode = HeightMode.GeometryRelative;

}