import re

with open('Assets/Scripts/Track/Editor/TrackGraphEditor.cs', 'r') as f:
    text = f.read()

# Extract using block
header = text[:text.find('public override void OnInspectorGUI()')]

# Extract OnInspectorGUI but replace its contents slightly
gui_match = re.search(r'public override void OnInspectorGUI\(\).*?private struct CourseSectionResult', text, re.DOTALL)
if not gui_match:
    gui_match = re.search(r'public override void OnInspectorGUI\(\)(.*?)\n    private', text, re.DOTALL)

# Rebuild OnInspectorGUI
gui_text = """    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8);

        if (GUILayout.Button("Validate Graph"))
        {
            var graph = (TrackGraph)target;
            var errors = new System.Collections.Generic.List<string>();
            if (graph.ValidateGraph(errors))
            {
                Debug.Log($"TrackGraph validation passed. nodes={graph.nodes.Count}, edges={graph.edges.Count}", graph);
            }
            else
            {
                Debug.LogError("TrackGraph validation failed:\\n- " + string.Join("\\n- ", errors), graph);
            }
        }

        if (GUILayout.Button("Recalculate Node Heights From Vertical Profiles"))
        {
            var graph = (TrackGraph)target;
            Undo.RecordObject(graph, "Recalculate Node Heights");
            int updatedCount = graph.RecalculateNodeHeightsFromVerticalProfiles();
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            Debug.Log($"Recalculated node heights from vertical profiles. updatedNodes={updatedCount}", graph);
        }

        if (GUILayout.Button("Apply Demo 25permille Grade To First Edge"))
        {
            var graph = (TrackGraph)target;
            Undo.RecordObject(graph, "Apply Demo Vertical Profile");
            bool applied = graph.ApplyDemoVerticalProfileToFirstEdge();
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            Debug.Log(applied
                ? "Applied demo vertical profile to first edge: 0 -> 25 -> 0 permille."
                : "Could not apply demo vertical profile. TrackGraph has no usable first edge.",
                graph);
        }

        GUILayout.Space(8);

        if (GUILayout.Button("Create TASC 1km Test Track"))
        {
            CreateTascTestTrack();
        }

        if (GUILayout.Button("Create Grade 2km Test Track"))
        {
            CreateGradeTestTrack();
        }

        if (GUILayout.Button("Create 2km Double Straight Course"))
        {
            CreateDoubleStraight2kmCourse();
        }
    }
"""

def extract_method(name):
    # Very simple extraction by searching for the method name and matching braces
    idx = text.find(name)
    if idx == -1: return ""
    
    # backtrack to include attributes or modifiers if possible, up to 'private' or 'public'
    start = text.rfind('    private', 0, idx)
    if start == -1: start = text.rfind('    public', 0, idx)
    
    brace_start = text.find('{', idx)
    brace_count = 1
    i = brace_start + 1
    while brace_count > 0 and i < len(text):
        if text[i] == '{': brace_count += 1
        elif text[i] == '}': brace_count -= 1
        i += 1
    
    return text[start:i]

def extract_method_with_summary(name):
    method_text = extract_method(name)
    idx = text.find(method_text)
    if idx == -1: return method_text
    
    # Check if there's a summary right before it
    summary_idx = text.rfind('    /// <summary>', 0, idx)
    if summary_idx != -1 and text[summary_idx:idx].strip().startswith('///'):
        return text[summary_idx:idx] + method_text
    return method_text

methods_to_keep = [
    "void CreateDoubleStraight2kmCourse",
    "TrackNode CreateNode",
    "TrackEdge CreateStraightEdge",
    "List<BlockSection> CreateSingleBlockSection",
    "void SetTurnoutConnection",
    "void EnsureDefaultTurnoutConnections",
    "void CreateGradeTestTrack",
    "void CreateTascTestTrack"
]

all_methods = "\n\n".join([extract_method_with_summary(m) for m in methods_to_keep])

new_file = header + gui_text + "\n" + all_methods + "\n}\n"

with open('Assets/Scripts/Track/Editor/TrackGraphEditor.cs', 'w') as f:
    f.write(new_file)
print("Done")
