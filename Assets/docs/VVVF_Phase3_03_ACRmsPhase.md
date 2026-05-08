# VVVF フェーズ 3-3: 交流・実効値・位相

## この章のゴール

この章では、交流電圧と交流電流を扱います。

Phase 1 で sin 波を学びました。この章では、その sin 波を電圧や電流として見ます。

```text
v = Vmax * sin(2*pi*f*t)
i = Imax * sin(2*pi*f*t)
```

VVVF では、インバータがモーターに周波数を変えられる交流を送ります。交流の基本が分かると、V/f 制御、PWM、三相交流がつながります。

## 1. 交流とは何か

交流は、向きと大きさが周期的に変わる電気です。

英語では `Alternating Current`、略して `AC` です。

```text
直流 DC:
向きが基本的に一定

交流 AC:
向きと大きさが周期的に変わる
```

電流で見ると、

```text
プラス方向に流れる
  -> 0 になる
  -> マイナス方向に流れる
  -> 0 になる
  -> またプラス方向に流れる
```

をくり返します。

## 2. マイナスの電流とは何か

電流がマイナスというのは、電流が逆向きに流れているという意味です。

```text
currentA = +10
  -> 決めた正方向に 10 A 流れている

currentA = -10
  -> 正方向とは逆向きに 10 A 流れている
```

マイナスだから電気が消えるわけではありません。

向きが逆なだけです。

## 3. 交流でも電力を取り出せる理由

交流は向きが変わりますが、電力は取り出せます。

抵抗で考えると分かりやすいです。

```text
電流が右向き
  -> 抵抗が発熱する

電流が左向き
  -> 抵抗が発熱する
```

向きが逆でも、抵抗は発熱します。

電力は、

```text
powerW = voltageV * currentA
```

です。

交流では、電圧と電流が同じタイミングで符号反転するなら、

```text
電圧がプラス、電流もプラス -> power はプラス
電圧がマイナス、電流もマイナス -> power はプラス
```

になります。

マイナス同士を掛けるとプラスなので、逆向きの半周期でも負荷にエネルギーが渡ります。

## 4. 交流電圧の式

交流電圧は、sin 波で表せます。

```text
voltageV = peakVoltageV * sin(2 * pi * frequencyHz * timeS)
```

例:

```text
peakVoltageV = 100
frequencyHz = 50
```

なら、電圧は `-100 V` から `+100 V` の間を 50 Hz で変化します。

## 5. 交流電流の式

交流電流も同じように表せます。

```text
currentA = peakCurrentA * sin(2 * pi * frequencyHz * timeS)
```

ただし、実際のモーターでは、電圧と電流が完全に同じタイミングとは限りません。

コイルがあるため、電流が遅れることがあります。

## 6. 実効値 RMS

交流は値が常に変わるので、単純に「何 V」と言いにくいです。

そこで使うのが実効値です。

実効値は、

```text
同じ抵抗を同じだけ発熱させる直流に換算した値
```

です。

sin 波の場合、

```text
rmsVoltageV = peakVoltageV / sqrt(2)
rmsCurrentA = peakCurrentA / sqrt(2)
```

です。

逆に、

```text
peakVoltageV = rmsVoltageV * sqrt(2)
peakCurrentA = rmsCurrentA * sqrt(2)
```

です。

## 例 1: 最大値から実効値

```text
peakVoltageV = 100
```

```text
rmsVoltageV = 100 / sqrt(2)
rmsVoltageV = 70.7
```

答え:

```text
約 70.7 V
```

## 7. なぜ実効値が必要か

例えば、交流の電圧は一瞬ごとに変化します。

```text
0 V
50 V
100 V
50 V
0 V
-50 V
-100 V
...
```

でも機器の性能や電力を考えるとき、毎瞬間の値だけでは扱いにくいです。

そこで、発熱や仕事量として同等な直流値を使います。

それが実効値です。

電車のモーター電流を `A` で表示するときも、多くの場合は瞬間値ではなく、実効値やそれに近い制御値を見ています。

## 8. 位相とは何か

位相は、波の進み具合です。

同じ周波数でも、電圧と電流のタイミングがずれることがあります。

```text
voltageV = Vmax * sin(angle)
currentA = Imax * sin(angle - phase)
```

`phase` があると、電流の波が電圧の波より遅れたり進んだりします。

## 9. コイルでは電流が遅れる

モーターの巻線はコイルです。

コイルには、

```text
電流の変化を邪魔する
```

性質があります。

そのため、交流をかけると電流が電圧より遅れやすくなります。

```text
電圧が先に変化する
  -> 少し遅れて電流が変化する
```

この「遅れ」が位相差です。

## 10. インダクタンス

コイルの「電流の変化を邪魔する強さ」を表す値がインダクタンスです。

単位は `H`、ヘンリーです。

周波数が高いほど、コイルは電流を流しにくくなります。

コイルの交流での流れにくさは、

```text
inductiveReactanceOhm = 2 * pi * frequencyHz * inductanceH
```

です。

記号では、

```text
XL = 2pi f L
```

です。

## 11. 周波数が上がると電流が流れにくくなる

コイルでは、

```text
XL = 2pi f L
```

なので、周波数 `f` が上がると `XL` も上がります。

つまり、

```text
高い周波数ほど、コイルには電流が流れにくい
```

です。

これが、VVVF で V/f 制御が必要になる理由の入口です。

周波数を上げるだけで電圧を上げないと、十分な電流や磁束を作りにくくなります。

## 12. 力率

交流では、電圧と電流のタイミングがずれると、単純に `V * I` が全部有効な仕事になるわけではありません。

そこで力率を使います。

```text
activePowerW = rmsVoltageV * rmsCurrentA * powerFactor
```

力率 `powerFactor` は `0` から `1` の値です。

```text
1 に近い
  -> 電圧と電流のタイミングがよく合っている

小さい
  -> 電圧と電流がずれていて、有効な電力が少ない
```

三相交流では、後で、

```text
powerW = sqrt(3) * lineVoltageV * lineCurrentA * powerFactor
```

を使います。

## 13. Unity で交流を計算する

```csharp
using UnityEngine;

public class AcWaveDebug : MonoBehaviour
{
    [SerializeField] private float rmsVoltageV = 100f;
    [SerializeField] private float rmsCurrentA = 10f;
    [SerializeField] private float frequencyHz = 50f;
    [SerializeField] private float currentLagDegrees = 30f;

    private void Update()
    {
        float angle = 2f * Mathf.PI * frequencyHz * Time.time;
        float currentLagRad = currentLagDegrees * Mathf.Deg2Rad;

        float peakVoltageV = rmsVoltageV * Mathf.Sqrt(2f);
        float peakCurrentA = rmsCurrentA * Mathf.Sqrt(2f);

        float voltageV = peakVoltageV * Mathf.Sin(angle);
        float currentA = peakCurrentA * Mathf.Sin(angle - currentLagRad);
        float instantPowerW = voltageV * currentA;

        Debug.Log($"V={voltageV:F1}, I={currentA:F1}, P={instantPowerW:F1}");
    }
}
```

確認すること:

- 電圧と電流が sin 波になる
- `currentLagDegrees` を変えると、電流のタイミングがずれる
- 瞬間電力 `instantPowerW` は時間で変化する

## 練習問題

### 問題 1

最大値 `141.4 V` の sin 波交流の実効値は何 V ですか。

```text
rms = peak / sqrt(2)
rms = 141.4 / 1.414
rms = 100
```

答え:

```text
100 V
```

### 問題 2

コイルのインダクタンス `0.01 H`、周波数 `50 Hz` のとき、リアクタンスは何 ohm ですか。

```text
XL = 2 * pi * f * L
XL = 2 * pi * 50 * 0.01
XL = 3.14
```

答え:

```text
約 3.14 ohm
```

## チェックリスト

- 交流は向きと大きさが周期的に変わる電気だと分かる
- マイナスの電流は逆向きの電流だと分かる
- 交流でも電力を取り出せる理由が分かる
- 実効値 RMS の意味が分かる
- コイルでは電流が遅れやすいと分かる
- 周波数が上がるとコイルに電流が流れにくくなると分かる
- 力率が交流電力に関係すると分かる
