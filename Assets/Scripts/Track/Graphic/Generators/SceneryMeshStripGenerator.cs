using UnityEngine;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SceneryMeshStripGenerator : MonoBehaviour
{
    public void Generate(TrackGraph graph, SceneryMeshStripRule rule)
    {
        if (graph == null || rule == null || rule.anchor == null)
        {
            return;
        }

        if (rule.profilePoints == null || rule.profilePoints.Count < 2)
        {
            return;
        }

        // 開始位置・終了位置・間隔
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
        int profileCount = rule.profilePoints.Count;

        List<Vector3> vertices = new List<Vector3>(ringCount * profileCount);
        List<Vector2> uvs = new List<Vector2>(vertices.Count);

        if (!BuildVertices(graph, rule, startM, endM, intervalM, ringCount, profileCount, vertices, uvs))
        {
            return;
        }

        List<int> triangles = SceneryMeshBuilder.BuildTriangles(segmentCount, profileCount, rule.closedShape);

        Mesh mesh = new Mesh();
        mesh.name = string.IsNullOrEmpty(rule.name) ? "Scenery Mesh Strip" : rule.name;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;

        if (rule.material != null)
        {
            GetComponent<MeshRenderer>().material = rule.material;
        }
    }

    private bool BuildVertices(
        TrackGraph graph,
        SceneryMeshStripRule rule,
        float startM,
        float endM,
        float intervalM,
        int ringCount,
        int profileCount,
        List<Vector3> vertices,
        List<Vector2> uvs)
    {
        List<float> profileDistances = rule.CalculateProfileDistance();
        float metersPerTile = Mathf.Max(0.01f, rule.textureMetersPerTile);
        for (int ring = 0; ring < ringCount; ring++)
        {
            // 開始位置からの距離
            float distanceM = Mathf.Min(startM + ring * intervalM, endM);

            if (!SceneryRuntimeResolver.TryResolveFrame(graph, rule.anchor, distanceM, out var frame))
            {
                return false;
            }


            // frameのrightとup座標軸としてオフセットを加味した原点を計算する
            Vector3 stripOrigin = frame.position + frame.right * rule.baseOffsetM + frame.up * rule.heightOffsetM;
            for (int profile = 0; profile < profileCount; profile++)
            {
                SceneryProfilePoint point = rule.profilePoints[profile];
                int index = ring * profileCount + profile;

                // 断面のローカル座標を、ワールド座標に変換
                Vector3 worldPoint = stripOrigin + frame.right * point.offsetM + frame.up * point.heightM;

                vertices.Add(transform.InverseTransformPoint(worldPoint));

                uvs.Add(new Vector2(profileDistances[profile] / metersPerTile, distanceM / metersPerTile));
            }
        }

        return true;
    }
}
