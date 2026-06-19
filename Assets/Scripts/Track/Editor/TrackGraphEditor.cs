using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrackGraph))]
public class TrackGraphEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8);

        if (GUILayout.Button("Validate Graph"))
        {
            ValidateGraph();
        }

        GUILayout.Space(8);

        if (GUILayout.Button("Import Track JSON"))
        {
            ImportTrackJson();
        }

    }

    private void ValidateGraph()
    {
        var graph = (TrackGraph)target;
        var errors = new List<string>();
        if (graph.ValidateGraph(errors))
        {
            Debug.Log($"TrackGraph validation passed. nodes={graph.nodes.Count}, edges={graph.edges.Count}, geometries={graph.geometries.Count}", graph);
            return;
        }

        Debug.LogError("TrackGraph validation failed:\n- " + string.Join("\n- ", errors), graph);
    }

    private void ImportTrackJson()
    {
        var graph = (TrackGraph)target;
        string path = EditorUtility.OpenFilePanel("Import Track JSON", "Assets/Data", "json");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        TrackLayoutJson layout = TrackJsonLoader.LoadFromFile(path);
        if (layout == null)
        {
            Debug.LogError($"Failed to import Track JSON: {path}", graph);
            return;
        }

        Undo.RecordObject(graph, "Import Track JSON");
        TrackJsonCompiler.CompileInto(graph, layout);

        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();

        Debug.Log($"Imported Track JSON '{path}'. nodes={graph.nodes.Count}, edges={graph.edges.Count}, geometries={graph.geometries.Count}", graph);
    }
}
