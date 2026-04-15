using UnityEngine;

[System.Serializable]
public class CarTrackState
{
    public int carIndex;               // 何両目か（0が先頭）
    public float offsetFromHeadM;      // 先頭車基準の配置オフセット[m]
    public string edgeId;              // 今いる線路のID
    public float distanceOnEdgeM;      // その線路のどこにいるか
    public Vector3 position;           // 3D空間の座標
    public Vector3 tangent;            // レールの向き（接線）
}
