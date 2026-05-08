# VVVF フェーズ 0: 高校物理前の準備

## この章のゴール

この章では、まだ高校物理を本格的に習っていなくても、VVVF シミュレーターを作るために最低限必要な「数値の見方」を身につけます。

ここで一番大事なのは、難しい公式を暗記することではありません。シミュレーター内の値を見たときに、次のように考えられるようになることです。

```text
この速度は km/h だと何キロくらいか
この車輪半径なら、今の速度で車輪は何 rpm くらいか
ギア比をかけると、モーターは何 rpm くらいか
速度が 2 倍になったら、回転数や周波数も 2 倍になるのか
出力が同じなら、速度が上がると牽引力はどうなるのか
```

VVVF の本格的な理解は、この後に出てくる三相交流、PWM、誘導電動機、V/f 制御につながります。ただし、その前に単位と変換に慣れておくとかなり楽になります。

## 1. 単位を理解する

シミュレーターでは、ただの数字だけを見ると意味が分かりません。

例えば `20` という数字だけでは、速度なのか、電圧なのか、時間なのか分かりません。

```text
20 m/s
20 km/h
20 V
20 A
20 Hz
20 rpm
```

このように、数字には必ず単位があります。

## よく使う単位

| 単位 | 読み方 | 意味 | シミュレーターでの例 |
| --- | --- | --- | --- |
| m | メートル | 距離 | 走行距離、車両長 |
| s | 秒 | 時間 | 加速時間、制御周期 |
| kg | キログラム | 質量 | 編成質量、車両質量 |
| m/s | メートル毎秒 | 速度 | Unity 内部の速度 |
| km/h | キロメートル毎時 | 速度 | 運転台の速度表示 |
| N | ニュートン | 力 | 牽引力、ブレーキ力 |
| W | ワット | 出力 | モーター出力 |
| V | ボルト | 電圧 | インバータ出力電圧 |
| A | アンペア | 電流 | モーター電流 |
| Hz | ヘルツ | 1 秒あたりの回数 | 周波数、音の高さ |
| rpm | 回毎分 | 1 分あたりの回転数 | 車輪回転数、モーター回転数 |
| rad/s | ラジアン毎秒 | 角速度 | 回転の速さ |

Unity の物理計算では、基本的に `m`, `s`, `kg` を使うと扱いやすいです。速度は `m/s`、力は `N`、出力は `W` にそろえると、式が自然につながります。

## 2. km/h と m/s の変換

鉄道の速度表示は普通 `km/h` です。しかし Unity の物理計算では `m/s` を使う方が自然です。

変換式はこれです。

```text
km/h = m/s * 3.6
m/s = km/h / 3.6
```

なぜ 3.6 なのかを確認します。

```text
1 km = 1000 m
1 h = 3600 s

1 km/h = 1000 m / 3600 s
       = 0.277... m/s

1 m/s = 3600 m / h
      = 3.6 km/h
```

## 例題 1

`speedMS = 20` のとき、速度は何 km/h ですか。

### 解き方

```text
speedKmh = speedMS * 3.6
speedKmh = 20 * 3.6
speedKmh = 72
```

答え:

```text
72 km/h
```

## 例題 2

速度 `90 km/h` は何 m/s ですか。

### 解き方

```text
speedMS = speedKmh / 3.6
speedMS = 90 / 3.6
speedMS = 25
```

答え:

```text
25 m/s
```

## 3. 速度と時間と距離

速度は「1 秒あたりにどれだけ進むか」です。

```text
distanceM = speedMS * timeS
```

例えば `20 m/s` で `10 s` 走ると、

```text
distanceM = 20 * 10
distanceM = 200
```

なので `200 m` 進みます。

Unity の現在の実装でも、速度から距離を進めています。

```csharp
float deltaDistanceM = speedMS * Time.deltaTime;
distance += deltaDistanceM;
```

`Time.deltaTime` は「前のフレームから今回のフレームまでに経った時間」です。つまり、毎フレーム少しずつ距離を足しています。

## 例題 3

速度 `15 m/s` で `8 s` 走ると、何 m 進みますか。

### 解き方

```text
distanceM = speedMS * timeS
distanceM = 15 * 8
distanceM = 120
```

答え:

```text
120 m
```

## 4. 車輪の回転数

列車が進むと、車輪が回転します。

車輪半径が分かれば、速度から車輪の回転数を計算できます。

まず、車輪の角速度を出します。

```text
wheelAngularSpeedRadS = speedMS / wheelRadiusM
```

`rad/s` は、1 秒間に何ラジアン回るかという単位です。

1 回転は `2π rad` です。

そのため、`rad/s` から `rpm` へ変換できます。

```text
wheelRpm = wheelAngularSpeedRadS * 60 / (2 * pi)
```

まとめると、

```text
wheelAngularSpeedRadS = speedMS / wheelRadiusM
wheelRpm = wheelAngularSpeedRadS * 60 / (2 * pi)
```

## 例題 4

速度 `20 m/s`、車輪半径 `0.43 m` のとき、車輪回転数は何 rpm くらいですか。

### 解き方

まず角速度を出します。

```text
wheelAngularSpeedRadS = speedMS / wheelRadiusM
wheelAngularSpeedRadS = 20 / 0.43
wheelAngularSpeedRadS = 46.51 rad/s
```

次に rpm に変換します。

```text
wheelRpm = 46.51 * 60 / (2 * pi)
wheelRpm = 2790.6 / 6.283
wheelRpm = 444.1
```

答え:

```text
約 444 rpm
```

## 5. ギア比とモーター回転数

鉄道車両では、モーターの回転がギアを通って車輪に伝わります。

ギア比が `6.5` なら、車輪が 1 回転する間にモーターは約 6.5 回転します。

```text
motorRpm = wheelRpm * gearRatio
```

## 例題 5

車輪回転数が `444 rpm`、ギア比が `6.5` のとき、モーター回転数は何 rpm ですか。

### 解き方

```text
motorRpm = wheelRpm * gearRatio
motorRpm = 444 * 6.5
motorRpm = 2886
```

答え:

```text
約 2886 rpm
```

ここまでで、速度からモーター回転数までつながりました。

```text
速度
  -> 車輪角速度
  -> 車輪 rpm
  -> モーター rpm
```

この流れは VVVF シミュレーターでかなり重要です。なぜなら、後でインバータ周波数から「同期回転数」を出し、実際のモーター回転数と比べるからです。

## 6. rpm と Hz

`rpm` は 1 分間あたりの回転数です。

`Hz` は 1 秒間あたりの回数です。

変換式はこれです。

```text
Hz = rpm / 60
rpm = Hz * 60
```

例えば `3000 rpm` は、

```text
Hz = 3000 / 60
Hz = 50
```

なので、1 秒間に 50 回転です。

## 例題 6

モーターが `2880 rpm` で回っているとき、1 秒間に何回転していますか。

### 解き方

```text
Hz = rpm / 60
Hz = 2880 / 60
Hz = 48
```

答え:

```text
48 回転/s
```

注意:

ここでの `Hz` は「機械的な回転数」です。後で出てくるインバータの `frequencyHz` は「電気的な周波数」です。モーターの極数が関係するので、機械回転数と電気周波数は必ずしも同じではありません。

## 7. 比例

比例は、片方が 2 倍になると、もう片方も 2 倍になる関係です。

車輪半径とギア比が一定なら、速度とモーター回転数は比例します。

```text
速度 10 m/s -> モーター 1443 rpm
速度 20 m/s -> モーター 2886 rpm
速度 30 m/s -> モーター 4329 rpm
```

このように、速度が 2 倍になると、モーター回転数も 2 倍になります。

## 例題 7

ある車両で、速度 `20 m/s` のときモーター回転数が `2886 rpm` でした。速度が `10 m/s` になったら、モーター回転数は何 rpm ですか。

### 解き方

速度が半分なので、回転数も半分です。

```text
motorRpm = 2886 / 2
motorRpm = 1443
```

答え:

```text
1443 rpm
```

## 8. 反比例

反比例は、片方が 2 倍になると、もう片方が半分になる関係です。

鉄道の力行で重要なのは、出力、速度、力の関係です。

```text
powerW = forceN * speedMS
```

出力 `powerW` が一定なら、

```text
forceN = powerW / speedMS
```

になります。

つまり、同じ出力なら、速度が上がるほど出せる牽引力は下がります。

これは高速域で加速が鈍くなる大きな理由の一つです。

## 例題 8

出力が `3,200,000 W`、速度が `20 m/s` のとき、牽引力は何 N ですか。

### 解き方

```text
forceN = powerW / speedMS
forceN = 3,200,000 / 20
forceN = 160,000
```

答え:

```text
160,000 N
```

## 例題 9

出力が同じ `3,200,000 W` で、速度が `40 m/s` になったら牽引力は何 N ですか。

### 解き方

```text
forceN = 3,200,000 / 40
forceN = 80,000
```

答え:

```text
80,000 N
```

速度が 2 倍になったので、牽引力は半分になりました。

## 9. グラフで見る

VVVF シミュレーターでは、グラフで見る力がかなり重要です。

### 速度とモーター回転数

速度とモーター回転数は比例します。

```text
motorRpm
^
|          /
|        /
|      /
|    /
|  /
+----------------> speedMS
```

### 速度と牽引力

出力一定の範囲では、速度が上がるほど牽引力は下がります。

```text
forceN
^
|\
| \
|  \
|   \
|    \____
+----------------> speedMS
```

実際の鉄道車両では、低速から高速までずっと同じ式ではありません。かなり大ざっぱに分けると、次のようになります。

```text
低速: 定加速または定トルクに近い
中速: トルク制限や電流制限の影響を受ける
高速: 出力一定になり、速度が上がるほど牽引力が下がる
```

この感覚が分かると、今の `TrainSpec` にある次のような値の意味が見えてきます。

```text
maxTractionForceN
maxTractionPowerW
motorPowerPerUnitW
maxMotorTorqueNm
gearRatio
wheelRadiusM
```

## 10. Unity で確認するコード例

まずは、速度から車輪 rpm とモーター rpm を計算するだけの関数を作ると良いです。

```csharp
using UnityEngine;

public static class VVVFPhase0Math
{
    public static float ConvertSpeedMSToKmh(float speedMS)
    {
        return speedMS * 3.6f;
    }

    public static float ConvertSpeedKmhToMS(float speedKmh)
    {
        return speedKmh / 3.6f;
    }

    public static float GetWheelAngularSpeedRadS(float speedMS, float wheelRadiusM)
    {
        if (wheelRadiusM <= 0f)
        {
            return 0f;
        }

        return speedMS / wheelRadiusM;
    }

    public static float ConvertRadSToRpm(float angularSpeedRadS)
    {
        return angularSpeedRadS * 60f / (2f * Mathf.PI);
    }

    public static float GetWheelRpm(float speedMS, float wheelRadiusM)
    {
        float wheelAngularSpeedRadS = GetWheelAngularSpeedRadS(speedMS, wheelRadiusM);
        return ConvertRadSToRpm(wheelAngularSpeedRadS);
    }

    public static float GetMotorRpm(float speedMS, float wheelRadiusM, float gearRatio)
    {
        float wheelRpm = GetWheelRpm(speedMS, wheelRadiusM);
        return wheelRpm * gearRatio;
    }
}
```

## 11. 実装で確認したい値

最初は、次の値を Debug.Log や HUD に出すだけで十分です。

```text
speedMS
speedKmh
wheelRpm
motorRpm
```

例:

```csharp
float speedKmh = VVVFPhase0Math.ConvertSpeedMSToKmh(speedMS);
float wheelRpm = VVVFPhase0Math.GetWheelRpm(speedMS, trainSpec.wheelRadiusM);
float motorRpm = VVVFPhase0Math.GetMotorRpm(speedMS, trainSpec.wheelRadiusM, trainSpec.gearRatio);

Debug.Log($"speed={speedKmh:F1} km/h, wheel={wheelRpm:F0} rpm, motor={motorRpm:F0} rpm");
```

速度を上げたとき、表示が次のようになれば正しく進んでいます。

```text
速度が上がる
  -> speedKmh が上がる
  -> wheelRpm が上がる
  -> motorRpm が上がる
```

## 練習問題

### 問題 1

`12.5 m/s` は何 km/h ですか。

### 問題 2

`108 km/h` は何 m/s ですか。

### 問題 3

速度 `25 m/s` で `6 s` 走ると、何 m 進みますか。

### 問題 4

速度 `18 m/s`、車輪半径 `0.45 m` のとき、車輪の角速度は何 rad/s ですか。

### 問題 5

問題 4 の角速度を rpm に直すと、約何 rpm ですか。

### 問題 6

車輪回転数 `382 rpm`、ギア比 `6.06` のとき、モーター回転数は約何 rpm ですか。

### 問題 7

出力 `2,400,000 W`、速度 `20 m/s` のとき、牽引力は何 N ですか。

### 問題 8

出力 `2,400,000 W` のまま速度が `30 m/s` になったら、牽引力は何 N ですか。

### 問題 9

ある車両で、速度 `15 m/s` のときモーター回転数が `2100 rpm` でした。速度 `30 m/s` では何 rpm ですか。

### 問題 10

モーターが `3600 rpm` で回っているとき、1 秒間に何回転していますか。

## 解答

### 解答 1

```text
12.5 * 3.6 = 45
```

答え:

```text
45 km/h
```

### 解答 2

```text
108 / 3.6 = 30
```

答え:

```text
30 m/s
```

### 解答 3

```text
25 * 6 = 150
```

答え:

```text
150 m
```

### 解答 4

```text
18 / 0.45 = 40
```

答え:

```text
40 rad/s
```

### 解答 5

```text
40 * 60 / (2 * pi)
= 2400 / 6.283
= 382
```

答え:

```text
約 382 rpm
```

### 解答 6

```text
382 * 6.06 = 2314.92
```

答え:

```text
約 2315 rpm
```

### 解答 7

```text
2,400,000 / 20 = 120,000
```

答え:

```text
120,000 N
```

### 解答 8

```text
2,400,000 / 30 = 80,000
```

答え:

```text
80,000 N
```

### 解答 9

速度が 2 倍なので、回転数も 2 倍です。

```text
2100 * 2 = 4200
```

答え:

```text
4200 rpm
```

### 解答 10

```text
3600 / 60 = 60
```

答え:

```text
60 回転/s
```

## この章の到達チェック

次のことができれば、フェーズ 0 は十分です。

- `m/s` と `km/h` を変換できる
- 速度と時間から距離を計算できる
- 車輪半径から車輪 rpm を計算できる
- ギア比からモーター rpm を計算できる
- rpm と Hz を変換できる
- 比例と反比例の違いを説明できる
- 同じ出力なら、速度が上がるほど牽引力が下がると説明できる

ここまでできれば、次は「フェーズ 1: 三角関数と波」に進めます。
