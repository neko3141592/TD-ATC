using UnityEngine;

public class SceneryVisualizer : MonoBehaviour
{
    private const string MeshStripPrefix = "SceneryMeshStrip_";
    private const string SinglePrefabPrefix = "SinglePrefab_";

    [Header("作成済みの路線データ")]
    public TrackGraph graph;

    [Header("線路沿いメッシュ")]
    public SceneryMeshStripRule[] meshStrips;

    [Header("単体プレハブ")]
    public ScenerySinglePrefabRule[] singlePrefabs;

    [ContextMenu("Generate All Scenery")]
    public void GenerateAllScenery()
    {
        if (!CanGenerate()) 

        {
            return;
        }

        ClearGeneratedChildren();
        GenerateMeshStrips();
        GenerateSinglePrefabs();
    }

    [ContextMenu("Clear Generated Scenery")]
    public void ClearGeneratedScenery()
    {
        ClearGeneratedChildren();
    }

    void Start()
    {
        GenerateAllScenery();        
    }

    private bool CanGenerate()
    {
        return graph != null;
    }

    private void GenerateMeshStrips()
    {
        if (meshStrips == null)
        {
            return;
        }

        for (int i = 0; i < meshStrips.Length; i++)
        {
            SceneryMeshStripRule rule = meshStrips[i];
            if (rule == null)
            {
                continue;
            }

            GameObject meshStripObject = CreateGeneratedChild(GetMeshStripObjectName(rule, i));
            SceneryMeshStripGenerator generator = meshStripObject.AddComponent<SceneryMeshStripGenerator>();
            generator.Generate(graph, rule);
        }
    }

    private void GenerateSinglePrefabs()
    {
        if (singlePrefabs == null)
        {
            return;
        }
        for(int i = 0; i < singlePrefabs.Length; i++)
        {
            ScenerySinglePrefabRule rule = singlePrefabs[i];
            if (rule == null)
            {
                continue;
            }

            GameObject singlePrefabObject = CreateGeneratedChild(GetSinglePrefabObjectName(rule, i));
            ScenerySinglePrefabGenerator generator = singlePrefabObject.AddComponent<ScenerySinglePrefabGenerator>();
            generator.Generate(graph, rule);
        }
    }

    private string GetMeshStripObjectName(SceneryMeshStripRule rule, int index)
    {
        return string.IsNullOrEmpty(rule.name)
            ? MeshStripPrefix + index
            : MeshStripPrefix + rule.name;
    }
    private string GetSinglePrefabObjectName(ScenerySinglePrefabRule rule, int index)
    {
        return string.IsNullOrEmpty(rule.name)
            ? SinglePrefabPrefix + index
            : SinglePrefabPrefix + rule.name;
    }

    private void ClearGeneratedChildren()
    {
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }
    }

    private GameObject CreateGeneratedChild(string objectName)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform);
        return child;
    }
}
