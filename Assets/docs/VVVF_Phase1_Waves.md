# VVVF フェーズ 1: 三角関数と波

## この章のゴール

この章では、VVVF の中心になる「周波数を変える」「三相交流を作る」「波が回転として見える」という感覚を身につけます。

フェーズ 0 では、速度から車輪 rpm、モーター rpm を出しました。フェーズ 1 では、そこに電気の波をつなげます。

最終的に理解したい流れはこれです。

```text
周波数 Hz を決める
  -> sin 波ができる
  -> 120 度ずらして 3 本の波を作る
  -> 三相交流になる
  -> 回転磁界のもとになる
  -> モーターの同期回転数につながる
```

この章ではまだ本格的なモーター計算はしません。まずは「波をコードで作れる」状態を目指します。

## 1. 波とは何か

波は、時間とともに値が上下するものです。

例えば、音、交流電圧、モーター電流は波として扱えます。

```text
時間が進む
  -> 値が上がる
  -> 値が下がる
  -> また同じ形をくり返す
```

この「くり返し」を表すのに `sin` を使います。

```text
wave = sin(angle)
```

`sin` は角度を入れると、`-1` から `1` の間でなめらかに変化する値を返します。

```text
angle = 0       -> sin(angle) = 0
angle = pi/2    -> sin(angle) = 1
angle = pi      -> sin(angle) = 0
angle = 3pi/2   -> sin(angle) = -1
angle = 2pi     -> sin(angle) = 0
```

つまり `0` から `2pi` まで進むと、波が 1 周します。

## 2. 角度を rad で考える

角度には `度` と `rad` があります。

```text
360 度 = 2pi rad
180 度 = pi rad
90 度 = pi/2 rad
120 度 = 2pi/3 rad
```

プログラムでは、`Mathf.Sin()` に入れる角度は `rad` です。

Unity で三角関数を使うときは、基本的に rad で考えます。

```csharp
float value = Mathf.Sin(angleRad);
```

## 3. 周波数 Hz とは何か

`Hz` は「1 秒間に何回くり返すか」です。

```text
1 Hz = 1 秒間に 1 回
2 Hz = 1 秒間に 2 回
10 Hz = 1 秒間に 10 回
```

周波数が高いほど、波は速く動きます。

音の場合は、周波数が高いほど音が高く聞こえます。

VVVF の場合は、周波数を変えることでモーターの回転磁界の速さを変えます。

## 4. 時間から角度を作る

`sin` に入れるのは角度です。でもシミュレーターでは、基本的に時間 `time` が進みます。

そこで、時間から角度を作ります。

```text
angle = 2 * pi * frequencyHz * time
```

なぜこうなるかを分解します。

```text
1 回転 = 2pi rad
frequencyHz = 1 秒あたりの回転回数
time = 経過時間
```

だから、

```text
2pi * frequencyHz * time
```

で「今までに何 rad 進んだか」が分かります。

## 例 1: 1 Hz の波

```text
frequencyHz = 1
time = 1

angle = 2 * pi * 1 * 1
angle = 2pi
```

`2pi rad` は 1 周なので、1 秒で 1 周します。

つまり `1 Hz` です。

## 例 2: 2 Hz の波

```text
frequencyHz = 2
time = 1

angle = 2 * pi * 2 * 1
angle = 4pi
```

`4pi rad` は 2 周なので、1 秒で 2 周します。

つまり `2 Hz` です。

## 5. sin 波を作る

時間 `time` と周波数 `frequencyHz` から、sin 波を作ります。

```csharp
float angle = 2f * Mathf.PI * frequencyHz * time;
float wave = Mathf.Sin(angle);
```

`wave` は `-1` から `1` の間で変化します。

```text
wave = 1   最大
wave = 0   中心
wave = -1  最小
```

電圧として考えるなら、例えば最大電圧をかけます。

```csharp
float voltageV = Mathf.Sin(angle) * maxVoltageV;
```

`maxVoltageV = 100` なら、`voltageV` は `-100 V` から `100 V` の間で変化します。

## 6. 位相とは何か

位相は「波の進み具合」です。

同じ周波数でも、少しずれて始まる波があります。

```text
waveA = sin(angle)
waveB = sin(angle - phase)
```

`phase` が大きいほど、波の位置がずれます。

三相交流では、このずれが重要です。

## 7. 三相交流

三相交流は、120 度ずつずれた 3 本の交流です。

```text
U 相
V 相
W 相
```

120 度は rad だと `2pi/3` です。

```text
120 度 = 2pi/3 rad
```

コードではこう書けます。

```csharp
float angle = 2f * Mathf.PI * frequencyHz * time;

float u = Mathf.Sin(angle);
float v = Mathf.Sin(angle - 2f * Mathf.PI / 3f);
float w = Mathf.Sin(angle + 2f * Mathf.PI / 3f);
```

これで、120 度ずつずれた 3 本の波ができます。

## 8. なぜ 3 本必要なのか

モーターをなめらかに回すには、固定子の中に「回る磁界」を作りたいです。

1 本の交流だけだと、磁界は強くなったり弱くなったりしますが、きれいに回転させるのは難しいです。

三相交流を使うと、3 本の波が順番に強くなったり弱くなったりするので、全体として回転しているような磁界を作れます。

かなり単純化すると、こういうイメージです。

```text
U 相が強い
  -> 少し後に V 相が強い
  -> 少し後に W 相が強い
  -> また U 相が強い
```

この順番の変化が、回転のもとになります。

## 9. 3 相の合計

理想的な三相交流では、3 本の値を足すとだいたい 0 になります。

```text
u + v + w = 0
```

例えばある瞬間に、

```text
u = 1
v = -0.5
w = -0.5
```

なら、

```text
1 + (-0.5) + (-0.5) = 0
```

になります。

これは三相交流の重要な性質です。

実装で `u + v + w` を Debug 表示して、ほぼ 0 になることを確認すると理解しやすいです。

## 10. Unity で sin 波を確認する

まずは、シーン上の GameObject に付けて Debug 表示するだけで十分です。

```csharp
using UnityEngine;

public class SineWaveDebug : MonoBehaviour
{
    [SerializeField] private float frequencyHz = 1f;

    private void Update()
    {
        float time = Time.time;
        float angle = 2f * Mathf.PI * frequencyHz * time;
        float wave = Mathf.Sin(angle);

        Debug.Log($"time={time:F2}, angle={angle:F2}, wave={wave:F3}");
    }
}
```

確認すること:

- `frequencyHz = 1` なら、約 1 秒で波が 1 周する
- `frequencyHz = 2` なら、約 1 秒で波が 2 周する
- `wave` は `-1` から `1` の間に収まる

## 11. Unity で三相交流を確認する

次に、3 相の値を出します。

```csharp
using UnityEngine;

public class ThreePhaseDebug : MonoBehaviour
{
    [SerializeField] private float frequencyHz = 1f;

    private void Update()
    {
        float time = Time.time;
        float angle = 2f * Mathf.PI * frequencyHz * time;

        float u = Mathf.Sin(angle);
        float v = Mathf.Sin(angle - 2f * Mathf.PI / 3f);
        float w = Mathf.Sin(angle + 2f * Mathf.PI / 3f);
        float sum = u + v + w;

        Debug.Log($"U={u:F3}, V={v:F3}, W={w:F3}, sum={sum:F3}");
    }
}
```

確認すること:

- `U`, `V`, `W` が同じ形でずれて動く
- `sum` がほぼ `0` になる
- 周波数を上げると、値の変化が速くなる

`sum` は完全に 0 ではなく、`0.000001` のような小さい値になることがあります。これはコンピューターの小数計算の誤差なので問題ありません。

## 12. 値を目で見えるようにする

Debug ログだけだと分かりにくいので、3 つのオブジェクトの高さで表示すると理解しやすいです。

```csharp
using UnityEngine;

public class ThreePhaseBars : MonoBehaviour
{
    [SerializeField] private float frequencyHz = 1f;
    [SerializeField] private float heightScale = 2f;
    [SerializeField] private Transform uBar;
    [SerializeField] private Transform vBar;
    [SerializeField] private Transform wBar;

    private void Update()
    {
        float angle = 2f * Mathf.PI * frequencyHz * Time.time;

        float u = Mathf.Sin(angle);
        float v = Mathf.Sin(angle - 2f * Mathf.PI / 3f);
        float w = Mathf.Sin(angle + 2f * Mathf.PI / 3f);

        SetBarHeight(uBar, u);
        SetBarHeight(vBar, v);
        SetBarHeight(wBar, w);
    }

    private void SetBarHeight(Transform bar, float value)
    {
        if (bar == null)
        {
            return;
        }

        Vector3 scale = bar.localScale;
        scale.y = 1f + Mathf.Abs(value) * heightScale;
        bar.localScale = scale;

        Vector3 position = bar.localPosition;
        position.y = value * heightScale;
        bar.localPosition = position;
    }
}
```

このコードでは、`u`, `v`, `w` の値に合わせて棒が上下します。

## 13. 周波数を時間で変える

VVVF では、周波数が固定ではありません。速度やノッチに応じて変わります。

まずは、時間とともに周波数を上げるだけの簡単なコードを作ります。

```csharp
using UnityEngine;

public class FrequencyRampDebug : MonoBehaviour
{
    [SerializeField] private float startFrequencyHz = 0.5f;
    [SerializeField] private float frequencyRisePerSecond = 0.5f;
    [SerializeField] private float maxFrequencyHz = 20f;

    private float frequencyHz;
    private float phaseRad;

    private void Start()
    {
        frequencyHz = startFrequencyHz;
    }

    private void Update()
    {
        frequencyHz = Mathf.Min(
            maxFrequencyHz,
            frequencyHz + frequencyRisePerSecond * Time.deltaTime
        );

        phaseRad += 2f * Mathf.PI * frequencyHz * Time.deltaTime;

        float wave = Mathf.Sin(phaseRad);

        Debug.Log($"frequencyHz={frequencyHz:F2}, phaseRad={phaseRad:F2}, wave={wave:F3}");
    }
}
```

ここでは `Time.time` から直接 `angle` を作らず、`phaseRad` を少しずつ増やしています。

```text
phaseRad += 2 * pi * frequencyHz * deltaTime
```

この方法は、周波数が途中で変わるシミュレーションに向いています。

## 14. Time.time と deltaTime の違い

固定周波数なら、これで十分です。

```csharp
float angle = 2f * Mathf.PI * frequencyHz * Time.time;
```

しかし周波数が変わる場合は、こちらの方が自然です。

```csharp
phaseRad += 2f * Mathf.PI * frequencyHz * Time.deltaTime;
```

理由は、周波数が時間で変わると「今この瞬間の周波数で、今回のフレーム分だけ位相を進める」必要があるからです。

VVVF シミュレーターでは、基本的に `phaseRad` を持って更新する方法を使う方が発展させやすいです。

## 15. VVVF と周波数の関係

VVVF は `Variable Voltage Variable Frequency` です。

ここでの `Frequency` は、インバータがモーターに出す交流の周波数です。

周波数が低い:

```text
三相交流がゆっくり変化する
回転磁界もゆっくり回る
モーターの同期速度が低い
```

周波数が高い:

```text
三相交流が速く変化する
回転磁界も速く回る
モーターの同期速度が高い
```

つまり、VVVF では周波数を変えることでモーターの回転速度を制御します。

## 16. 同期回転数への接続

三相交流の周波数から、回転磁界の速度を計算できます。

```text
synchronousRpm = 120 * frequencyHz / poleCount
```

`poleCount` はモーターの極数です。

例えば 4 極モーターで `frequencyHz = 50` の場合:

```text
synchronousRpm = 120 * 50 / 4
synchronousRpm = 1500 rpm
```

この式はフェーズ 5 で本格的に扱います。今は「周波数が上がると同期回転数も上がる」と分かれば十分です。

## 17. 速度と周波数をつなげる準備

フェーズ 0 で、速度からモーター rpm を出しました。

```text
wheelAngularSpeedRadS = speedMS / wheelRadiusM
wheelRpm = wheelAngularSpeedRadS * 60 / (2 * pi)
motorRpm = wheelRpm * gearRatio
```

フェーズ 1 では、周波数から同期 rpm を出せるようになります。

```text
synchronousRpm = 120 * frequencyHz / poleCount
```

この 2 つがそろうと、次に「すべり」を計算できます。

```text
slip = (synchronousRpm - motorRpm) / synchronousRpm
```

すべりは誘導電動機の重要な値です。これは後のフェーズで扱います。

## 18. 練習問題

### 問題 1

`frequencyHz = 1` のとき、1 秒で何 rad 進みますか。

答え:

```text
2pi rad
```

### 問題 2

`frequencyHz = 5` のとき、1 秒で何回波がくり返されますか。

答え:

```text
5 回
```

### 問題 3

三相交流の位相差 120 度は、rad でいくつですか。

答え:

```text
2pi/3 rad
```

### 問題 4

4 極モーターに `frequencyHz = 30` の三相交流を入れたとき、同期回転数は何 rpm ですか。

解き方:

```text
synchronousRpm = 120 * frequencyHz / poleCount
synchronousRpm = 120 * 30 / 4
synchronousRpm = 900
```

答え:

```text
900 rpm
```

### 問題 5

Unity で `Mathf.Sin()` に入れる角度の単位は何ですか。

答え:

```text
rad
```

## 19. この章のチェックリスト

次が説明できれば、フェーズ 1 はかなり進んでいます。

- `Hz` は 1 秒あたりのくり返し回数だと分かる
- `1 回転 = 2pi rad` だと分かる
- `angle = 2 * pi * frequencyHz * time` の意味が分かる
- `sin` 波を Unity で作れる
- 三相交流 `u`, `v`, `w` を作れる
- 三相交流は 120 度ずれていると分かる
- 周波数を上げると波の変化が速くなると分かる
- 三相交流の周波数がモーターの同期回転数につながると分かる

## 20. 次にやること

次の小目標は、`ThreePhaseDebug` を実際に Unity で動かして、次の値を見ることです。

```text
U
V
W
U + V + W
frequencyHz
phaseRad
```

その次に、速度から出した `motorRpm` と、周波数から出した `synchronousRpm` を同時に表示します。

この 2 つが表示できるようになると、次のフェーズで「すべり」と「誘導電動機」に入れます。
