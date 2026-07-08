using UnityEngine;
using UnityEngine.Serialization;
using System;
using System.Collections.Generic;

[Serializable]
public class SceneryLoftSurfaceMaterial
{
    public int surfaceIndex;
    public Material material;
}

[Serializable]
public class SceneryLoftRule
{
    public string name;
    public float startDistanceM;
    public float endDistanceM;
    public float sampleIntervalM = 2f;

    public SceneryAnchor anchor;

    public List<SceneryGuideLine> guideLines;

    public bool closedShape = false;

    [Header("Rendering")]
    [FormerlySerializedAs("material")]
    public Material defaultMaterial;

    [FormerlySerializedAs("materials")]
    public List<SceneryLoftSurfaceMaterial> surfaceMaterials = new();
    public float textureMetersPerTile = 2f;
}
