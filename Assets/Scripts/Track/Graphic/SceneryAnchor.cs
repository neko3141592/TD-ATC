using System;

public enum SceneryAnchorKind
{
    Geometry,
    Edge
}

[Serializable]
public class SceneryAnchor
{
    public SceneryAnchorKind kind;
    public string id;
    public float startDistanceM;
    public float endDistanceM;

    public float GetLengthM(TrackGraph graph)
    {
        if (graph == null)
        {
            return 0f;
        }

        if (kind == SceneryAnchorKind.Geometry)
        {
            TrackGeometry geometry = graph.FindGeometry(id);
            return geometry != null ? geometry.lengthM : 0f;
        }

        TrackEdge edge = graph.FindEdge(id);
        return edge != null ? edge.lengthM : 0f;
    }
}
