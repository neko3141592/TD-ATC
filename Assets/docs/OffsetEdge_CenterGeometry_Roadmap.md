# Center Geometry + Offset Edge Roadmap

## 目的

中心線を `TrackGraph.edges` に入れず、`TrackGraph.geometries` の Native geometry としてだけ保持する。
走行可能な線路は `TrackEdge` として保持し、各 edge が中心線 geometry への参照、offset profile、距離 LUT を持つ。

この方式では、走行ネットワークは `TrackEdge` / `TrackNode` / `BlockSection` が担当し、座標計算は中心線 `TrackGeometry` と edge 側 offset data が担当する。

```text
TrackGraph
  geometries
    Center_001_Geo
      Native geometry
      origin
      horizontal / vertical / cant segments

  edges
    Up_001
      nodeAId / nodeBId
      baseGeometryId = Center_001_Geo
      offsetSegments
      offsetDistanceMap
      lengthM = offset line length

    Down_001
      nodeAId / nodeBId
      baseGeometryId = Center_001_Geo
      offsetSegments
      offsetDistanceMap
      lengthM = offset line length
```

## 走行ネットワークを保てる理由

保てる。

理由は、経路探索、閉塞、駅、信号、ATC、他列車探索が必要とする情報は、中心線ではなく走行可能 edge にあるため。

```text
TrackEdge:
  edgeId
  nodeAId
  nodeBId
  lengthM
  speedLimitMS
  blockSections
  offset data

TrackNode:
  nodeId
  connectedEdgeIds
  turnout / junction data

TrackGeometry:
  center line math only
```

中心線 geometry は接続を持たないため、経路探索には出てこない。
走行ネットワークに出るのは左右線、待避線、渡り線などの driveable edge だけになる。

そのため、次の機能は従来どおり edge graph で扱える。

- 列車の現在位置: `edgeId + distanceOnEdgeM`
- edge 終端での次 edge 解決
- node の接続 edge 一覧
- 分岐器の通常位 / 反位
- 閉塞区間の占有
- 駅停止位置
- ATC 前方探索
- 他列車探索
- メッシュ生成
- 走行音の継ぎ目判定

## 役割分担

### TrackGeometry

`TrackGeometry` は Native geometry 専用に寄せる。
中心線や完全に独立した基準線だけを表す。

```csharp
public class TrackGeometry
{
    public string geometryId;
    public float lengthM;
    public float gaugeM = 1.067f;

    public Vector3 originPosition;
    public Quaternion originRotation = Quaternion.identity;

    public List<TrackHorizontalSegment> horizontalSegments = new();
    public List<TrackVerticalSegment> verticalSegments = new();
    public List<TrackCantSegment> cantSegments = new();
}
```

`TrackGeometryKind.Offset` は廃止する。
`TrackGeometry` は Native geometry のみを表す。

### TrackEdge

`TrackEdge` は走行可能な線路を表す。
edge 自体が中心線 geometry からの offset 評価に必要な情報を持つ。

```csharp
public class TrackEdge
{
    public string edgeId;
    public string physicalId;
    public string nodeAId;
    public string nodeBId;

    public string baseGeometryId;
    public List<TrackOffsetSegment> offsetSegments = new();
    public TrackOffsetDistanceMap offsetDistanceMap;

    public List<BlockSection> blockSections = new();

    public float lengthM;
    public float speedLimitMS = 33.33f;
    public float gaugeM = 1.067f;
}
```

`edge.lengthM` は中心線距離ではなく、offset 後の走行距離。
列車、閉塞、駅、メッシュはこの距離を使う。

```text
baseGeometry.lengthM:
  中心線の距離

edge.lengthM:
  その edge 上を列車が走る実距離

edge.offsetDistanceMap:
  edge distance -> base geometry distance
```

### TrackOffsetDistanceMap

既存の `TrackOffsetDistanceMap` をそのまま使う。

```text
offsetDistanceM -> baseDistanceM
```

これは edge 上の走行距離から、中心線 geometry 上の距離を引くための LUT。
position / rotation / tangent は焼かない。

### TrackOffsetSegment

既存の `TrackOffsetSegment` をそのまま使う。

```text
Constant:
  複線の固定 offset

Linear:
  検証用または簡易線形

Cubic:
  待避線、渡り線、分岐線形
```

## 実行時評価

`TrackRuntimeResolver.TryResolvePose(graph, edgeId, distanceOnEdgeM, ...)` の評価手順を次にする。

```text
1. edgeId から TrackEdge を取得
2. edge.baseGeometryId から中心線 TrackGeometry を取得
3. distanceOnEdgeM を edge.lengthM で clamp
4. edge.offsetDistanceMap.SampleBaseDistance(distanceOnEdgeM)
5. edge.offsetSegments から baseDistanceM の offsetM を評価
6. base geometry を baseDistanceM で評価
7. base pose の right 方向へ offsetM 移動
8. offset 後の位置を finite difference で再評価して tangent / rotation を作る
```

固定 offset の複線でも、可変 offset の待避線でも同じ API で評価する。

```text
p0 = EvaluateOffsetEdgePosition(edge, distanceOnEdgeM - ds)
p1 = EvaluateOffsetEdgePosition(edge, distanceOnEdgeM + ds)
tangent = normalize(p1 - p0)
rotation = LookRotation(tangent, up)
```

`ds` は最初は `0.05m`。
端点では片側差分にする。

## コンパイル時評価

Editor compiler は入力 layout から次の順で `TrackGraph` を生成する。

```text
1. 中心線 Native geometry を作る
2. 各走行線の offsetSegments を作る
3. offsetDistanceMap を作る
4. offsetDistanceMap.OffsetLengthM を edge.lengthM に入れる
5. edge の 0m と lengthM を評価して node を作る
6. node.connectedEdgeIds を構築する
7. blockSections / stations / speedLimit / turnout を設定する
8. ValidateGraph を通す
```

中心線 geometry には node を作らない。
node は走行可能 edge の接続点だけに作る。

## 変更ロードマップ

### Phase 1: データ型を追加する

目的:

- 新設計のフィールドを追加し、既存 asset を壊さず移行できる状態にする。

作業:

1. `TrackGeometry` に `originPosition` と `originRotation` を追加する。
2. `TrackEdge` に `baseGeometryId` を追加する。
3. `TrackEdge` に `offsetSegments` と `offsetDistanceMap` を追加する。
4. `TrackEdge.geometryId` と `TrackGeometry.geometryKind` を削除する。
5. `TrackGraph.ValidateGraph()` に次の検証を追加する。
   - driveable edge は `nodeAId` / `nodeBId` を持つ。
   - offset edge は `baseGeometryId` を持つ。
   - `baseGeometryId` は `TrackGraph.geometries` に存在する。
   - `offsetDistanceMap` がある場合、`edge.lengthM` と `OffsetLengthM` が許容誤差内。

完了条件:

- 既存 course asset は新方式へ再生成する前提になる。
- 新フィールド追加後も compile が通る。

### Phase 2: Native geometry 評価を edge/node 依存から外す

目的:

- 中心線 geometry を edge なしで評価できるようにする。

作業:

1. `TrackRuntimeResolver` に `TryResolveNativeGeometryPose(graph, geometryId, distanceM, ...)` を追加する。
2. Native 評価の始点を `TrackNode` ではなく `TrackGeometry.originPosition` / `originRotation` に変える。
3. 既存 `TryResolveProfilePose(geometry, fromNode, ...)` は移行用 wrapper にする。
4. 中心線 geometry だけを手動生成して、任意距離の pose をログ確認する。

完了条件:

- `TrackGeometry` 単体で始点、終点、中間点を評価できる。
- 中心線 edge が存在しなくても Native geometry の評価が成功する。

### Phase 3: edge offset 評価 API を作る

目的:

- Offset geometry ではなく、edge 側 offset data から pose を解決する。

作業:

1. `TryResolvePose(graph, edgeId, distanceOnEdgeM, ...)` を edge offset 評価に変更する。
2. `edge.baseGeometryId` を解決する。
3. `edge.offsetDistanceMap.SampleBaseDistance(distanceOnEdgeM)` を使う。
4. `TrackOffsetUtility.EvaluateOffsetAtBaseDistance(edge.offsetSegments, baseDistanceM)` を使う。
5. base geometry pose から offset position を作る。
6. finite difference で tangent / rotation を作る。
7. `TryGetGradientPermille()` と `TryGetCantMm()` はまず base geometry の値を返す。

完了条件:

- `edge.geometryId` に Offset geometry がなくても、edge pose を評価できる。
- 固定 offset 複線でメッシュと列車位置が出る。

### Phase 4: LUT builder を baseGeometryId 対応に変える

目的:

- `baseEdgeId` を使わず、中心線 geometry を基準に LUT を生成する。

作業:

1. `TrackOffsetDistanceMapBuilder.Build()` の引数を `baseGeometryId` にする。
2. 基準長を `graph.FindGeometry(baseGeometryId).lengthM` から取る。
3. offset position 評価は `resolver` の Native geometry 評価を使う。
4. 旧 `baseEdgeId` overload は削除する。
5. 生成した `OffsetLengthM` を `edge.lengthM` に入れる。

完了条件:

- 中心線 edge なしで LUT を生成できる。
- 固定 offset で中心線長と edge 長が一致する。
- Cubic offset で edge 長が中心線長より少し長くなる。

### Phase 5: Editor compiler を切り替える

目的:

- 新規生成コースを CenterGeometry + OffsetEdge 方式にする。

作業:

1. `CreateCenterGeometry()` を作る。
2. `CreateOffsetEdge()` を作る。
3. `CreateOffsetEdge()` は次を行う。
   - `offsetSegments` 作成
   - `offsetDistanceMap` 作成
   - `edge.lengthM` 設定
   - edge 始点 / 終点 pose 評価
   - `TrackNode` 作成
   - `TrackEdge` 作成
4. `CreateLongDoubleTrackCourse()` を新方式へ移す。
5. 本線も `offset = -spacing * 0.5f` や `offset = 0f` の OffsetEdge として生成する。
6. 反対線は `offset = +spacing * 0.5f` で生成する。
7. 中心線 edge は生成しない。

完了条件:

- `TrackGraph.edges` に中心線 edge が存在しない。
- `TrackGraph.geometries` に中心線 Native geometry が存在する。
- 左右線 edge だけで走行、描画、ValidateGraph が通る。

### Phase 6: Graph 系処理の確認

目的:

- 走行ネットワークが edge graph として維持されていることを確認する。

作業:

1. `TrackRouteTracer` が左右線 edge を辿れることを確認する。
2. `BlockOccupancyManager` が `edge.lengthM` 基準で占有できることを確認する。
3. `NextStationResolver` が station の `edgeId + distanceFromEdgeStart` を解決できることを確認する。
4. `TrackGraphUndirectedHelpers` が中心線 geometry を見ないことを確認する。
5. `TrackVisualizer` が `graph.edges` だけを描画することを確認する。

完了条件:

- 中心線 geometry が network に混入しない。
- 列車は左右線 edge だけを走る。
- 閉塞と駅距離が edge 実距離で動く。

### Phase 7: 旧 OffsetGeometry を削除する

目的:

- データモデルを一本化し、旧走行経路を残さない。

作業:

1. `TrackGeometryKind.Offset` を削除する。
2. `TrackGeometry.baseEdgeId` / `offsetSegments` / `offsetDistanceMap` を削除する。
3. resolver の旧 `edge.geometryId` / `OffsetGeometry` 分岐を削除する。
4. 旧方式 asset は Editor compiler で再生成する。

完了条件:

- Offset 情報は `TrackEdge` 側だけに存在する。
- `TrackGeometry` は Native geometry として単純化されている。

## 検証項目

### 直線複線

```text
Center length = 1000m
Up offset = -1.9m
Down offset = +1.9m
```

期待:

- Up / Down の `edge.lengthM` はほぼ `1000m`。
- 始点と終点 node は中心線から左右に正しくずれる。
- 列車が edge 終端まで走る。

### 曲線複線

```text
Center: curve radius 600m
Up offset = -1.9m
Down offset = +1.9m
```

期待:

- 内側線と外側線で `edge.lengthM` が変わる。
- `offsetDistanceMap` により終端位置が中心線の終端に対応する。
- メッシュの継ぎ目が大きくずれない。

### 待避線

```text
offset 0m -> 4m cubic
parallel 4m
offset 4m -> 0m cubic
```

期待:

- tangent が base tangent のままにならない。
- 開き始めと戻り終わりで姿勢が自然につながる。
- edge length が中心線距離より長くなる。

### 渡り線

```text
left main offset -1.9m
right main offset +1.9m
crossover offset -1.9m -> +1.9m cubic
```

期待:

- node 接続で route tracer が渡り線を選べる。
- `TurnoutConnection` が edge 単位で設定できる。
- 中心線 geometry は turnout 判定に出ない。

## 注意点

### edge.lengthM と baseGeometry.lengthM を混同しない

`edge.lengthM` は列車が走る距離。
`baseGeometry.lengthM` は中心線上の距離。

固定 offset の直線では同じになりやすいが、曲線や可変 offset では一致しない。

### node は edge の接続点

中心線 geometry の始点終点に node を作らない。
node は走行可能 edge 同士の接続点だけに作る。

### station / block は edge distance

駅停止位置、閉塞境界、速度制限境界は `edge.lengthM` 上の距離で持つ。
中心線距離で入力したい場合は、compiler が edge distance へ変換する。

### vertical / cant の扱い

最初は中心線 geometry の勾配・カントを使う。
将来、edge 固有の勾配・カントが必要になったら `TrackEdge` に次を追加する。

```csharp
public List<TrackVerticalSegment> verticalSegments = new();
public List<TrackCantSegment> cantSegments = new();
```

この場合も距離基準は edge distance にする。

## 最小実装順

最小で動かすなら、この順番にする。

```text
1. TrackGeometry に origin を追加
2. TrackEdge に baseGeometryId / offsetSegments / offsetDistanceMap を追加
3. Native geometry 単体評価を追加
4. edge offset pose 評価を追加
5. LUT builder を baseGeometryId 対応にする
6. 直線複線 generator を新方式にする
7. 曲線複線 generator を新方式にする
8. Cubic offset の待避線を作る
9. 旧 OffsetGeometry を削除する
```
