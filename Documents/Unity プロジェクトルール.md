# Unity プロジェクトルール

> チームで開発する上でのフォルダ構成・命名規約・作業ルールをまとめています。
迷ったらこのページを見返してください。
> 

---

## 1. 開発環境

| 項目 | 内容 |
| --- | --- |
| Unity バージョン | **Unity 6.3 LTS**（全員必ず同じバージョンを使うこと） |
| レンダーパイプライン | **URP（Universal Render Pipeline）** |
| ビルドターゲット | **WebGL**（unityroom に提出するため） |

⚠️ バージョンが違うとプロジェクトが壊れる原因になります。Unity Hub でバージョンを確認してから開いてください。

---

## 2. フォルダ構成

`Assets/` 以下を次のように整理します。**自分で新しいフォルダを勝手に作らないように！** 必要な場合はカピに相談。

```
Assets/
├── _Project/            ← チームの成果物はすべてこの中
│   ├── Scripts/         ← C# スクリプト
│   ├── Scenes/          ← シーンファイル
│   │   ├── Main/        ← 本番用シーン（Title, Game, Result など）
│   │   └── Dev/         ← 個人テスト用シーン（自分の名前のフォルダを作る）
│   │       ├── Kapi/
│   │       ├── /
│   │       └── ...
│   ├── Prefabs/         ← Prefab
│   ├── UI/              ← UI 関連素材（スプライト、フォントなど）
│   ├── Art/             ← 2D / 3D アート素材
│   ├── Audio/           ← BGM・SE
│   ├── Animations/      ← アニメーション関連
│   ├── Materials/       ← マテリアル・シェーダー
│   └── Resources/       ← 動的ロードが必要な場合のみ使用
├── ExternalAssets/       ← アセットストアや外部素材はここに入れる
└── Settings/            ← URP 設定ファイルなど（基本触らない）
```

### ポイント

**`_Project/` と `ExternalAssets/` を分ける理由**
アセットストアからインポートした素材と自分たちが作ったものを混ぜると、何が自作で何が外部素材かわからなくなります。外部アセットは `ExternalAssets/` に入れてください。

**`Scenes/Dev/` の使い方**
新しい機能を試したいとき、本番シーンを直接いじるのではなく `Dev/自分の名前/` に テスト用シーンを作って実験してください。動作確認ができたら本番シーンに反映します。

---

## 3. 命名規約

命名規約はプロジェクト全体である対象(クラス名やpublic/privateなど)命名傾向をルールに従って統一することで、どの様な対象かを予測することができます。

### スクリプト（C#）

| 対象 | ルール | 例 |
| --- | --- | --- |
| クラス名 / ファイル名 | PascalCase | `PlayerController.cs` |
| public 変数・プロパティ | PascalCase | `MoveSpeed`, `MaxHp` |
| private / local 変数 | camelCase | `currentSpeed`, `isJumping` |
| 定数 | UPPER_SNAKE_CASE | `MAX_PLAYER_COUNT` |
| メソッド名 | PascalCase | `TakeDamage()`, `Initialize()` |

### アセット

| 対象 | ルール | 例 |
| --- | --- | --- |
| シーン | PascalCase | `TitleScene.unity`, `GameScene.unity` |
| Prefab | PascalCase | `Player.prefab`, `EnemyBullet.prefab` |
| スプライト / テクスチャ | snake_case | `player_idle.png`, `bg_forest.png` |
| BGM / SE | snake_case + プレフィックス | `bgm_title.mp3`, `se_jump.wav` |
| マテリアル | PascalCase | `PlayerMaterial.mat` |
| アニメーション | PascalCase | `PlayerRun.anim` |

### ブランチ名（Git）

```
feature/名前-作業内容
```

例：`feature/kapi-scene-manager`, `feature/tanaka-title-ui`

---

## 4. シーン管理ルール

### 本番シーンの構成（予定）

| シーン名 | 内容 |
| --- | --- |
| `TitleScene` | タイトル画面 |
| `GameScene` | メインのゲームプレイ |
| `ResultScene` | リザルト画面 |

ゲーム企画によって変わる可能性はありますが、この3つを基本とします。

### 担当制

シーンファイル（.unity）は**担当者を1人決めて、その人だけが編集する**。複数人で同じシーンを編集するとコンフリクトの原因になります。

Prefab も同様に、1つの Prefab は1人が責任を持つ形にします。

---

## 5. 作業ルール

### やること ✅

- Unity を開く前に `git pull origin main` で最新を取得する
- 作業は必ず自分のブランチで行う
- こまめにコミットする（1〜2時間に1回が目安）
- 新しくファイルを作ったら `.meta` ファイルも一緒にコミットする（`git add .` なら自動で含まれる）
- アセットストアから何かインポートしたらチームに共有する

### やらないこと 🚫

- `_Project/Scenes/Main/` の本番シーンを担当者以外が編集する
- 他の人が担当している Prefab を直接編集する（変更が必要なら担当者に依頼）
- `Settings/` フォルダ内のファイルを変更する（URP設定が壊れると全員に影響する）
- `ExternalAssets/` の中身を移動・リネームする（参照が切れる）
- Assets 直下にファイルやフォルダを直接置く

---

## 6. コミットメッセージの書き方

短くていいので「何をしたか」が伝わるように書いてください。

### 良い例 ✅

```
プレイヤーの移動処理を実装
タイトル画面のUIを配置
ジャンプSEを追加
スコア計算のバグを修正
```

### 悪い例 ❌

```
修正
update
作業中
あああ
```