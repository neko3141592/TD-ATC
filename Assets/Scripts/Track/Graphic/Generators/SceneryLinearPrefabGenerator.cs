using UnityEngine;

public class SceneryLinearPrefabGenerator : MonoBehaviour
{
    public void Generate(TrackGraph graph, SceneryLinearPrefabRule rule)
    {
        if (graph == null || rule == null || rule.anchor == null || rule.prefab == null)
        {
            return;
        }

        float anchorLengthM = rule.anchor.GetLengthM(graph);
        float startM = Mathf.Clamp(rule.anchor.startDistanceM, 0f, anchorLengthM);
        float endM = Mathf.Clamp(rule.anchor.endDistanceM, 0f, anchorLengthM);
        float spacingM = Mathf.Max(0.1f, rule.spacingM);

        if (endM < startM)
        {
            return;
        }

        for (float distanceM = startM; distanceM <= endM; distanceM += spacingM)
        {
            InstantiateAtDistance(graph, rule, distanceM);
        }

        if (!Mathf.Approximately(Mathf.Repeat(endM - startM, spacingM), 0f))
        {
            InstantiateAtDistance(graph, rule, endM);
        }
    }

    private void InstantiateAtDistance(TrackGraph graph, SceneryLinearPrefabRule rule, float distanceM)
    {
        if (!SceneryRuntimeResolver.TryResolveFrame(graph, rule.anchor, distanceM, out SceneryFrame frame))
        {
            return;
        }

        Vector3 position = frame.position + frame.right * rule.offsetM;
        if (rule.heightMode == SceneryGuideHeightMode.ConstantWorldY)
        {
            position.y = rule.heightOffsetM;
        }
        else
        {
            position += frame.up * rule.heightOffsetM;
        }

        Quaternion rotation = frame.rotation * Quaternion.Euler(rule.rotationOffsetEuler);
        Instantiate(rule.prefab, position, rotation, transform);
    }
}
