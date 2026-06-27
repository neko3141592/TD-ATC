using UnityEngine;

public class TrackGraphWalker : MonoBehaviour
{
    [Header("Track")]
    public TrackGraph graph;
    public string currentEdgeId;
    [Min(0f)] public float distanceOnEdgeM;

    [Header("Motion")]
    public bool autoMove = true;
    public float speedMps;
    public float accelerationMps2 = 1f;
    [Min(0f)] public float maxSpeedMps = 25f;

    [Header("Pose")]
    public bool followRotation = true;
    public Vector3 positionOffset;
    public Vector3 rotationOffsetEuler;

    private readonly TrackRuntimeResolver resolver = new TrackRuntimeResolver();

    private void Start()
    {
        ApplyPose();
    }

    private void Update()
    {
        if (graph == null || string.IsNullOrEmpty(currentEdgeId))
        {
            return;
        }

        if (autoMove)
        {
            Move(Time.deltaTime);
        }

        ApplyPose();
    }

    [ContextMenu("Apply Pose")]
    public void ApplyPose()
    {
        if (graph == null || string.IsNullOrEmpty(currentEdgeId))
        {
            return;
        }

        TrackEdge edge = graph.FindEdge(currentEdgeId);
        if (edge == null)
        {
            return;
        }

        distanceOnEdgeM = Mathf.Clamp(distanceOnEdgeM, 0f, Mathf.Max(0f, edge.lengthM));
        if (!resolver.TryResolvePose(
            graph,
            currentEdgeId,
            distanceOnEdgeM,
            out Vector3 position,
            out _,
            out Quaternion rotation))
        {
            return;
        }

        Vector3 worldOffset = rotation * positionOffset;
        if (followRotation)
        {
            transform.SetPositionAndRotation(
                position + worldOffset,
                rotation * Quaternion.Euler(rotationOffsetEuler)
            );
        }
        else
        {
            transform.position = position + worldOffset;
        }
    }

    private void Move(float deltaTime)
    {
        TrackEdge edge = graph.FindEdge(currentEdgeId);
        if (edge == null)
        {
            return;
        }

        speedMps = Mathf.Clamp(speedMps + accelerationMps2 * deltaTime, 0f, maxSpeedMps);
        distanceOnEdgeM += speedMps * deltaTime;

        while (edge != null && distanceOnEdgeM > edge.lengthM)
        {
            distanceOnEdgeM -= edge.lengthM;

            string nextEdgeId = graph.ResolveNextEdgeId(edge.nodeBId, currentEdgeId);
            if (string.IsNullOrEmpty(nextEdgeId) || nextEdgeId == currentEdgeId)
            {
                distanceOnEdgeM = edge.lengthM;
                speedMps = 0f;
                return;
            }

            currentEdgeId = nextEdgeId;
            edge = graph.FindEdge(currentEdgeId);
        }
    }
}
