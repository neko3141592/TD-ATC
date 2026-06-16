using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TrackMeshGenerator : MonoBehaviour
{
    private const string GeneratedMeshName = "Procedural Track Edge";

    [Header("断面の頂点 (2D)")]
    [Tooltip("Blenderなどで作成した断面の頂点を右回りまたは左回りで設定します。")]
    public Vector2[] profilePoints;

    [Header("メッシュの分割解像度 (何メートルごとに断面を作るか)")]
    public float segmentLengthM = 1f;

    [Header("断面を閉じるか")]
    public bool closedShape = true;

    [Header("テクスチャの実寸")]
    [Tooltip("テクスチャ1枚を何メートルとして貼るか。1ならUVは1mごとに1周します。")]
    [Min(0.01f)]
    public float textureMetersPerTile = 1f;

    public float ResolvedTextureMetersPerTile => textureMetersPerTile > 0.01f ? textureMetersPerTile : 1f;

    public void GenerateTrackMesh(TrackRuntimeResolver resolver, TrackGraph graph, string edgeId, float totalLengthM)
    {
        if (!CanGenerate(resolver, graph, edgeId))
        {
            return;
        }

        int segments = Mathf.CeilToInt(totalLengthM / segmentLengthM);
        int vertsInShape = profilePoints.Length;
        Vector3[] vertices = new Vector3[vertsInShape * (segments + 1)];
        Vector2[] uvs = new Vector2[vertices.Length];
        float[] profileDistancesM = BuildProfileDistances(vertsInShape);

        BuildVerticesAndUvs(resolver, graph, edgeId, totalLengthM, segments, vertsInShape, vertices, uvs, profileDistancesM);
        int[] triangles = BuildTriangles(segments, vertsInShape);

        GetComponent<MeshFilter>().mesh = CreateMesh(vertices, triangles, uvs);
    }

    private bool CanGenerate(TrackRuntimeResolver resolver, TrackGraph graph, string edgeId)
    {
        if (resolver == null || graph == null || string.IsNullOrEmpty(edgeId))
        {
            Debug.LogWarning("Track mesh generation requires a resolver, graph, and edge id.", this);
            return false;
        }

        if (profilePoints == null || profilePoints.Length < 2)
        {
            Debug.LogWarning("断面データ(profilePoints)がセットされていません。", this);
            return false;
        }

        if (segmentLengthM <= 0f)
        {
            Debug.LogWarning("segmentLengthM must be greater than zero.", this);
            return false;
        }

        return true;
    }

    private void BuildVerticesAndUvs(
        TrackRuntimeResolver resolver,
        TrackGraph graph,
        string edgeId,
        float totalLengthM,
        int segments,
        int vertsInShape,
        Vector3[] vertices,
        Vector2[] uvs,
        float[] profileDistancesM)
    {
        for (int i = 0; i <= segments; i++)
        {
            float currentDist = Mathf.Min(i * segmentLengthM, totalLengthM);

            if (resolver.TryResolvePose(graph, edgeId, currentDist, out Vector3 pos, out _, out Quaternion rotation))
            {
                BuildProfileAtDistance(i, vertsInShape, currentDist, pos, rotation, vertices, uvs, profileDistancesM);
            }
        }
    }

    private void BuildProfileAtDistance(
        int segmentIndex,
        int vertsInShape,
        float distanceM,
        Vector3 position,
        Quaternion rotation,
        Vector3[] vertices,
        Vector2[] uvs,
        float[] profileDistancesM)
    {
        float metersPerTile = ResolvedTextureMetersPerTile;

        for (int profileIndex = 0; profileIndex < vertsInShape; profileIndex++)
        {
            int index = segmentIndex * vertsInShape + profileIndex;
            Vector2 profilePoint = profilePoints[profileIndex];
            Vector3 localOffset = new Vector3(profilePoint.x, profilePoint.y, 0f);
            Vector3 worldPoint = position + rotation * localOffset;

            vertices[index] = transform.InverseTransformPoint(worldPoint);
            uvs[index] = new Vector2(profileDistancesM[profileIndex] / metersPerTile, distanceM / metersPerTile);
        }
    }

    private float[] BuildProfileDistances(int vertsInShape)
    {
        float[] profileDistancesM = new float[vertsInShape];

        for (int i = 1; i < vertsInShape; i++)
        {
            profileDistancesM[i] = profileDistancesM[i - 1] + Vector2.Distance(profilePoints[i - 1], profilePoints[i]);
        }

        return profileDistancesM;
    }

    private int[] BuildTriangles(int segments, int vertsInShape)
    {
        int shapeLines = closedShape ? vertsInShape : vertsInShape - 1;
        int[] triangles = new int[shapeLines * segments * 6];
        int ti = 0;

        for (int i = 0; i < segments; i++)
        {
            int currentSegmentStart = i * vertsInShape;
            int nextSegmentStart = (i + 1) * vertsInShape;

            for (int j = 0; j < shapeLines; j++)
            {
                int currentShapePoint = j;
                int nextShapePoint = (j + 1) % vertsInShape;

                int a = currentSegmentStart + currentShapePoint;
                int b = currentSegmentStart + nextShapePoint;
                int c = nextSegmentStart + currentShapePoint;
                int d = nextSegmentStart + nextShapePoint;

                triangles[ti++] = a;
                triangles[ti++] = c;
                triangles[ti++] = b;

                triangles[ti++] = b;
                triangles[ti++] = c;
                triangles[ti++] = d;
            }
        }

        return triangles;
    }

    private static Mesh CreateMesh(Vector3[] vertices, int[] triangles, Vector2[] uvs)
    {
        Mesh mesh = new Mesh();
        mesh.name = GeneratedMeshName;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        return mesh;
    }
}
