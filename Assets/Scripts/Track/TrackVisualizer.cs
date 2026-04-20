using UnityEngine;

public class TrackVisualizer : MonoBehaviour
{
    [Header("作成済みの路線データ")]
    public TrackGraph graph;

    [Header("頂点データを設定したジェネレーターのひな形")]
    // 先ほど断面をセットしたTrackMeshGeneratorがついているオブジェクトを割り当てておき、
    // 頂点の座標設定などをコピーして使います。
    public TrackMeshGenerator generatorTemplate;

    [Header("レールに貼るマテリア")]
    public Material railMaterial;

    [Header("軌間 (レール間の距離：メートル)")]
    public float trackGauge = 1.067f; // 一般的なJR在来線は1.067、新幹線は1.435

    [Header("枕木")]
    public GameObject sleeperPrefab;

    

    void Start()
    {
        GenerateAllTrackMeshes();
    }

    [ContextMenu("Generate All Meshes")]
    public void GenerateAllTrackMeshes()
    {
        if (graph == null || generatorTemplate == null) return;
        if (graph.edges == null || graph.edges.Count == 0) return;

        // 生成済みの古いレールがあれば削除（リセット）
        while (transform.childCount > 0) {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }

        TrackRuntimeResolver resolver = new TrackRuntimeResolver();

        // 左右のレール用に、中心から軌間の半分だけズラした断面データを事前計算する
        Vector2[] basePoints = generatorTemplate.profilePoints;
        Vector2[] leftPoints = new Vector2[basePoints.Length];
        Vector2[] rightPoints = new Vector2[basePoints.Length];
        
        float halfGauge = trackGauge / 2f;
        for (int i = 0; i < basePoints.Length; i++)
        {
            leftPoints[i] = new Vector2(basePoints[i].x - halfGauge, basePoints[i].y);
            rightPoints[i] = new Vector2(basePoints[i].x + halfGauge, basePoints[i].y);
        }

        // グラフ内の全てのエッジ（線路区間）をループ処理
        foreach (var edge in graph.edges)
        {
            // === 左レールの生成 ===
            GameObject edgeObjL = new GameObject("RailMesh_L_" + edge.edgeId);
            edgeObjL.transform.SetParent(this.transform);

            TrackMeshGenerator genL = edgeObjL.AddComponent<TrackMeshGenerator>();
            genL.profilePoints = leftPoints;
            genL.segmentLengthM = generatorTemplate.segmentLengthM;
            genL.closedShape = generatorTemplate.closedShape;

            if (railMaterial != null) edgeObjL.GetComponent<MeshRenderer>().material = railMaterial;
            genL.GenerateTrackMesh(resolver, graph, edge.edgeId, edge.lengthM);

            GameObject edgeObjR = new GameObject("RailMesh_R_" + edge.edgeId);
            edgeObjR.transform.SetParent(this.transform);

            TrackMeshGenerator genR = edgeObjR.AddComponent<TrackMeshGenerator>();
            genR.profilePoints = rightPoints;
            genR.segmentLengthM = generatorTemplate.segmentLengthM;
            genR.closedShape = generatorTemplate.closedShape;

            if (railMaterial != null) edgeObjR.GetComponent<MeshRenderer>().material = railMaterial;
            genR.GenerateTrackMesh(resolver, graph, edge.edgeId, edge.lengthM);
            GenerateSleepers(edge, resolver);
        }
    }
    private void GenerateSleepers(TrackEdge edge, TrackRuntimeResolver resolver)
    {
        GameObject parent = new GameObject("Sleepers_" + edge.edgeId);
        parent.transform.SetParent(this.transform);
        for (float dist = 0f; dist <= edge.lengthM; dist++)
        {
            if (resolver.TryResolvePose(graph, edge.edgeId, dist, out Vector3 pos, out Vector3 tangent))
            {
                Quaternion rotation = Quaternion.LookRotation(tangent, Vector3.up);
                pos.y -= 0.22f;
                if (sleeperPrefab != null)
                {
                    Instantiate(sleeperPrefab, pos, rotation, parent.transform);
                }
            }
        }
    }
}
