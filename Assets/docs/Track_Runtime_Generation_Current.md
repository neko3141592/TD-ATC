# Track Runtime / Graph / Geometry Current Design

## 目的

このドキュメントは、現在の線路走行ロジックと、`TrackGraph` / `TrackGeometry` の生成ロジックを説明する。

現在の実装は、旧 `edge.geometryId` 方式や `OffsetGeometry` 方式を使わない。
走行可能な線路はすべて `TrackEdge` として持ち、各 edge が中心線 `TrackGeometry` を `baseGeometryId` で参照する。

```text
TrackGeometry:
  中心線、または基準線の Native geometry

TrackEdge:
  走行可能な線路
  baseGeometryId + offsetSegments + offsetDistanceMap を持つ

TrackRuntimeResolver:
  edgeId + edge distance から pose を解決する
```

## データモデル

### TrackGeometry

`TrackGeometry` は基準線の数学情報だけを持つ。
走行ネットワークには参加しない。

```csharp
public class TrackGeometry
{
    public string geometryId;
    public float lengthM;
    public float gaugeM;

    public Vector3 originPosition;
    public Quaternion originRotation;

    public List<TrackHorizontalSegment> horizontalSegments;
    public List<TrackVerticalSegment> verticalSegments;
    public List<TrackCantSegment> cantSegments;
}
```

役割:

- 中心線の始点姿勢を持つ。
- 直線、曲線、緩和曲線の平面線形を持つ。
- 勾配とカントを持つ。
- `TrackNode` とは直接つながらない。

### TrackEdge

`TrackEdge` は走行可能な線路とグラフ接続を表す。

```csharp
public class TrackEdge
{
    public string edgeId;
    public string physicalId;
    public string nodeAId;
    public string nodeBId;

    public List<BlockSection> blockSections;

    public float lengthM;
    public float speedLimitMS;
    public float gaugeM;

    public string baseGeometryId;
    public List<TrackOffsetSegment> offsetSegments;
    public TrackOffsetDistanceMap offsetDistanceMap;
}
```

役割:

- 経路探索、閉塞、駅、信号、ATC の単位になる。
- `nodeAId` / `nodeBId` で走行ネットワークに参加する。
- `baseGeometryId` で基準線を参照する。
- `offsetSegments` で基準線からの横 offset を定義する。
- `offsetDistanceMap` で edge 距離から基準線距離へ変換する。

`edge.lengthM` は中心線距離ではなく、offset 後の走行距離。

```text
baseGeometry.lengthM:
  基準線上の距離

edge.lengthM:
  列車がその edge 上で走る距離

edge.offsetDistanceMap:
  edge distance -> base geometry distance
```

### TrackOffsetSegment

`TrackOffsetSegment` は基準線距離に対する横 offset を定義する。

```text
Constant:
  複線などの固定 offset

Linear:
  簡易的な offset 変化

Cubic:
  待避線、渡り線、分岐線などの滑らかな offset 変化
```

`TrackOffsetUtility.EvaluateOffsetAtBaseDistance()` が、指定した基準線距離の offset を評価する。

### TrackOffsetDistanceMap

`TrackOffsetDistanceMap` は LUT。

```text
edge distance -> base geometry distance
```

保持するのは `baseDistanceByOffsetIndex` だけ。
position / rotation / tangent は保存しない。

`OffsetLengthM` は LUT のサンプル数と `sampleIntervalM` から計算される offset edge の全長。

## 走行時の pose 解決

入口は `TrackRuntimeResolver.TryResolvePose()`。

```text
TryResolvePose(graph, edgeId, distanceOnEdgeM)
```

処理手順:

```text
1. graph.FindEdge(edgeId)
2. edge.baseGeometryId があることを確認
3. distanceOnEdgeM を edge.lengthM に clamp
4. edge.offsetDistanceMap.SampleBaseDistance(distanceOnEdgeM)
5. edge.offsetSegments から offsetM を評価
6. baseGeometryId の TrackGeometry を baseDistanceM で評価
7. base pose の right 方向へ offsetM 移動
8. 前後位置を再評価して tangent / rotation を作る
```

### 基準線 Geometry の評価

`TryResolveGeometryPose()` は `TrackGeometry` 単体を評価する。

```text
geometry.originPosition
geometry.originRotation
  -> horizontalSegments
  -> verticalSegments
  -> cantSegments
  -> position / tangent / rotation
```

平面線形:

- `Straight`
- `Curve`
- `TransitionIn`
- `TransitionOut`

勾配:

- `TrackGradientUtility.GetVerticalHeightAt()`
- `TrackGradientUtility.GetGradientPermilleAt()`

カント:

- `TrackGradientUtility.GetCantMmAt()`

### OffsetEdge の位置評価

`TryResolveOffsetEdgePosition()` は、edge 上距離から offset 後の位置を出す。

```text
distanceOnEdgeM
  -> offsetDistanceMap.SampleBaseDistance()
  -> TrackOffsetUtility.EvaluateOffsetAtBaseDistance()
  -> TryResolveGeometryPose(baseGeometryId, baseDistanceM)
  -> basePosition + baseRotation * Vector3.right * offsetM
```

### tangent / rotation

Offset が変化する edge では、基準線 tangent をそのまま使わない。
offset 後の位置を前後差分して tangent を出す。

```text
sample0 = distance - 0.05m
sample1 = distance + 0.05m
p0 = offset edge position at sample0
p1 = offset edge position at sample1
tangent = normalize(p1 - p0)
rotation = LookRotation(tangent, base up)
```

端点では clamp された片側差分になる。

### 勾配とカント

現在は edge 固有の勾配・カントを持たない。
`TryGetGradientPermille()` と `TryGetCantMm()` は、edge distance を base distance に変換し、基準線 geometry の値を返す。

```text
edge distance
  -> base distance
  -> baseGeometry.verticalSegments / cantSegments
```

## Graph の役割

`TrackGraph` は走行ネットワークを持つ。

```text
nodes:
  edge 接続点

edges:
  走行可能な線路

geometries:
  中心線 / 基準線

turnoutConnections:
  分岐接続

stations:
  edgeId + distanceFromEdgeStart
```

中心線 geometry は node を持たない。
経路探索に出てくるのは `TrackEdge` だけ。

### 経路探索

`TrackRouteTracer` は `TrackEdge.lengthM` と `TrackNode.connectedEdgeIds` を使う。

つまり、経路探索は geometry を直接見ない。

```text
current edge
  -> exit node
  -> connected edge
  -> next edge
```

### 駅

`StationData` は走行 edge 上の距離で持つ。

```text
station.edgeId
station.distanceFromEdgeStart
```

中心線距離で駅を入力したい場合は、生成側で edge distance へ変換する必要がある。

## ValidateGraph

現在の `ValidateGraph()` は全 edge を OffsetEdge として検証する。
旧 edge 形式は通さない。

検証する内容:

- node ID の重複がない。
- edge ID の重複がない。
- edge が `nodeAId` / `nodeBId` を持つ。
- node 側の `connectedEdgeIds` に edge が入っている。
- edge が `baseGeometryId` を持つ。
- `baseGeometryId` が `TrackGraph.geometries` に存在する。
- edge が `offsetSegments` を持つ。
- edge が `offsetDistanceMap` を持つ。
- `edge.lengthM` と `offsetDistanceMap.OffsetLengthM` が許容誤差内。
- junction / turnout connection の参照が正しい。

旧 `edge.geometryId` や edge 内 `horizontalSegments` は検証対象ではない。

## 生成ロジック

### Editor 生成

`TrackGraphEditor` の Inspector ボタン:

```text
Create Center Geometry Double Straight Course
```

生成されるもの:

```text
geometries:
  Center_001_Geo

edges:
  Up_001
    baseGeometryId = Center_001_Geo
    offset = -1.9m

  Down_001
    baseGeometryId = Center_001_Geo
    offset = +1.9m
```

中心線 edge は生成しない。

生成手順:

```text
1. graph.nodes / edges / geometries / stations / turnout data を clear
2. Center_001_Geo を Native geometry として作る
3. Up_001 用 offsetSegments を作る
4. Up_001 用 offsetDistanceMap を作る
5. offsetDistanceMap.OffsetLengthM を Up_001.lengthM に入れる
6. Up_001 の 0m / lengthM pose を resolver で評価して node を作る
7. Down_001 も同じ手順で作る
8. StationData を Up_001 上に作る
9. UpdateNodeTypesAndJunctionIds()
10. SyncTurnoutStates()
```

### TrackBuilder 生成

`TrackBuilder.ConnectToNode()` も新方式で edge を作る。

流れ:

```text
1. 現在までに積まれた horizontal / vertical / cant segments から base geometry を作る
2. offset = 0m の TrackOffsetSegment を作る
3. TrackOffsetDistanceMapBuilder.Build() で LUT を作る
4. edge.lengthM に LUT の OffsetLengthM を入れる
5. TrackEdge に baseGeometryId / offsetSegments / offsetDistanceMap を入れる
6. node の connectedEdgeIds を更新する
```

この builder は、単線でも「基準 geometry + offset 0 の走行 edge」という形にする。

### LUT 生成

`TrackOffsetDistanceMapBuilder.Build()` は `baseGeometryId` を受け取る。

```text
baseGeometryId
offsetSegments
sampleIntervalM
integrationStepM
```

積分手順:

```text
baseDistance = 0
previousOffsetPosition = EvaluateOffsetPosition(baseDistance)

while baseDistance < baseGeometry.lengthM:
  nextBaseDistance = baseDistance + integrationStepM
  nextOffsetPosition = EvaluateOffsetPosition(nextBaseDistance)
  accumulatedOffsetDistance += distance(previous, next)

  accumulatedOffsetDistance が sampleInterval を超えるたびに
    offset distance sample -> base distance を LUT に追加
```

このため、固定 offset の直線では `edge.lengthM` はほぼ中心線長と一致する。
曲線や可変 offset では、内外線や開き線形に応じて `edge.lengthM` が変わる。

## メッシュ / 枕木 / 架線

`TrackVisualizer` と `TrackMeshGenerator` は `TrackRuntimeResolver.TryResolvePose()` を使う。

つまり、描画側は center geometry や LUT を直接知らなくてよい。

```text
foreach edge in graph.edges:
  resolver.TryResolvePose(graph, edge.edgeId, distance)
  -> mesh / sleeper / catenary position
```

描画対象は `graph.edges`。
中心線 geometry は描画されない。

## 現在の制約

- 既存の旧方式 asset は新方式で再生成が必要。
- edge 固有の勾配・カントはまだない。
- 駅や閉塞は edge distance で指定する必要がある。
- 中心線距離で入力する UI / layout は、生成時に edge distance へ変換する必要がある。
- Editor 生成は現在、直線複線の最小コースだけ。
- 曲線複線、待避線、渡り線は同じ仕組みで拡張する。

## 次に拡張する場所

曲線複線:

```text
Center geometry に Curve / Transition segment を入れる
左右 edge は Constant offset
LUT により内外線の lengthM が変わる
```

待避線:

```text
offsetSegments:
  Cubic 0m -> 4m
  Constant 4m
  Cubic 4m -> 0m
```

渡り線:

```text
offsetSegments:
  Cubic -1.9m -> +1.9m
```

分岐:

```text
TrackNode.connectedEdgeIds
TurnoutConnection
TurnoutState
```

この場合も走行 pose は resolver が edge 単位で解決する。

