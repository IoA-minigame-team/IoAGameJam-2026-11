#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// エディタ専用。メニュー「Hitofuri/増永/…」から、タイトル画面とリザルト画面を今開いているシーンに組み立てる。
/// 手で UI を並べる手間を省き、参照のつなぎ忘れを防ぐため。作った後は位置や色をインスペクターで自由に直してよい。
/// 得点の画面(CSceneBuilder)とは別のファイルにして、このタスクだけで直せるようにした。小さな道具が重なっているのはそのため。
/// エディタ専用のフォルダ(Editor/)が無いので、#if UNITY_EDITOR で囲んでビルドに入らないようにしている。
/// </summary>
public static class TitleResultSceneBuilder
{
    // 調整値のアセットの置き場(得点の画面と同じものを使う)
    const string TUNING_FOLDER = "Assets/_Project/Scenes/Dev/Masunaga";
    // Build Settings に入れるシーン。上から順で、最初のシーンがゲームを起動したときに開く
    static readonly string[] BUILD_SCENES =
    {
        "Assets/_Project/Scenes/Main/TitleScene.unity",
        "Assets/_Project/Scenes/Main/InGame.unity",
        "Assets/_Project/Scenes/Main/ResultScene.unity",
    };

    // タイトルの奥に置くもの。どれも髙橋さんと三條さんの Prefab を、中身を変えずにシーンへ置くだけ
    const string HERO_PREFAB = "Assets/_Project/Prefabs/Player.prefab";
    const string STAGE_PREFAB = "Assets/_Project/Prefabs/FloatingMeadowStage.prefab";
    const string POST_PREFAB = "Assets/_Project/Prefabs/PostProcessVolume.prefab";
    const string HERO_NAME = "C_TitleHero";
    const string STAGE_NAME = "C_TitleStage";
    const string POST_NAME = "C_TitlePost";
    const string TITLE_CANVAS_NAME = "C_TitleCanvas";
    // 主人公は画面の右手前に大きく立ち、少しこちらを向く。Player の原点は体の中心なので、足が地面に着く高さに置く
    static readonly Vector3 HERO_POSITION = new Vector3(0.95f, 1.08f, -1.5f);
    static readonly Vector3 HERO_EULER = new Vector3(0f, 205f, 0f);
    static readonly Vector3 TITLE_CAMERA_POSITION = new Vector3(0f, 1.25f, -3.6f);
    static readonly Vector3 TITLE_CAMERA_TARGET = new Vector3(0f, 1f, 0f);

    static readonly Color INK = new Color(0.043f, 0.043f, 0.051f, 1f);
    static readonly Color PAPER = new Color(0.925f, 0.92f, 0.9f, 1f);
    static readonly Color ASH = new Color(0.56f, 0.553f, 0.533f, 1f);
    static readonly Color GOLD = new Color(0.914f, 0.71f, 0.29f, 1f);
    static readonly Color GOLD_HI = new Color(1f, 0.886f, 0.627f, 1f);
    static readonly Color FRAME = new Color(1f, 1f, 1f, 0.14f);

    [MenuItem("Hitofuri/増永/タイトル画面を作る")]
    static void BuildTitle()
    {
        FeedbackTuning feedbackTuning = LoadOrCreate<FeedbackTuning>("FeedbackTuning");
        EnsureEventSystem();

        // 作り直せるよう、前に作ったものがあれば先に消す
        DestroyIfExists(TITLE_CANVAS_NAME);
        DestroyIfExists(HERO_NAME);
        DestroyIfExists(STAGE_NAME);
        DestroyIfExists(POST_NAME);
        Animator heroAnimator = BuildTitleBackdrop();

        Canvas canvas = NewCanvas(TITLE_CANVAS_NAME);
        // 奥の主人公が見えるよう、背景は塗らない。文字が読めるよう、左側だけ暗くする
        Image shade = Img(canvas.transform, "LeftShade", new Color(INK.r, INK.g, INK.b, 0.62f));
        Place(shade.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(1060, 1200));

        RectTransform content = Place(Rect("Content", canvas.transform), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(110, 0), new Vector2(1000, 900));
        // 主人公に重ならないよう、左側に収まる大きさにする
        content.localScale = new Vector3(0.86f, 0.86f, 1f);
        CanvasGroup contentGroup = content.gameObject.AddComponent<CanvasGroup>();

        // タイトルに出すのは、ロゴ、始める、遊び方、自己ベストだけにする。説明は「遊び方」の中へ移す
        TextMeshProUGUI logo = Text(content, "Logo", "ヒトフリ", 210, PAPER, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(-6, -200), new Vector2(1000, 240), true);
        logo.characterSpacing = 6;
        logo.gameObject.AddComponent<ZapTextEffect>();
        CanvasGroup logoGroup = logo.gameObject.AddComponent<CanvasGroup>();
        RectTransform logoSlash = Slash(content, "LogoSlash", new Vector2(-30, -330), 900, -5);

        List<CanvasGroup> reveal = new List<CanvasGroup>();
        RectTransform buttonRow = Place(Rect("Buttons", content), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -500), new Vector2(980, 90));
        Button start = NewButton(buttonRow, "StartButton", "始める", 44, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(300, 86));
        start.GetComponentInChildren<TextMeshProUGUI>().gameObject.AddComponent<ZapTextEffect>();
        Button howToOpen = NewButton(buttonRow, "HowToButton", "遊び方", 32, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(330, 0), new Vector2(220, 70));
        reveal.Add(buttonRow.gameObject.AddComponent<CanvasGroup>());
        TextMeshProUGUI best = Text(content, "Best", "まだ記録はありません。", 22, ASH, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -620), new Vector2(980, 40));
        reveal.Add(best.gameObject.AddComponent<CanvasGroup>());

        // 主人公が斬ったときに、画面の端から端へ走る斬り跡
        RectTransform screenSlash = Slash(canvas.transform, "ScreenSlash", new Vector2(-200, -330), 2500, -12);
        screenSlash.sizeDelta = new Vector2(2500, 10);

        Image flashImage = Img(canvas.transform, "Flash", GOLD_HI);
        Stretch(flashImage.rectTransform);
        CanvasGroup flash = flashImage.gameObject.AddComponent<CanvasGroup>();
        flash.blocksRaycasts = false;

        Button howToClose;
        GameObject howToPanel = BuildHowToPanel(canvas.transform, out howToClose);

        TitleScreen screen = canvas.gameObject.AddComponent<TitleScreen>();
        Wire(screen, "tuning", feedbackTuning);
        Wire(screen, "startButton", start);
        Wire(screen, "bestText", best);
        Wire(screen, "logoSlash", logoSlash);
        Wire(screen, "screenSlash", screenSlash);
        Wire(screen, "heroAnimator", heroAnimator);
        Wire(screen, "logoGroup", logoGroup);
        WireArray(screen, "revealInOrder", reveal.ToArray());
        Wire(screen, "flash", flash);
        Wire(screen, "content", contentGroup);
        Wire(screen, "howToButton", howToOpen);
        Wire(screen, "howToPanel", howToPanel);
        Wire(screen, "howToCloseButton", howToClose);

        Selection.activeGameObject = canvas.gameObject;
        CheckFont();
        Debug.Log("[TitleResultSceneBuilder] タイトル画面を作った。主人公の位置と向きは C_TitleHero を、カメラは Main Camera を動かして直せる");
    }

    /// <summary>遊び方の欄。タイトルの「遊び方」で開き、「閉じる」で戻る。閉じた状態で保存する</summary>
    static GameObject BuildHowToPanel(Transform canvas, out Button close)
    {
        // 暗い幕はクリックを受け止めて、後ろのボタンに通さない
        Image dim = Img(canvas, "HowToPanel", new Color(INK.r, INK.g, INK.b, 0.92f));
        dim.raycastTarget = true;
        Stretch(dim.rectTransform);
        RectTransform box = Place(Rect("Box", dim.transform), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 760));

        TextMeshProUGUI head = Text(box, "Head", "遊び方", 64, PAPER, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(600, 80), true);
        head.characterSpacing = 16;
        head.gameObject.AddComponent<ZapTextEffect>();
        Text(box, "Tagline", "刀は一本だけ。突き立てて、離れて、斬り戻す。\nその一本の線が、群れを島の外へ弾き飛ばす。", 30, PAPER, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -120), new Vector2(1100, 100));

        string[,] steps = { { "押す", "突き立てる", "その場に刀を置く" }, { "押したまま", "手ぶらで走る", "速く走れるが、斬れない" }, { "離す", "斬り戻す", "刀へ飛び、ここまで一直線に斬り抜ける" } };
        for (int i = 0; i < 3; i++)
        {
            float x = i * 370f;
            Text(box, "Key" + i, steps[i, 0], 22, GOLD_HI, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -260), new Vector2(340, 30), true);
            TextMeshProUGUI verb = Text(box, "Verb" + i, steps[i, 1], 38, PAPER, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -294), new Vector2(340, 50), true);
            verb.gameObject.AddComponent<ZapTextEffect>();
            Text(box, "Desc" + i, steps[i, 2], 22, ASH, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -352), new Vector2(340, 60));
        }
        Text(box, "Move", "移動は WASD か 左スティック。ボタンは左クリック・Space・Aボタンのどれでも同じ。\n敵は斬っても倒れない。島の外へ落とすと倒せる。一振りで落とした最多の数が記録になる。", 24, ASH, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -470), new Vector2(1100, 100));

        close = NewButton(box, "CloseButton", "閉じる", 32, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(240, 72));
        dim.gameObject.SetActive(false);
        return dim.gameObject;
    }

    /// <summary>
    /// タイトルの奥の景色(足場、主人公、刀が光って見えるための後処理)を置き、主人公の Animator を返す。
    /// Prefab が見つからないものは飛ばす(その場合も画面は作れる)
    /// </summary>
    static Animator BuildTitleBackdrop()
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = TITLE_CAMERA_POSITION;
            camera.transform.LookAt(TITLE_CAMERA_TARGET);
            // 夜の島に見えるよう、空は墨色にする
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = INK;
            // 刀の光(Bloom)が出るよう、カメラの後処理をオンにする
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            // コードで変えた値がシーンに保存されるよう、変えたことを知らせる
            EditorUtility.SetDirty(camera.transform);
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(cameraData);
        }

        PlacePrefab(STAGE_PREFAB, STAGE_NAME, Vector3.zero, Vector3.zero);
        PlacePrefab(POST_PREFAB, POST_NAME, Vector3.zero, Vector3.zero);

        GameObject hero = PlacePrefab(HERO_PREFAB, HERO_NAME, HERO_POSITION, HERO_EULER);
        if (hero == null) return null;
        // タイトルでは見た目だけ使う。操作、被弾、当たり判定のスクリプトは切る(Prefab は変えず、このシーンの中だけの設定)
        foreach (MonoBehaviour behaviour in hero.GetComponents<MonoBehaviour>()) SetSerializedBool(behaviour, "m_Enabled", false);
        CharacterController body = hero.GetComponent<CharacterController>();
        if (body != null) SetSerializedBool(body, "m_Enabled", false);
        // 斬り戻しの線(狙いの線)は、操作のスクリプトを切ると出たままになるので隠す
        Transform slashLine = hero.transform.Find("SlashLine");
        if (slashLine != null) SetSerializedBool(slashLine.gameObject, "m_IsActive", false);
        return hero.GetComponentInChildren<Animator>();
    }

    static GameObject PlacePrefab(string path, string name, Vector3 position, Vector3 euler)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[TitleResultSceneBuilder] {path} が見つからないので置かなかった");
            return null;
        }
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.name = name;
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(euler);
        // Prefab から置いたものは、変えた値を「このシーンだけの上書き」として記録しないと保存されない
        PrefabUtility.RecordPrefabInstancePropertyModifications(go);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        return go;
    }

    // インスペクターのチェックを外すのと同じ変更を、コードから入れる(Prefab から置いたものでも上書きとして保存される)
    static void SetSerializedBool(Object target, string property, bool value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(property);
        if (p == null) return;
        p.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void DestroyIfExists(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) Undo.DestroyObjectImmediate(go);
    }

    [MenuItem("Hitofuri/増永/リザルト画面を作る")]
    static void BuildResult()
    {
        FeedbackTuning feedbackTuning = LoadOrCreate<FeedbackTuning>("FeedbackTuning");
        EnsureEventSystem();

        Canvas canvas = NewCanvas("C_ResultCanvas");
        Stretch(Img(canvas.transform, "Background", INK).rectTransform);
        RectTransform box = Place(Rect("Content", canvas.transform), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 860));

        TextMeshProUGUI head = Text(box, "Head", "島を守り抜いた", 52, PAPER, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(900, 70), true);
        head.characterSpacing = 20;
        head.gameObject.AddComponent<ZapTextEffect>();
        TextMeshProUGUI sub = Text(box, "Sub", "全五波", 26, ASH, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -16), new Vector2(500, 40));
        Img(box, "HeadLine", FRAME).rectTransform.SetFrom(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -90), new Vector2(1500, 2));

        Text(box, "MaxLabel", "一振り最多", 24, ASH, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -130), new Vector2(400, 36), true);
        TextMeshProUGUI maxSwing = Text(box, "MaxSwing", "0", 300, PAPER, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(-10, -150), new Vector2(560, 320), true);
        maxSwing.gameObject.AddComponent<ZapTextEffect>();
        Text(box, "Unit", "体", 60, PAPER, TextAlignmentOptions.BottomLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(520, -360), new Vector2(120, 90));
        TextMeshProUGUI title = Text(box, "Title", "素振り", 180, PAPER, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -480), new Vector2(800, 220), true);
        title.characterSpacing = 20;
        title.gameObject.AddComponent<ZapTextEffect>();
        RectTransform titleSlash = Slash(box, "TitleSlash", new Vector2(-30, -600), 700, -5);
        RectTransform badge = Place(Rect("NewBest", box), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -720), new Vector2(240, 44));
        badge.gameObject.AddComponent<Image>().color = GOLD_HI;
        TextMeshProUGUI badgeText = Text(badge, "Label", "自己ベスト更新", 24, INK, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240, 44), true);
        badgeText.raycastTarget = false;

        string[] labels = { "得点", "最大連鎖", "落とした数", "刻んだ大銘" };
        TextMeshProUGUI[] values = new TextMeshProUGUI[4];
        for (int i = 0; i < 4; i++)
        {
            float y = -260 - i * 90;
            Text(box, "StatLabel" + i, labels[i], 28, ASH, TextAlignmentOptions.Left, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-300, y), new Vector2(300, 60));
            values[i] = Text(box, "StatValue" + i, "0", 44, PAPER, TextAlignmentOptions.Right, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, y), new Vector2(300, 60), true);
            Img(box, "StatLine" + i, FRAME).rectTransform.SetFrom(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, y - 66), new Vector2(600, 2));
        }

        Button retry = NewButton(box, "RetryButton", "もう一度", 36, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(280, 80));
        Button toTitle = NewButton(box, "TitleButton", "タイトルへ", 36, new Vector2(0, 0), new Vector2(0, 0), new Vector2(310, 0), new Vector2(280, 80));

        ResultScreen screen = canvas.gameObject.AddComponent<ResultScreen>();
        Wire(screen, "tuning", feedbackTuning);
        Wire(screen, "headText", head);
        Wire(screen, "subText", sub);
        Wire(screen, "maxSwingText", maxSwing);
        Wire(screen, "titleText", title);
        Wire(screen, "titleSlash", titleSlash);
        Wire(screen, "newBestBadge", badge.gameObject);
        Wire(screen, "scoreText", values[0]);
        Wire(screen, "chainText", values[1]);
        Wire(screen, "fallsText", values[2]);
        Wire(screen, "majorText", values[3]);
        Wire(screen, "retryButton", retry);
        Wire(screen, "titleButton", toTitle);

        Selection.activeGameObject = canvas.gameObject;
        CheckFont();
        Debug.Log("[TitleResultSceneBuilder] リザルト画面を作った");
    }

    /// <summary>
    /// シーンの名前で移れるよう、3つのシーンを Build Settings に入れる。もう入っているシーンはそのままにし、順番だけ整える。
    /// Build Settings はチーム共通の設定なので、押す前にチームに伝える
    /// </summary>
    [MenuItem("Hitofuri/増永/シーンを Build Settings に入れる")]
    static void AddScenesToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        foreach (string path in BUILD_SCENES)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogWarning($"[TitleResultSceneBuilder] {path} がまだ無いので飛ばした");
                continue;
            }
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        // ほかの人が入れたシーンは消さずに、後ろに残す
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
        {
            if (System.Array.IndexOf(BUILD_SCENES, existing.path) < 0) scenes.Add(existing);
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[TitleResultSceneBuilder] Build Settings のシーンを {scenes.Count} 個にした");
    }

    // ================= 小さな道具 =================

    static T LoadOrCreate<T>(string name) where T : ScriptableObject
    {
        string path = $"{TUNING_FOLDER}/{name}.asset";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[TitleResultSceneBuilder] {path} を作った");
        return asset;
    }

    static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        // このプロジェクトは Input System だけを使う設定なので、古い StandaloneInputModule ではなくこちらを付ける
        GameObject go = NewObject("EventSystem");
        go.AddComponent<EventSystem>();
        InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    static GameObject NewObject(string name)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    static Canvas NewCanvas(string name)
    {
        GameObject go = NewObject(name);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static void SetFrom(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size) => Place(rt, anchor, pivot, pos, size);

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box, bool bold = false)
    {
        RectTransform rt = Place(Rect(name, parent), anchor, pivot, pos, box);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        if (bold) t.fontStyle = FontStyles.Bold;
        return t;
    }

    static Image Img(Transform parent, string name, Color color)
    {
        Image img = Rect(name, parent).gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static RectTransform Slash(Transform parent, string name, Vector2 pos, float width, float angle)
    {
        Image img = Img(parent, name, GOLD_HI);
        RectTransform rt = img.rectTransform;
        Place(rt, new Vector2(0, 1), new Vector2(0, 0.5f), pos, new Vector2(width, 6));
        rt.localRotation = Quaternion.Euler(0, 0, angle);
        return rt;
    }

    static Button NewButton(Transform parent, string name, string label, float fontSize, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        RectTransform rt = Place(Rect(name, parent), anchor, pivot, pos, size);
        Image bg = rt.gameObject.AddComponent<Image>();
        bg.color = Color.white;
        Outline outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = GOLD;
        outline.effectDistance = new Vector2(2, -2);

        Button button = rt.gameObject.AddComponent<Button>();
        // 背景の白に色を掛ける形で、選択中とマウスを乗せたときに金を濃くする
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(GOLD.r, GOLD.g, GOLD.b, 0.12f);
        colors.highlightedColor = new Color(GOLD.r, GOLD.g, GOLD.b, 0.32f);
        colors.selectedColor = new Color(GOLD.r, GOLD.g, GOLD.b, 0.32f);
        colors.pressedColor = new Color(GOLD.r, GOLD.g, GOLD.b, 0.5f);
        button.colors = colors;

        TextMeshProUGUI t = Text(rt, "Label", label, fontSize, PAPER, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, true);
        t.characterSpacing = 20;
        return button;
    }

    static void Wire(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[TitleResultSceneBuilder] {target.GetType().Name}.{field} が見つからない"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireArray(Object target, string field, Object[] values)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[TitleResultSceneBuilder] {target.GetType().Name}.{field} が見つからない"); return; }
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CheckFont()
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font != null && font.HasCharacter('斬', true, true)) return;
        Debug.LogWarning("[TitleResultSceneBuilder] TextMeshPro の既定のフォントに日本語が入っていないので、文字が□になる。" +
            "日本語フォント(同梱の ShipporiMinchoB1-ExtraBold.ttf など)を右クリック → Create → TextMeshPro → Font Asset で作り、" +
            "Project Settings → TextMesh Pro → Settings の Default Font Asset に入れる");
    }
}
#endif
