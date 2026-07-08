using UnityEngine;
using System.Collections.Generic;

public static class SceneryMeshBuilder
{
    public static void BuildProfileDistances(IReadOnlyList<Vector3> profilePoints, List<float> profileDistances)
    {
        if (profileDistances == null)
        {
            return;
        }

        profileDistances.Clear();

        if (profilePoints == null || profilePoints.Count == 0)
        {
            return;
        }

        float currentDistanceM = 0f;
        profileDistances.Add(0f);

        for (int i = 1; i < profilePoints.Count; i++)
        {
            currentDistanceM += Vector3.Distance(profilePoints[i - 1], profilePoints[i]);
            profileDistances.Add(currentDistanceM);
        }
    }

    public static List<int> BuildTriangles(int segmentCount, int profileCount, bool closedShape)
    {
        int profileEdges = closedShape ? profileCount : profileCount - 1;
        List<int> triangles = new List<int>(segmentCount * profileEdges * 6);

        for (int segment = 0; segment < segmentCount; segment++)
        {
            int currentStart = segment * profileCount;
            int nextStart = (segment + 1) * profileCount;

            for (int p = 0; p < profileEdges; p++)
            {
                // 次の頂点番号
                int nextP = (p + 1) % profileCount;

                // 断面currentの頂点1
                int a = currentStart + p;
                // 断面currentの頂点2
                int b = currentStart + nextP;

                // 断面nextの頂点1
                int c = nextStart + p;
                // 断面nextの頂点2
                int d = nextStart + nextP;

                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);

                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
            }
        }

        return triangles;
    }

    public static List<List<int>> BuildSubMeshTriangles(
        int segmentCount,
        int profileCount,
        bool closedShape,
        Material defaultMaterial,
        IReadOnlyList<SceneryLoftSurfaceMaterial> surfaceMaterials,
        List<Material> materials)
    {
        if (materials == null)
        {
            return new List<List<int>>();
        }

        materials.Clear();

        int profileEdges = closedShape ? profileCount : profileCount - 1;
        List<List<int>> subMeshTriangles = new List<List<int>>();

        for (int segment = 0; segment < segmentCount; segment++)
        {
            int currentStart = segment * profileCount;
            int nextStart = (segment + 1) * profileCount;

            for (int surfaceIndex = 0; surfaceIndex < profileEdges; surfaceIndex++)
            {
                Material material = ResolveSurfaceMaterial(defaultMaterial, surfaceMaterials, surfaceIndex);
                int subMeshIndex = GetOrCreateSubMeshIndex(material, materials, subMeshTriangles);
                List<int> triangles = subMeshTriangles[subMeshIndex];

                int nextProfile = (surfaceIndex + 1) % profileCount;

                int a = currentStart + surfaceIndex;
                int b = currentStart + nextProfile;
                int c = nextStart + surfaceIndex;
                int d = nextStart + nextProfile;

                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);

                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
            }
        }

        return subMeshTriangles;
    }

    private static Material ResolveSurfaceMaterial(
        Material defaultMaterial,
        IReadOnlyList<SceneryLoftSurfaceMaterial> surfaceMaterials,
        int surfaceIndex)
    {
        Material material = defaultMaterial;

        if (surfaceMaterials == null)
        {
            return material;
        }

        for (int i = 0; i < surfaceMaterials.Count; i++)
        {
            SceneryLoftSurfaceMaterial surfaceMaterial = surfaceMaterials[i];
            if (surfaceMaterial != null &&
                surfaceMaterial.surfaceIndex == surfaceIndex &&
                surfaceMaterial.material != null)
            {
                material = surfaceMaterial.material;
            }
        }

        return material;
    }

    private static int GetOrCreateSubMeshIndex(
        Material material,
        List<Material> materials,
        List<List<int>> subMeshTriangles)
    {
        for (int i = 0; i < materials.Count; i++)
        {
            if (materials[i] == material)
            {
                return i;
            }
        }

        int index = materials.Count;
        materials.Add(material);
        subMeshTriangles.Add(new List<int>());
        return index;
    }

    public static bool TryResolveProfilePoints(
        TrackGraph graph,
        SceneryLoftRule rule,
        float distanceM,
        List<Vector3> points)
    {
        if (points == null)
        {
            return false;
        }

        points.Clear();

        if (graph == null || rule == null || rule.anchor == null ||
            rule.guideLines == null || rule.guideLines.Count < 2)
        {
            return false;
        }

        if (!SceneryRuntimeResolver.TryResolveFrame(graph, rule.anchor, distanceM, out SceneryFrame frame))
        {
            return false;
        }

        foreach (SceneryGuideLine guideLine in rule.guideLines)
        {
            if (guideLine == null)
            {
                return false;
            }

            points.Add(ResolveGuideLinePoint(frame, guideLine));
        }

        return true;
    }

    private static Vector3 ResolveGuideLinePoint(
        SceneryFrame frame,
        SceneryGuideLine guideLine)
    {
        Vector3 point =
            frame.position
            + frame.right * guideLine.baseOffsetM;

        switch (guideLine.heightMode)
        {
            case SceneryGuideHeightMode.ConstantWorldY:
                point.y = guideLine.heightM;
                return point;

            case SceneryGuideHeightMode.AnchorRelative:
            default:
                return point + frame.up * guideLine.heightM;
        }
    }
}
