# 背景・沿線オブジェクト配置ルール

## 目的

背景は「見た目の飾り」ではなく、線路・駅・トンネル・地形と同じ路線データの一部として扱う。
今後、地面、家、盛土・切土、防音壁、フェンス、トンネル、ホームを追加しても破綻しないように、配置の基準とデータの持ち方を統一する。

## 基本方針

背景配置は次の 3 種類に分ける。

1. 線路追従配置
   - 線路・中心線に沿って連続生成するもの。
   - 例: 近景の地面、盛土、切土、防音壁、フェンス、トンネル、ホーム。

2. 世界固定配置
   - ワールド座標に直接置くもの。
   - 例: 遠景の地面、山、ビル群、大きなランドマーク。

3. 散布配置
   - 指定範囲に複数の prefab を並べるもの。
   - 例: 家、電柱、樹木、小物。

原則として、運転中に近くを通過する物は線路追従配置にする。遠くに見えるだけの物だけ世界固定配置にする。

## 座標ルール

線路追従配置は、既存の `TrackRuntimeResolver` と同じ考え方で配置する。

- `s`: 基準線上の距離 m。
- `offsetM`: 基準線から左右方向への距離 m。
- `heightOffsetM`: 基準線の高さからの上下差 m。
- `startM`, `endM`: 配置区間。

左右方向は、線路進行方向を向いたときの右を正、左を負とする。

配置に使う基準は 2 種類に分ける。

- `geometryId`
  - 路線の中心線・地形帯・トンネル・防音壁など、複数線路にまたがる背景に使う。
  - 基本はこちらを優先する。

- `edgeId`
  - 特定の線路に密着する物に使う。
  - 例: ホーム端、停止目標に紐づく駅設備、線路別の標識。

## 高さルール

高さは `verticalMode` で明示する。

- `followTrack`
  - 基準線の勾配に追従する。
  - 近景の地面、盛土、切土、防音壁、フェンス、トンネル、ホームに使う。

- `constantY`
  - ワールドの `y` を一定にする。
  - 遠景の地面、水平な水面、背景用の街並みに使う。

- `profile`
  - 線路に沿って断面形状を押し出す。
  - 盛土、切土、法面、築堤、掘割、高架橋、広い地面帯に使う。

近景の地面を `constantY` だけで作らない。勾配区間でレールと地面が浮いたり埋まったりするため。

高架区間は、レール直下の道床・桁・橋脚・側壁を `followTrack` または `profile` で作る。
一方で、高架下の地表面や道路は線路に追従させず、`constantY` または別の地形データとして扱う。

## 回転ルール

線路追従配置では、基本的に線路の yaw と pitch を使う。ただし cant の roll は背景には使わない。

- レール、枕木、車両位置: cant を反映してよい。
- 地面、防音壁、フェンス、ホーム、家、トンネル壁: cant を無視する。

理由は、カントで地面や建物まで傾くと不自然になるため。

将来実装では、背景用に `SceneryFrame` を作り、次の姿勢を返すのがよい。

- `position`: 基準線上の位置。
- `forward`: 水平曲線と勾配に沿う向き。
- `right`: `forward` から作る左右方向。
- `up`: 原則 `Vector3.up`。

## データ構造

背景データは `Assets/Data/scenery.json` と `Assets/Data/scenery.schema.json` を追加して管理する。
ランタイム側は `Assets/Scripts/Scenery` 配下に置く。

最小構成は次の形にする。

```json
{
  "version": "1.0",
  "assets": [
    {
      "id": "noise_wall_2m",
      "prefabPath": "Assets/Prefabs/Scenery/NoiseWall_2m.prefab"
    }
  ],
  "placements": [
    {
      "id": "noise_wall_nt01_left",
      "type": "linearPrefab",
      "assetId": "noise_wall_2m",
      "anchor": {
        "kind": "geometry",
        "id": "NT01_NT02_CL",
        "startM": 120,
        "endM": 520
      },
      "offsetM": -5.2,
      "heightOffsetM": 0,
      "verticalMode": "followTrack",
      "spacingM": 2.0
    }
  ]
}
```

## 配置タイプ

### `meshStrip`

線路に沿って断面を押し出す連続メッシュ。

用途:
- 近景の地面
- 盛土
- 切土
- トンネル内壁
- 長い擁壁

主な項目:
- `anchor`
- `profilePoints`
- `materialId`
- `sampleIntervalM`
- `verticalMode`

### `linearPrefab`

一定間隔で prefab を並べる。

用途:
- 防音壁
- フェンス
- 架線柱の発展形
- ガードレール
- 街灯

主な項目:
- `anchor`
- `assetId`
- `spacingM`
- `offsetM`
- `heightOffsetM`
- `rotationOffsetEuler`

### `singlePrefab`

1 個だけ置く。

用途:
- トンネル坑口
- 駅舎
- ランドマーク
- 特殊な建物

主な項目:
- `anchor`
- `assetId`
- `distanceM`
- `offsetM`
- `heightOffsetM`
- `rotationOffsetEuler`

### `scatterPrefab`

範囲内に prefab を散布する。

用途:
- 家
- 樹木
- 小物

主な項目:
- `anchor`
- `assetIds`
- `startM`, `endM`
- `minOffsetM`, `maxOffsetM`
- `spacingM`
- `jitterM`
- `seed`

散布配置は必ず `seed` を持たせる。毎回ランダム結果が変わると、調整と差分管理が難しくなる。

### `stationPlatform`

ホーム専用。駅データと線路データをまたいで扱う。

用途:
- 島式ホーム
- 相対式ホーム
- ホーム端
- ホーム屋根

主な項目:
- `stationId`
- `edgeId`
- `startM`, `endM`
- `side`
- `platformHeightM`
- `platformWidthM`
- `edgeClearanceM`

ホームは単なる背景ではなく停止位置・駅設備と関係するため、汎用 `meshStrip` だけで済ませない。

### `tunnel`

トンネル専用。入口、内部、出口を 1 つの区間として扱う。

用途:
- 坑口
- トンネル内壁
- 暗転・環境音・照明切替のトリガー

主な項目:
- `anchor`
- `startM`, `endM`
- `portalAssetId`
- `liningProfile`
- `clearanceProfileId`
- `insideLightingId`

トンネルは見た目だけでなく、カメラ露出、音、明るさに影響するため専用タイプにする。

## 種類別の採用ルール

### 地面

近景は `meshStrip + geometryId + profile` にする。
遠景だけ `constantY` の大きな平面または別メッシュにする。

推奨:
- レール周辺 0-20m: 線路追従の `meshStrip`
- 20m 以遠: 世界固定の遠景地形

### 家などのオブジェクト

家は `scatterPrefab` を基本にする。
駅舎や目立つ建物だけ `singlePrefab` にする。

家は線路のすぐ横に置かず、最低でも線路中心から 8m 以上離す。
将来、地形高さが取れるようになったら `groundSnap` を追加する。

### 盛土・切土

`meshStrip` で断面を定義する。

例:
- 盛土: 線路中心から外側へ下がる断面。
- 切土: 線路中心から外側へ上がる断面。

盛土・切土は地面と衝突しやすいので、単独 prefab ではなく連続メッシュで作る。

### 高架

高架橋は `meshStrip` を基本にする。
線路に沿う桁、スラブ、側壁、防音壁、避難通路は `followTrack` または `profile` で作る。

橋脚は `linearPrefab` または `singlePrefab` で置く。
橋脚の上端は高架橋に合わせ、下端は将来の `groundSnap` または `constantY` の地表面に合わせる。

高架下の道路、地面、建物は高架橋とは別レイヤーとして扱い、線路勾配に追従させない。

### 防音壁・フェンス

基本は `linearPrefab`。
曲線が強い区間や長い壁は `meshStrip` でもよい。

配置基準は原則 `geometryId`。
線路ごとに違う位置へ置きたい場合だけ `edgeId` を使う。

### トンネル

`tunnel` タイプで扱う。
入口・内部・出口を別々の独立オブジェクトにしない。

理由:
- 区間開始・終了で照明や音を切り替える必要がある。
- 坑口と内壁の位置ずれを防ぎたい。
- 将来、閉塞感やカメラ処理を入れやすい。

### ホーム

`stationPlatform` タイプで扱う。
駅データ `stations.json` の `stationId` と、線路データの `edgeId` を結びつける。

ホームは見た目だけでなく停止位置、ドア位置、TASC、旅客扱いに関係するため、通常背景とは分ける。

## クリアランス

線路沿いの配置は、将来必ず検証できるように `clearanceProfileId` を持てる設計にする。

暫定の目安:

- 車両限界: 線路中心から左右 1.7m 以内、レール面から高さ 4.2m 以内には通常オブジェクトを置かない。
- パンタグラフ・架線空間: 線路中心付近の高さ 4.2-5.5m はトンネル・屋根・架線柱で特別扱いする。
- ホーム: 専用ルールで管理し、汎用オブジェクトとしては置かない。

数値は車両モデル確定後に調整する。重要なのは、配置データ側に検証対象を残すこと。

## 実装順

1. `SceneryPlacement` 系の JSON クラスを追加する。
2. `scenery.schema.json` を追加する。
3. `SceneryRuntimeResolver` を追加し、背景用フレームを返せるようにする。
4. `meshStrip` と `linearPrefab` だけ先に実装する。
5. 地面、防音壁、フェンスを作る。
6. `scatterPrefab` で家を置く。
7. `stationPlatform` を駅データと接続する。
8. `tunnel` を専用タイプとして追加する。

最初に実装するのは、地面用 `meshStrip` と防音壁・フェンス用 `linearPrefab` でよい。
この 2 つがあれば、沿線制作を始めながら後続タイプへ拡張できる。
