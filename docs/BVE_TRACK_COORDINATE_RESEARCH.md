# BVE方式を参考にした複線・待避線・渡り線の座標生成リサーチ

## 目的

現在の `TrackGraph` は、各 `TrackEdge` が独立した線形レシピと始点ノードのワールド姿勢を持ち、列車走行にもそのエッジ列を使う構造になっている。この方式は分岐・閉塞・走行制御には向いている一方、複数の平行線路や待避線を作るたびに、各線のノード・エッジ・曲線レシピを個別に整合させる必要がある。

BVE / Bve trainsim 5・6 の Map 方式は、距離程を主軸にして「自軌道」と「他軌道の相対オフセット」を定義する。これを参考に、TD-ATC で複線、待避線、渡り線を扱いやすくするための接続案を整理する。

## 参照した BVE 系資料

- Bve trainsim 公式 Map file format: <https://bvets.net/jp/edit/formats/route/map.html>
- Bve trainsim 公式 Lay tracks tutorial: <https://bvets.net/en/edit/tutorial/layout.html>
- openBVE route CSV / RW documentation: <https://openbve-project.net/documentation_hugo/en/routes/csv.html>

以降では、BVE5/6 Map を主対象にし、必要に応じて BVE2/4・openBVE の `Track.Rail*` 系も補助的に比較する。

## BVE の線路座標の基本

### 1. 距離程がすべてのイベントの基準

BVE5/6 Map では、数値だけのステートメントが「現在の距離程」を表し、その距離に対して曲線、勾配、速度制限、駅、ストラクチャ、他軌道位置などを記述する。公式仕様では、基本ステートメントは `マップ要素.関数(...)` や `マップ要素[キー].関数(...)` の形で、`0;` や `123;` のような数値ステートメントが現在距離程になる。

このため、BVE の路線データは基本的に「距離程上のキーイベント列」であり、Unity のように最初からワールド座標のノードを多数置く設計ではない。

### 2. 自軌道は Curve / Gradient / Cant で前進積分する

自軌道は `Curve.BeginTransition()`、`Curve.Begin(radius, cant)`、`Curve.End()`、`Curve.Interpolate(...)` などで平面曲線とカントを定義する。半径は右曲線が正、左曲線が負、直線が 0 として扱われる。勾配は `Gradient.BeginTransition()`、`Gradient.Begin(gradient)`、`Gradient.End()`、`Gradient.Interpolate(...)` で距離程上に定義され、単位はパーミルである。

公式チュートリアルでも、軌間 `Curve.SetGauge(1.067)` の後、緩和曲線、円曲線、反向曲線、勾配を距離程ごとに開始・終了させる例が示されている。重要なのは、BVE の自軌道は「現在距離の姿勢から、曲率・勾配・カントを距離方向に積分して姿勢を得る」モデルである点である。

### 3. 他軌道は自軌道からの相対 X / Y と相対曲率で表す

BVE5/6 の複線・側線表現の中核は `Track[trackKey]` である。

- `Track[trackKey].X.Interpolate(x, radius)` は、現在距離程における他軌道の自軌道からの横方向 X 座標を設定し、前後の設定点との間を補間する。
- `Track[trackKey].Y.Interpolate(y, radius)` は、同様に上下方向 Y 座標を設定する。
- `Track[trackKey].Position(x, y, radiusH, radiusV)` は X と Y の同時指定であり、`Track[].X.Interpolate` と `Track[].Y.Interpolate` の組み合わせに相当する。
- `radiusH` / `radiusV` は、自軌道に対する他軌道の相対的な水平・縦曲線半径で、0 は直線として扱われる。
- 他軌道にも `Track[trackKey].Cant.*` があり、他軌道固有のカント、軌間、カント遷移関数を持てる。

つまり BVE は、平行線路を「自軌道と同じ距離程上に、横方向 3.8 m などのオフセットを持つ別トラック」として定義できる。線路間隔の変化、待避線への開き、再合流は、X オフセットを距離程に沿って 0 → 3.8 → 0 のように補間するだけで表現できる。

### 4. ストラクチャは任意軌道を基準に配置できる

BVE5/6 の `Structure[structureKey].Put(trackKey, x, y, z, rx, ry, rz, tilt, span)` や `Repeater[repeaterKey].Begin(...)` は、配置先 `trackKey` を指定できる。`trackKey = 0` は自軌道であり、他軌道名も基準にできる。さらに `Structure[].PutBetween(trackKey1, trackKey2, ...)` は 2 つの軌道間にストラクチャを置ける。

この設計により、レール、枕木、架線柱、ホーム、軌道間の床・バラストなどを「どの線路を基準に置くか」で統一できる。

### 5. BVE2/4・openBVE の RailIndex 方式

openBVE / BVE2/4 系 CSV/RW では、`Track.RailStart RailIndex; X; Y; RailType`、`Track.Rail RailIndex; X; Y; RailType`、`Track.RailEnd RailIndex; X; Y` のように、正の `RailIndex` を持つ追加レールを開始・更新・終了する。X はプレイヤー軌道からの左右距離、Y は上下距離で、`Track.Rail` がないブロックでは前回値が反復される。

BVE5/6 の `Track[trackKey]` は、この `RailIndex` を文字列キー化し、X/Y 補間、相対曲率、他軌道カントなどを明示的に強化したものと見なせる。

## BVE での待避線・渡り線の作り方

BVE の Map は、実際の列車が走る進路グラフというより、運転台視点から見える線路・沿線オブジェクトの幾何を距離程で生成する仕組みである。そのため「分岐器オブジェクトを置けば物理的に進路が切り替わる」というより、以下のように線路形状を距離程で描く。

### 待避線

待避線は、主に次のような相対オフセット列として表現できる。

```text
0;     Track['Siding'].Position(0.0, 0.0);      # 本線と同位置から開始、またはまだ描かない
100;   Track['Siding'].Position(0.0, 0.0);
160;   Track['Siding'].Position(4.0, 0.0);      # 分岐器・リード曲線で本線から離れる
600;   Track['Siding'].Position(4.0, 0.0);      # 駅部で平行に走る
680;   Track['Siding'].Position(0.0, 0.0);      # 合流側で本線へ戻る
```

見た目としては、`Track['Siding']` を基準にレール・枕木を連続配置し、分岐器やポイントの専用ストラクチャを距離程に置く。駅ホームは自軌道または待避線を基準に配置する。

### 渡り線

複線間の渡り線は、上下本線が一定間隔で平行している前提で、片方の線からもう片方へ斜めに接続する追加軌道を相対オフセットで描く。

```text
0;     Track['Down'].Position(4.0, 0.0);        # 下り線は右 4 m に平行
300;   Track['Crossover'].Position(0.0, 0.0);   # 上り線側から開始
340;   Track['Crossover'].Position(4.0, 0.0);   # 下り線側へ移る
360;   Track['Crossover'].Position(4.0, 0.0);   # 必要なら短く重ねる / 終端表現
```

両渡り線やシーサスクロッシングは、同様の追加 `trackKey` を複数定義し、交差部・分岐器のストラクチャを適切な距離程に置く。BVE ではこの幾何は表示用で、列車の走行進路としては通常、自軌道が固定である点に注意する必要がある。

## 現在の TD-ATC 実装の整理

### データ構造

現在の `TrackGraph` は ScriptableObject で、`nodes`、`edges`、`turnoutStates`、`stations` を持つ。ノードは `TrackNode`、エッジは `TrackEdge` として分かれ、分岐選択は `TurnoutState.selectedOutgoingEdgeId` で保持される。

`TrackNode` はワールド座標・ワールド回転・出線エッジ ID 群を持つ。出線が複数ある場合、`TrackGraph.UpdateNodeTypesAndJunctionIds()` がノードを `Junction` にし、`junctionId` を設定する。

`TrackEdge` は from/to ノード、長さ、速度制限、軌間、閉塞 ID、水平・縦・カントセグメントを持つ。線形レシピは `TrackHorizontalSegment`、`TrackVerticalSegment`、`TrackCantSegment` として距離開始・長さ・曲線種別・半径・勾配・カントを保持する。

### 座標解決

`TrackRuntimeResolver.TryResolvePose()` は、エッジ ID とエッジ内距離から、始点ノードのワールド座標・姿勢を基準に平面線形を積み上げ、縦断高さ・勾配・カントを加えて最終姿勢を返す。

水平線形は `Straight`、`Curve`、`TransitionIn`、`TransitionOut` の 4 種で、直線、円曲線、クロソイド近似を計算する。カントは `cantMm / gaugeM` からロール角に変換される。

### 生成と走行

`TrackVisualizer` は全 `TrackEdge` に対してバラスト、左右レール、枕木、架線柱を生成する。左右レールは、中心線に対して `trackGauge * 0.5f` の固定オフセットを持つ断面を `TrackMeshGenerator` に渡して生成する。

`TrackMeshGenerator` はエッジ上を一定距離ごとにサンプリングし、`TrackRuntimeResolver.TryResolvePose()` から得た位置・回転に断面プロファイルを乗せてメッシュ化する。

走行側は、エッジ終端で `TrackGraph.ResolveNextEdgeId()` を使い、分岐ノードなら `TurnoutState.selectedOutgoingEdgeId` を優先し、なければ戻り方向を避ける既定出線を選ぶ。`TrackRouteTracer` もこの解決ロジックで前方・後方のエッジ列をたどる。

## BVE と現在実装の共通点

| 観点 | BVE | 現在の TD-ATC | 共通点 |
| --- | --- | --- | --- |
| 距離軸 | 現在距離程にイベントを置く | エッジ内距離 `distanceOnEdgeM` にセグメントを置く | 線形を距離方向の関数として解決する |
| 曲線 | `Curve.Begin` / `Interpolate` で半径指定 | `TrackHorizontalSegment.radiusM` と曲線種別 | 半径符号で左右曲線を表現しやすい |
| 緩和曲線 | `Curve.BeginTransition` | `TransitionIn` / `TransitionOut` | 直線・円曲線間の遷移を別セグメント化する |
| 勾配 | `Gradient.*`、単位 ‰ | `TrackVerticalSegment`、単位 ‰ | 縦断を距離に対する勾配関数で持つ |
| カント | `Curve` / `Track[].Cant`、軌間から角度へ変換 | `TrackCantSegment` と `gaugeM` からロール角へ変換 | カント値と軌間を分離して姿勢化する |
| 構造物配置 | `Structure` / `Repeater` が軌道基準 | `TrackVisualizer` が線路中心線基準でメッシュ・枕木を配置 | 線路姿勢をサンプリングしてオブジェクトを置く |

## BVE と現在実装の乖離点

### 1. BVE は「相対軌道」、現在実装は「独立エッジ」

BVE は自軌道を主軸に、他軌道を `Track[trackKey].Position(x, y, ...)` で相対定義する。平行線路なら X を固定するだけで済む。

現在実装は各線路を走行可能な `TrackEdge` として独立生成する。複線なら、左右線それぞれのノード座標・エッジ長・曲線セグメントを整合させる必要がある。これが、複数の平行線路を作りにくい主因である。

### 2. BVE の他軌道は主に表示用、現在実装のエッジは走行・閉塞用

BVE の `Track[trackKey]` は見た目の追加軌道や構造物基準として強いが、列車進路切替のグラフではない。TD-ATC の `TrackEdge` は列車走行、閉塞、速度制限、分岐選択に直結する。

そのため、BVE 方式をそのまま導入して `Track[trackKey]` だけを増やすと、見える線路は増えるが、列車が走れる線路網や閉塞管理には接続されない。

### 3. 分岐・待避線の意味が違う

BVE では待避線・渡り線は相対座標で描画できるが、運転列車の進路は基本的に自軌道中心で固定される。一方 TD-ATC は `TrackNode` / `TrackEdge` と `TurnoutState` によって分岐先を選ぶため、待避線・渡り線は走行可能なグラフエッジである必要がある。

### 4. TD-ATC のメッシュ生成は中心線 1 本につき左右レール固定

`TrackVisualizer` は各 `TrackEdge` の中心線から左右レールを固定軌間で生成する。BVE のように「自軌道の右 4 m に別 trackKey を作り、そこにも Repeater を置く」という抽象がまだない。

### 5. 座標定義の編集粒度が違う

BVE は距離程ごとのイベント列なので、`100; Track['Siding'].X.Interpolate(4.0);` のような編集が自然である。現在実装は `TrackBuilder` で現在姿勢を進めながらノードとエッジを作るため、途中から他線を派生させるには、派生開始点の座標・姿勢を resolver で取り出し、新しいノード・エッジへ変換する工程が必要になる。

## 現在実装との接続方針

BVE 方式は、そのまま置き換えるのではなく、以下の 2 層に分けて導入するのが安全である。

1. **レイアウト入力層**: BVE 的な距離程・相対オフセットで複線、待避線、渡り線を記述する。
2. **実行グラフ層**: 既存の `TrackGraph` / `TrackEdge` / `TrackNode` / `TurnoutState` にコンパイルし、走行・閉塞・メッシュ生成は既存基盤を使う。

### 推奨追加モデル: RelativeTrackLayout

新しい中間データとして、以下のような概念を追加する。

```csharp
public class RelativeTrackLayout : ScriptableObject
{
    public List<ReferenceRouteSegment> referenceSegments;
    public List<RelativeTrackDefinition> tracks;
    public List<TrackConnectionDefinition> connections;
}

public class RelativeTrackDefinition
{
    public string trackKey;       // 例: Main, Down, Siding1, CrossoverA
    public string baseTrackKey;   // 例: Main。BVE の自軌道に相当
    public List<TrackOffsetKeyframe> offsetKeys;
    public List<TrackCantSegment> cantSegments;
    public bool generateRuntimeEdges;
    public bool visualOnly;
}

public class TrackOffsetKeyframe
{
    public float distanceM;
    public float lateralOffsetM;  // BVE Track[].X
    public float verticalOffsetM; // BVE Track[].Y
    public float relativeRadiusHM;
    public float relativeRadiusVM;
}
```

この段階では BVE の `Track[trackKey].Position` に近い入力を保持し、まだ `TrackEdge` にしない。

### コンパイル方針 A: 見た目だけの他線

装飾用・遠景用の平行線路は `visualOnly = true` として、`TrackGraph` へ走行エッジを作らず、`TrackVisualizer` 側に「基準エッジ + 相対オフセット軌道」を渡してメッシュだけ生成する。

この場合、BVE と同じ考え方で、現在の中心線姿勢に `right * lateralOffsetM + up * verticalOffsetM` を足して他線中心をサンプリングする。閉塞や分岐は持たないため、実装コストが低い。

### コンパイル方針 B: 走行可能な平行線・待避線

列車が走る必要のある線は `generateRuntimeEdges = true` とし、相対オフセット軌道をサンプリングして `TrackNode` / `TrackEdge` に変換する。

処理イメージ:

1. 基準線の距離程 `s` から `TrackRuntimeResolver.TryResolvePose()` で基準位置・姿勢を得る。
2. `TrackOffsetKeyframe` を補間して、`lateralOffsetM` / `verticalOffsetM` を得る。
3. `position + rotation * new Vector3(lateralOffsetM, verticalOffsetM, 0)` を他線中心点にする。
4. キー点または曲率変化点ごとに `TrackNode` を作る。
5. 隣接ノード間を `TrackEdge` にし、必要ならサンプル列から近似的に `TrackHorizontalSegment` を逆算する。短期的には polyline 近似用の新しい水平セグメント型を追加してもよい。
6. 待避線・渡り線の入口・出口で、既存本線ノードと接続し、`outgoingEdgeIds` に接続エッジを追加する。
7. `UpdateNodeTypesAndJunctionIds()` と `SyncTurnoutStates()` を呼び、分岐状態を既存ロジックに同期する。

### コンパイル方針 C: 本線も BVE 的入力から生成する

将来的には、本線自体も BVE 的な距離程イベント列から `TrackEdge` を生成できるようにする。現在の `TrackHorizontalSegment` / `TrackVerticalSegment` / `TrackCantSegment` は BVE の `Curve` / `Gradient` / `Cant` と対応がよいため、本線については比較的変換しやすい。

## 段階的な実装案

### Phase 1: BVE 的な他軌道メッシュだけを追加

- `RelativeTrackDefinition` と `TrackOffsetKeyframe` を追加する。
- `TrackRuntimeResolver` に「基準エッジ + 距離 + 横/縦オフセット」から姿勢付き pose を返す補助メソッドを追加する。
- `TrackVisualizer` に、既存 `graph.edges` とは別に `visualOnly` 他線を描く経路を追加する。
- まずは固定オフセット複線と、X 補間で開く待避線をメッシュとして生成できる状態にする。

効果: 既存の走行・閉塞ロジックを壊さず、BVE 的な複線作成の利点をすぐ確認できる。

### Phase 2: 相対オフセット線を走行可能 TrackEdge に焼き込む

- `RelativeTrackLayoutCompiler` を追加し、中間データから `TrackGraph` を生成または追記する。
- 固定オフセット平行線は、基準線の水平・縦・カントセグメントをコピーし、ノードだけをオフセットする最適化を行う。
- オフセットが変化する待避線・渡り線は、サンプリングから polyline / clothoid 近似セグメントへ変換する。
- 入口・出口に接続ノードを置き、既存の `TurnoutState` で分岐選択できるようにする。

効果: 待避線や渡り線が列車走行・分岐選択・閉塞対象になる。

### Phase 3: BVE Map ライクなエディタ入力

- 距離程イベント UI を作る。
- `Curve.BeginTransition`、`Curve.Begin(radius, cant)`、`Gradient.Begin(...)`、`Track['Siding'].Position(...)` に近い入力フォームを用意する。
- 出力先は既存 `TrackGraph.asset` か、新規 `RelativeTrackLayout.asset` とする。

効果: 路線制作者は BVE 的な距離程編集で複雑な配線を作れる。

## 実装時の注意点

### 他線の姿勢計算

単純に `position + right * offset` だけでは、オフセットが変化する区間の接線方向が基準線と一致しない。待避線や渡り線では、`offset(s)` の微分を含めて接線を計算する必要がある。

簡易式:

```text
P_other(s) = P_base(s) + R_base(s) * (x(s), y(s), 0)
T_other(s) ≒ normalize(P_other(s + ds) - P_other(s - ds))
```

最初は有限差分で十分だが、将来的には `relativeRadiusH` を使った解析的な曲率制御が望ましい。

### エッジ長と距離程のズレ

BVE の距離程は自軌道の距離であり、他軌道が斜めに開く区間では他軌道自身の実長が基準距離差より長くなる。走行可能 `TrackEdge` に焼き込む場合は、他軌道の弧長を再計算して `lengthM` に入れる必要がある。

### 分岐器モデルと進路ロジック

BVE 的な見た目の渡り線と、TD-ATC の `TurnoutState` は別概念である。走行可能化する場合は、分岐開始点・合流点を必ず `TrackNodeType.Junction` にし、選択可能な `outgoingEdgeIds` を持たせる必要がある。

### 閉塞と速度制限

BVE の `SpeedLimit.Begin/End` や `Section.Begin` は距離程イベントだが、現在の TD-ATC では `TrackEdge.speedLimitMS`、`blockId`、`blockSections` に寄せている。BVE 的入力から生成する場合は、距離程イベントをエッジ内距離へ分割・写像する必要がある。

## まとめ

- BVE の強みは、距離程を主軸にして他軌道を相対 X/Y オフセットで定義できることにある。これにより、複線、待避線、渡り線の見た目を少ない記述で作れる。
- 現在の TD-ATC は、走行・閉塞・分岐に強い `TrackGraph` 方式である一方、平行線路を作るには独立エッジを手作業で整合させる必要がある。
- 最適解は、BVE 的な相対軌道を「入力・編集・見た目生成の層」として導入し、走行可能な線だけ既存 `TrackGraph` へコンパイルする二層構造である。
- 短期的には visualOnly の相対他線メッシュを追加し、中期的に待避線・渡り線を `TrackEdge` へ焼き込むのが安全である。
