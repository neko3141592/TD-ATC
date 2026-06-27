using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class TrackJsonLoader
{
    public static TrackLayoutJson LoadFromFile(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Cannot load track JSON. Path is empty.");
            return null;
        }

        if (!File.Exists(path))
        {
            Debug.LogError($"Cannot load track JSON. File does not exist: {path}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogError($"Cannot load track JSON. File is empty: {path}");
                return null;
            }

            TrackLayoutJson layout = JsonUtility.FromJson<TrackLayoutJson>(json);
            NormalizeLayout(layout);
            return layout;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load track JSON '{path}'. {ex.Message}");
            return null;
        }
    }

    private static void NormalizeLayout(TrackLayoutJson layout)
    {
        if (layout == null)
        {
            return;
        }

        layout.geometries ??= new List<TrackGeometryJson>();
        layout.trackGroups ??= new List<TrackGroupJson>();
    }
}
