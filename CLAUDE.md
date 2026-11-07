# ヒトフリ

Unity 6.3 LTS(URP)で作る見下ろし型3Dアクション。IoAプロジェクトのゲームジャム作品で、お題は「ひとつだけ」。3人チーム、開発1週間、提出先は unityroom(WebGL)。

担当ごとの指示は `CLAUDE.local.md` にある(各自が自分用を置く、Git管理外)。

## 最初に確認すること

最初の依頼に取りかかる前に、プロジェクトのルートに `CLAUDE.local.md` があるか確認する。

なければ、作業を始める前に作成を提案する。

1. 誰が使っているか(髙橋、三條、増永のどれか)を聞く
2. `Documents/ヒトフリ_チーム連携ガイド.md` の「はじめに」とその人の章から、次の項目を埋めた案を見せる
   - 担当の範囲
   - 編集してよい `Scripts/` のフォルダ
   - 持っている調整値(ScriptableObject)と Prefab、シーン
   - テスト用シーンのフォルダ(`Assets/_Project/Scenes/Dev/` の下の自分の名前。ローマ字表記は本人に確認する)
   - ブランチ名の接頭辞(`feature/名前-`)
   - 迷ったときに読む仕様書と連携ガイドの章
   - 担当で特に守ること(連携ガイドのその人の「気をつけること」から3〜5行)
3. 了承を得てからルートに作成する。断られたら、そのセッションでは再度提案せずに依頼に進む

`CLAUDE.local.md` はセッション開始時に読み込まれるので、作成後は Claude Code を開き直すよう伝える。

## 資料の場所

何を作るか、どうつなぐかは `Documents/` にある。必要になったときだけ読み、内容をここに書き写さない。

- `Documents/ヒトフリ_企画書_v3/` … 目指す体験。判断に迷ったら「気持ちよさの方針」に沿うかで決める
- `Documents/ヒトフリ_仕様書/` … 決まっていること、任せること、初期値
- `Documents/ヒトフリ_チーム連携ガイド/` … 担当の境界とイベント。**他担当と関わるコードを書く前に「基盤コード辞書」を必ず読む**
- `Documents/Unity_プロジェクトルール.md` … チーム共通のルール。Claudeに関わる所はこのファイルに抜き出してある

仕様の「決まっていること」と食い違う実装を求められたら、書く前に指摘する。

## 技術の制約

WebGL提出なので、次は使わない。提案もしない。

- VFX Graph(Particle System を使う)
- UniTask のスレッド切り替え(`SwitchToThreadPool`、`RunOnThreadPool` など)、`System.Threading` のスレッド
- R3、DIコンテナ(VContainer、Zenject など)、Addressables
- 物理エンジンによる敵の吹き飛び(Rigidbody に力を加えない。初速と摩擦で自前で動かす)

使ってよいもの: UniTask、Input System、Cinemachine、uGUI と TextMeshPro、Particle System、C#標準の `event`。

上にないパッケージやアセットを足す提案は「チームへの共有が必要」と添える。勝手に `Packages/manifest.json` を書き換えない。

## アーキテクチャの原則

- **他担当のクラスを直接参照しない:** 起きたことは `GameEvents` の `Raise〜` で流し、受け手が購読する
- **イベントは OnEnable で登録、OnDisable で必ず解除する**
- **値を書き換えるのは持ち主だけ:** 他担当の値は読むだけ
- **`Time.timeScale` を触ってよいのは `TimeController` だけ:** 止める、遅くするは `TimeController` に要求を出す
- **銘の効果は `GameContext.MeiStats` の最終値をそのまま使う:** 基本値は各自の Tuning、倍率は MeiStats。自分で銘の計算をしない
- **調整値は担当ごとの ScriptableObject に置く:** `PlayerTuning`(髙橋)、`EnemyTuning`(三條)、`FeedbackTuning` と `MeiTuning`(増永)。マジックナンバーをコードに直書きしない
- **UIは unscaled time で動かす**

## 担当

```
Assets/_Project/Scripts/
  Shared/    … 基盤コード(髙橋)
  Player/    … 髙橋
  Enemy/     … 三條
  Game/      … 三條
  Mei/       … 増永
  Feedback/  … 増永
  UI/        … 増永
```

- **自分の担当フォルダ以外のファイルは編集しない:** 必要なら変更案を文章で示し、持ち主に頼む形にする
- **`Shared/` は髙橋以外は編集しない:** 例外として増永は `SeId` と `VfxId` の列挙に項目を追加してよい。基盤コードの変更は追加を基本にし、既存のシグネチャを変えない
- **他担当の中身が未完成でも待たない:** 基盤コードの口を呼ぶ形で書き、仮の値やダミーで進める

## フォルダとファイル

- **新しいフォルダを作らない:** 必要そうなら作らずに理由を添えて提案する(作るかどうかは髙橋が決める)
- **`Assets/` 直下にファイルやフォルダを置かない:** 自作物はすべて `Assets/_Project/` の中
- **触らない場所:** `Settings/`(URP設定)、`ExternalAssets/` の中身の移動とリネーム、`Library/`、`Temp/`、`Logs/`、`UserSettings/`、`Build/`
- **`Resources/` は動的ロードが必要なときだけ使う**
- **`.unity`、`.prefab`、`.asset` を直接編集しない:** YAML を手で書き換えると壊れやすい。インスペクターでの設定手順を文章で示す
- **本番シーンは `Scenes/Main/` にあり、`GameScene` は三條だけが保存する:** 他の人は `Scenes/Dev/自分の名前/` で試す
- **`.meta` ファイルを作らない、消さない、書き換えない:** スクリプトを新規作成したら、Unityエディタに戻って meta を生成してもらう
- **Unity エディタでの動作確認はできない:** コードを書いたら、確認してほしい点(どのシーンで、何をすると、何が起きればよいか)を最後に短く添える

## 命名

| 対象 | ルール | 例 |
| --- | --- | --- |
| クラス名、ファイル名、メソッド名 | PascalCase | `PlayerController.cs`、`TakeDamage()` |
| public 変数、プロパティ | PascalCase | `MoveSpeed` |
| private 変数、ローカル変数 | camelCase(`_` や `m_` を付けない) | `currentSpeed` |
| 定数 | UPPER_SNAKE_CASE(C#の一般的な慣習と違うので注意) | `MAX_HIT_COUNT` |
| シーン、Prefab、マテリアル、アニメーション | PascalCase | `GameScene`、`PlayerRun.anim` |
| スプライト、テクスチャ | snake_case | `player_idle.png` |
| BGM、SE | snake_case とプレフィックス | `bgm_title`、`se_slash` |

## コードの書き方

- インスペクターに出す値は `[SerializeField]` の private フィールドにする。外から読む値はプロパティで出す
- `GetComponent` や `Find` を Update 内で呼ばない
- 非同期処理は UniTask かコルーチンで書き、UniTask では `destroyCancellationToken` を渡す
- コメントは日本語で、「なぜそうしたか」を書く

## Git

- ブランチの作成、コミット、プッシュ、main へのマージは頼まれたときだけ行う
- ブランチ名は `feature/名前-作業内容`(例: `feature/kapi-slash-return`)
- コミットメッセージは日本語で、何をしたかを短く書く(例: `斬り戻しの当たり判定を実装`)。「修正」「update」だけにしない
- 新規ファイルは `.meta` と一緒にコミットする
- 1つのコミットに担当外のファイルを混ぜない

## 返答の仕方

- 日本語で答える。文中にセミコロンを使わない
- 分からないこと、仕様に書かれていないことは推測で埋めず、質問するか「仕様にない」と伝える
