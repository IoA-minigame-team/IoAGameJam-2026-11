# 3種類の追跡先・ウェーブ・GameEvents形式の確認

対象は `C:/Users/sanjo/Ioa/gamejam/IoAGameJam-2026-11` です。2026-11-10に、既存ウェーブを再確認し、3種類の追跡先を実装しました。共有GameEventsの既存定義・署名は変えていません。

## 3種類は何を追うか

| 現在の見た目／役割 | 追跡先 | 初期速度 |
| --- | --- | --- |
| 元の丸い影／Shadow | 主人公 | 1.8m/s |
| 盾持ち／ShieldBearer | 主人公。主人公の方向へ盾を構えながら旋回 | 0.95m/s |
| 人狼／LampEater | 刀が刺さっており、引き寄せ範囲内なら刀。それ以外は主人公 | 2.4m/s |

人狼モデルは前回までの灯喰らい枠の見た目を継続使用しています。今回、その枠に実際の追跡先切り替えを追加しました。通常の引き寄せ範囲は刀から10mで、使うたびに `GameContext.MeiStats.LureRadiusMultiplier` を掛けます。

刀に近づいた灯喰らいは約1.05m手前で止まります。刀から1.8m以内まで到着した個体を群がり数に含めます。これらは仮の調整値で、EnemyTuningから変更できます。主人公への停止距離は0.75mです。

出現直後は約1.05秒のポップ再生を待ち、吹き飛び・踏みとどまり・落下中は追跡を行いません。地面で止まると追跡へ戻ります。自身の追跡では足場を調べ、崖から歩いて落ちないようにします。簡単な方向変更はありますが、障害物のある迷路の経路探索や群れの押し合いは今回追加していません。

## まず画面で試す

1. Unity上部の `Hitofuri > Sanjo > Open Enemy Pursuit Test` を選びます。
2. `Assets/_Project/Scenes/Dev/Sanjo/EnemyPursuitTest.unity` が開いたらGameのScaleを1xにしてPlayを押します。
3. 水色のカプセルが主人公の代役です。3体がポップした後、主人公へ近づきます。
4. `Move test player` で代役を移動させます。影と盾は新しい位置を追います。
5. `Sword -X`／`Sword +X` で白い刀の代役を置きます。範囲内の人狼だけが刀へ向かいます。ボタンの符号はワールド座標のX方向です。
6. 到着すると上部のSwarmが増えます。`Pull sword` で刀を抜くと、人数が0になり、人狼も主人公へ戻ります。
7. `Launch roster` で吹き飛びを確認できます。吹き飛んでいる間は追跡せず、止まると再開します。
8. `Start 5 waves` で、同じ追跡処理を付けた敵を既存の5ウェーブから出現させます。`Throw wave enemies (test)`で実際の落下を起こすと、WaveClearedから次へ進みます。

`GameOver test` はPlayerDamaged(0)から既存のGameOver経路を通し、移動と群がりを止めます。`Reset 3 characters` で3体の確認へ戻れます。ウェーブモードではカメラを島全体へ引き、3体の確認へ戻すと近い表示へ戻します。

このDevシーンの代役操作だけは、プレイヤー担当の代わりにGameContextを更新してSwordPlanted／SlashStartedを発行します。本番用のEnemyPursuitはGameContextを読むだけです。本番のPlayerやSharedのコードは変更していません。

## 重点確認したGameEventsの形式

すべてC#標準の `event` を使います。発行は既存の `GameEvents.Raise...`、受信は `OnEnable` で登録し `OnDisable` で解除する形です。UnityEventやSendMessageへ置き換えていません。

| 共有イベント | 型／値 | 今回の扱い |
| --- | --- | --- |
| `SwordPlanted` | `Vector3` | 灯喰らいが刀の位置を記憶して追跡先を選び直す |
| `SlashStarted` | `SlashStartedInfo` | 刀を追う状態を解除し、主人公へ戻る。群がり数も解除 |
| `SwarmCountChanged` | `int` | 到着・離脱・無効化・吹き飛び等で人数が変わった時に通知 |
| `WaveStarted`／`WaveCleared` | `int`、1始まり | 既存のウェーブ進行と同じ通知を維持 |
| `GlowChanged` | `ILaunchable, float`、割合0〜1 | 既存の蓄積通知を維持 |
| `EnemyTeetered` | `ILaunchable` | 既存の縁での踏みとどまり通知を維持 |
| `EnemyFell` | `EnemyFellInfo` | slashIdと位置を保持する既存の落下経路を維持 |
| `GameOver` | 値なし | 敵、追跡、群がり、ウェーブがそれぞれ自分の状態を停止 |

`EnemyPursuit` の実際の購読は次の形式です。

```csharp
void OnEnable()
{
    GameEvents.SwordPlanted += OnSwordPlanted;
    GameEvents.SlashStarted += OnSlashStarted;
    GameEvents.GameOver += OnGameOver;
}

void OnDisable()
{
    GameEvents.SwordPlanted -= OnSwordPlanted;
    GameEvents.SlashStarted -= OnSlashStarted;
    GameEvents.GameOver -= OnGameOver;
}
```

群がり数を使う担当は、具象の敵クラスを参照せず、次の口で受け取れます。

```csharp
void OnEnable() => GameEvents.SwarmCountChanged += OnSwarmCountChanged;
void OnDisable() => GameEvents.SwarmCountChanged -= OnSwarmCountChanged;
void OnSwarmCountChanged(int count)
{
    // 自分の担当する刀の光や自動斬り戻しの時間へ反映する
}
```

人数を決めて発行するのはEnemySwarmCounterです。刀の光の表現や自動斬り戻しの計算をこのクラスへ入れていません。

## 通知順で注意した点

- 実際のPlayerはSlashStartedを流した後でIsSwordPlantedをfalseへ変えます。受信直後に古いtrueを読み直して刀へ戻らないよう、SlashStartedを刀追跡の解除として扱います。
- 途中から出現した個体は過去のSwordPlantedを受け取れません。OnEnable時にはGameContextの現在値も読み、既に置いてある刀を選べます。
- Launchは吹き飛び開始の命令です。EnemyHitや姿勢通知を受けた側からLaunchを呼び直していません。
- 吹き飛び開始時は所属を即座に解除します。次のUpdateやヒットストップ終了まで、群がり数を残しません。
- 同じ人数を毎フレーム通知しません。無効化・再有効化でも二重登録しないよう、所属集合で集計します。
- 銘選択中のPause／HitStopはTimeControllerの状態に従い、Time.timeScaleへ直接書き込みません。

EnemyMotionEventsはB担当内の姿勢と所属の通知です。今回追加したGuardChangedとSwarmMembershipChangedもその範囲で使い、他担当への共通窓口はGameEventsのSwarmCountChanged等にまとめています。共有GameEventsに新しい署名を勝手に追加していません。

## 何をどこに入れたか

| 場所 | 追加・変更 |
| --- | --- |
| `Assets/_Project/Scripts/Enemy/EnemyPursuit.cs` | 新規。追跡先の判断、刀イベントの受信、到着・離脱、盾の構え要求 |
| `Assets/_Project/Scripts/Enemy/Enemy.cs` | 変更。追跡側から使う地上移動・旋回の口。Root移動と足場判定はEnemyが所有 |
| `Assets/_Project/Scripts/Enemy/EnemyTuning.cs` | 変更。役割、速度、停止距離、10mの引き寄せ、到着判定、ポップ待ち時間 |
| `Assets/_Project/Scripts/Enemy/EnemyMotionEvents.cs` | B担当内の構え・所属の通知を追加 |
| `Assets/_Project/Scripts/Enemy/EnemyActionAnimation.cs` | 盾の構えをイベント受信。吹き飛びの姿勢復帰後にも構えへ戻れるようにする |
| `Assets/_Project/Scripts/Game/EnemySwarmCounter.cs` | 新規。所属する人数をまとめて共有SwarmCountChangedを発行 |
| `Assets/_Project/Scripts/Game/EnemyPursuitDemo.cs` | 新規。Devシーンの代役とボタン。本番へ置かない |
| `Assets/_Project/Scripts/Enemy/EnemyPursuitAssetSetup.cs` | 新規。Unity APIによる3体と確認シーンの設定。Editor専用 |
| `Assets/_Project/Scripts/Enemy/EnemyLaunchAssetSetup.cs` | 素材再生成で追跡部品が消えないよう接続 |
| `Assets/_Project/Scripts/Game/EnemyPursuitPlayModeChecks.cs` | 新規。追跡・共有イベント・追跡付き5ウェーブのPlayモード確認。Editor専用 |
| `Assets/_Project/Scripts/Game/EnemyWavePlayModeChecks.cs` | 出現位置の検証と移動後の位置を区別。主人公回避の検証は専用の塞がった出現位置ケースで維持 |
| `Assets/_Project/Data/Tuning/ShadowEnemyTuning.asset` | 影役と1.8m/sを設定 |
| `Assets/_Project/Data/Tuning/ShieldEnemyTuning.asset` | 盾役と0.95m/sを設定 |
| `Assets/_Project/Data/Tuning/WerewolfEnemyTuning.asset` | 灯喰らい役と2.4m/sを設定 |
| `Assets/_Project/Data/Tuning/EnemyWavePlan.asset` | 古いAI未実装ラベルを更新。数・区域・ウェーブ構成は維持 |
| `Assets/_Project/Prefabs/FloatingShadowEnemy.prefab`、`FloatingShieldEnemy.prefab`、`FloatingWerewolfEnemy.prefab` | 追跡部品を追加 |
| `Assets/_Project/Prefabs/WaveGameSystem.prefab` | EnemySwarmCounterを1個追加 |
| `Assets/_Project/Scenes/Dev/Sanjo/EnemyPursuitTest.unity` | 新規の確認シーン |
| `Assets/_Project/Materials/PursuitTestPlayer.mat`、`PursuitTestSword.mat` | 代役を見分けるためのDev用材質 |
| `Documents/三條_追跡先とGameEvents形式の確認.md` | この説明書のプロジェクト内コピー |

本番InGameシーンのファイル自体は今回保存し直していません。既存のPrefab参照を通じて追跡部品と人数カウンターが反映されます。Shared／Player／Mei／Feedback／UI／Packages／ProjectSettings、既存のモデルとアニメーション素材は変更していません。新しいプロジェクトフォルダーも作っていません。

本番で主人公を追わせるには、プレイヤー担当がGameContext.PlayerTransformを登録する必要があります。現在のPlayerControllerにはその処理があります。主人公も刀も登録されていないシーンでは追跡を開始しません。

GameEvents形式の照合結果は「追跡AI-GameEvents形式の確認.json」、実測の通知ログは「追跡AI-GameEvents実測ログ.txt」、Playモードの結果と全配置・GUID・ハッシュは同じ出力フォルダーに保存します。

今回の確認は、既存ウェーブの追加前46項目・追加後46項目、追跡と共有イベントの55項目です。追跡付きの5ウェーブでも全30体の落下と、WaveStarted／WaveClearedが1〜5の順に1回ずつ届くことを確認しました。イベント形式の静的照合では、実行用7クラスの購読・解除22組が対応し、共有基盤と他担当側の90ファイルが変更されていないことを確認しています。
