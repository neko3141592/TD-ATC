# Track Geometry / Graph / Terrain Roadmap

## 目的

このドキュメントは、線路の座標計算、複線、待避線、渡り線、閉塞、信号、ATC、地形、走行音を同じ基盤で扱うための実装ロードマップです。

2026-06 時点の方針は、**待避線・渡り線も含めて 3 次放物線を基本線形として使う** ことです。R > 250m の範囲では 3 次放物線で実用上の誤差が小さい前提にし、当面はクロソイド専用実装を入れません。

方針:

```text
編集:
  BVE 的な距離程 + 相対オフセットで作る

生成:
  走行可能な線路は TrackGraph + TrackGeometry に変換する

走行:
  TrainController は edgeId + distanceOnEdgeM で走る

座標:
  TrackGeometry を数式評価する

曲線:
  直線 / 円曲線 / 3 次放物線を使う
  待避線・渡り線も 3 次放物線で横へ開く

オフセット線:
  offsetDist -> baseDist の距離 LUT だけ焼く
  position / rotation / tangent は保存しない

安全:
  閉塞、信号、分岐、連動、ATC、他列車は TrackGraph を使う
```

重要なのは、**座標を大量に焼かない** ことです。焼くのは、基準線とオフセット線の距離ずれを解決するための `offsetDistanceM -> baseDistanceM` の LUT だけです。

## 最終アーキテクチャ

### 全体像

```text
RelativeTrackLayout
  距離程イベント
  基準線
  複線オフセット
  待避線
  渡り線
  駅、速度制限、信号の元データ

  -> Editor Compiler

TrackGraph
  nodes
  edges
  turnout connections
  block sections
  signals
  interlocking routes

TrackGeometry
  NativeGeometry
  OffsetGeometry

TrackRuntimeResolver
  edgeId + distanceOnEdgeM
    -> geometry
    -> analytic pose

Visualizer / Terrain / Audio
  geometry を必要間隔でサンプリングして生成
```

### TrackGraph の役割

`TrackGraph` は runtime の接続と安全装置を担当します。

```text
TrackGraph:
  走行可能な接続
  分岐
  閉塞
  信号
  進路
  他列車探索
  ATC 前方探索
```

走行可能な線路は必ず `TrackEdge` にします。

- 本線
- 複線の上下線
- 待避線
- 渡り線
- 折返し線
- 車庫線
- 対向列車が走る線

見た目だけの遠景線路や留置線は、`visualOnly` として `TrackGraph` に入れなくてもよいです。

### TrackGeometry の役割

`TrackGeometry` は座標評価だけを担当します。

```csharp
public sealed class TrackGeometry
{
    public string geometryId;
    public TrackGeometryKind kind;
    public float lengthM;
    public float gaugeM;
}
```

種類は 2 つに分けます。

```text
NativeGeometry:
  horizontalSegments
  verticalSegments
  cantSegments
  を直接持つ

OffsetGeometry:
  baseGeometryId
  offsetDistanceM -> baseDistanceM LUT
  lateral / vertical offset profile
  optional own vertical / cant profile
```

### NativeGeometry

基準線や完全に独立した線路に使います。

```csharp
public sealed class NativeTrackGeometry
{
    public string geometryId;
    public float lengthM;
    public float gaugeM;
    public List<TrackHorizontalSegment> horizontalSegments;
    public List<TrackVerticalSegment> verticalSegments;
    public List<TrackCantSegment> cantSegments;
}
```

評価は数式で行います。

```text
distanceM
  -> horizontal segments: 直線 / 円曲線 / 3 次放物線
  -> vertical segments: 勾配 / 縦曲線
  -> cant segments: カント
  -> TrackPose
```

### OffsetGeometry

複線、待避線、渡り線に使います。

```csharp
public sealed class OffsetTrackGeometry
{
    public string geometryId;
    public string baseGeometryId;
    public float lengthM;
    public float distanceMapIntervalM;
    public float[] baseDistanceByOffsetIndex;
    public List<TrackOffsetSegment> offsetSegments;
    public List<TrackVerticalSegment> verticalSegments;
    public List<TrackCantSegment> cantSegments;
}
```

実行時は以下の流れです。

```text
offsetDistanceM
  -> LUT で baseDistanceM に変換
  -> base geometry を数式評価
  -> offset profile を評価
  -> base pose の right / up 方向に offset
  -> finite difference で tangent を出す
```

position / rotation / tangent は配列保存しません。

## 3 次放物線方針

### 基本式

待避線や渡り線で、距離 `L` の間に横オフセットを `D` だけ変化させる場合、次の 3 次式を使います。

```text
t = s / L
u = 3t^2 - 2t^3
offset(s) = startOffset + (endOffset - startOffset) * u
```

コード:

```csharp
float SmoothStep3(float t)
{
    t = Mathf.Clamp01(t);
    return t * t * (3f - 2f * t);
}

float EvaluateCubicOffset(float s, float lengthM, float startOffsetM, float endOffsetM)
{
    float t = Mathf.Clamp01(s / Mathf.Max(0.001f, lengthM));
    return Mathf.Lerp(startOffsetM, endOffsetM, SmoothStep3(t));
}
```

特徴:

```text
s = 0:
  offset = startOffset
  offset 変化率 = 0

s = L:
  offset = endOffset
  offset 変化率 = 0
```

つまり、始点と終点で本線または平行線に自然につながります。

### 適用範囲

当面の前提:

```text
R > 250m:
  3 次放物線で扱う

R <= 250m:
  生成時に警告する
  必要になったら設計を再検討する
```

今はクロソイド専用セグメントを増やさず、3 次放物線を標準の緩和・分岐線形として使います。

### 待避線

待避線入口:

```text
本線 offset 0m
  -> 3 次放物線で offset +4m
  -> 側線平行区間
```

待避線出口:

```text
側線 offset +4m
  -> 3 次放物線で offset 0m
  -> 本線
```

### 渡り線

片渡り線:

```text
片側本線 offset 0m
  -> 3 次放物線で offset +線間距離
  -> 反対側本線
```

必要なら中央に短い直線区間を入れます。

```text
0m 側
  -> 3 次放物線
  -> short straight
  -> 3 次放物線
  -> 反対側
```

最初は厳密な分岐器番数を再現しません。まずは、自然に走れて、Graph 接続・閉塞・ATC が扱える渡り線を優先します。

### tangent / rotation

可変オフセットでは、base tangent をそのまま使いません。数式で position を評価し、有限差分で接線を出します。

```text
p0 = EvaluateOffsetPosition(offsetDistanceM - ds)
p1 = EvaluateOffsetPosition(offsetDistanceM + ds)
tangent = normalize(p1 - p0)
```

`ds` は最初は `0.05m` でよいです。

## 距離 LUT 方針

### 何を焼くか

焼くのはこれだけです。

```text
offsetDistanceM -> baseDistanceM
```

例:

```text
offset線距離 150.123m
  -> base線距離 148.956m
```

実装イメージ:

```csharp
public sealed class OffsetDistanceMap
{
    public float sampleIntervalM = 0.05f;
    public float[] baseDistanceByOffsetIndex;
}
```

サンプル取得:

```text
index = offsetDistanceM / sampleIntervalM
i0 = floor(index)
i1 = i0 + 1
t = index - i0
baseDistanceM = lerp(table[i0], table[i1], t)
```

### 何を焼かないか

以下は焼きません。

- `Vector3 position`
- `Quaternion rotation`
- `Vector3 tangent`
- 全距離のメッシュ座標
- 全距離のカント姿勢

理由:

- メモリが増える
- 数式線形の意味が失われる
- 修正時に再生成範囲が増える
- 補間誤差と姿勢ズレの原因になる

### サンプリング精度

10km 路線を想定するなら、距離 LUT は以下が現実的です。

```text
通常区間:
  0.10m

待避線・渡り線・分岐器:
  0.05m

精度検証用:
  0.01m も可能だが通常は不要
```

最初の実装では一律 `0.10m` でよいです。待避線・渡り線を入れる段階で `0.05m` にします。

## 勾配・カント

### 勾配

最初は base geometry の勾配を使ってよいです。

その後、offset 線自身の実延長に合わせて `verticalSegments` を持たせます。

```text
固定複線:
  始点/終点標高を共有
  offset 線自身の lengthM で勾配を再計算

待避線・渡り線:
  接続先 node の標高へ自然につなぐ
  必要なら縦曲線を入れる
```

### カント

最初は base geometry のカントを使います。

ただし、待避線・渡り線では本線と同じカントが不自然な場合があるため、最終的には `OffsetGeometry` に独自 `cantSegments` を持たせます。

```text
本線複線:
  base cant をコピーまたは線ごとに設定

待避線:
  基本 0mm または低カント

渡り線:
  分岐器・リード曲線は原則小さめまたは 0mm
```

## 実装ロードマップ

### Phase 0: 現状固定

目的:

- 既存走行を壊さず、変更前の基準を作る。

作業:

1. 現在の 2km 複線コースで走行できることを確認する。
2. `TrackGraph.ValidateGraph()` の結果を確認する。
3. `TrackRuntimeResolver` の既存評価結果を、直線・円曲線・勾配でログ確認する。
4. 既存 `TrackEdge.horizontalSegments` 方式はまだ消さない。

完了条件:

- 既存シーンが走る。
- 既存コース生成が動く。
- 変更前の validation 結果を把握している。

### Phase 1: TrackGeometryView を入れる

目的:

- データ構造を変えずに、resolver の評価入口だけ整理する。

作業:

1. `TrackGeometryView` を追加する。
2. `TrackRuntimeResolver` に `GetGeometryView(edge)` を追加する。
3. `TryResolvePose()` が `TrackGeometryView` を評価するようにする。
4. `TryGetGradientPermille()` と `TryGetCantMm()` も view 経由にする。
5. `TrackEdge` のフィールドはまだ変えない。

完了条件:

- 既存 asset の移行なしでビルドが通る。
- 走行位置、勾配、カント、メッシュが変わらない。

### Phase 2: TrackGeometry 型を追加する

目的:

- `TrackEdge` から線形データを分離する入口を作る。

作業:

1. `TrackGeometry` / `NativeTrackGeometry` を追加する。
2. `TrackGraph` に `geometries` を追加する。
3. `TrackEdge` に `geometryId` を追加する。
4. `geometryId` がある場合は geometry を使う。
5. `geometryId` が空の場合は旧 edge 内 segments を使う。

注意:

- この段階では旧互換フォールバックを残す。
- フォールバック削除は主要 asset 移行後に行う。

完了条件:

- 新規生成した edge が `geometryId` で走れる。
- 既存 edge もまだ走れる。

### Phase 3: Geometry 評価を共通 API にする

目的:

- 走行、メッシュ、地形、台車、音が同じ pose 評価を使うようにする。

作業:

1. `TrackPose` を定義する。
2. `EvaluateNativePose(geometry, distanceM)` を作る。
3. `TrackRuntimeResolver.TryResolvePose()` は edge から geometry を解決し、共通 API を呼ぶ。
4. `CalculateStraight` / `CalculateCircularCurve` の意味名を整理する。
5. `CalculateCubicParabola` または `EvaluateCubicOffset` を追加する。
6. 終端評価と nodeB の位置差を validation で確認する。

完了条件:

- resolver の中心が `geometry + distance -> TrackPose` になる。
- mesh / visualizer / train は resolver 経由のまま動く。

### Phase 4: 3 次放物線セグメントを追加する

目的:

- 待避線・渡り線用の基本線形を追加する。

作業:

1. `TrackCurveType` に `CubicParabola` を追加する。
2. `TrackHorizontalSegment` に必要なパラメータを追加する。
   - `startOffsetM`
   - `endOffsetM`
   - または専用 `TrackOffsetSegment` を作る
3. 最初は `TrackOffsetSegment` として実装する。
4. `EvaluateCubicOffset()` を作る。
5. `EvaluateOffsetPosition()` から使う。

完了条件:

- offset 0m -> 4m の開き線形を数式評価できる。
- 始点/終点で平行に接続できる。

### Phase 5: OffsetDistanceMap を追加する

目的:

- オフセット線の実距離と基準線距離のズレを扱う。

作業:

1. `OffsetDistanceMap` 型を追加する。
2. `SampleBaseDistance(offsetDistanceM)` を実装する。
3. LUT は float[] で持つ。
4. 最初は `0.10m` 間隔で生成する。
5. 範囲外 distance は clamp する。
6. LUT 生成時に 3 次 offset profile を使って実弧長を積算する。

完了条件:

- `offsetDistanceM -> baseDistanceM` を高速に取得できる。
- Lerp 補間で滑らかに base 距離を得られる。

### Phase 6: 固定オフセット OffsetGeometry

目的:

- 複線を `baseGeometry + fixed offset + distance LUT` で表現する。

作業:

1. `OffsetTrackGeometry` を追加する。
2. 固定 `lateralOffsetM` / `verticalOffsetM` を持たせる。
3. base geometry を一定間隔で進め、offset 線の弧長を積算して LUT を作る。
4. `EvaluateOffsetPose()` を実装する。
5. 固定オフセットでは tangent は base tangent を使ってよい。
6. position は毎回数式評価する。

完了条件:

- 複線直線が OffsetGeometry で走れる。
- 複線円曲線で内外線の `lengthM` が変わる。
- position/rotation 配列を持たずに走れる。

### Phase 7: 複線の勾配・カント

目的:

- 内外線で実延長が変わっても、標高と姿勢を破綻させない。

作業:

1. 基準線の始点/終点標高を取る。
2. OffsetGeometry の `lengthM` に合わせて `verticalSegments` を作る。
3. 最初は単純勾配でよい。
4. カントは base をコピーする。
5. 必要なら複線ごとの cant override を入れる。

完了条件:

- 複線カーブで両線の終端標高が一致する。
- 内外線で勾配‰がわずかに変わる。
- `TrainController.CurrentGradientPermille` が走行線ごとに変わる。

### Phase 8: 可変オフセット評価

目的:

- 待避線や渡り線のように offset が変わる線で、姿勢を自然にする。

作業:

1. `TrackOffsetSegment` を追加する。
2. offset profile を距離で評価できるようにする。
3. `EvaluateOffsetPosition(offsetDistanceM)` を分離する。
4. `EvaluateOffsetPose()` では finite difference で tangent を出す。
5. `ds = 0.05m` から始める。
6. LUT 生成時も offset profile を使って実弧長を積算する。

完了条件:

- offset が変わる線でも向きが本線 tangent のままにならない。
- 線路が開く/戻る区間で車両姿勢が自然になる。

### Phase 9: 待避線の最小生成

目的:

- 走行可能な待避線を、3 次放物線付き線形として生成する。

作業:

1. 本線上に分岐開始 node を置く。
2. 待避線入口を `OffsetGeometry` で作る。
3. 線形は `offset 0m -> 側線 offset` の 3 次放物線にする。
4. 平行待避区間は固定 offset として作る。
5. 待避線出口は `側線 offset -> offset 0m` の 3 次放物線にする。
6. 入口/出口を `TrackGraph` の node/edge に接続する。
7. turnout connection を設定する。
8. R <= 250m 相当になる設定なら警告する。

完了条件:

- 本線から待避線へ分岐して走れる。
- 待避線から本線へ戻れる。
- 後退時にも接続が辿れる。
- validation が通る。

### Phase 10: 渡り線の最小生成

目的:

- 複線間を 3 次放物線付き渡り線で接続する。

作業:

1. 上下線の接続位置を決める。
2. 片渡り線を `OffsetGeometry` で作る。
3. offset 0m -> 線間距離 の 3 次放物線を基本にする。
4. 必要に応じて中央に短い直線区間を入れる。
5. 両端を上下線の node に接続する。
6. turnout connection を両端に設定する。
7. R <= 250m 相当になる設定なら警告する。

完了条件:

- 上り線から下り線へ渡れる。
- 下り線から上り線へ渡れる。
- 走行方向・後退方向のどちらでも graph が破綻しない。

### Phase 11: RelativeTrackLayout

目的:

- BVE 的に距離程 + 相対軌道で路線を編集できるようにする。

作業:

1. `RelativeTrackLayout` ScriptableObject を追加する。
2. 基準線イベントを持つ。
3. 複線 offset 定義を持つ。
4. 待避線/渡り線定義を持つ。
5. 3 次放物線の開始距離、長さ、開始 offset、終了 offset を定義できるようにする。
6. `visualOnly` と `generateRuntimeEdge` を分ける。
7. Editor compiler で TrackGraph + TrackGeometry に変換する。

完了条件:

- 複線を少ない入力で生成できる。
- 待避線・渡り線を 3 次放物線で生成できる。
- visual-only 線路を graph に入れずに描画できる。
- 走行可能な線路だけ graph edge になる。

### Phase 12: 閉塞

目的:

- ATC と信号が先行列車に反応できるようにする。

作業:

1. 最初は 1 edge = 1 block で自動生成する。
2. `TrackBlockSection` を edge 内距離で持つ。
3. `BlockOccupancyManager` を作る。
4. 最初は先頭車だけで在線判定する。
5. 次に `CarTrackState` 全体で複数閉塞占有にする。
6. `TrackRouteTracer` で前方閉塞を探索する。

完了条件:

- 同一進路に 2 列車を置くと後続が在線閉塞を検出する。
- 分岐状態が変わると探索先も変わる。

### Phase 13: 信号・連動・ATC

目的:

- 信号と ATC を graph / block / turnout に接続する。

作業:

1. `TrackSignal` を追加する。
2. `InterlockingRoute` を追加する。
3. ポイント状態が進路と一致しているか判定する。
4. 競合進路を開通させない。
5. 前方閉塞在線なら信号を停止にする。
6. ATC は閉塞終端または防護信号を停止目標にする。
7. 速度制限と閉塞停止目標のうち厳しい方を採用する。

完了条件:

- ポイントが違う方向なら信号が開かない。
- 先行列車がいると信号/ATC が落ちる。
- 渡り線・待避線経由でも前方探索が破綻しない。

### Phase 14: メッシュ・地形

目的:

- geometry を使って線路、路盤、地形、架線柱を生成する。

作業:

1. レールメッシュは `0.25m - 0.5m` 程度でサンプリングする。
2. 分岐器・渡り線は `0.05m - 0.10m` 程度にする。
3. バラスト/路盤は `1m` 程度でよい。
4. 地形は `2m - 5m` 程度でよい。
5. 100m - 250m 程度の chunk に分割する。
6. 架線柱、信号、標識、駅停止位置は geometry pose から配置する。

完了条件:

- 10km 路線でも chunk 単位で生成・表示できる。
- 通常線路と分岐器でサンプリング密度を変えられる。
- 地形を消しても走行は壊れない。

### Phase 15: 台車・車軸・音

目的:

- 車両中心ではなく台車/車軸位置で音と姿勢を扱う。

作業:

1. `CarTrackState` から車両中心位置を取る。
2. 台車オフセットを edge 経路上で前後に trace する。
3. 車軸位置も同様に trace する。
4. 継ぎ目データを `TrackJointData` として持つ。
5. 車軸が joint を跨いだら `PlayOneShot` する。
6. 台車ごとの AudioSource を使う。

完了条件:

- 編成内の台車ごとにジョイント音タイミングがずれる。
- 速度に応じて間隔が自然に変わる。
- 分岐器通過音やポイント音へ拡張できる。

### Phase 16: 他列車・ダイヤ

目的:

- Train Drive ATS 的に他列車が閉塞・信号・ATCへ影響する状態にする。

作業:

1. `TrainSchedule` を追加する。
2. AI 列車は固定 route を走る。
3. 駅で時刻まで待つ。
4. AI 列車も `BlockOccupancyManager` に登録する。
5. 遅れによって閉塞が詰まることを確認する。
6. プレイヤー列車の遅れが後続列車に影響する入口を作る。

完了条件:

- 先行 AI 列車に追いつくと信号/ATC が落ちる。
- AI 列車が進むと閉塞が順に開く。
- 待避・追い越しシナリオを作れる。

## 最初に実装する順番

今すぐ実装に入るなら、この順番にします。

1. `TrackGeometryView`
2. `TrackGeometry` / `NativeTrackGeometry`
3. `TrackRuntimeResolver` の geometry 評価化
4. 3 次放物線 offset 評価
5. `OffsetDistanceMap`
6. 固定オフセット `OffsetGeometry`
7. 複線の勾配再計算
8. 可変オフセット評価
9. 待避線生成
10. 渡り線生成
11. `RelativeTrackLayout`
12. 閉塞
13. 信号・連動・ATC
14. メッシュ・地形
15. 台車・車軸・音
16. 他列車・ダイヤ

## 判断基準

迷った場合は以下を優先します。

1. 走行可能な線路は graph edge にする。
2. 座標は数式で評価する。
3. 距離のズレだけ LUT で解決する。
4. position / rotation / tangent は大量保存しない。
5. 待避線・渡り線は 3 次放物線で横へ開く。
6. R <= 250m 相当の線形は警告し、今は無理に対応しない。
7. `TrackGraph` は閉塞・信号・連動・ATC の接続用に残す。
8. BVE 的入力は editor/compiler 側に閉じ込める。
9. runtime では完成済みの graph / geometry / LUT だけを見る。
10. メッシュと地形は走行データから生成するが、走行データに混ぜない。
11. 大きな移行では互換フォールバックを一時的に残し、主要 asset 移行後に外す。

## 後で見直す項目

- 3 次放物線と実クロソイドの誤差検証
- R <= 250m の扱い
- 縦曲線の導入
- 待避線・渡り線の現実的な分岐器番数
- カント遷移の扱い
- OffsetGeometry の独自勾配と独自カント
- LUT の適応サンプリング
- 10km 路線での chunk 分割
- 信号・閉塞・連動のデータ編集 UI
- AI 列車の運行管理
- 地形生成のキャッシュと再生成単位
