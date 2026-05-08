# VVVF フェーズ 3-5: 電車・VVVF・電流のつながり

## この章のゴール

この章では、Phase 3 で学んだ電圧、電流、交流、磁界を、電車の VVVF シミュレーターに接続します。

ここでの目標は、次の問いに答えられることです。

```text
VVVF では電圧・電流・周波数は何をしているのか
電流計は何を表示すればいいのか
なぜ電車ごとに電流が違うのか
力行と回生で電流はどう変わるのか
```

## 1. VVVF の全体像

VVVF は `Variable Voltage Variable Frequency` です。

```text
Variable Voltage
  -> 電圧を変えられる

Variable Frequency
  -> 周波数を変えられる
```

電車では、直流電源を受けて、インバータが三相交流を作ります。

```text
架線・蓄電池などの直流
  -> 直流リンク
  -> VVVFインバータ
  -> 三相交流
  -> 主電動機
  -> 車輪
```

## 2. 周波数の役割

周波数は、三相交流の変化の速さです。

モーターでは、周波数が回転磁界の速さに関係します。

```text
周波数が低い
  -> 回転磁界がゆっくり回る
  -> モーターの目標回転数が低い

周波数が高い
  -> 回転磁界が速く回る
  -> モーターの目標回転数が高い
```

つまり周波数は、

```text
どれくらい速く回したいか
```

に関係します。

## 3. 電圧の役割

電圧は、電流を流そうとする強さであり、モーターの磁束にも関係します。

周波数を上げると、コイルの流れにくさも大きくなります。

```text
XL = 2 * pi * f * L
```

つまり、周波数を上げるだけで電圧を上げないと、十分な電流や磁束を作りにくくなります。

そこで V/f 制御では、

```text
周波数を上げる
  -> 電圧も上げる
```

ようにします。

## 4. 電流の役割

電流は、実際にモーターに流れている量です。

モーターでは、電流は次のものに関係します。

```text
トルク
発熱
インバータの負荷
電流制限
空転・滑走
回生電力
```

運転台の電流計は、瞬間的な PWM 波形そのものではなく、実効値や制御上の電流値を表示することが多いです。

シミュレーターでも、最初は `motorCurrentRmsA` や `dcCurrentA` を使うのが現実的です。

## 5. V/f 制御の入口

V/f 制御では、基本的に周波数と電圧を比例させます。

```text
voltageCommandV = ratedVoltageV * frequencyHz / baseFrequencyHz
```

ただし定格電圧を超えないようにします。

```text
voltageCommandV = min(voltageCommandV, ratedVoltageV)
```

例:

```text
ratedVoltageV = 1050
baseFrequencyHz = 80
```

```text
20 Hz -> 262.5 V
40 Hz -> 525 V
80 Hz -> 1050 V
100 Hz -> 1050 V で頭打ち
```

## 6. なぜ高速域でトルクが落ちるか

低中速では、周波数と電圧を一緒に上げられます。

```text
周波数 上がる
電圧   上がる
磁束   保ちやすい
トルク 出しやすい
```

しかし、電圧には上限があります。

定格電圧に達した後は、周波数を上げても電圧をそれ以上上げられません。

```text
周波数 上がる
電圧   頭打ち
磁束   弱くなる
トルク 落ちる
```

これが高速域で加速が鈍る理由の一つです。

Phase 2 の定出力領域ともつながります。

```text
powerW = forceN * speedMS
```

出力が上限に達すると、速度が上がるほど牽引力は下がります。

## 7. VVVF で扱う電流の種類

電車の VVVF では、電流がいくつかあります。

```text
dcCurrentA
  -> 直流リンク側の電流

phaseCurrentA
  -> U/V/W 相に流れる交流電流

motorCurrentRmsA
  -> モーター電流の実効値

groupCurrentA
  -> 1C2M や 1C4M の制御群電流

displayCurrentA
  -> 運転台の電流計表示
```

シミュレーターでは、最初から全部を厳密に分ける必要はありません。

おすすめは、

```text
motorCurrentRmsA
dcCurrentA
displayCurrentA
```

の 3 つから始めることです。

## 8. 力行時の電流

力行時は、電気エネルギーを使って列車を加速します。

```text
直流側電力
  -> インバータ
  -> モーター
  -> 機械出力
```

機械出力は、

```text
mechanicalPowerW = tractionForceN * speedMS
```

です。

効率を考えると、必要な電気入力は、

```text
electricPowerW = mechanicalPowerW / efficiency
```

直流側電流は、

```text
dcCurrentA = electricPowerW / dcVoltageV
```

になります。

低速では `speedMS` が小さいので、この式だけだと電力が小さく見えます。ただし実際には低速で大トルクを出すために大きな電流が流れます。

そのため、電流計のモデルではトルク由来の電流も考えます。

```text
motorCurrentA ≒ motorTorqueNm / torqueConstant
```

## 9. 回生時の電流

回生時は、列車の運動エネルギーを電気に戻します。

```text
列車の運動
  -> 車輪
  -> モーター
  -> インバータ
  -> 直流側へ戻る
```

回生電力は、ざっくり、

```text
regenMechanicalPowerW = brakeForceN * speedMS
```

です。

直流側へ戻る電流は、力行とは逆向きとしてマイナスにします。

```text
dcCurrentA = -regenMechanicalPowerW * efficiency / dcVoltageV
```

運転台電流計も、回生時はマイナス側に振ると分かりやすいです。

## 10. 低速で回生が弱くなる理由

停止直前では、モーターの回転が遅くなります。

回生では、モーターを発電機として使います。

回転が遅くなると、発電できる電圧や制御余裕が減り、回生が弱くなります。

そのため実車では、

```text
中高速: 回生ブレーキが効く
低速: 回生が弱くなる
停止直前: 空気ブレーキに切り替わる
```

ような動きになります。

シミュレーターでは、速度で回生力を落とします。

```text
regenScale = InverseLerp(cutOutEndSpeedMS, cutOutStartSpeedMS, speedMS)
regenForceN *= regenScale
```

## 11. 1C2M / 1C4M と電流

`1C2M` は、1つの制御装置で2つのモーターを制御することです。

`1C4M` は、1つの制御装置で4つのモーターを制御することです。

```text
1C2M:
VVVF制御群 1つ -> モーター 2つ

1C4M:
VVVF制御群 1つ -> モーター 4つ
```

制御群ごとに電流を持つと、本格化しやすいです。

```text
VVVFControlGroupState
  frequencyHz
  voltageV
  motorCurrentRmsA
  dcCurrentA
  motorCount
  torqueNm
```

編成全体の電流は、各制御群の合計として出せます。

```text
totalCurrentA = sum(groupCurrentA)
```

## 12. 電流計表示の作り方

シミュレーターでリアルに見せるには、計算した電流をそのまま表示せず、計器の遅れを入れます。

```csharp
displayCurrentA = Mathf.MoveTowards(
    displayCurrentA,
    targetCurrentA,
    responseRateAps * Time.deltaTime
);
```

こうすると、

```text
ノッチ投入
  -> 針が少し遅れて上がる

ノッチオフ
  -> 針が少し遅れて戻る

回生
  -> マイナス側へ振れる
```

ようになります。

小さな揺れを入れると、さらに計器らしくなります。

```text
displayCurrentA += smallNoiseA
```

ただし揺れを入れすぎると見にくくなるので、控えめにします。

## 13. 最初の実装モデル

Phase 3 の段階では、次の簡易モデルで十分です。

力行時:

```text
mechanicalPowerW = tractionForceN * speedMS
electricPowerW = mechanicalPowerW / efficiency
dcCurrentA = electricPowerW / dcVoltageV
```

低速トルク補正:

```text
motorCurrentA = motorTorqueNm / torqueConstant
targetCurrentA = max(dcCurrentA, motorCurrentA)
```

回生時:

```text
regenPowerW = brakeForceN * speedMS
dcCurrentA = -regenPowerW * efficiency / dcVoltageV
```

表示:

```text
displayCurrentA -> targetCurrentA に遅れて追従
```

## 14. Unity 用の計算例

```csharp
using UnityEngine;

public static class TrainCurrentEstimator
{
    public static float EstimateTractionDcCurrentA(
        float tractionForceN,
        float speedMS,
        float dcVoltageV,
        float efficiency
    )
    {
        float safeVoltageV = Mathf.Max(1f, dcVoltageV);
        float safeEfficiency = Mathf.Clamp(efficiency, 0.01f, 1f);
        float mechanicalPowerW = Mathf.Max(0f, tractionForceN) * Mathf.Max(0f, speedMS);
        float electricPowerW = mechanicalPowerW / safeEfficiency;
        return electricPowerW / safeVoltageV;
    }

    public static float EstimateRegenDcCurrentA(
        float regenBrakeForceN,
        float speedMS,
        float dcVoltageV,
        float efficiency
    )
    {
        float safeVoltageV = Mathf.Max(1f, dcVoltageV);
        float safeEfficiency = Mathf.Clamp01(efficiency);
        float regenPowerW = Mathf.Max(0f, regenBrakeForceN) * Mathf.Max(0f, speedMS);
        return -regenPowerW * safeEfficiency / safeVoltageV;
    }
}
```

この段階では、これは「表示用の推定値」です。

将来的には、モーターモデルから `motorCurrentRmsA` を出し、その値を使うようにします。

## 15. Phase 4 以降への接続

Phase 4 では PWM とインバータを扱います。

Phase 3 で理解した値は、次のようにつながります。

```text
dcVoltageV
  -> インバータの入力電圧

frequencyHz
  -> 三相交流の周波数

voltageCommandV
  -> PWM で作りたい平均電圧

motorCurrentRmsA
  -> モーター負荷、トルク、発熱

dcCurrentA
  -> 架線側または直流リンク側の電流
```

VVVF 本体の実装では、最終的に次の状態を持たせると扱いやすいです。

```text
InverterRuntimeState
  dcVoltageV
  outputFrequencyHz
  voltageCommandV
  carrierFrequencyHz
  dcCurrentA

MotorRuntimeState
  motorRpm
  synchronousRpm
  slip
  torqueNm
  motorCurrentRmsA
```

## 練習問題

### 問題 1

力行時、牽引力 `150000 N`、速度 `20 m/s` の機械出力は何 MW ですか。

```text
P = F * v
P = 150000 * 20
P = 3000000 W
```

答え:

```text
3.0 MW
```

### 問題 2

上の出力を効率 `0.9`、直流電圧 `1500 V` で供給する場合、直流側電流は約何 A ですか。

```text
electricPowerW = 3000000 / 0.9
electricPowerW = 3333333

dcCurrentA = 3333333 / 1500
dcCurrentA = 2222
```

答え:

```text
約 2222 A
```

### 問題 3

回生ブレーキ力 `100000 N`、速度 `15 m/s`、効率 `0.85`、直流電圧 `1500 V` のとき、回生電流は約何 A ですか。

```text
regenPowerW = 100000 * 15
regenPowerW = 1500000

dcCurrentA = -1500000 * 0.85 / 1500
dcCurrentA = -850
```

答え:

```text
約 -850 A
```

## チェックリスト

- VVVF は電圧と周波数を変える制御だと分かる
- 周波数は回転磁界の速さに関係すると分かる
- 電圧は電流や磁束を作るために必要だと分かる
- 電流はトルク、発熱、計器表示に関係すると分かる
- 力行時は電流を消費側として扱える
- 回生時は電流をマイナス側として扱える
- 電流計には遅れを入れるとリアルになると分かる
