# 加減速の力ベース移行方針（Phase 1: 単一編成）

## 1. このドキュメントの目的

現状の「加速度を直接合成する方式」から、将来の

- 勾配対応
- 10両編成のM/T別合算
- TASCの精密化

に繋がる「力ベース（N）で計算する方式」へ、破綻なく段階移行する。

今回は **まず加減速のみ** を対象とする（勾配・10両合算は次段階）。

---

## 2. 現状整理（コード上の事実）

- 速度更新は `TrainController.ApplyPhysics()` で、
  - `tractionAcceleration`
  - `brakeDeceleration`
  - `coastDeceleration`
  を直接 `m/s^2` で合成している。  
  対象: `Assets/Scripts/Train/TrainController.cs`

- `TrainSpec` には既に力ベース移行に必要な値がある。
  - `massKg`
  - `maxTractionForceN`
  - `maxTractionPowerW`
  - `GetTractionForceCapN(speedMS)`
  - `GetRunningResistanceForceN(speedMS)`  
  対象: `Assets/Scripts/Core/TrainSpec.cs`

- `BrakeSystemController` は現状 `totalBrakeDecel (m/s^2)` を返す。  
  対象: `Assets/Scripts/Train/BrakeSystemController.cs`

---

## 3. Phase 1 のゴール（今回）

ゴール:

- `TrainController` の縦方向運動を `F=ma` に置き換える
- 既存UI/ATC/Notchの入出力互換を維持する
- 体感を大きく壊さず、次段（勾配・10両）へ進める

非ゴール:

- 勾配抵抗の導入（Phase 2）
- 車両ごとのM/T分離計算（Phase 3）

---

## 4. 計算モデル（Phase 1）

基本式:

`a = (F_trac - F_brake - F_resist) / m`

各項:

1. `m`
- `trainSpec.massKg`

2. `F_trac`
- ノッチ比: `powerNotchRatios`
- 速度低下係数: `powerSpeedCurve`
- 牽引上限: `GetTractionForceCapN(speedMS)`（力一定＋出力一定の上限）
- 実装式（案）:
  - `demand = maxTractionForceN * notchRatio * powerCurveMultiplier`
  - `F_trac = min(demand, tractionCap)`

3. `F_brake`
- 既存互換で当面は `BrakeSystemController.totalBrakeDecel` を流用
- `F_brake = totalBrakeDecel * massKg`

4. `F_resist`
- 常時作用として `GetRunningResistanceForceN(speedMS)` を使う
- 現状の `coastDeceleration` は互換用途として残すか、段階的に廃止

---

## 5. 変更対象（最小）

1. `TrainController.cs`
- `ApplyPhysics()` の合成ロジックを力ベースへ置換
- デバッグ用に以下を公開（UI表示可能）
  - `CurrentTractionForceN`
  - `CurrentBrakeForceN`
  - `CurrentResistanceForceN`
  - `CurrentNetForceN`

2. `TrainSpec.cs`
- 追加メソッド（推奨）
  - `GetPowerNotchRatio(int notch)`
  - `GetTractionDemandForceN(int notch, float speedMS)`
- 既存メソッドは残す（互換維持）

3. `BrakeSystemController.cs`
- 当面変更なし（`totalBrakeDecel` を使い続ける）

---

## 6. 実装手順（安全順）

1. `TrainSpec` に牽引力計算ヘルパーを追加  
2. `TrainController` にデバッグ力プロパティを追加  
3. `ApplyPhysics()` を `F=ma` 計算へ置換  
4. 既存UIが壊れていないことを確認  
5. 走行感が大きく変わったら `TrainSpec` パラメータで再調整

---

## 7. 受け入れチェック（Phase 1）

1. `P5/B0` で速度上昇し、速度が上がると加速度が自然に落ちる  
2. `B7` で減速し、`B0` に戻すと減速率が自然に低下  
3. 惰行時に速度は常に低下（発散しない）  
4. ATC介入時に高位優先ブレーキが機能する  
5. `CurrentNetForceN` の符号が挙動と一致する（加速時+ / 減速時-）

---

## 8. 次段（Phase 2/3）への接続

Phase 2（勾配）:
- `F_grade = m * g * sin(theta)` を追加
- 最初は先頭位置の勾配のみ、その後「車両別位置」で精密化

Phase 3（10両M/T合算）:
- `CarSpec`（M/T, mass, traction/brake/resistance）を作成
- `ConsistSpec` で10両配列を保持
- 各車両で `F_i` を計算し `ΣF / Σm` に置換

---

## 9. 実装時の注意

- 一気に勾配やM/T分離まで入れない（原因切り分け不能になる）
- 単位は `m, s, N, kg, m/s^2` に固定する
- まず `Update` でも可、制御が増えた段階で `FixedUpdate` へ移行検討

