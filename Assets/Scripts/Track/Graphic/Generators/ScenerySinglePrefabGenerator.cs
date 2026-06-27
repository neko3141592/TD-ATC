using UnityEngine;

public class ScenerySinglePrefabGenerator : MonoBehaviour {
    public void Generate(TrackGraph graph, ScenerySinglePrefabRule rule)
    {
        if (graph == null || rule == null || rule.anchor == null)
        {
            return;
        }

        if (rule.prefab == null)
        {
            return;
        }

        float distanceM = Mathf.Clamp(rule.anchor.startDistanceM, 0f, rule.anchor.GetLengthM(graph));
        if (!SceneryRuntimeResolver.TryResolveFrame(graph, rule.anchor, distanceM, out var frame))
        {
            return;   
        }

        Vector3 stripOrigin = frame.position + frame.right * rule.baseOffsetM + frame.up * rule.heightOffsetM;
        Quaternion rotation = frame.rotation * Quaternion.Euler(rule.rotationOffsetEuler);
        GameObject instance = Instantiate(rule.prefab, stripOrigin, rotation, transform);

    }


}
