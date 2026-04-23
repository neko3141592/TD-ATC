using System;
using UnityEngine;
using System.Collections.Generic;

// ServiceType は列車サービスの運転種別を表します。
public enum ServiceType
{
    Local,
    SemiExpress,
    Express,
    RapidExpress,
    Depot,
    TestRun,
}

[Serializable]
public class ServiceStop
{
    // stationId は使用中の TrackGraph にある StationData.stationId と一致している必要があります。
    public string stationId;
    // stopHere により、ある列車は通過し、別の列車は同じ駅に停車する設定を作れます。
    public bool stopHere = true;
    // dwellSeconds は将来のドア扱いや停車時分用の予約値で、現時点では実行時ロジックには使っていません。
    public float dwellSeconds = 30f;
}

[CreateAssetMenu(fileName = "TrainServiceDefinition", menuName = "Train/Service Definition")]
public class TrainServiceDefinition : ScriptableObject
{
    // serviceName はエディタ設定や将来の HUD / セッション画面で使う人間向けの表示名です。
    public string serviceName;
    public ServiceType serviceType;
    // stops は進行方向順に並び、この列車の停車・通過順序の正本として扱います。
    public List<ServiceStop> stops = new List<ServiceStop>();
}
