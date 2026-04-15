using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TrackMeshGenerator : MonoBehaviour
{
    [Header("断面の頂点 (2D)")]
    [Tooltip("Blenderなどで作成した断面の頂点を右回りまたは左回りで設定します。")]
    public Vector2[] profilePoints;

    [Header("メッシュの分割解像度 (何メートルごとに断面を作るか)")]
    public float segmentLengthM = 1f;

    [Header("断面を閉じるか")]
    public bool closedShape = true;

    /// <summary>
    /// TrackRuntimeResolverを使って、指定したエッジのメッシュを生成します。
    /// </summary>
    /// <param name="resolver">計算エンジン</param>
    /// <param name="graph">路線のグラフデータ</param>
    /// <param name="edgeId">生成するエッジのID</param>
    /// <param name="totalLengthM">このエッジの合計の長さ（メートル）</param>
    public void GenerateTrackMesh(TrackRuntimeResolver resolver, TrackGraph graph, string edgeId, float totalLengthM)
    {
        if (profilePoints == null || profilePoints.Length < 2)
        {
            Debug.LogWarning("断面データ(profilePoints)がセットされていません。");
            return;
        }

        if (segmentLengthM <= 0f)
        {
            Debug.LogWarning("segmentLengthM must be greater than zero.", this);
            return;
        }

        // 分割数を計算
        int segments = Mathf.CeilToInt(totalLengthM / segmentLengthM);
        int vertsInShape = profilePoints.Length;
        int vertCount = vertsInShape * (segments + 1);

        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];

        for (int i = 0; i <= segments; i++)
        {
            float currentDist = Mathf.Min(i * segmentLengthM, totalLengthM);

            if (resolver.TryResolvePose(graph, edgeId, currentDist, out Vector3 pos, out Vector3 tangent))
            {
                Quaternion rotation = tangent != Vector3.zero ? Quaternion.LookRotation(tangent, Vector3.up) : Quaternion.identity;

                for (int j = 0; j < vertsInShape; j++)
                {
                    int index = i * vertsInShape + j;

                    Vector3 localOffset = new Vector3(profilePoints[j].x, profilePoints[j].y, 0f);
                    
                    // 算出された座標(pos)はワールド座標ベースなので、このオブジェクトのローカル座標に変換してメッシュの頂点とします
                    Vector3 worldPoint = pos + (rotation * localOffset);
                    vertices[index] = transform.InverseTransformPoint(worldPoint);

                    float u = (float)j / (vertsInShape - 1);
                    float v = currentDist;
                    uvs[index] = new Vector2(u, v);
                }
            }
        }

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

        Mesh mesh = new Mesh();
        mesh.name = "Procedural Track Edge";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
    }
}
