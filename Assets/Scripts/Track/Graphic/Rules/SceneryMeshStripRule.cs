using System;
using System.Collections.Generic;
using UnityEngine;

public enum SceneryProfileHeightMode
{
    TrackRelative,
    ConstantWorldY,
    GroundProfile
}

[Serializable]
public class SceneryProfilePoint
{
    public float offsetM;
    public SceneryProfileHeightMode heightMode = SceneryProfileHeightMode.TrackRelative;
    public float heightM;
}

[Serializable]
public class SceneryMeshStripRule
{
    public string name;
    public SceneryAnchor anchor;

    [Header("Placement")]
    public float baseOffsetM;
    public float heightOffsetM;

    [Header("Shape")]
    public List<SceneryProfilePoint> profilePoints;
    public bool closedShape = false;

    [Header("Sampling")]
    [Min(0.5f)] public float sampleIntervalM = 2f;

    [Header("Rendering")]
    public Material material;
    [Min(0.01f)] public float textureMetersPerTile = 2f;

    public List<float> CalculateProfileDistance()
    {
        if (profilePoints == null || profilePoints.Count == 0)
        {
            return new List<float>();
        }

        List<float> profileDistances = new(profilePoints.Count);

        float currentDistance = 0;
        profileDistances.Add(0);
        for (int i = 0; i < profilePoints.Count - 1; i++)
        {
            Vector2 next = new Vector2(profilePoints[i + 1].offsetM, profilePoints[i + 1].heightM);
            Vector2 current = new Vector2(profilePoints[i].offsetM, profilePoints[i].heightM);
            currentDistance += Vector2.Distance(next, current);
            profileDistances.Add(currentDistance);
        }

        return profileDistances;
    }
}