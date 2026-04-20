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
            var graph = (TrackGraph)target;
            var errors = new List<string>();
            if (graph.ValidateGraph(errors))
            {
                Debug.Log($"TrackGraph validation passed. nodes={graph.nodes.Count}, edges={graph.edges.Count}", graph);
            }
            else
            {
                Debug.LogError("TrackGraph validation failed:\n- " + string.Join("\n- ", errors), graph);
            }
        }

        GUILayout.Space(8);

        if (GUILayout.Button("Create Simple Test Track"))
        {
            var graph = (TrackGraph)target;
            graph.nodes.Clear();
            graph.edges.Clear();
            graph.turnoutStates.Clear();
            graph.stations.Clear(); // Reset station data as well.

            TrackBuilder builder = new TrackBuilder(graph);

            // Start point
            builder.Start(Vector3.zero, Quaternion.identity);

            // 1. Long opening straight
            builder.AddStraight(2360f);
            
            // Place a station near the beginning of the opening straight.
            builder.AddStation("ST_Start", "始発駅", offsetMFromNode: 50f);
            
            builder.PutNode("N1");

            // 2. Right-hand curve with transition sections
            float r = 450f;
            float curveL = (2f * Mathf.PI * r) * (90f / 360f);
            float clothoidL = 40f;
            
            builder.AddClothoidIn(clothoidL, r);
            builder.AddCurve(curveL, r);
            builder.AddClothoidOut(clothoidL, r);
            builder.PutNode("N2");

            // 3. Short straight
            builder.AddStraight(400f);
            builder.PutNode("N3");

            // 4. Left-hand curve with transition sections
            r = 600f;
            curveL = (2f * Mathf.PI * r) * (70f / 360f);
            clothoidL = 40f;
            builder.AddClothoidIn(clothoidL, -r);
            builder.AddCurve(curveL, -r);
            builder.AddClothoidOut(clothoidL, -r);
            builder.PutNode("N4");

            // 5. Final long straight
            builder.AddStraight(2500f);

            builder.PutNode("N5");

            r = 300f;
            curveL = (2f * Mathf.PI * r) * (40f / 360f);
            clothoidL = 60f;
            builder.AddClothoidIn(clothoidL, -r);
            builder.AddCurve(curveL, -r);
            builder.AddClothoidOut(clothoidL, -r);

            builder.PutNode("N6");

            builder.AddStraight(250f);
            
            // Place the terminal stop inside the final straight.
            builder.AddStation("ST_End", "終点駅", offsetMFromNode: 50f);
            
            builder.PutNode("End");

            graph.UpdateNodeTypesAndJunctionIds();
            graph.SyncTurnoutStates();

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
        }

        if (GUILayout.Button("Create Endless Circle Track"))
        {
            var graph = (TrackGraph)target;

            graph.nodes.Clear();
            graph.edges.Clear();
            
            // Build a circular test course (radius 100 m).
            float radius = 100f;
            float curveLength = (2f * Mathf.PI * radius) / 4f; // Arc length for one 90-degree quarter.
        
            var nodes = new TrackNode[4];
            for(int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                // Place the node on the circle and rotate it so local forward follows the loop.
                nodes[i] = new TrackNode { 
                    nodeId = $"N{i:000}", 
                    worldPosition = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * radius, 0, Mathf.Cos(angle * Mathf.Deg2Rad) * radius), 
                    worldRotation = Quaternion.Euler(0, angle + 90f, 0),
                    outgoingEdgeIds = new List<string>()
                };
                graph.nodes.Add(nodes[i]);
            }
            
            // Connect the four quarter-circle edges (0->1, 1->2, 2->3, 3->0).
            for(int i = 0; i < 4; i++)
            {
                int nextIndex = (i + 1) % 4;
                string edgeId = $"E{i:000}";
                
                var edge = new TrackEdge { 
                    edgeId = edgeId, 
                    fromNodeId = nodes[i].nodeId, 
                    toNodeId = nodes[nextIndex].nodeId, 
                    lengthM = curveLength 
                };
                
                // Each edge is a single 90-degree right-hand curve.
                edge.mathCurves.Add(new TrackCurveData { 
                    trackCurveType = TrackCurveType.Curve, 
                    lengthM = curveLength,
                    radiusM = radius 
                });
                
                graph.edges.Add(edge);
                nodes[i].outgoingEdgeIds.Add(edgeId); // Register the route so trains can continue around the loop.
            }

            // Refresh node/junction metadata after rebuilding the graph.
            graph.UpdateNodeTypesAndJunctionIds();
            graph.SyncTurnoutStates();

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
        }

        GUILayout.Space(8);

        if (GUILayout.Button("Create Passing Track (Turnout Demo)"))
        {
            var graph = (TrackGraph)target;
            graph.nodes.Clear();
            graph.edges.Clear();
            graph.turnoutStates.Clear();

            TrackBuilder builder = new TrackBuilder(graph);

            // 1. Approach from the start to the first turnout.
            builder.Start(Vector3.zero, Quaternion.identity);
            builder.AddStraight(200f);
            TrackNode pointStart = builder.PutNode("Point_Start");

            // 2. Main line through track and merge point.
            builder.AddStraight(500f);
            TrackNode pointMerge = builder.PutNode("Point_Merge");

            // 3. Add a short exit straight after the merge.
            builder.AddStraight(300f);
            builder.PutNode("End_Station");

            // 4. Build the passing loop from Point_Start to Point_Merge.
            builder.StartFrom(pointStart);
            
            float trL = 30f;  // Longer transition curves improve ride quality.
            float trR = 250f; // Gentle turnout curve comparable to a low-speed mainline turnout.
            float diagonalL = 30f; // Increase this value to widen the passing-track spacing.

            // Branch to the right using transition curves.
            builder.AddClothoidInOut(trL, trR);

            if (diagonalL > 0f) builder.AddStraight(diagonalL); // Use a diagonal straight to gain lateral spacing.

            builder.AddClothoidInOut(trL, -trR);
            
            // Measure forward advance relative to the main line and fill the remaining straight length.
            float curveAdvanceZ = Vector3.Dot(builder.currentPos - pointStart.worldPosition, pointStart.worldRotation * Vector3.forward);
            builder.AddStraight(500f - curveAdvanceZ * 2f);
            
            // Return to the main line with the opposite S-curve.
            builder.AddClothoidInOut(trL, -trR);

            if (diagonalL > 0f) builder.AddStraight(diagonalL); // Mirror the diagonal straight on the way back.

            builder.AddClothoidInOut(trL, trR);

            // Merge back into the main route at Point_Merge.
            builder.ConnectToNode(pointMerge);

            // Refresh node/junction metadata after rebuilding the graph.
            graph.UpdateNodeTypesAndJunctionIds();
            graph.SyncTurnoutStates();

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
        }

        GUILayout.Space(8);

        if (GUILayout.Button("Create 5km Course (with Passing Tracks & Clothoids)"))
        {
            var graph = (TrackGraph)target;
            graph.nodes.Clear();
            graph.edges.Clear();
            graph.turnoutStates.Clear();

            TrackBuilder builder = new TrackBuilder(graph);

            // Start position
            builder.Start(Vector3.zero, Quaternion.identity);
            builder.AddStraight(550f);
            
            // --- Passing track 1 (station area) ---
            TrackNode station1Start = builder.PutNode("Station1_Start");
            
            // Main line through track
            builder.AddStraight(550f);
            TrackNode station1End = builder.PutNode("Station1_End");

            // Passing track 1 with transition-based turnouts
            float trL = 30f;  // Longer transition curves improve ride quality.
            float trR = 200f; // Gentle turnout curve comparable to a low-speed mainline turnout.
            float diagonalL = 30f; // Increase this value to widen the track spacing.
            
            builder.StartFrom(station1Start);
            // Branch out and settle into a parallel alignment.
            builder.AddClothoidInOut(trL, trR);
            if (diagonalL > 0f) builder.AddStraight(diagonalL); // Use a diagonal straight to gain lateral spacing.
            builder.AddClothoidInOut(trL, -trR);

            // Measure forward advance relative to the main line and fill the remaining straight length.
            float curveAdvanceZ1 = Vector3.Dot(builder.currentPos - station1Start.worldPosition, station1Start.worldRotation * Vector3.forward);
            builder.AddStraight(600f - curveAdvanceZ1 * 2f);

            // Merge using the mirrored turnout geometry.
            builder.AddClothoidInOut(trL, -trR);
            if (diagonalL > 0f) builder.AddStraight(diagonalL); // Mirror the diagonal straight on the merge side.
            builder.AddClothoidInOut(trL, trR);

            // Rejoin the main line and cap the branch route at 45 km/h.
            builder.ConnectToNode(station1End, 45f);

            // --- Curved section (clothoid test) ---
            builder.StartFrom(station1End);
            builder.AddStraight(300f);

            // Right-hand curve (R = 400 m)
            builder.AddClothoidIn(40f, 400f); // Transition curve
            builder.AddCurve(300f, 400f);     // Circular arc
            builder.AddClothoidOut(40f, 400f); // Transition curve

            builder.AddStraight(400f);

            // Tighter left-hand curve (R = -200 m) with a 60 km/h node speed limit.
            builder.PutNode(speedLimitKmH: 60f);
            builder.AddClothoidIn(50f, -200f);
            builder.AddCurve(200f, -200f);
            builder.AddClothoidOut(50f, -200f);
            builder.PutNode(); // Clear the temporary speed limit.

            builder.AddStraight(500f);

            // --- Passing track 2 (station area) ---
            TrackNode station2Start = builder.PutNode("Station2_Start");
            
            // Main line through track
            builder.AddStraight(500f);
            TrackNode station2End = builder.PutNode("Station2_End");

            // Passing track 2 with the same turnout geometry.
            builder.StartFrom(station2Start);
            builder.AddClothoidInOut(trL, trR);
            if (diagonalL > 0f) builder.AddStraight(diagonalL); // Widen the lateral spacing.
            builder.AddClothoidInOut(trL, -trR);

            float curveAdvanceZ2 = Vector3.Dot(builder.currentPos - station2Start.worldPosition, station2Start.worldRotation * Vector3.forward);
            builder.AddStraight(500f - curveAdvanceZ2 * 2f);

            builder.AddClothoidInOut(trL, -trR);
            if (diagonalL > 0f) builder.AddStraight(diagonalL); // Widen the lateral spacing.
            builder.AddClothoidInOut(trL, trR);

            builder.ConnectToNode(station2End, 45f);

            // --- Terminal section ---
            builder.StartFrom(station2End);
            builder.AddStraight(1000f);
            builder.PutNode("End_Terminal");

            graph.UpdateNodeTypesAndJunctionIds();
            graph.SyncTurnoutStates();

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
        }
    }
}
