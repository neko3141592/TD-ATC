# VVVF フェーズ 2: 車両運動と回転運動

## この章のゴール

この章では、モーターが出したトルクが、ギアと車輪を通って列車を前に押す力になる流れを理解します。

フェーズ 0 では速度、rpm、単位を扱いました。フェーズ 1 では sin 波と三相交流を扱いました。フェーズ 2 では、電気側の話に進む前に、車両を動かす力学を固めます。

最終的に理解したい流れはこれです。

```text
モーターがトルクを出す
  -> ギアで車輪側トルクに変わる
  -> 車輪がレールを押す
  -> 牽引力が生まれる
  -> F = ma で列車が加速する
  -> 速度が変わる
```

VVVF を本格的に作るには、最終的に「インバータがどんな電圧と周波数を出したら、モーターがどれだけトルクを出すか」を計算します。ただし、まずはトルクから車両加速までを理解します。

## 1. 力とは何か

力は、物体の動きを変える原因です。

単位は `N`、ニュートンです。

```text
力が大きい
  -> 加速しやすい

質量が大きい
  -> 加速しにくい
```

列車では、力行力、ブレーキ力、走行抵抗、勾配抵抗などが出てきます。

```text
力行力      前に進める力
ブレーキ力  止める力
走行抵抗    速度に応じて進行を邪魔する力
勾配抵抗    上り坂で進行を邪魔する力
```

## 2. F = ma

車両運動の中心はこれです。

```text
F = m * a
```

意味は、

```text
力 = 質量 * 加速度
```

です。

加速度を求めたいときは、式を変形します。

```text
a = F / m
```

つまり、同じ力でも質量が重いほど加速度は小さくなります。

## 例 1: 牽引力から加速度を出す

編成質量が `280000 kg`、牽引力が `220000 N` のとき、加速度は何 `m/s^2` ですか。

```text
a = F / m
a = 220000 / 280000
a = 0.7857...
```

答え:

```text
約 0.79 m/s^2
```

鉄道では、加速度を `km/h/s` で言うこともあります。

```text
0.79 m/s^2 * 3.6 = 2.84 km/h/s
```

つまり、1 秒ごとに速度が約 `2.84 km/h` ずつ増える加速です。

## 3. 合力

列車にかかる力は 1 つではありません。

```text
牽引力  = 前向き
抵抗    = 後ろ向き
ブレーキ = 後ろ向き
```

実際に加速に使われる力は、これらを合計した力です。

```text
netForceN = tractionForceN - brakeForceN - externalForceN
```

現在のシミュレーターでも、基本的にこの考え方で速度を更新しています。

```text
acceleration = netForceN / massKg
speedMS += acceleration * deltaTime
```

## 例 2: 抵抗込みの加速度

```text
tractionForceN = 220000
externalForceN = 20000
brakeForceN = 0
massKg = 280000
```

合力:

```text
netForceN = 220000 - 0 - 20000
netForceN = 200000
```

加速度:

```text
acceleration = 200000 / 280000
acceleration = 0.714...
```

答え:

```text
約 0.71 m/s^2
```

抵抗があるので、例 1 より加速度が下がります。

## 4. トルクとは何か

トルクは「回す力」です。

単位は `Nm`、ニュートンメートルです。

```text
力 N * 半径 m = トルク Nm
```

例えば、半径 `0.5 m` の車輪の外側を `1000 N` で押すと、

```text
torqueNm = forceN * radiusM
torqueNm = 1000 * 0.5
torqueNm = 500
```

なので、`500 Nm` のトルクになります。

逆に、トルクから力を出すなら、

```text
forceN = torqueNm / radiusM
```

です。

## 5. 車輪トルクから牽引力を出す

車輪にトルクがかかると、車輪はレールを押します。その反作用で列車が前に進みます。

車輪トルクから牽引力を出す式はこれです。

```text
tractionForceN = wheelTorqueNm / wheelRadiusM
```

車輪半径が小さいほど、同じトルクでも牽引力は大きくなります。

## 例 3: 車輪トルクから牽引力

```text
wheelTorqueNm = 10000
wheelRadiusM = 0.43
```

```text
tractionForceN = 10000 / 0.43
tractionForceN = 23255.8
```

答え:

```text
約 23256 N
```

## 6. ギア比

鉄道車両では、モーターが直接車輪を回すのではなく、歯車を通します。

ギア比は、モーター回転数と車輪回転数の比です。

```text
motorRpm = wheelRpm * gearRatio
```

例えば `gearRatio = 6.5` なら、

```text
車輪が 1 回転する間に、モーターは 6.5 回転する
```

という意味です。

ギアは回転数を下げる代わりに、トルクを大きくします。

```text
wheelTorqueNm = motorTorqueNm * gearRatio * efficiency
```

`efficiency` は伝達効率です。歯車や軸受で少し損失があるので、`1.0` より少し小さい値にします。

例:

```text
efficiency = 0.92
```

なら、92% が車輪側に伝わるという意味です。

## 7. モータートルクから牽引力を出す

車輪トルクは、

```text
wheelTorqueNm = motorTorqueNm * gearRatio * efficiency
```

牽引力は、

```text
tractionForceN = wheelTorqueNm / wheelRadiusM
```

なので、まとめると、

```text
tractionForceN = motorTorqueNm * gearRatio * efficiency / wheelRadiusM
```

これが Phase 2 の最重要式です。

## 例 4: モータートルクから牽引力

```text
motorTorqueNm = 1250
gearRatio = 6.5
efficiency = 0.92
wheelRadiusM = 0.43
```

まず車輪トルク:

```text
wheelTorqueNm = 1250 * 6.5 * 0.92
wheelTorqueNm = 7475
```

牽引力:

```text
tractionForceN = 7475 / 0.43
tractionForceN = 17383.7
```

答え:

```text
約 17384 N
```

これはモーター 1 基ぶんの牽引力です。

モーターが 16 基あるなら、

```text
totalTractionForceN = 17383.7 * 16
totalTractionForceN = 278139.2
```

答え:

```text
約 278139 N
```

## 8. 牽引力から必要なモータートルクを逆算する

シミュレーターでは、逆向きの計算もよく使います。

例えば、今の `TrainSpec` は牽引力を出しています。そこから「この牽引力を出すには、モーター 1 基あたり何 Nm 必要か」を逆算できます。

基本式:

```text
tractionForceN = motorTorqueNm * gearRatio * efficiency / wheelRadiusM
```

これを `motorTorqueNm` について解きます。

```text
motorTorqueNm = tractionForceN * wheelRadiusM / gearRatio / efficiency
```

モーターが複数あるなら、1 基あたりの牽引力に分けてから計算します。

```text
tractionForcePerMotorN = totalTractionForceN / motorCount
motorTorquePerMotorNm = tractionForcePerMotorN * wheelRadiusM / gearRatio / efficiency
```

## 例 5: 既存の牽引力からモータートルクを逆算

```text
totalTractionForceN = 220000
motorCount = 16
wheelRadiusM = 0.43
gearRatio = 6.5
efficiency = 0.92
```

1 基あたりの牽引力:

```text
tractionForcePerMotorN = 220000 / 16
tractionForcePerMotorN = 13750
```

1 基あたりのモータートルク:

```text
motorTorquePerMotorNm = 13750 * 0.43 / 6.5 / 0.92
motorTorquePerMotorNm = 988.7
```

答え:

```text
約 989 Nm
```

この値が、モーターモデルを作る時の参考になります。

## 9. 角速度とトルクから出力を出す

出力は、単位時間あたりにどれだけ仕事をするかです。

単位は `W`、ワットです。

直線運動では、

```text
powerW = forceN * speedMS
```

回転運動では、

```text
powerW = torqueNm * angularSpeedRadS
```

この 2 つは対応しています。

```text
力 * 速度
トルク * 角速度
```

## 例 6: 牽引力と速度から出力

```text
tractionForceN = 220000
speedMS = 20
```

```text
powerW = 220000 * 20
powerW = 4400000
```

答え:

```text
4.4 MW
```

`MW` はメガワットです。

```text
1 MW = 1000000 W
```

## 例 7: トルクと角速度から出力

```text
motorTorqueNm = 989
motorAngularSpeedRadS = 302.3
```

```text
powerW = 989 * 302.3
powerW = 298974.7
```

答え:

```text
約 299 kW
```

`kW` はキロワットです。

```text
1 kW = 1000 W
```

## 10. rpm と rad/s の変換

フェーズ 0 で扱った式を再確認します。

```text
angularSpeedRadS = rpm * 2 * pi / 60
rpm = angularSpeedRadS * 60 / (2 * pi)
```

なぜ `2pi` と `60` が出るのか:

```text
1 回転 = 2pi rad
1 分 = 60 秒
```

だからです。

モーター rpm から角速度を出すなら、

```text
motorAngularSpeedRadS = motorRpm * 2 * pi / 60
```

になります。

## 例 8: モーター rpm から rad/s

```text
motorRpm = 2887
```

```text
motorAngularSpeedRadS = 2887 * 2 * pi / 60
motorAngularSpeedRadS = 302.3
```

答え:

```text
約 302.3 rad/s
```

## 11. なぜ高速になると牽引力が下がるのか

モーターやインバータには、出せる最大出力があります。

出力は、

```text
powerW = forceN * speedMS
```

です。

最大出力が一定なら、

```text
forceN = powerW / speedMS
```

になります。

つまり速度が上がると、同じ出力では出せる牽引力が下がります。

例:

```text
powerW = 3200000
speedMS = 10
forceN = 3200000 / 10 = 320000 N

speedMS = 20
forceN = 3200000 / 20 = 160000 N
```

速度が 2 倍になると、同じ出力で出せる力は半分になります。

これが高速域で加速が鈍くなる大きな理由です。

## 12. 力行の 3 つの領域

鉄道車両の力行は、ざっくり次のように分けられます。

```text
低速: 定加速領域
中速: 定トルク領域
高速: 定出力領域
```

### 定加速領域

加速度を一定に近づける領域です。

```text
tractionForceN = massKg * targetAccelerationMS2 + resistance
```

速度が低いので、出力にはまだ余裕があります。

### 定トルク領域

モーターが出せる最大トルクに近い領域です。

```text
tractionForceN = motorTorqueNm * gearRatio * efficiency / wheelRadiusM
```

### 定出力領域

高速になって出力上限に当たる領域です。

```text
tractionForceN = maxPowerW / speedMS
```

速度が上がるほど牽引力が下がります。

## 13. 現在の TrainSpec とのつながり

現在の `TrainSpec` には、すでにこの考え方が入っています。

代表的な値:

```text
maxTractionForceN
maxTractionPowerW
motorPowerPerUnitW
maxTargetAccelerationMS2
accelControlEndSpeedMS
torqueControlEndSpeedMS
maxMotorTorqueNm
motorTorquePerUnitNm
gearRatio
drivelineEfficiency
wheelRadiusM
```

今は `TrainSpec.GetTractionDemandForceN(...)` が、速度やノッチから牽引力を作っています。

VVVF 化では、最終的にこの牽引力を次のように作りたいです。

```text
VVVFController
  -> InverterModel
  -> MotorModel
  -> motorTorqueNm
  -> GearWheelModel
  -> tractionForceN
```

ただし、最初から置き換える必要はありません。

まずは既存の牽引力から、必要なモータートルクを逆算して表示するのが安全です。

## 14. Unity で確認するコード

まずは、速度とトルクから各値を計算するだけのクラスを作ると理解しやすいです。

```csharp
using UnityEngine;

public class TorqueMotionDebug : MonoBehaviour
{
    [Header("Vehicle")]
    [SerializeField] private float massKg = 280000f;
    [SerializeField] private float speedMS = 20f;

    [Header("Motor and Driveline")]
    [SerializeField] private float motorTorqueNm = 1250f;
    [SerializeField] private int motorCount = 16;
    [SerializeField] private float gearRatio = 6.5f;
    [SerializeField] private float drivelineEfficiency = 0.92f;
    [SerializeField] private float wheelRadiusM = 0.43f;

    private void Update()
    {
        float wheelTorquePerMotorNm = motorTorqueNm * gearRatio * drivelineEfficiency;
        float tractionForcePerMotorN = wheelTorquePerMotorNm / wheelRadiusM;
        float totalTractionForceN = tractionForcePerMotorN * motorCount;
        float accelerationMS2 = totalTractionForceN / Mathf.Max(1f, massKg);
        float powerW = totalTractionForceN * speedMS;

        Debug.Log(
            $"traction={totalTractionForceN:F0} N, " +
            $"accel={accelerationMS2:F3} m/s^2, " +
            $"power={powerW / 1000000f:F2} MW"
        );
    }
}
```

確認すること:

- `motorTorqueNm` を上げると牽引力が上がる
- `motorCount` を増やすと総牽引力が上がる
- `massKg` を増やすと加速度が下がる
- `speedMS` を上げると、同じ牽引力でも必要出力が増える

## 15. 既存牽引力からトルクを逆算するコード

既存の力行モデルが出した牽引力から、モーター 1 基あたりの必要トルクを計算します。

```csharp
using UnityEngine;

public static class TractionTorqueMath
{
    public static float GetTractionForcePerMotorN(float totalTractionForceN, int motorCount)
    {
        if (motorCount <= 0)
        {
            return 0f;
        }

        return Mathf.Max(0f, totalTractionForceN) / motorCount;
    }

    public static float GetMotorTorqueFromTractionForceNm(
        float tractionForcePerMotorN,
        float wheelRadiusM,
        float gearRatio,
        float efficiency
    )
    {
        float safeGearRatio = Mathf.Max(0.01f, gearRatio);
        float safeEfficiency = Mathf.Max(0.01f, efficiency);
        return tractionForcePerMotorN * wheelRadiusM / safeGearRatio / safeEfficiency;
    }

    public static float GetTractionForceFromMotorTorqueN(
        float motorTorqueNm,
        float wheelRadiusM,
        float gearRatio,
        float efficiency
    )
    {
        float safeWheelRadiusM = Mathf.Max(0.01f, wheelRadiusM);
        return motorTorqueNm * gearRatio * efficiency / safeWheelRadiusM;
    }
}
```

このような小さい計算クラスを作っておくと、あとで `MotorModel` や `GearWheelModel` に発展させやすいです。

## 16. 練習問題

### 問題 1

質量 `300000 kg` の列車に、合力 `240000 N` がかかっています。加速度は何 `m/s^2` ですか。

解き方:

```text
a = F / m
a = 240000 / 300000
a = 0.8
```

答え:

```text
0.8 m/s^2
```

### 問題 2

車輪半径 `0.43 m`、車輪トルク `8600 Nm` のとき、牽引力は何 N ですか。

解き方:

```text
tractionForceN = wheelTorqueNm / wheelRadiusM
tractionForceN = 8600 / 0.43
tractionForceN = 20000
```

答え:

```text
20000 N
```

### 問題 3

モータートルク `1000 Nm`、ギア比 `6.5`、効率 `0.92`、車輪半径 `0.43 m` のとき、モーター 1 基あたりの牽引力は何 N ですか。

解き方:

```text
tractionForceN = motorTorqueNm * gearRatio * efficiency / wheelRadiusM
tractionForceN = 1000 * 6.5 * 0.92 / 0.43
tractionForceN = 13907
```

答え:

```text
約 13907 N
```

### 問題 4

牽引力 `160000 N`、速度 `20 m/s` のとき、出力は何 MW ですか。

解き方:

```text
powerW = forceN * speedMS
powerW = 160000 * 20
powerW = 3200000
```

答え:

```text
3.2 MW
```

### 問題 5

最大出力 `3200000 W`、速度 `25 m/s` のとき、出力上限から見た最大牽引力は何 N ですか。

解き方:

```text
forceN = powerW / speedMS
forceN = 3200000 / 25
forceN = 128000
```

答え:

```text
128000 N
```

## 17. この章のチェックリスト

次が説明できれば、フェーズ 2 はかなり進んでいます。

- `F = ma` の意味が分かる
- 合力から加速度を計算できる
- トルクは回す力だと分かる
- 車輪トルクから牽引力を計算できる
- ギア比で回転数とトルクが変わると分かる
- モータートルクから牽引力を計算できる
- 牽引力から必要なモータートルクを逆算できる
- `powerW = forceN * speedMS` の意味が分かる
- `powerW = torqueNm * angularSpeedRadS` の意味が分かる
- 高速になると同じ出力で出せる牽引力が下がると分かる

## 18. 次にやること

次の小目標は、現在のシミュレーターで次の値を表示することです。

```text
speedMS
wheelRpm
motorRpm
tractionForceN
motorTorquePerMotorNm
powerW
accelerationMS2
```

まずは既存の力行計算を変えずに、表示だけ追加します。

その後、`MotorSpec` と `GearWheelModel` を作ると、VVVF のモーターモデルに進みやすくなります。
