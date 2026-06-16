using UnityEngine;

public class TrackVisualizer : MonoBehaviour
{
    private const string LeftRailPrefix = "RailMesh_L_";
    private const string RightRailPrefix = "RailMesh_R_";
    private const string SleepersPrefix = "Sleepers_";
    private const string CatenaryPolePrefix = "Poles_";
    private const string CatenaryWirePrefix = "CatenaryWire_";
    private const float SleeperHeightOffsetM = -0.085f;
    private const float SleeperSpacingM = 1f;

    [Header("作成済みの路線データ")]
    public TrackGraph graph;

    [Header("レール")]
    // 先ほど断面をセットしたTrackMeshGeneratorがついているオブジェクトを割り当てておき、
    // 頂点の座標設定などをコピーして使います。
    public TrackMeshGenerator generatorTemplate;
    public Material railMaterial;
    public float trackGauge = 1.067f;

    [Header("枕木")]
    public GameObject sleeperPrefab;

    [Header("バラスト")]
    public TrackMeshGenerator ballastTemplate;
    public Material ballastMaterial;
    public float ballastLateralOffsetM = 0f;
    public float ballastHeightOffsetM = 0f;

    [Header("架線柱")]
    public CatenaryPolePlacementRule[] catenaryPoles;

    [Header("架線")]
    public bool generateCatenaryWire = true;
    public Material catenaryWireMaterial;
    [Min(0.1f)] public float catenaryWireHeightM = 5f;
    [Min(0.1f)] public float catenaryWireSampleIntervalM = 5f;
    [Min(0.001f)] public float catenaryWireWidthM = 0.035f;
    [Min(0f)] public float catenaryWireStaggerM = 0.25f;
    [Min(0.1f)] public float catenaryWireStaggerIntervalM = 50f;


    void Start()
    {
        GenerateAllTrackMeshes();
    }

    [ContextMenu("Generate All Meshes")]
    public void GenerateAllTrackMeshes()
    {
        if (!CanGenerate())
        {
            return;
        }

        ClearGeneratedChildren();

        TrackRuntimeResolver resolver = new TrackRuntimeResolver();
        Vector2[] leftRailProfile = CreateOffsetProfile(-trackGauge * 0.5f);
        Vector2[] rightRailProfile = CreateOffsetProfile(trackGauge * 0.5f);


        // バラスト・レール・枕木
        foreach (TrackEdge edge in graph.edges)
        {
            GenerateBallast(edge, resolver);
            GenerateRail(edge, resolver, LeftRailPrefix, leftRailProfile);
            GenerateRail(edge, resolver, RightRailPrefix, rightRailProfile);
            GenerateSleepers(edge, resolver);
            GenerateCatenaryWire(edge, resolver);
            GenerateCatenaryWire(edge, resolver, 0.5f);
        }

        GenerateCatenaryPole(catenaryPoles, resolver);
    }

    private bool CanGenerate()
    {
        if (graph == null || generatorTemplate == null)
        {
            return false;
        }

        return graph.edges != null && graph.edges.Count > 0;
    }

    private void ClearGeneratedChildren()
    {
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }
    }

    private Vector2[] CreateOffsetProfile(float lateralOffsetM)
    {
        return CreateOffsetProfile(generatorTemplate.profilePoints, lateralOffsetM, 0f);
    }

    private Vector2[] CreateOffsetProfile(Vector2[] baseProfile, float lateralOffsetM, float heightOffsetM)
    {
        Vector2[] offsetProfile = new Vector2[baseProfile.Length];

        for (int i = 0; i < baseProfile.Length; i++)
        {
            offsetProfile[i] = new Vector2(baseProfile[i].x + lateralOffsetM, baseProfile[i].y + heightOffsetM);
        }

        return offsetProfile;
    }

    private void GenerateRail(TrackEdge edge, TrackRuntimeResolver resolver, string objectNamePrefix, Vector2[] profile)
    {
        GameObject railObject = CreateGeneratedChild(objectNamePrefix + edge.edgeId);
        TrackMeshGenerator generator = railObject.AddComponent<TrackMeshGenerator>();

        generator.profilePoints = profile;
        generator.segmentLengthM = generatorTemplate.segmentLengthM;
        generator.closedShape = generatorTemplate.closedShape;
        generator.textureMetersPerTile = generatorTemplate.textureMetersPerTile;

        if (railMaterial != null)
        {
            railObject.GetComponent<MeshRenderer>().material = railMaterial;
        }

        generator.GenerateTrackMesh(resolver, graph, edge.edgeId, edge.lengthM);
    }

    private void GenerateSleepers(TrackEdge edge, TrackRuntimeResolver resolver)
    {
        GameObject parent = CreateGeneratedChild(SleepersPrefix + edge.edgeId);

        for (float distanceM = 0f; distanceM <= edge.lengthM; distanceM += SleeperSpacingM)
        {
            if (resolver.TryResolvePose(graph, edge.edgeId, distanceM, out Vector3 position, out _, out Quaternion rotation))
            {
                InstantiateSleeper(position, rotation, parent.transform);
            }
        }
    }

    private void InstantiateSleeper(Vector3 position, Quaternion rotation, Transform parent)
    {
        if (sleeperPrefab == null)
        {
            return;
        }

        position.y += SleeperHeightOffsetM;
        Instantiate(sleeperPrefab, position, rotation, parent);
    }

    private void GenerateCatenaryPole(CatenaryPolePlacementRule[] rules, TrackRuntimeResolver resolver)
    {
        if (rules == null)
        {
            return;
        }

        foreach (CatenaryPolePlacementRule rule in rules)
        {
            if (rule == null || string.IsNullOrEmpty(rule.geometryId) || rule.prefab == null)
            {
                continue;
            }

            TrackGeometry edge = graph.FindGeometry(rule.geometryId);

            if (edge == null)
            {
                continue;
            }

            if (rule.spacingM <= 0.001f)
            {
                continue;
            }

            float startDistanceM = Mathf.Max(0f, rule.startDistanceM);
            float endDistanceM = Mathf.Min(rule.endDistanceM, edge.lengthM);
            if (endDistanceM < startDistanceM)
            {
                continue;
            }

            GameObject parent = CreateGeneratedChild(CatenaryPolePrefix + rule.geometryId);

            for (float distanceM = startDistanceM; distanceM <= endDistanceM; distanceM += rule.spacingM)
            {
                if (resolver.TryResolveGeometryPose(graph, rule.geometryId, distanceM, out Vector3 position, out _, out Quaternion rotation))
                {
                    InstantiateCatenaryPole(position, rotation, parent.transform, rule);
                }
            }
        }
    }
    private void InstantiateCatenaryPole(Vector3 position, Quaternion rotation, Transform parent, CatenaryPolePlacementRule rule) 
    {
        if (rule.prefab == null)
        {
            return;
        }

        position += rotation * Vector3.right * rule.sideOffsetM;
        position.y += rule.heightOffsetM;
        Instantiate(rule.prefab, position, rotation, parent);
    }

    private void GenerateCatenaryWire(TrackEdge edge, TrackRuntimeResolver resolver, float offset = 0)
    {
        if (!generateCatenaryWire || edge == null || edge.lengthM <= 0f)
        {
            return;
        }

        float sampleIntervalM = Mathf.Max(0.1f, catenaryWireSampleIntervalM);
        int pointCount = Mathf.CeilToInt(edge.lengthM / sampleIntervalM) + 1;
        if (pointCount < 2)
        {
            return;
        }

        GameObject wireObject = CreateGeneratedChild(CatenaryWirePrefix + edge.edgeId);
        LineRenderer lineRenderer = wireObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = pointCount;
        lineRenderer.startWidth = catenaryWireWidthM;
        lineRenderer.endWidth = catenaryWireWidthM;
        lineRenderer.numCornerVertices = 2;
        lineRenderer.numCapVertices = 2;

        Material material = catenaryWireMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                material = new Material(shader);
            }
        }

        if (material != null)
        {
            lineRenderer.material = material;
        }

        for (int i = 0; i < pointCount; i++)
        {
            float distanceM = Mathf.Min(i * sampleIntervalM, edge.lengthM);
            if (!resolver.TryResolvePose(graph, edge.edgeId, distanceM, out Vector3 position, out _, out Quaternion rotation))
            {
                lineRenderer.SetPosition(i, wireObject.transform.InverseTransformPoint(position));
                continue;
            }

            float staggerM = CalculateCatenaryWireStagger(distanceM);
            Vector3 wirePosition =
                position
                + Vector3.up * (catenaryWireHeightM + offset)
                + rotation * Vector3.right * staggerM;

            lineRenderer.SetPosition(i, wireObject.transform.InverseTransformPoint(wirePosition));
        }
    }

    private float CalculateCatenaryWireStagger(float distanceM)
    {
        if (catenaryWireStaggerM <= 0f)
        {
            return 0f;
        }

        float intervalM = Mathf.Max(0.1f, catenaryWireStaggerIntervalM);
        float t = Mathf.PingPong(distanceM / intervalM, 1f);
        return Mathf.Lerp(-catenaryWireStaggerM, catenaryWireStaggerM, t);
    }

    private void GenerateBallast(TrackEdge edge, TrackRuntimeResolver resolver)
    {
        if (ballastTemplate == null)
        {
            return;
        }

        GameObject ballastObject = CreateGeneratedChild("Ballast_" + edge.edgeId);
        TrackMeshGenerator generator = ballastObject.AddComponent<TrackMeshGenerator>();

        generator.profilePoints = CreateOffsetProfile(
            ballastTemplate.profilePoints,
            ballastLateralOffsetM,
            ballastHeightOffsetM
        );
        generator.segmentLengthM = ballastTemplate.segmentLengthM;
        generator.closedShape = false;
        generator.textureMetersPerTile = ballastTemplate.textureMetersPerTile;

        if (ballastMaterial != null)
        {
            ballastObject.GetComponent<MeshRenderer>().material = ballastMaterial;
        }

        generator.GenerateTrackMesh(resolver, graph, edge.edgeId, edge.lengthM);
    }

    private GameObject CreateGeneratedChild(string objectName)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform);
        return child;
    }
}
