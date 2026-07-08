using System;
using System.Collections.Generic;

public class BackgroundMaterialJson
{
    public string name;
    public string path;
}

public class BackgroundPrefabJson
{
    public string name;
    public string path;
}

public class BackgroundGroupJson
{
    public string id;
    public string name;
    public List<BackgroundLoftJson> lofts;
}

public enum AnchorKind
{
    Geometry,
    Edge
}

public enum HeightMode
{
    ConstantWorldY,
    AnchorRelative
}

public class BackgroundAnchorJson
{
    public string id;
    public AnchorKind kind;
    public float startDistanceM;
    public float endDistanceM;
}

public class BackgroundGuideLineJson
{
    public string id;
    public float baseOffsetM;
    public float heightM;
    public HeightMode heightMode;
}

public class BackgroundLoftJson
{
    public string id;
    public string name;
    public string defaultMaterial;
    public float sampleIntervalM;
    public BackgroundAnchorJson anchor;
    public List<BackgroundGuideLineJson> guideLines;

}

public class BackgroundLayoutJson
{
    public string version;
    public List<BackgroundMaterialJson> materials;
    public List<BackgroundPrefabJson> prefabs;
    public List<BackgroundGroupJson> backgroundGroups;
}
