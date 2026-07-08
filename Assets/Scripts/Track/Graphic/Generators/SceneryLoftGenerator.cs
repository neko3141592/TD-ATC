using UnityEngine;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SceneryLoftGenerator : MonoBehaviour
{
    public void Generate(TrackGraph graph, SceneryLoftRule rule)
    {
        if (graph == null || rule == null || rule.anchor == null)
        {
            return;
        }
        
        if (rule.guideLines == null || rule.guideLines.Count < 2)
        {
            return;
        }

        float anchorLengthM = rule.anchor.GetLengthM(graph);
        float startM = Mathf.Clamp(rule.anchor.startDistanceM, 0f, anchorLengthM);
        float endM = Mathf.Clamp(rule.anchor.endDistanceM, 0f, anchorLengthM);
        float intervalM = Mathf.Max(0.1f, rule.sampleIntervalM);

        if (endM <= startM)
        {
            return;
        }

        // 間の個数
        int segmentCount = Mathf.CeilToInt((endM - startM) / intervalM);

        // 断面の個数
        int ringCount = segmentCount + 1;

        // 断面の頂点の個数
        int profileCount = rule.guideLines.Count;

        List<Vector3> vertices = new List<Vector3>(ringCount * profileCount);
        List<Vector2> uvs = new List<Vector2>(vertices.Count);

        if (!BuildVertices(graph, rule, startM, endM, intervalM, ringCount, profileCount, vertices, uvs))
        {
            return;
        }

        List<Material> materials = new();
        List<List<int>> subMeshTriangles = SceneryMeshBuilder.BuildSubMeshTriangles(
            segmentCount,
            profileCount,
            rule.closedShape,
            rule.defaultMaterial,
            rule.surfaceMaterials,
            materials);

        if (subMeshTriangles.Count == 0)
        {
            return;
        }

        Mesh mesh = new Mesh();
        mesh.name = string.IsNullOrEmpty(rule.name) ? "Scenery Loft" : rule.name;
        mesh.SetVertices(vertices);
        mesh.subMeshCount = subMeshTriangles.Count;
        for (int i = 0; i < subMeshTriangles.Count; i++)
        {
            mesh.SetTriangles(subMeshTriangles[i], i);
        }
        
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;

        if (HasAnyMaterial(materials))
        {
            GetComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
        }

    }

    private static bool HasAnyMaterial(IReadOnlyList<Material> materials)
    {
        if (materials == null)
        {
            return false;
        }

        for (int i = 0; i < materials.Count; i++)
        {
            if (materials[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool BuildVertices(
        TrackGraph graph,
        SceneryLoftRule rule,
        float startM,
        float endM,
        float intervalM,
        int ringCount,
        int profileCount,
        List<Vector3> vertices,
        List<Vector2> uvs)
    {
        List<float> profileDistances = new();
        float metersPerTile = Mathf.Max(0.01f, rule.textureMetersPerTile);
        
        for (int ring = 0; ring < ringCount; ring++)
        {
            List<Vector3> profiles = new();
            float distanceM = Mathf.Min(startM + ring * intervalM, endM);
            if(!SceneryMeshBuilder.TryResolveProfilePoints(graph, rule, distanceM, profiles))
            {
                return false;
            }

            SceneryMeshBuilder.BuildProfileDistances(profiles, profileDistances);
            for (int profile = 0; profile < profileCount; profile++)
            {
                vertices.Add(transform.InverseTransformPoint(profiles[profile]));
                uvs.Add(new Vector2(profileDistances[profile] / metersPerTile, distanceM / metersPerTile));
            }
        }

        return true;
    }
}
