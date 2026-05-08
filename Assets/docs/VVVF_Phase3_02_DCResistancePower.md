# VVVF フェーズ 3-2: 直流・抵抗・電力

## この章のゴール

この章では、直流回路、抵抗、オームの法則、電力、発熱を扱います。

VVVF の前段には、架線やバッテリーなどの直流電源があります。電車のインバータは、まず直流を受け取り、それを三相交流に変換します。

```text
直流電源
  -> インバータ
  -> 三相交流
  -> モーター
```

そのため、まず直流の基本を理解します。

## 1. 直流とは何か

直流は、向きが基本的に変わらない電気です。

英語では `Direct Current`、略して `DC` です。

```text
直流 DC:
電圧の向きが一定
電流の向きも基本的に一定
```

電池は直流です。

```text
+ 極
- 極
```

があり、つなぐと一定方向に電流が流れます。

鉄道でも、直流 1500 V や直流 750 V の路線があります。

## 2. 抵抗とは何か

抵抗は、電流の流れにくさです。

単位は `ohm`、記号は `Ω` です。

```text
抵抗が大きい
  -> 電流が流れにくい

抵抗が小さい
  -> 電流が流れやすい
```

水のたとえなら、

```text
細い管 -> 抵抗が大きい
太い管 -> 抵抗が小さい
```

です。

## 3. オームの法則

抵抗に流れる電流は、電圧と抵抗で決まります。

```text
V = I * R
```

これがオームの法則です。

変形すると、

```text
I = V / R
R = V / I
```

です。

単位:

```text
V: 電圧 [V]
I: 電流 [A]
R: 抵抗 [ohm]
```

## 例 1: 電圧と抵抗から電流

```text
voltageV = 100
resistanceOhm = 20
```

```text
currentA = voltageV / resistanceOhm
currentA = 100 / 20
currentA = 5
```

答え:

```text
5 A
```

## 4. 電力

電力は、

```text
powerW = voltageV * currentA
```

です。

オームの法則と組み合わせると、抵抗で消費される電力は次のようにも書けます。

```text
powerW = currentA * currentA * resistanceOhm
powerW = voltageV * voltageV / resistanceOhm
```

つまり、

```text
P = VI
P = I^2 R
P = V^2 / R
```

です。

特に `I^2 R` は重要です。電流が 2 倍になると、発熱は 4 倍になります。

## 5. 発熱と損失

配線や半導体やモーターの巻線には抵抗があります。

そこに電流が流れると熱が出ます。

```text
lossW = currentA * currentA * resistanceOhm
```

電流が大きいほど、損失は急に増えます。

例:

```text
R = 0.01 ohm
I = 1000 A
```

```text
lossW = 1000 * 1000 * 0.01
lossW = 10000 W
```

答え:

```text
10 kW
```

たった `0.01 ohm` でも、1000 A 流れると 10 kW の熱になります。

だから電車では、大電流を扱う装置の冷却が重要です。

## 6. なぜ電車によって電流が違うのか

電車の電流が違う理由は、必要な電力や電圧が違うからです。

基本は、

```text
powerW = voltageV * currentA
```

です。

同じ電力でも、電圧が高ければ電流は小さくなります。

```text
currentA = powerW / voltageV
```

例:

```text
powerW = 3000000 W
```

1500 V の場合:

```text
currentA = 3000000 / 1500
currentA = 2000 A
```

750 V の場合:

```text
currentA = 3000000 / 750
currentA = 4000 A
```

同じ 3 MW でも、750 V では 1500 V の約 2 倍の電流が必要です。

## 7. 電流計の数字を単純比較できない理由

電車の電流計は、何の電流を表示しているかが車両によって違います。

例えば、

```text
架線から取っている電流
インバータの直流側電流
モーターの三相交流電流
1ユニットの電流
1モーターの電流
編成全体の電流
```

があります。

同じ走りでも、

```text
1ユニット電流を表示する車両
編成全体の電流を表示する車両
```

では数字が大きく変わります。

シミュレーターで電流計を作るときは、まず「この電流計は何を表示しているのか」を決める必要があります。

## 8. 効率

電気を入れても、全部が機械出力になるわけではありません。

一部は熱や音になります。

```text
mechanicalPowerW = electricPowerW * efficiency
```

効率が `0.9` なら、

```text
electricPowerW = 1000 kW
mechanicalPowerW = 900 kW
lossW = 100 kW
```

です。

VVVF、モーター、ギアにはそれぞれ損失があります。

```text
架線電力
  -> インバータ損失
  -> モーター損失
  -> ギア損失
  -> 車輪での機械出力
```

## 9. 回生時の電力

力行時は、電気エネルギーが機械エネルギーになります。

```text
電気 -> モーター -> 車輪 -> 列車が加速
```

回生時は逆です。

```text
列車の運動 -> 車輪 -> モーター -> 電気
```

回生電力は、ざっくり次のように考えられます。

```text
regenPowerW = brakeForceN * speedMS
```

架線側に戻る電流は、

```text
regenCurrentA = -regenPowerW * efficiency / dcVoltageV
```

マイナス符号は、力行とは逆向きの電力の流れを表すために使います。

## 10. Unity での直流電力計算

```csharp
using UnityEngine;

public static class ElectricPowerMath
{
    public static float GetPowerW(float voltageV, float currentA)
    {
        return voltageV * currentA;
    }

    public static float GetCurrentA(float powerW, float voltageV)
    {
        float safeVoltageV = Mathf.Max(1f, Mathf.Abs(voltageV));
        return powerW / safeVoltageV;
    }

    public static float GetCopperLossW(float currentA, float resistanceOhm)
    {
        return currentA * currentA * Mathf.Max(0f, resistanceOhm);
    }
}
```

実装では、ゼロ除算を避けるために `Mathf.Max` を使います。

## 練習問題

### 問題 1

`100 V` を `25 ohm` の抵抗にかけると、電流は何 A ですか。

```text
I = V / R
I = 100 / 25
I = 4
```

答え:

```text
4 A
```

### 問題 2

`1500 V`、`1200 A` のとき、電力は何 MW ですか。

```text
P = V * I
P = 1500 * 1200
P = 1800000
```

答え:

```text
1.8 MW
```

### 問題 3

抵抗 `0.02 ohm` に `800 A` が流れると、損失は何 kW ですか。

```text
P = I^2 R
P = 800 * 800 * 0.02
P = 12800 W
```

答え:

```text
12.8 kW
```

## チェックリスト

- 直流は向きが基本的に一定の電気だと分かる
- 抵抗は電流の流れにくさだと分かる
- `V = I * R` を使える
- `P = V * I` を使える
- `P = I^2 R` で発熱が分かる
- 電流が大きいと損失が急に増えると分かる
- 電車の電流計は表示対象によって数字が変わると分かる
