# 三種同時確認と白黒の和風UI

対象プロジェクト: `C:\Users\sanjo\Ioa\gamejam\IoAGameJam-2026-11`

主人公はもともとあった `PlayerController.cs` と `PlayerHealth.cs` を使用している。移動、刀の設置、斬り戻し、接触被弾のソースと、実主人公の `PlayerIntegrationActor.prefab` は今回変更していない。

UIと三種同時確認は40項目、実主人公の通し確認は35項目、既存の5ウェーブ確認は46項目が成功した。

今回作るのは **ゲーム画面内のUI、ゲームオーバー時の操作パネル、3種類を見比べる確認シーン**。押した先のタイトル画面そのものは他担当の範囲として作っていない。

## 開き方

Unityの **Hitofuri → Sanjo → Open Three Enemy Review** を選び、PlayしてGameビューをクリックする。

場所は `Assets/_Project/Scenes/Dev/Sanjo/ThreeEnemyReview.unity`。

中央の「第一波」が消えると、影・盾持ち・灯喰らいを1体ずつ同時に出す。名前札と下の3枚の札で、追跡先、攻撃開始回数、盾の構えを見比べられる。

操作はWASDで移動、左クリックを押し続けて刀を設置し、移動後に離すと斬り戻し。ゲームパッドなら左スティックとSouthボタン。カメラをそろえ、Dで画面右、Wで画面上へ進む。

## 確認する順番

1. **追跡と自動攻撃**: 操作せず観察すると全員が主人公を追う。近距離では攻撃モーションと約1mの短い踏み込みを行う。札の「自動攻撃 ○回」が増える。
2. **刀への誘引**: 左クリックを押したまま刀を置き、主人公を移動する。灯喰らいだけが「追跡先 刀」に切り替わる。影と盾は主人公を追う。離して斬り戻すと灯喰らいも主人公へ戻る。
3. **正面ガード**: 「正面ガード配置」を押す。刀を置いたままW→D→Sで敵の上側を迂回し、右側でボタンを離す。WとSは各約0.3秒、Dは約1.7秒が目安。盾は飛ばず、後ろの影には同じ斬り戻しが当たる。
4. **通常配置へ戻す**: 「配置を戻す」を押す。固定していた盾と影も、追跡・自動攻撃を再開する。
5. **接触被弾と終了**: 「接触被弾：停止」を押して有効にする。実際のPlayerHealthが2→1→0と通知し、ゲームオーバー時の操作パネルが出る。

確認シーンは **接触被弾を停止した状態で開始**する。すぐゲームオーバーにならず観察するための設定で、画面にも明示している。通常の5ウェーブと本番InGameでは被弾を有効にする。

正面ガード配置では、盾を左向きに固定し、後ろの影も位置を固定する。方向を比較するための確認用配置で、通常AIの変更ではない。盾は最新仕様どおり正面で完全ガードし、0.2倍で飛ばす方式ではない。灯喰らいは固定配置でも通常の追跡を続ける。

刀の基本誘引範囲は10m×銘の倍率。範囲外なら灯喰らいも主人公を追う。既存の自動斬り戻しは約3秒なので、押している間に移動する。ダメージは既存の接触判定で、攻撃の特定フレームだけを当てる武器判定は追加していない。

## ゲーム画面の表示

- **第○波**: 中央に約2.2秒表示し、表示が終わってから最初の敵を出す。
- **残り数**: 島上の敵＋これから出る予定の敵。時間差出現の途中でも正しい総残数を出す。
- **補助行**: 「出現待ち ○体」または「島上の影 ○体」を表示する。
- **進行線**: 白い札の中の黒い線で残りの割合を示す。
- **一時停止**: 右上のボタンまたはEscで停止し、同じ操作で再開する。

通常5ウェーブの確認は **Hitofuri → Sanjo → Open Real Player Test**。
`Assets/_Project/Scenes/Dev/Sanjo/PlayerIntegrationTest.unity`、本番の `Assets/_Project/Scenes/Main/InGame.unity`、以前の1体確認用 `PlayerIntegrationRouteTest.unity` に同じUIを接続した。通常ゲームでは3枚の確認札や配置ボタンを出さない。

## ゲームオーバー時の操作

| ボタン | ゲーム側で行うこと |
|---|---|
| 最初から再挑戦 | シーンを作り直し、主人公・刀・敵・進行を初期化して第1波から開始 |
| このウェーブから再挑戦 | 同様に初期化し、到達した波の最初から開始。その波の全出現予定を戻す |
| タイトルへ | 他担当の遷移処理へ `GameScreenEvents.TitleRequested` を通知する |

**タイトル画面そのものは作っていない。** 現在のプロジェクトには本番タイトルシーンがないため、遷移処理が未登録の間は「タイトルへ」を無効にし、「タイトル画面は準備中」と表示する。担当者が受信処理を登録するとボタンが有効になる。

主人公の内部フィールドを書き換えて復活させず、元のPrefabを含むシーンを再読込する。被弾回数と刀の設置状態を確実に初期化する。スコアや銘の引き継ぎ方は各担当の初期化方針に合わせる。

## 何をどこへ入れたか

以下はすべて上記Unityプロジェクトからの位置。

| 配置先 | 内容 |
|---|---|
| `Assets/_Project/Scenes/Dev/Sanjo/ThreeEnemyReview.unity` | 実主人公と三種同時確認。草はこのシーン内だけ表示を抑えた |
| `Assets/_Project/Data/Tuning/ThreeEnemyReviewPlan.asset` | 1波・3種類各1体・同時出現の設定 |
| `Assets/_Project/Prefabs/InkReviewInterface.prefab` | 編集可能なuGUI。白黒の札、明朝体、円相、残数の線、開始表示、ゲームオーバー操作 |
| `Assets/_Project/Art/ShipporiMinchoB1-Medium.ttf` | 日本語フォント。OSへはインストールしない |
| `Assets/_Project/Scripts/Game/InkReviewInterface.cs` | 共有イベントの受信、表示、札とボタンの更新 |
| `Assets/_Project/Scripts/Game/InkReviewSession.cs` | 確認配置、被弾切替、ゲーム側の再挑戦 |
| `Assets/_Project/Scripts/Game/GameScreenEvents.cs` | タイトルへの操作要求を他担当へ渡すC# event |
| `Assets/_Project/Scripts/Game/InkRingGraphic.cs` | 画像ファイルに依存しない円相の装飾 |
| `Assets/_Project/Scripts/Game/InkButtonFeedback.cs` | ボタンの白黒反転 |
| `Assets/_Project/Scripts/Game/InkReviewMarker.cs` | 確認シーン内だけの敵の名前札 |
| `Assets/_Project/Scripts/Game/InkReviewAssetSetup.cs` | Unity APIでPrefab・シーンを保存するEditor専用ツール |
| `Assets/_Project/Scripts/Game/InkReviewPlayModeChecks.cs` | 実入力、イベント、残数、ゲーム側ボタンを確認するEditor専用ツール |
| `Assets/_Project/Scripts/Game/WaveController.cs` | 指定した波から新しいラウンドを始める `StartRunAtWave(int)` を追加 |

基本出現位置は影 `(-5.5,床,2.5)`、盾 `(5.5,床,2.5)`、灯喰らい `(0,床,-3.8)`。半径0の固定ポイントで同じ更新内に3体出す。正面ガード配置は主人公 `(-6,床+身長補正,0)`、盾 `(-1,床,0)`、影 `(2,床,0)`。

旧 `PlayerIntegrationHUD` は該当シーン内で無効化し、二重表示を防いだ。元のコードは残している。既存の `Scripts/UI/` 内の担当者のUIコードも変更していない。

## イベントと他担当への接続

共有 `GameEvents` と `ILaunchable` は変更していない。戦闘と進行の連携は従来どおり、OnEnableで登録、OnDisableで解除し、発行はRaiseメソッドを使う。UIが被弾や撃破を代わりに発行する処理はない。

`WaveStarted` を受けた中央表示中だけ `TimeController.RequestPause(this)` を要求し、unscaled timeで表示を進めて解除する。通常の一時停止も同じ窓口を使う。UI操作が主人公の刀操作と同時に入らないよう、ボタンを押すフレームを分けた。

既存のGameEventsにはタイトル操作の署名がないため、その要求だけを `GameScreenEvents.TitleRequested` に分けた。移動先の画面を持つ担当者は自分のクラスで次の形を接続する。

```csharp
void OnEnable()  => GameScreenEvents.TitleRequested += OnTitleRequested;
void OnDisable() => GameScreenEvents.TitleRequested -= OnTitleRequested;

void OnTitleRequested()
{
    // 画面担当が、自分のタイトルシーンへ遷移する処理を呼ぶ。
}
```

本番の画面遷移窓口が別に決まった場合も、`InkReviewSession.RequestTitle()` の呼出先だけを差し替えられる。

日本語フォントはチームへの素材追加共有が必要。[Google Fonts公式](https://github.com/google/fonts/tree/main/ofl/shipporiminchob1)から取得し、ライセンス原文を `Documents/ShipporiMinchoB1-OFL.txt` に同梱した。Packages・ProjectSettings・ビルド設定は変更していない。将来ビルドする際は、実際に使うシーンをBuild Profilesに登録する。

## 確認資料

- [ゲーム画面の画像](C:/Users/sanjo/Documents/Codex/2026-11-08/new-chat/outputs/ThreeEnemyReview-game.png)
- [確認動画](C:/Users/sanjo/Documents/Codex/2026-11-08/new-chat/outputs/ThreeEnemyReview-preview.mp4)
- [UIと三種の検証結果](C:/Users/sanjo/Documents/Codex/2026-11-08/new-chat/outputs/三種同時確認と和風UI-検証結果.txt)
- [実測したイベント](C:/Users/sanjo/Documents/Codex/2026-11-08/new-chat/outputs/三種同時確認と和風UI-実測イベント.tsv)

実入力での三種の追跡・攻撃・正面ガードと、ボタンのマウス操作を確認した。第2波へ進める検証区間だけは、敵を柵のない縁へ配置してILaunchableで実際に落とす独立した進行fixtureを使った。EnemyFellやWaveClearedの通知自体は代わりに発行していない。タイトル要求の検証でも、受信を記録するだけの確認用ハンドラを使い、移動先のタイトル画面は作っていない。


