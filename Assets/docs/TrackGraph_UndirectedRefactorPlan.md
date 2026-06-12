# Track Graph Undirected Refactor Plan

## 結論

物理線路を `fromNodeId -> toNodeId` の有向エッジとして扱う現在の設計は、単線片方向や単純な複線では動くが、折り返し、渡り線、シーサス、車庫、入換を入れると破綻しやすい。

今後は、線路そのものは無向エッジとして扱い、列車・進路・経路探索側が「どちら向きに通過するか」を持つ設計へ移行する。

ただし既存コードは `fromNodeId` / `toNodeId` 前提の箇所が多いため、大きな改修になる。いきなり全置換せず、互換期間を置いて段階的に移行する。

## 現在の問題

現在の主要な前提は以下。

- `TrackEdge` が `fromNodeId` と `toNodeId` を持つ
- `TrackNode` は `outgoingEdgeIds` だけを持つ
- `TrackGraph.ResolveNextEdgeId()` はノードから出るエッジを選ぶ
- `TrackRouteTracer.TryTraceAhead()` は常に `currentEdge.toNodeId` 側へ進む
- `TrackRouteTracer.TryTraceBehind()` は常に `currentEdge.fromNodeId` 側へ戻る
- `TrainController` は `distanceOnEdgeM` が増える方向を前進として扱う
- `ATCController` は `currentEdge.lengthM - distanceOnEdgeM` で前方距離を計算している
- `TurnoutState` は `selectedOutgoingEdgeId` だけを持つ

このため、同じ物理線路を逆向きに走らせるには逆向きエッジを複製する必要が出る。これは閉塞、進路、分岐、連動表で同じ物理線路を二重管理する原因になる。

## 目標設計

### 物理エッジ

線路はノードAとノードBをつなぐ無向エッジとして扱う。

```csharp
public class TrackEdge
{
    public string edgeId;
    public string nodeAId;
    public string nodeBId;
    public float lengthM;
    public List<BlockSection> blockSections;
}
```

既存データとの互換のため、移行初期は `fromNodeId` / `toNodeId` を残し、内部的に `nodeAId = fromNodeId`, `nodeBId = toNodeId` とみなす。

### ノード隣接

ノードは出線ではなく、接続エッジを持つ。

```csharp
public class TrackNode
{
    public string nodeId;
    public List<string> connectedEdgeIds;
}
```

初期移行では `outgoingEdgeIds` を残しつつ、新しく `connectedEdgeIds` を追加する。エディタ生成時は両方を同期する。

### 通過方向

エッジ自体ではなく、列車や経路が通過方向を持つ。

```csharp
public enum EdgeTravelDirection
{
    AtoB,
    BtoA
}
```

`Forward` / `Reverse` という名前は避ける。列車の前後切り替えやレバーサと混ざるため。

### 走行位置

`distanceOnEdgeM` は常に `nodeA` からの距離とする。

- `AtoB` 走行時: 距離は増える
- `BtoA` 走行時: 距離は減る

列車の実際の前進方向は、アクティブ運転台とレバーサから決める。

```csharp
bool activeCabIsRear = activeCabEnd == CabEnd.Rear;
bool reverserWantsBackward = reverserPosition == ReverserPosition.Reverse;
bool travelTowardNodeA = activeCabIsRear ^ reverserWantsBackward;
```

暫定的には以下のように整理する。

| Active cab | Reverser | Edge direction |
| --- | --- | --- |
| Front | Forward | AtoB |
| Front | Reverse | BtoA |
| Rear | Forward | BtoA |
| Rear | Reverse | AtoB |

`Neutral` は牽引力を出さない。

### 分岐器

分岐器は「選択された出線」ではなく、ノード上で許可される接続ペアとして扱う。

```csharp
public class TurnoutConnection
{
    public string nodeId;
    public string fromEdgeId;
    public string toEdgeId;
    public string stateId;
}
```

例:

```text
Normal : E1 <-> E2
Reverse: E1 <-> E3
```

列車が `E1` から分岐ノードへ入ったとき、現在の転てつ状態に対応する接続ペアから次のエッジを決める。

## 影響範囲

最低でも以下に影響する。

- `Scripts/Track/TrackEdge.cs`
- `Scripts/Track/TrackNode.cs`
- `Scripts/Track/TurnoutState.cs`
- `Scripts/Track/TrackGraph.cs`
- `Scripts/Track/TrackRouteTracer.cs`
- `Scripts/Track/TrackTraceSegment.cs`
- `Scripts/Track/TrackRuntimeResolver.cs`
- `Scripts/Track/BlockOccupancyManager.cs`
- `Scripts/Track/NextStationResolver.cs`
- `Scripts/Track/Editor/TrackGraphEditor.cs`
- `Scripts/Track/TrackBuilder.cs`
- `Scripts/Track/TrackVisualizer.cs`
- `Scripts/Safety/ATCController.cs`
- `Scripts/Station/StationStopController.cs`
- `Scripts/Train/Controller/TrainController.Track.cs`
- `Scripts/Train/Controller/CarTrackState.cs`

## 改修ステップ

### Phase 1: 型だけ追加して既存挙動を壊さない

目的: 既存の片方向走行を維持したまま、新しい概念をコードに入れる。

1. `EdgeTravelDirection` を追加する。
2. `TrackEdge` に `nodeAId` / `nodeBId` を追加する。
3. 互換用に、未設定なら `nodeAId = fromNodeId`, `nodeBId = toNodeId` と解釈するヘルパーを作る。
4. `TrackNode` に `connectedEdgeIds` を追加する。
5. 互換用に、未設定なら `outgoingEdgeIds` を `connectedEdgeIds` として扱う。
6. `TrackGraph` に以下のヘルパーを追加する。

```csharp
public string GetNodeAId(TrackEdge edge);
public string GetNodeBId(TrackEdge edge);
public string GetOtherNodeId(TrackEdge edge, string nodeId);
public bool IsEdgeConnectedToNode(TrackEdge edge, string nodeId);
public float GetDistanceFromNode(TrackEdge edge, string nodeId, float distanceFromA);
```

この段階では、走行・ATC・閉塞の挙動は変えない。

### Phase 2: ノード隣接を無向にする

目的: 経路解決が `outgoingEdgeIds` 依存から抜けられるようにする。

1. `TrackGraphEditor` の生成処理で、エッジを `nodeA` と `nodeB` の両方の `connectedEdgeIds` に登録する。
2. `ValidateGraph()` で以下を検証する。
   - エッジの `nodeAId` / `nodeBId` が存在する
   - 両端ノードの `connectedEdgeIds` に対象エッジが入っている
   - 片側にしか登録されていない接続をエラーにする
3. 既存アセットを壊さないため、`outgoingEdgeIds` の検証は警告扱いに下げる。

### Phase 3: 経路解決を接続ペア方式にする

目的: 分岐器を「出線選択」から「進入エッジと退出エッジの接続」へ変える。

1. `TurnoutConnection` を追加する。
2. `TurnoutState.selectedOutgoingEdgeId` は互換用として残す。
3. `TrackGraph.ResolveConnectedEdgeId(nodeId, incomingEdgeId)` を追加する。
4. 通常ノードでは、`connectedEdgeIds` から `incomingEdgeId` 以外の候補を返す。
5. 分岐ノードでは、現在の分岐状態に合う `TurnoutConnection` を探し、`incomingEdgeId` とペアになっている相手エッジを返す。
6. 片渡り、両渡り、シーサスは `TurnoutConnection` の組み合わせで表す。

### Phase 4: トレースを方向対応にする

目的: ATC、閉塞、駅探索が逆向き走行でも使えるようにする。

1. `TrackTraceSegment` に方向を追加する。

```csharp
public EdgeTravelDirection direction;
```

2. `TryTraceAhead()` に現在方向を渡す。

```csharp
TryTraceAhead(
    TrackGraph graph,
    string currentEdgeId,
    float distanceOnEdgeM,
    EdgeTravelDirection direction,
    float lookaheadDistanceM,
    List<TrackTraceSegment> results)
```

3. `AtoB` なら `distanceOnEdgeM -> lengthM` を前方とする。
4. `BtoA` なら `distanceOnEdgeM -> 0` を前方とする。
5. ノードを跨ぐときは `ResolveConnectedEdgeId(nodeId, incomingEdgeId)` で次エッジを選ぶ。
6. 次エッジへ入る初期距離は以下。
   - 次エッジを AtoB で入るなら `0`
   - 次エッジを BtoA で入るなら `lengthM`
7. `TryTraceBehind()` も同じ考え方で方向対応する。

### Phase 5: TrainController を方向対応にする

目的: 実際に同じエッジを逆向きに走れるようにする。

1. `TrainController` に現在のエッジ通過方向を追加する。

```csharp
[SerializeField] private EdgeTravelDirection currentEdgeDirection = EdgeTravelDirection.AtoB;
public EdgeTravelDirection CurrentEdgeDirection => currentEdgeDirection;
```

2. `AdvanceEdgeTransitionIfNeeded()` を、距離増加だけでなく距離減少にも対応する。
3. `distanceOnEdgeM > lengthM` の場合は nodeB 側へ抜ける。
4. `distanceOnEdgeM < 0` の場合は nodeA 側へ抜ける。
5. 抜けたノードで `ResolveConnectedEdgeId()` を呼ぶ。
6. 次エッジのどちら側から入ったかで `currentEdgeDirection` と `distanceOnEdgeM` を設定する。
7. `TrackRuntimeResolver` は `distanceOnEdgeM` から位置を出し、列車の向きだけ方向に応じて反転する。

### Phase 6: ATC / 閉塞 / 駅探索を方向対応にする

目的: 走行方向が変わっても安全系が同じ考え方で動くようにする。

1. `BlockOccupancyManager` のトレース呼び出しに `targetTrain.CurrentEdgeDirection` を渡す。
2. `ATCController` の速度制限探索を `TryTraceAhead()` ベースへ寄せる。
3. `currentEdge.lengthM - train.DistanceOnEdgeM` のような片方向前提の計算をなくす。
4. `StationStopController` / `NextStationResolver` も trace segment の direction を使って距離を計算する。
5. `blockSections` は `nodeA` からの距離で定義する。
6. 逆向き走行時でも同じ `blockId` を参照する。

### Phase 7: 運転台前後切り替えとレバーサを入れる

目的: 折り返し運転で、どちらの運転席が前かを明示する。

1. `CabEnd` を追加する。

```csharp
public enum CabEnd
{
    Front,
    Rear
}
```

2. `ReverserPosition` を追加または既存ノッチ管理に統合する。

```csharp
public enum ReverserPosition
{
    Reverse,
    Neutral,
    Forward
}
```

3. `activeCabEnd` と `reverserPosition` から物理的な進行方向を決める。
4. `Neutral` では牽引力を出さない。
5. カメラ、運転台UI、メーターの参照先を active cab に合わせて切り替える。

## 実装順の優先度

最優先は以下。

1. `EdgeTravelDirection` と無向接続ヘルパーを追加する
2. `TrackRouteTracer` を方向対応にする
3. `TrainController` を `distanceOnEdgeM < 0` に対応させる
4. `BlockOccupancyManager` を方向対応 trace に載せる
5. ATC を方向対応 trace に載せる
6. 分岐器を接続ペア方式にする
7. 前後切り替えスイッチとレバーサを入れる

## 注意点

### いきなりアセット構造を壊さない

既存の `TrackGraph.asset` や自動生成コースが `fromNodeId` / `toNodeId` に依存している。最初からフィールド名を消すと全アセットが壊れる可能性が高い。

最初は互換フィールドを残して、コード側だけ新しいヘルパーを使う。

### blockId は物理区間に紐づける

逆向き走行用に別エッジを作らない場合、閉塞も物理エッジ上の区間として持てる。これは正しい。

```text
edge E001
  blockSections:
    0-200m: B001
    200-400m: B002
```

逆向きに走っても `B002 -> B001` と参照順が逆になるだけで、blockId は同じ。

### 分岐器はエッジ単体ではなく接続ペア

分岐器状態は「この出線を選ぶ」では足りない。

同じノードで、どのエッジから入ってきたかによって抜ける先が変わるため、`(incomingEdgeId, outgoingEdgeId)` のペアで持つ。

### ATCは最後に安定化する

無向化の途中でATCを同時に大きく直すと原因追跡が難しくなる。まず経路トレースと在線が正しいことを確認し、その後ATCを載せる。

## 完了判定

最低限、以下が通れば移行成功とする。

- 既存の片方向コースが今まで通り走れる
- 同じエッジを `AtoB` / `BtoA` の両方向に走れる
- `distanceOnEdgeM < 0` で前エッジへ接続できる
- 分岐ノードで接続ペアに従って進路が決まる
- 閉塞が両方向で同じ blockId を使う
- 次閉塞に列車がいるとATCの閉塞パターンが出る
- 終端駅で反対運転台に切り替えて折り返せる

