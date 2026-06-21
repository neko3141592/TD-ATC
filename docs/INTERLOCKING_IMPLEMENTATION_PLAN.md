# 連動装置（Interlocking）実装調査・設計メモ

## 1. この文書の目的

この文書は、現在の TD-ATC プロジェクトに「連動装置」を追加するための調査結果と実装方針をまとめるものです。コード本体にはまだ変更を加えず、既存の線路グラフ・分岐器・閉塞・ATC の実装を前提に、どのクラスを追加し、どの既存クラスへ最小限の接点を設ければ安全に拡張できるかを整理します。

ここでいう連動装置は、駅構内や分岐部で次の条件を満たすための保安ロジックです。

- 進路を構成する分岐器を正しい向きへ転換する。
- 構成済み進路を鎖錠し、列車通過中に分岐器が変わらないようにする。
- 競合する進路を同時に開通させない。
- 進路内・防護区間内に他列車が在線している場合は信号を進行現示にしない。
- ATC に対して「どこまで進めるか」「次の停止目標はどこか」を与える。

本プロジェクトは既に ATC・閉塞・分岐器の基礎を持っているため、連動装置はそれらを置き換えるのではなく、上位の進路管理レイヤーとして追加するのが最も安全です。

---

## 2. 既存実装の調査結果

### 2.1 線路グラフ

`TrackGraph` は ScriptableObject として、ノード、エッジ、分岐状態、駅、分岐接続、線形データを保持しています。既に `FindNode`、`FindEdge`、`FindTurnoutState`、`FindTurnoutConnection` で ID ベースに参照できる構造になっています。

連動装置から見ると、これは「連動図表の物理配線データ」に相当します。進路定義はこの `TrackGraph` の ID を参照する形にするのが自然です。

### 2.2 分岐器

現在の分岐器表現は次の 3 要素で構成されています。

- `TrackNode.trackNodeType == Junction` と `junctionId`
- `TurnoutState` の `normalConnectionId` / `reverseConnectionId` / `selectedPosition`
- `TurnoutConnection` の `nodeId` / `edgeAId` / `edgeBId`

`TrackGraphUndirectedHelpers.ResolveTurnoutConnection` は、ジャンクションノードに到達した列車が、現在の `TurnoutState.ActiveConnectionId` に含まれる 2 本のエッジ間だけを通れるように解決しています。したがって連動装置は、直接ルートトレースを作り直すよりも、`TurnoutState.selectedPosition` を制御・鎖錠する役割を持たせるのがよいです。

ただし現状では、分岐器に「転換中」「鎖錠中」「故障」「手動扱い禁止」といった状態はありません。連動装置を入れる場合、`TurnoutState` に実運用状態を直接追加するか、別クラス `TurnoutRuntimeState` で実行時状態を持つ必要があります。既存アセット互換を考えると、まずは別クラスで持つ方が安全です。

### 2.3 閉塞・在線

`TrackEdge` は `blockSections` を持ち、1 本のエッジ上に複数の閉塞区間を定義できます。`BlockOccupancyManager` は列車の先頭・最後尾・先頭張り出しを `TrackRouteTracer` で追跡し、占有中の `blockId` を再構築しています。また、前方最初の在線閉塞を検索する `TryFindFirstOccupiedBlockAhead` も実装済みです。

連動装置で重要なのは、進路内に含まれる閉塞と防護区間の閉塞を確認できることです。現在の `BlockOccupancyManager.OccupiedTrainsByBlock` は公開されているため、連動側は blockId 単位で在線確認できます。

### 2.4 ATC

`ATCController` は次の候補から最も厳しい速度制限を選択しています。

- 現在・前方エッジの速度制限
- 前方在線閉塞を停止目標にしたパターン
- `TrainServiceDefinition` のサービス速度制限

前方在線閉塞については `BlockOccupancyManager.TryFindFirstOccupiedBlockAhead` を使い、停止目標距離を作って ATC パターンを計算しています。連動装置を導入した後は、ATC の停止目標に「未開通進路の終端」「停止信号」「入換禁止境界」も追加できるようにする必要があります。

### 2.5 経路追跡

`TrackRouteTracer.TryTraceAhead` は現在のエッジ・距離・進行方向から、分岐状態に従って前方セグメント列を返します。これは ATC の速度制限探索や閉塞探索で既に使われています。

連動装置でも以下に利用できます。

- 進路定義の検証時に、start edge から exit edge まで到達可能か確認する。
- 進路開通後に列車がどこまで進入したか判定する。
- 進路解放を「列車が通過済みの閉塞から順次解放」に拡張する。

---

## 3. 連動装置を追加する基本方針

### 3.1 既存コードを壊さないレイヤー構成

推奨構成は次の通りです。

```text
TrainController
  ↓ 現在位置・列車ID
BlockOccupancyManager
  ↓ blockId 在線情報
InterlockingController  ← RouteCommand / StationDispatcher / UI
  ↓ 進路状態・分岐鎖錠・信号現示・停止限界
ATCController
  ↓ 許容速度・ブレーキ指令
NotchManager / BrakeSystem
```

ポイントは、`TrackGraph` を「物理配線」、`BlockOccupancyManager` を「軌道回路/閉塞在線」、`InterlockingController` を「進路・信号・転てつ制御」として分離することです。

### 3.2 ScriptableObject と Runtime State を分ける

連動装置には、編集時に作る固定データと、Play 中に変化する状態があります。

固定データは ScriptableObject にし、Runtime 状態は MonoBehaviour が辞書で持つべきです。

固定データ例:

- `InterlockingTable`
- `RouteDefinition`
- `RouteTurnoutRequirement`
- `SignalDefinition`
- `OverlapDefinition`
- `ConflictDefinition`

Runtime 状態例:

- 進路の要求中/構成中/開通中/列車進入済み/解放済み
- 分岐器の目標位置/現在位置/鎖錠/転換中
- 信号の停止/進行/抑止
- 進路ごとの接近鎖錠タイマー

Unity アセットに Runtime 値を書き戻すと、Play 後に意図しない状態が残る可能性があるため避けます。

---

## 4. 追加するデータモデル案

### 4.1 InterlockingTable

`Assets/Scripts/Safety/Interlocking/InterlockingTable.cs` などに置く想定の ScriptableObject です。

保持する内容:

```csharp
public class InterlockingTable : ScriptableObject
{
    public TrackGraph trackGraph;
    public List<SignalDefinition> signals;
    public List<RouteDefinition> routes;
    public List<ConflictDefinition> conflicts;
}
```

役割:

- 連動装置の固定データ入口。
- 路線ごと、駅ごとに差し替え可能にする。
- `TrackGraph` の ID と照合して検証する。

### 4.2 SignalDefinition

信号機または ATC の停止限界を表すデータです。ゲーム内に物理的な信号柱を置かない場合でも、「進路の開始点」として必要です。

推奨フィールド:

```csharp
[Serializable]
public class SignalDefinition
{
    public string signalId;
    public string edgeId;
    public float distanceOnEdgeM;
    public EdgeTravelDirection direction;
    public List<string> routeIds;
}
```

意味:

- `signalId`: 進路要求の起点。
- `edgeId` / `distanceOnEdgeM`: 停止限界または信号位置。
- `direction`: その信号が防護する進行方向。
- `routeIds`: この信号から選べる進路。

ATC 専用路線でも、車上信号に「次の進路が開通しているか」を渡すため、論理信号は定義した方が実装が安定します。

### 4.3 RouteDefinition

1 つの進路を表します。

推奨フィールド:

```csharp
[Serializable]
public class RouteDefinition
{
    public string routeId;
    public string displayName;
    public string startSignalId;
    public string entryEdgeId;
    public float entryDistanceM;
    public EdgeTravelDirection direction;

    public List<string> routeBlockIds;
    public List<string> overlapBlockIds;
    public List<RouteTurnoutRequirement> turnoutRequirements;
    public List<string> conflictingRouteIds;

    public string exitEdgeId;
    public float exitDistanceM;
}
```

`routeBlockIds` は列車が実際に走る進路内閉塞、`overlapBlockIds` は停止余裕・防護区間です。最初はどちらも手入力で十分です。後で `TrackRouteTracer` から自動生成できるようにします。

### 4.4 RouteTurnoutRequirement

進路ごとに必要な分岐器位置を定義します。

```csharp
[Serializable]
public class RouteTurnoutRequirement
{
    public string junctionId;
    public TurnoutPosition requiredPosition;
}
```

`TrackGraph` は既に `junctionId` と `TurnoutPosition` を持つため、この形なら既存実装と自然に接続できます。

### 4.5 ConflictDefinition

競合進路を手入力で明示するためのデータです。

```csharp
[Serializable]
public class ConflictDefinition
{
    public string routeAId;
    public string routeBId;
    public string reason;
}
```

`routeBlockIds` の重複から自動判定できる競合もありますが、側面衝突や過走余裕のように閉塞重複だけでは判定しにくいものもあります。初期実装では `RouteDefinition.conflictingRouteIds` に直接入れてもよいですが、検証しやすさを考えると別リストも有効です。

---

## 5. Runtime クラス案

### 5.1 InterlockingController

連動装置の中心になる MonoBehaviour です。

主な責務:

1. 進路要求を受け付ける。
2. 進路の安全条件を確認する。
3. 必要分岐器を転換する。
4. 分岐器を鎖錠する。
5. 進路を開通状態にする。
6. 信号現示または ATC 停止限界を更新する。
7. 列車通過に応じて進路を解放する。

主な参照:

```csharp
[SerializeField] private InterlockingTable table;
[SerializeField] private TrackGraph trackGraph;
[SerializeField] private BlockOccupancyManager blockOccupancyManager;
```

内部状態:

```csharp
private Dictionary<string, RouteRuntimeState> routesById;
private Dictionary<string, TurnoutRuntimeState> turnoutsByJunctionId;
private Dictionary<string, SignalRuntimeState> signalsById;
```

### 5.2 RouteRuntimeState

進路状態を表します。

```csharp
public enum RouteState
{
    Idle,
    Requested,
    SettingTurnouts,
    Locked,
    Cleared,
    Occupied,
    Releasing,
    Failed
}
```

推奨状態遷移:

```text
Idle
  → Requested
  → SettingTurnouts
  → Locked
  → Cleared
  → Occupied
  → Releasing
  → Idle
```

失敗時:

```text
Requested / SettingTurnouts / Locked
  → Failed
  → Idle
```

### 5.3 TurnoutRuntimeState

`TurnoutState` は現在アセット側にある静的/半静的状態です。連動実装では次を Runtime 側で管理します。

```csharp
public class TurnoutRuntimeState
{
    public string junctionId;
    public TurnoutPosition currentPosition;
    public TurnoutPosition targetPosition;
    public bool isMoving;
    public bool isLocked;
    public string lockedByRouteId;
    public float moveCompleteTime;
}
```

初期実装では `isMoving` を即時完了にしても構いません。後で転換時間、転換音、転換不能を追加できます。

### 5.4 SignalRuntimeState

```csharp
public enum InterlockingSignalAspect
{
    Stop,
    Proceed,
    Caution,
    Off
}
```

ATC 路線の場合は多現示信号ではなく、`Stop` か `Proceed` をまず返せれば十分です。

---

## 6. 進路要求から開通までの処理

### 6.1 基本フロー

`RequestRoute(routeId)` が呼ばれた時の処理です。

1. `RouteDefinition` を取得する。
2. 既に同じ進路が `Cleared` / `Occupied` なら何もしない。
3. 進路内閉塞 `routeBlockIds` に他列車が在線していないか確認する。
4. 防護区間 `overlapBlockIds` に他列車が在線していないか確認する。
5. `conflictingRouteIds` のいずれかが `Locked` / `Cleared` / `Occupied` でないか確認する。
6. 必要な分岐器が他進路で鎖錠されていないか確認する。
7. 必要分岐器を `requiredPosition` へ転換する。
8. 転換完了後、分岐器を当該進路で鎖錠する。
9. 進路状態を `Cleared` にする。
10. 信号現示を `Proceed` にする。

### 6.2 安全条件の詳細

#### 6.2.1 進路内在線チェック

`BlockOccupancyManager.OccupiedTrainsByBlock` を参照し、`routeBlockIds` に他列車が存在すれば開通不可です。

注意点:

- 入換や続行運転などを後で入れる場合は「自列車だけ在線可」のような例外が必要になります。
- 初期実装では列車 ID を指定せず、単純に誰か在線していれば不可でよいです。

#### 6.2.2 防護区間チェック

`overlapBlockIds` も原則として空いている必要があります。防護区間は、停止信号を冒進した場合の余裕、または分岐部の側面防護として使います。

初期段階では進路内閉塞と同じ扱いで十分です。後で「進路は開通するが警戒現示にする」などへ拡張できます。

#### 6.2.3 競合進路チェック

次の条件の進路は競合扱いにします。

- 同じ閉塞を使う。
- 同じ分岐器を異なる位置で要求する。
- 交差・合流・側面衝突の危険がある。
- 過走余裕が互いに重なる。

最初は `conflictingRouteIds` を手入力し、Editor 検証で次を警告します。

- `routeBlockIds` が重複しているのに競合定義がない。
- 同じ `junctionId` に異なる `requiredPosition` を要求しているのに競合定義がない。

#### 6.2.4 分岐器鎖錠チェック

`TurnoutRuntimeState.isLocked` が true で、`lockedByRouteId` が自進路以外なら転換不可です。

同じ位置を要求している場合でも、競合進路が開通中なら進路開通不可にします。これは分岐器だけを見ると安全でも、列車同士の衝突が防げないためです。

---

## 7. 列車通過と進路解放

### 7.1 初期実装: 全進路一括解放

最も簡単で安全な方式です。

1. 進路が `Cleared` の状態で、進路内閉塞に列車が入ったら `Occupied` にする。
2. その後、進路内閉塞と防護区間の全てが空になったら `Releasing` にする。
3. 分岐器鎖錠を解除する。
4. 信号を `Stop` に戻す。
5. 進路状態を `Idle` に戻す。

この方式は現実より保守的ですが、ゲーム実装の初期段階ではバグが少なく安全です。

### 7.2 拡張実装: 区分解放

駅構内の連続進路や高密度運転を表現したくなったら、進路を複数のサブセクションに分けます。

追加データ例:

```csharp
public class RouteReleaseSection
{
    public string sectionId;
    public List<string> blockIds;
    public List<string> turnoutIdsToUnlockAfterClear;
}
```

列車が通過済みの block が空になったタイミングで、該当する分岐器だけを順次解放します。ただしデバッグ難度が上がるため、最初から実装しない方がよいです。

---

## 8. ATC との接続方針

### 8.1 連動なしの場合の現状

現状の ATC は、前方に他列車が在線する閉塞がある場合、その閉塞手前を停止目標にします。逆に言うと、前方に列車がいなければ分岐器が未開通でも進行可能な速度が出てしまう可能性があります。

### 8.2 連動ありで追加すべき ATC ターゲット

`ATCController` に、次のような連動由来の停止目標候補を追加するのがよいです。

```csharp
public interface IInterlockingAtcProvider
{
    bool TryGetStopTargetAhead(
        TrainController train,
        out string sourceLabel,
        out float distanceToTargetM,
        out float targetSpeedMS);
}
```

候補例:

- 次の信号が停止現示の場合: 信号位置を停止目標にする。
- 自列車向け進路が未開通の場合: 進路入口または停止限界を停止目標にする。
- 開通済み進路の終端以遠に次進路がない場合: 進路終端を停止目標にする。
- 分岐器転換中の場合: 分岐器手前または信号位置を停止目標にする。

`ATCController` 側では既存の `AtcTargetCandidate` と同じ式で候補化し、速度制限・在線閉塞・サービス速度と一緒に最も厳しいものを選べばよいです。

### 8.3 ATC 現示と信号現示の関係

現在の `ATCController.CurrentSignalAspect` は、前方次閉塞が在線、または ATC 許容速度が停止相当なら Red を返します。連動装置を入れる場合は、次の優先順位にします。

1. ATC カットアウトなら Off。
2. 連動が停止現示なら Red。
3. 前方在線閉塞で停止パターンなら Red。
4. それ以外は Green。

将来的に Caution や速度現示を増やす場合も、連動由来の現示を最上位に置くと分かりやすいです。

---

## 9. 分岐器制御との接続方針

### 9.1 既存 `TurnoutState` の更新

現在の経路解決は `TurnoutState.ActiveConnectionId` を見ているため、分岐器を転換するには最終的に `TurnoutState.selectedPosition` を変える必要があります。

推奨手順:

1. `TurnoutRuntimeState.targetPosition` を設定。
2. 転換時間を待つ。
3. `TrackGraph.FindTurnoutState(junctionId).selectedPosition` を更新。
4. `TurnoutRuntimeState.currentPosition` を更新。
5. 鎖錠する。

初期実装では 2 を省略し、即時更新で構いません。

### 9.2 転換可否

転換前に次を確認します。

- 当該分岐器を含む閉塞に列車が在線していない。
- 当該分岐器が他進路で鎖錠されていない。
- 転換先が `TurnoutState.normalConnectionId` または `reverseConnectionId` として有効。

分岐器位置だけでは、その分岐器上に列車がいるか判定しにくいため、`RouteDefinition` 側に `turnoutBlockIds` を追加するか、分岐器ごとの保護 block を持つ `TurnoutDefinition` を追加するとよいです。

---

## 10. UI・デバッグ表示

連動装置は状態が見えないとデバッグが困難です。最低限、次の UI または Debug Inspector を用意することを推奨します。

### 10.1 進路一覧パネル

表示項目:

- routeId
- state
- startSignalId
- routeBlockIds
- overlapBlockIds
- locked turnout list
- failure reason

操作:

- 進路要求ボタン
- 進路取消ボタン
- 強制解放ボタン（デバッグ専用）

### 10.2 分岐器一覧パネル

表示項目:

- junctionId
- currentPosition
- targetPosition
- isMoving
- isLocked
- lockedByRouteId

操作:

- 手動 Normal / Reverse（非鎖錠時のみ）

### 10.3 信号/ATC 表示

表示項目:

- signalId
- aspect
- clearedRouteId
- stop target distance

既存の `ATCIndicatorDisplay` に連動由来の停止目標ラベルを追加すると、運転中に「なぜ止められているか」が分かりやすくなります。

---

## 11. Editor 検証ツール

連動データは手入力ミスが起きやすいため、実行時だけでなく Editor 上で検証できると効果が大きいです。

検証項目:

1. すべての `routeId` が一意。
2. すべての `signalId` が一意。
3. `startSignalId` が存在する。
4. `entryEdgeId` / `exitEdgeId` が `TrackGraph` に存在する。
5. `routeBlockIds` / `overlapBlockIds` が実在する blockId と一致する。
6. `junctionId` が `TrackGraph` の分岐ノードまたは `TurnoutState` に存在する。
7. `requiredPosition` に対応する connectionId が空でない。
8. 競合進路 ID が存在する。
9. 閉塞重複がある進路同士に競合定義がある。
10. 同じ分岐器に異なる要求を出す進路同士に競合定義がある。
11. `TrackRouteTracer` で entry から exit まで到達可能。

初期段階では `InterlockingTable.Validate(List<string> errors)` を実装し、Inspector のボタンから実行できるだけでも十分です。

---

## 12. JSON 路線データとの関係

現在の JSON レイアウトは、線形、軌道、接続を中心に定義されています。連動装置を JSON から生成したい場合、次の拡張が考えられます。

```json
{
  "signals": [
    {
      "signalId": "S1",
      "trackName": "T1",
      "distanceM": 950.0,
      "direction": "AtoB",
      "routeIds": ["R1", "R2"]
    }
  ],
  "routes": [
    {
      "routeId": "R1",
      "startSignalId": "S1",
      "entryTrackName": "T1",
      "exitTrackName": "T3",
      "routeBlockIds": ["B1", "B2"],
      "overlapBlockIds": ["B3"],
      "turnouts": [
        { "junctionId": "J1", "position": "Normal" }
      ],
      "conflicts": ["R2"]
    }
  ]
}
```

ただし、最初から JSON コンパイラに組み込む必要はありません。まずは Unity Editor 上で `InterlockingTable.asset` を手作成し、仕様が固まってから JSON 化する方が安全です。

---

## 13. 段階的な実装計画

### Phase 1: 読み取り専用の連動データ

目的:

- 連動表を ScriptableObject として定義できるようにする。
- 実行時制御はまだしない。

作業:

- `InterlockingTable`
- `SignalDefinition`
- `RouteDefinition`
- `RouteTurnoutRequirement`
- `ConflictDefinition`
- `Validate()`

完了条件:

- Inspector で進路データを作れる。
- `TrackGraph` との ID 不整合を検出できる。

### Phase 2: 進路要求と安全判定

目的:

- 進路が開通できるか判定する。
- 開通できない理由を表示する。

作業:

- `InterlockingController`
- `RouteRuntimeState`
- `RequestRoute(routeId)`
- 在線チェック
- 競合チェック
- 分岐器鎖錠チェック

完了条件:

- 空き進路は `Cleared` になる。
- 在線中・競合中・分岐器鎖錠中の進路は拒否される。

### Phase 3: 分岐器転換・鎖錠

目的:

- 進路要求に応じて分岐器を転換し、鎖錠する。

作業:

- `TurnoutRuntimeState`
- `SetTurnoutPosition(junctionId, position)`
- 鎖錠/解錠処理
- `TurnoutState.selectedPosition` への反映

完了条件:

- 開通済み進路の分岐器を手動転換できない。
- 進路解放後に転換できる。

### Phase 4: 信号・ATC 連携

目的:

- 未開通進路では列車を停止させる。
- 開通進路では進行可能にする。

作業:

- `SignalRuntimeState`
- `IInterlockingAtcProvider`
- `ATCController` への連動候補追加
- UI 表示

完了条件:

- 停止現示の信号手前で ATC パターンが発生する。
- 進路開通で停止目標が消える、または進路終端まで延びる。

### Phase 5: 進路解放

目的:

- 列車通過後に進路と分岐器を自動解放する。

作業:

- 進路内閉塞への進入検知
- 全閉塞クリアによる一括解放
- 信号停止戻し

完了条件:

- 列車が進路を通過すると、進路状態が `Idle` に戻る。
- 分岐器鎖錠が解除される。

### Phase 6: 拡張

候補:

- 接近鎖錠
- 時素解錠
- 区分解放
- 側面防護
- 入換進路
- 自動進路制御 PRC/ARC
- 連動図表 UI
- 故障・手動扱い

---

## 14. 既存コードへの最小変更ポイント

コード実装時に変更が必要になりそうな箇所は次の通りです。

### 14.1 `ATCController`

追加内容:

- `InterlockingController` または `IInterlockingAtcProvider` 参照。
- 連動由来の `AtcTargetCandidate` を作るメソッド。
- `CurrentSignalAspect` に連動現示を反映。

変更の粒度:

- 既存の在線閉塞候補と同じ形で候補を 1 つ追加するだけにする。
- ATC の計算式は変更しない。

### 14.2 `TrackGraph` / `TurnoutState`

初期実装では変更不要にできます。

ただし将来、分岐器状態を Inspector で見たい場合は `TurnoutState` に次を足す可能性があります。

- lock 表示
- move 表示
- failure 表示

ただし Runtime 専用状態を ScriptableObject に置くと副作用が出やすいため、まずは `InterlockingController` 側に持つことを推奨します。

### 14.3 `BlockOccupancyManager`

初期実装では変更不要です。

ただし将来、次があると便利です。

- `bool IsBlockOccupied(string blockId, string exceptTrainId = null)`
- `bool AreBlocksClear(IEnumerable<string> blockIds, string exceptTrainId = null)`
- `IReadOnlyCollection<string> GetTrainsInBlock(string blockId)`

現在は `OccupiedTrainsByBlock` が公開されているため、連動側で直接読めます。

### 14.4 `TrackRouteTracer`

初期実装では変更不要です。

将来、進路定義の自動検証・自動生成をする場合は、次があると便利です。

- 特定の exit edge/distance まで trace する API。
- 分岐器位置を仮定して trace する API。
- 現在の `TurnoutState` に依存しない route validation API。

---

## 15. 受け入れテスト案

### 15.1 単体テスト相当

1. 空き進路を要求すると `Cleared` になる。
2. routeBlockIds に在線があると開通しない。
3. overlapBlockIds に在線があると開通しない。
4. 競合進路が開通中なら開通しない。
5. 必要分岐器が他進路で鎖錠中なら開通しない。
6. 分岐器が要求位置に転換される。
7. 開通後に分岐器が鎖錠される。
8. 進路解放後に分岐器が解錠される。

### 15.2 Play Mode テスト

1. 進路未開通のまま列車を接近させると、ATC が停止パターンを出す。
2. 進路を開通すると、ATC 停止目標が消える。
3. 列車が進路に進入すると、進路状態が `Occupied` になる。
4. 列車が進路を抜けると、進路状態が `Idle` に戻る。
5. 競合進路を同時要求しても片方しか開通しない。
6. 分岐器を反対側に要求する進路は、先行進路解放まで待たされる。

### 15.3 手動確認

1. 分岐器の見た目と `TurnoutState.selectedPosition` が一致する。
2. UI の信号現示と ATC 表示が一致する。
3. 進路開通音・信号変化・ATC ding が不自然に連発しない。

---

## 16. 実装時の注意点

### 16.1 フレーム順序

`BlockOccupancyManager` は `Update()` で在線を再構築しています。`InterlockingController` も `Update()` で状態更新する場合、実行順によって 1 フレーム古い在線情報を見る可能性があります。

対策:

- Script Execution Order で `BlockOccupancyManager` を先にする。
- または `InterlockingController.LateUpdate()` で在線情報を読む。
- 重要な進路要求時には直前に明示的な occupancy rebuild API を呼べるようにする。

初期実装では `LateUpdate()` が簡単です。

### 16.2 ScriptableObject の Runtime 書き換え

`TrackGraph` は ScriptableObject です。Play 中に `TurnoutState.selectedPosition` を更新すると、Editor 上でアセットが dirty になる可能性があります。

対策:

- Play 開始時に `TrackGraph` を Instantiate して runtime copy を使う。
- または分岐器状態も完全に Runtime 側へ移し、経路解決 API が Runtime 状態を参照できるようにする。

現状の `TrackRouteTracer` は `TrackGraph` の `TurnoutState` を参照するため、短期的には runtime copy 方式が現実的です。

### 16.3 進路データの粒度

閉塞単位だけで進路を定義すると、長い閉塞では信号位置や分岐位置とのズレが大きくなります。

短期:

- `routeBlockIds` / `overlapBlockIds` で安全側に定義する。

中期:

- signal position、entry distance、exit distance を使って停止目標を正確化する。

長期:

- track circuit / axle counter 区間、進路区分、鎖錠区分を別データにする。

---

## 17. 推奨する最初の実装単位

最初の Pull Request では、次の範囲に絞るのがよいです。

1. `Assets/Scripts/Safety/Interlocking/` を作成。
2. `InterlockingTable` と各 Definition クラスを追加。
3. `InterlockingController` を追加。
4. `RequestRoute(routeId)` と安全判定だけ実装。
5. Debug Inspector またはログで結果確認。
6. ATC 連携・進路解放は次 PR に分ける。

理由:

- データ定義と安全判定だけなら、既存の ATC や列車走行に影響しにくい。
- 進路開通条件のテストを先に固められる。
- 分岐器転換や ATC 停止目標は、データ構造が固まってから追加した方が手戻りが少ない。

---

## 18. まとめ

このプロジェクトでは、既に次の基盤が揃っています。

- `TrackGraph` による ID ベースの線路グラフ。
- `TurnoutState` / `TurnoutConnection` による分岐器の通過方向解決。
- `BlockOccupancyManager` による blockId 単位の在線管理。
- `ATCController` による停止パターン生成。
- `TrackRouteTracer` による前方/後方トレース。

したがって連動装置は、これらを置き換えるのではなく、進路・信号・分岐鎖錠を管理する新しいレイヤーとして追加するのが最適です。

最小実装では、手入力の `InterlockingTable`、`InterlockingController`、進路安全判定、分岐器鎖錠から始めます。その後、ATC 停止目標、進路解放、接近鎖錠、区分解放へ段階的に拡張すると、既存の運転・ATC 実装を壊さずに本格的な連動装置へ発展させられます。
