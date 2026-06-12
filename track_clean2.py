import re

with open('Assets/Scripts/Track/Editor/TrackGraphEditor.cs', 'r') as f:
    text = f.read()

# Replace the buttons
text = text.replace("""        if (GUILayout.Button("Create 10km Single Track Course"))
        {
            CreateSingleTrack10kmCourse();
        }

        if (GUILayout.Button("Create 20km Grade Station Junction Course"))
        {
            CreateGradeStationJunction20kmCourse();
        }
""", "")

methods_to_remove = [
    "CreateSingleTrack10kmCourse",
    "CreateGradeStationJunction20kmCourse",
    "Is20kmPassingLoopSection",
    "BuildRandomStationIntervals",
    "AddPlainSectionWithStationNearEnd",
    "AddCurvedSectionWithStationNearEnd",
    "AddPassingLoopSectionWithStationNearEnd",
    "ApplyReadableGradeProfile",
    "ApplyFlatGradeProfile",
    "RandomRealisticGradePermille",
    "RandomRange",
    "ResolveGeneratedStationId",
    "ResolveGeneratedStationName",
    "AddPlainSection",
    "AddCurvedSection",
    "AddMajorStationWithPassingLoop",
    "GetMostRecentSidingEdgeId",
    "ConfigurePassingLoopStartTurnout",
    "FindIncomingEdgeId",
    "CreatePassingSidingEndNode",
    "AssignSequentialBlockSections",
    "CourseSectionResult" # struct
]

for method in methods_to_remove:
    # Find the start of the method or struct
    # It might have a summary block right above it
    pattern = r'(    /// <summary>.*?</summary>\s*/// <remarks>.*?</remarks>\s*)?(    /// <param.*?</param>\s*)*(    /// <returns>.*?</returns>\s*)?(    (?:private|public|protected|internal)(?: static)? (?:void|bool|int|float|string|TrackNode|CourseSectionResult(?:\[\])?) ' + method + r'[\s\S]*?^    })'
    
    # Also struct pattern
    if method == "CourseSectionResult":
        pattern = r'(    private struct CourseSectionResult\s*\{[\s\S]*?^    })'
        
    text = re.sub(pattern, "", text, flags=re.MULTILINE | re.DOTALL)

with open('Assets/Scripts/Track/Editor/TrackGraphEditor.cs', 'w') as f:
    f.write(text)

