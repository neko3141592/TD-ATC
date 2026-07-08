using System;
using UnityEngine;

public enum SceneryGuideHeightMode
{
    AnchorRelative,
    ConstantWorldY
}

[Serializable]
public class SceneryGuideLine
{
    public string id;

    public float baseOffsetM = 0f;
    public float heightM = 0f;

    public SceneryGuideHeightMode heightMode = SceneryGuideHeightMode.AnchorRelative;

}