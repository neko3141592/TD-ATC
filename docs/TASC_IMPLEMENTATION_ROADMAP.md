# TASC Implementation Roadmap

作成日: 2026-04-26

## 目的

このドキュメントは、TASC（定位置停止補助）を安定して実装するための専用ロードマップです。

現在の方針は、TASCをいきなり複雑な停止制御にせず、次の順で積み上げることです。

1. 停止目標までの `v_allow` パターンを作る
2. パターン速度と現在速度の偏差から、ブレーキ表で目標TASC段を決める
3. 目標TASC段へ0.2秒ごとに1段ずつ追従する
4. 停止直前だけ停止位置合わせモードへ切り替える
5. 必要なら後で必要減速度モードを追加する

`a_required = v^2 / (2d)` は停止直前で暴れやすいため、最初のTASC本実装では使わない方針にします。

## 現在の到達点

- `TASCController` は追加済み。
- `StationStopController.DistanceToStopM` から停止目標までの距離を読める。
- `ATCPatternCalculator.CalculateAllowSpeedMS()` で `v_allow` を計算できる。
- `TrainSpec.tascBrakeSubstepsPerNotch` により、TASC用の細かいブレーキ段数を定義できる。
- `NotchManager.SetTASCBrakeStep(int)` でTASC連続ブレーキ段を送れる。
- `BrakeSystemController` はTASC連続ブレーキ段を `TrainSpec.GetTascBrakeStepDeceleration()` で減速度へ変換できる。
- HUDにTASCの簡易状態を表示できる。

## 基本設計

### TASC の制御入力

TASCは次の値を入力として使います。

- 現在速度 `train.SpeedMS`
- 停止目標までの距離 `stationStop.DistanceToStopM`
- パターン計算用の想定減速度 `patternDecelerationMS2`
- TrainSpecのTASC細分化段数 `tascBrakeSubstepsPerNotch`

### TASC の出力

TASCは通常ブレーキノッチではなく、細分化されたTASC連続ブレーキ段を出します。

例:

```text
tascBrakeSubstepsPerNotch = 4

TASC step 1〜4   = B1-1〜B1-4
TASC step 5〜8   = B2-1〜B2-4
TASC step 9〜12  = B3-1〜B3-4
TASC step 25〜28 = B7-1〜B7-4
```

TASCは `NotchManager.SetTASCBrakeStep(step)` へこの連続段を送ります。

### ブレーキ減速度への変換

TASC自身は減速度をブレーキ制御装置へ直接渡しません。

流れ:

```text
TASCController
  -> NotchManager.SetTASCBrakeStep(step)

TrainController.Physics
  -> BrakeSystemController.UpdateBrake(..., useTascBrakeStep: true, tascBrakeStep: step)

BrakeSystemController
  -> TrainSpec.GetTascBrakeStepDeceleration(step)
```

これにより、手動・ATC・TASCの全てが「ノッチ/段数を出し、ブレーキ制御装置側で減速度へ変換する」設計で揃います。

## Phase 5.1: v_allow 最小TASC

目標: TASCの最小動作を安定させる。

実装内容:

- `TASCController` が停止目標距離を読む。
- `patternDecelerationMS2` を使って `v_allow` を計算する。
- 現在速度が `v_allow` を超えたらTASCブレーキ段を出す。
- TASCが作動条件外なら `SetTASCBrakeStep(0)` で解除する。

現在の基本式:

```text
v_allow = sqrt(2 * patternDecelerationMS2 * targetDistanceM)
```

実装済みの主要フィールド:

```csharp
patternDecelerationMS2
startDistanceM
tascSafetyMarginM
maxTascServiceBrakeNotch
overspeedPerTascStepKmH
```

完了条件:

- 駅接近時にTASCが `Active` になる。
- 停止目標距離が短くなるほどTASC Patternが下がる。
- 現在速度がTASC Patternを超えるとTASC Stepが出る。
- TASC Stepがブレーキ制御装置に届き、実際に制動力が出る。

## Phase 5.2: パターン偏差テーブル制御

目標: 超過速度から直接ブレーキ段を出すのをやめ、読みやすいブレーキ表で目標段を決める。

追加する考え方:

```text
speedErrorKmH = currentSpeedKmH - vAllowKmH
baseStep = patternDecelerationMS2 に近い TASC step
targetStep = speedErrorKmH ごとの表で決める
```

最初のブレーキ表:

```text
speedError < 0.5 km/h   -> 0
0.5〜1.0 km/h           -> baseStep - 2
1.0〜2.0 km/h           -> baseStep
2.0〜4.0 km/h           -> baseStep + 2
4.0〜7.0 km/h           -> baseStep + 4
7.0〜10.0 km/h          -> baseStep + 7
10.0 km/h以上           -> baseStep + 10
```

実装する関数:

```csharp
private int FindClosestTascStepForDeceleration(float decelerationMS2)
private int SolvePatternTargetBrakeStep(float currentSpeedMS, float allowSpeedMS)
private int ClampTascBrakeStep(int step)
```

`FindClosestTascStepForDeceleration()` は、`TrainSpec.GetTascBrakeStepDeceleration(step)` を全TASC段で調べ、指定減速度に一番近い段を返します。

完了条件:

- パターン超過が小さい時は弱いブレーキになる。
- パターン超過が大きい時だけ強いブレーキになる。
- TASCがB7相当まで使えるが、一気に最大段へ飛ばない。
- ブレーキの強弱は表を書き換えるだけで調整できる。

## Phase 5.3: 0.2秒ごとの段数追従

目標: 目標TASC段へ一気に飛ばず、込め・緩めを段階的に行う。

追加する状態:

```csharp
[SerializeField] private int targetTascBrakeStep;
[SerializeField, Min(0.01f)] private float stepFollowIntervalSeconds = 0.2f;
private float stepFollowTimer;
```

動き:

```text
targetStep > currentStep:
  0.2秒ごとに currentStep += 1

targetStep < currentStep:
  0.2秒ごとに currentStep -= 1

targetStep == currentStep:
  維持
```

実装する関数:

```csharp
private int MoveStepTowardTarget(int currentStep, int targetStep)
```

完了条件:

- `B0 -> B20` のような急な飛びが発生しない。
- パターンへ戻ったら、ブレーキが少しずつ弱まる。
- パターンを大きく超過している間は、0.2秒ごとに段数が増える。
- HUDで `Target Step` と `Current Step` を確認できる。

## Phase 5.4: TASC HUD 拡張

目標: TASCの判断をInspectorなしで確認できるようにする。

HUDに追加する項目:

```text
[TASC]
Status
Mode
Pattern
Target Distance
Speed Error
Base Step
Target Step
Current Step
```

追加する公開プロパティ:

```csharp
public float CurrentSpeedErrorKmH => currentSpeedErrorKmH;
public int CurrentBaseTascBrakeStep => currentBaseTascBrakeStep;
public int CurrentTargetTascBrakeStep => targetTascBrakeStep;
public string CurrentControlModeLabel => currentControlMode.ToString();
```

完了条件:

- TASCが強くなった理由がHUDだけで分かる。
- 止まらない原因が「パターンが高すぎる」「表が弱すぎる」「追従が遅すぎる」のどれか判断できる。

## Phase 5.5: Stop Align モード

目標: 停止直前だけ、パターン制御ではなく停止位置合わせ用の低速制御に切り替える。

切り替え条件:

```text
targetDistanceM <= stopAlignStartDistanceM
```

初期値:

```text
stopAlignStartDistanceM = 10m
```

基本方針:

- なるべくB0を使わない。
- B1-1〜B2程度を中心に使う。
- 過走した場合、または停止寸前だけB0を許可する。

停止直前の目標速度表:

```text
10m: 12 km/h
5m: 7 km/h
2m: 3 km/h
1m: 1.5 km/h
0.3m: 0.5 km/h
```

ブレーキ表:

```text
速度が高すぎる -> B2相当
少し高い       -> B1-3相当
ほぼ合っている -> B1-1相当
0.3m以内かつ0.8km/h以下 -> B0
過走 -> B0
```

追加する関数:

```csharp
private float BuildStopAlignTargetSpeedKmH(float remainingDistanceM)
private int SolveStopAlignTargetBrakeStep(float currentSpeedMS, float remainingDistanceM)
private int GetTascStepForServiceNotchSubstep(int serviceBrakeNotch, int substep)
```

完了条件:

- 10m未満で `Mode: StopAlign` になる。
- 停止直前にTASC Stepが暴れない。
- B0へすぐ落ちず、B1-1付近で粘る。
- 停止点を超えたらTASC指令を解除する。

## Phase 5.6: 手動検証と調整

目標: 停止性能を手動検証し、パラメータを調整する。

確認ケース:

- 40km/hから駅停止
- 60km/hから駅停止
- 80km/hから駅停止
- 100km/hから駅停止
- 低速で駅へ進入
- 速すぎる状態で駅へ進入
- 停止位置手前で止まりそうなケース
- 過走するケース

見る値:

```text
TASC Pattern
Speed Error
Base Step
Target Step
Current Step
Stop Error
Brake Decel
BC Pressure
```

調整順:

1. `patternDecelerationMS2`
2. パターン偏差テーブル
3. `stepFollowIntervalSeconds`
4. Stop Alignの距離/速度表
5. Stop Alignのブレーキ表

## Phase 5.7: 必要減速度モードの再検討

目標: 必要なら、中距離用の補助として必要減速度モードを追加する。

このフェーズは後回しにします。

理由:

- `a_required = v^2 / (2d)` は距離が短いほど急激に大きくなる。
- 停止直前に使うと、B7張り付きや急な段数変化が起きやすい。
- まずはパターン偏差テーブルとStop Alignで安定させる方が安全。

追加する場合の制約:

```text
a_required の上限を入れる
a_required の変化率制限を入れる
distance の下限を2〜5m程度にする
停止直前では使わない
```

## 実装順

次に実装する順番:

1. Phase 5.2: パターン偏差テーブル制御
2. Phase 5.3: 0.2秒ごとの段数追従
3. Phase 5.4: TASC HUD 拡張
4. Patternモードの手動確認
5. Phase 5.5: Stop Align モード
6. Stop Alignの手動確認
7. 必要なら Phase 5.7 を検討

## 最初に実装する具体タスク

次の作業では、まず以下を実装します。

- `TASCController` に `targetTascBrakeStep` を追加。
- `patternDecelerationMS2` に近い `baseStep` を計算する。
- `speedErrorKmH` からブレーキ表で `targetStep` を決める。
- `currentTascBrakeStep` を `targetStep` へ0.2秒ごとに1段ずつ近づける。
- HUDに `Speed Error / Base Step / Target Step / Current Step` を追加する。

この段階ではStop Alignモードはまだ入れません。
