#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// エディタ専用。メニュー「Hitofuri/増永/…」から、増永の画面と仕組みを今開いているシーンに組み立てる。
/// このPRでは、スコアとゲーム中のUIだけを作る(銘の選択画面やタイトルは、それぞれのタスクで足す)。
/// 手で UI を並べる手間を省き、参照のつなぎ忘れを防ぐため。作った後は位置や色をインスペクターで自由に直してよい。
/// エディタ専用のフォルダ(Editor/)が無いので、#if UNITY_EDITOR で囲んでビルドに入らないようにしている。
/// </summary>
public static class CSceneBuilder
{
    // 調整値のアセットの置き場。決まった置き場が無いので、今は自分の Dev フォルダに置く(髙橋さんと相談して移す)
    const string TUNING_FOLDER = "Assets/_Project/Scenes/Dev/Masunaga";

    static readonly Color INK = new Color(0.043f, 0.043f, 0.051f, 1f);
    static readonly Color PANEL = new Color(0.085f, 0.085f, 0.1f, 1f);
    static readonly Color PAPER = new Color(0.925f, 0.92f, 0.9f, 1f);
    static readonly Color ASH = new Color(0.56f, 0.553f, 0.533f, 1f);
    static readonly Color GOLD = new Color(0.914f, 0.71f, 0.29f, 1f);
    static readonly Color GOLD_HI = new Color(1f, 0.886f, 0.627f, 1f);
    static readonly Color FRAME = new Color(1f, 1f, 1f, 0.14f);

    // ================= メニュー =================

    [MenuItem("Hitofuri/増永/スコアとゲーム中のUIを作る")]
    static void BuildGame()
    {
        FeedbackTuning feedbackTuning = LoadOrCreate<FeedbackTuning>("FeedbackTuning");

        GameObject systems = NewObject("C_Systems");
        ScoreCounter score = systems.AddComponent<ScoreCounter>();
        Wire(score, "tuning", feedbackTuning);

        Canvas canvas = NewCanvas("C_GameCanvas");
        GameHud hud = BuildHud(canvas.transform);
        Wire(hud, "tuning", feedbackTuning);
        Wire(hud, "score", score);

        CTestDriver driver = NewObject("C_TestDriver").AddComponent<CTestDriver>();
        Wire(driver, "score", score);

        Selection.activeGameObject = canvas.gameObject;
        CheckFont();
        Debug.Log("[CSceneBuilder] スコアとゲーム中のUIを作った。再生して 1、2、3、0 キーで試せる");
    }

    // ================= ゲーム中の画面 =================

    static GameHud BuildHud(Transform canvas)
    {
        RectTransform root = Rect("HUD", canvas);
        Stretch(root);

        // 得点と連鎖(左上)
        Text(root, "ScoreLabel", "得点", 22, ASH, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -36), new Vector2(200, 30), true);
        TextMeshProUGUI score = Text(root, "Score", "0", 76, PAPER, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -60), new Vector2(600, 90), true);
        score.gameObject.AddComponent<ZapTextEffect>();
        TextMeshProUGUI chain = Text(root, "Chain", "連鎖 0　×1.0", 30, PAPER, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -150), new Vector2(500, 44));
        chain.rectTransform.pivot = new Vector2(0, 0.5f);

        // ウェーブ(右上)
        Text(root, "WaveLabel", "波", 22, ASH, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -36), new Vector2(200, 30), true);
        TextMeshProUGUI wave = Text(root, "Wave", "第一波", 56, PAPER, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -62), new Vector2(400, 70), true);
        wave.characterSpacing = 12;
        wave.gameObject.AddComponent<ZapTextEffect>();
        TextMeshProUGUI waveCount = Text(root, "WaveCount", "1 / 5", 26, ASH, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -136), new Vector2(200, 36));

        // 一振りのカウント(上の中央)
        RectTransform swing = Place(Rect("Swing", root), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(500, 260));
        CanvasGroup swingGroup = swing.gameObject.AddComponent<CanvasGroup>();
        swingGroup.blocksRaycasts = false;
        Text(swing, "Label", "一振り", 28, ASH, TextAlignmentOptions.Top, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(400, 40), true);
        TextMeshProUGUI swingCount = Text(swing, "Count", "0", 150, PAPER, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(400, 170), true);
        swingCount.gameObject.AddComponent<ZapTextEffect>();
        Text(swing, "Unit", "体", 40, PAPER, TextAlignmentOptions.Left, new Vector2(0.5f, 1), new Vector2(0, 0.5f), new Vector2(120, -150), new Vector2(100, 60));
        TextMeshProUGUI swingPoints = Text(swing, "Points", "+0", 38, PAPER, TextAlignmentOptions.Top, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -210), new Vector2(400, 50), true);

        // 称号(中央)
        RectTransform callout = Place(Rect("Callout", root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(900, 320));
        CanvasGroup calloutGroup = callout.gameObject.AddComponent<CanvasGroup>();
        calloutGroup.blocksRaycasts = false;
        TextMeshProUGUI calloutText = Text(callout, "Title", "一閃", 220, PAPER, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(900, 250), true);
        calloutText.characterSpacing = 20;
        calloutText.gameObject.AddComponent<ZapTextEffect>();
        RectTransform calloutSlash = Slash(callout, "Slash", new Vector2(-420, 20), 840, -6);
        calloutSlash.anchorMin = calloutSlash.anchorMax = new Vector2(0.5f, 0.5f);
        TextMeshProUGUI calloutSub = Text(callout, "Sub", "一振りで五体", 26, ASH, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(600, 40));

        // ウェーブの帯(中央より少し下)
        RectTransform banner = Place(Rect("Banner", root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(1200, 220));
        CanvasGroup bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.blocksRaycasts = false;
        Img(banner, "LineTop", FRAME).rectTransform.SetFrom(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(800, 2));
        TextMeshProUGUI bannerText = Text(banner, "Text", "第一波", 96, PAPER, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1100, 120), true);
        bannerText.characterSpacing = 40;
        bannerText.gameObject.AddComponent<ZapTextEffect>();
        TextMeshProUGUI bannerSub = Text(banner, "Sub", "", 26, ASH, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -62), new Vector2(1100, 40));
        Img(banner, "LineBottom", FRAME).rectTransform.SetFrom(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(800, 2));

        GameHud hud = root.gameObject.AddComponent<GameHud>();
        Wire(hud, "scoreText", score);
        Wire(hud, "chainText", chain);
        Wire(hud, "chainPunch", chain.rectTransform);
        Wire(hud, "waveText", wave);
        Wire(hud, "waveCountText", waveCount);
        Wire(hud, "swingGroup", swingGroup);
        Wire(hud, "swingCountText", swingCount);
        Wire(hud, "swingPointsText", swingPoints);
        Wire(hud, "swingPunch", swingCount.rectTransform);
        Wire(hud, "calloutGroup", calloutGroup);
        Wire(hud, "calloutText", calloutText);
        Wire(hud, "calloutSubText", calloutSub);
        Wire(hud, "calloutSlash", calloutSlash);
        Wire(hud, "bannerGroup", bannerGroup);
        Wire(hud, "bannerText", bannerText);
        Wire(hud, "bannerSubText", bannerSub);
        return hud;
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
        Debug.Log($"[CSceneBuilder] {path} を作った");
        return asset;
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

    /// <summary>金の斜めの線。左端を軸にして、スクリプトから横に伸ばす</summary>
    static RectTransform Slash(Transform parent, string name, Vector2 pos, float width, float angle)
    {
        Image img = Img(parent, name, GOLD_HI);
        RectTransform rt = img.rectTransform;
        Place(rt, new Vector2(0, 1), new Vector2(0, 0.5f), pos, new Vector2(width, 6));
        rt.localRotation = Quaternion.Euler(0, 0, angle);
        return rt;
    }

    // private の [SerializeField] にエディタから参照を入れる(インスペクターでドラッグするのと同じ)
    static void Wire(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[CSceneBuilder] {target.GetType().Name}.{field} が見つからない"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CheckFont()
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font != null && font.HasCharacter('斬', true, true)) return;
        Debug.LogWarning("[CSceneBuilder] TextMeshPro の既定のフォントに日本語が入っていないので、文字が□になる。" +
            "日本語フォント(同梱の ShipporiMinchoB1-ExtraBold.ttf など)を右クリック → Create → TextMeshPro → Font Asset で作り、" +
            "Project Settings → TextMesh Pro → Settings の Default Font Asset に入れる");
    }
}
#endif
