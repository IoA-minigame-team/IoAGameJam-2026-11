#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// エディタ専用。メニュー「Hitofuri/増永/銘の選択画面を作る」から、銘の仕組みと選択画面を今開いているシーンに組み立てる。
/// 手で UI を並べる手間を省き、参照のつなぎ忘れを防ぐため。作った後は位置や色をインスペクターで自由に直してよい。
/// 得点の画面(CSceneBuilder)とは別のファイルにして、銘のタスクだけで直せるようにした。小さな道具が重なっているのはそのため。
/// エディタ専用のフォルダ(Editor/)が無いので、#if UNITY_EDITOR で囲んでビルドに入らないようにしている。
/// </summary>
public static class MeiSceneBuilder
{
    // 調整値のアセットの置き場。決まった置き場が無いので、今は自分の Dev フォルダに置く(髙橋さんと相談して移す)
    const string TUNING_FOLDER = "Assets/_Project/Scenes/Dev/Masunaga";
    // 得点の画面(CSceneBuilder)が作るキャンバスの名前。あればその上に重ねる
    const string GAME_CANVAS_NAME = "C_GameCanvas";

    static readonly Color PANEL = new Color(0.085f, 0.085f, 0.1f, 1f);
    static readonly Color PAPER = new Color(0.925f, 0.92f, 0.9f, 1f);
    static readonly Color ASH = new Color(0.56f, 0.553f, 0.533f, 1f);
    static readonly Color FRAME = new Color(1f, 1f, 1f, 0.14f);

    [MenuItem("Hitofuri/増永/銘の選択画面を作る")]
    static void BuildMei()
    {
        // 2つ置くと GameContext.MeiStats を取り合うので、もう置いてあれば作らない
        if (Object.FindAnyObjectByType<MeiManager>() != null)
        {
            Debug.LogWarning("[MeiSceneBuilder] このシーンにはもう MeiManager があるので、何も作らなかった");
            return;
        }

        MeiTuning meiTuning = LoadOrCreate<MeiTuning>("MeiTuning");
        FeedbackTuning feedbackTuning = LoadOrCreate<FeedbackTuning>("FeedbackTuning");
        // 選択画面のボタンは EventSystem が無いと押せない
        EnsureEventSystem();

        // 得点の画面があればその上に、無ければ専用のキャンバスに作る。後から足すので HUD より手前に描かれる
        GameObject canvasObject = GameObject.Find(GAME_CANVAS_NAME);
        Transform canvas = canvasObject != null ? canvasObject.transform : NewCanvas("C_MeiCanvas").transform;

        MeiSelectWindow window = BuildMeiWindow(canvas);
        MeiManager mei = NewObject("C_Mei").AddComponent<MeiManager>();
        Wire(mei, "tuning", meiTuning);
        Wire(mei, "feedbackTuning", feedbackTuning);
        Wire(mei, "window", window);
        // テスト用シーンでは、刻むたびに倍率を Console に出す
        SetBool(mei, "logChanges", true);
        AddShin(mei.gameObject, meiTuning);

        // 刻んだ銘の一覧(左下)
        TextMeshProUGUI meiList = Text(canvas, "MeiList", "", 26, PAPER, TextAlignmentOptions.BottomLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(60, 40), new Vector2(1000, 50));
        // 選択画面より奥に描く
        meiList.transform.SetSiblingIndex(window.transform.GetSiblingIndex());
        MeiHud meiHud = meiList.gameObject.AddComponent<MeiHud>();
        Wire(meiHud, "meiManager", mei);
        Wire(meiHud, "listText", meiList);

        MeiTestDriver driver = NewObject("C_MeiTestDriver").AddComponent<MeiTestDriver>();
        Wire(driver, "meiManager", mei);

        Selection.activeGameObject = mei.gameObject;
        Debug.Log("[MeiSceneBuilder] 銘の仕組みと選択画面を作った。再生して 7(すぐ開く)、8(15体落とす)、9(大銘)、0、Y、U(震)で試せる");
    }

    /// <summary>
    /// 銘の選択画面をもう作ってあるシーンに、大銘「震」を足す。
    /// 調整値のアセットはもう作ってあるので、コードの初期値は入らない。大銘を候補に出す設定と、効果の無い残光と引力を外す設定もここで入れる。
    /// </summary>
    [MenuItem("Hitofuri/増永/大銘「震」を置く")]
    static void BuildShin()
    {
        MeiManager mei = Object.FindAnyObjectByType<MeiManager>();
        if (mei == null)
        {
            Debug.LogWarning("[MeiSceneBuilder] このシーンに MeiManager が無い。先に「銘の選択画面を作る」を押す");
            return;
        }
        MeiTuning meiTuning = LoadOrCreate<MeiTuning>("MeiTuning");
        AddShin(mei.gameObject, meiTuning);
        OfferOnlyKaeshiAndShin(meiTuning);
        Selection.activeGameObject = mei.gameObject;
        Debug.Log("[MeiSceneBuilder] 大銘「震」を置いた。再生して 0(震を刻む)、Y(試しの敵を置く)、U(斬り抜けが終わったことにする)で試せる");
    }

    static void AddShin(GameObject target, MeiTuning meiTuning)
    {
        if (target.GetComponent<ShinEffect>() != null) return;
        ShinEffect shin = Undo.AddComponent<ShinEffect>(target);
        Wire(shin, "tuning", meiTuning);
        // 髙橋さんの斬り抜けと同じく、Enemy レイヤーの当たり判定だけを探す
        SerializedObject so = new SerializedObject(shin);
        so.FindProperty("enemyMask").intValue = LayerMask.GetMask("Enemy");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void OfferOnlyKaeshiAndShin(MeiTuning meiTuning)
    {
        SerializedObject so = new SerializedObject(meiTuning);
        so.FindProperty("offerMajorMei").boolValue = true;
        SerializedProperty list = so.FindProperty("majorMeis");
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty def = list.GetArrayElementAtIndex(i);
            MajorMeiId id = (MajorMeiId)def.FindPropertyRelative("id").enumValueIndex;
            def.FindPropertyRelative("enabled").boolValue = id == MajorMeiId.Kaeshi || id == MajorMeiId.Shin;
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(meiTuning);
        AssetDatabase.SaveAssets();
    }

    // ================= 選択画面 =================

    static MeiSelectWindow BuildMeiWindow(Transform canvas)
    {
        RectTransform root = Rect("MeiSelectWindow", canvas);
        Stretch(root);
        MeiSelectWindow window = root.gameObject.AddComponent<MeiSelectWindow>();

        // 開いている間だけ出す部分。暗い幕はクリックを受け止めて、後ろのゲームに通さない
        Image dim = Img(root, "Panel", new Color(0.03f, 0.03f, 0.04f, 0.78f));
        dim.raycastTarget = true;
        Stretch(dim.rectTransform);
        CanvasGroup group = dim.gameObject.AddComponent<CanvasGroup>();
        RectTransform panel = dim.rectTransform;

        TextMeshProUGUI eyebrow = Text(panel, "Eyebrow", "十五体を落とした", 22, ASH, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(-580, 420), new Vector2(800, 34), true);
        TextMeshProUGUI title = Text(panel, "Title", "銘を刻む", 64, PAPER, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(-580, 384), new Vector2(800, 80), true);
        title.characterSpacing = 16;
        title.gameObject.AddComponent<ZapTextEffect>();
        TextMeshProUGUI sub = Text(panel, "Sub", "", 24, ASH, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(-580, 296), new Vector2(1100, 36));

        MeiCardView[] cards = new MeiCardView[3];
        for (int i = 0; i < 3; i++) cards[i] = BuildCard(panel, "Card" + i, new Vector2(-400 + i * 400, -10), new Vector2(360, 520), true);
        MeiCardView keep = BuildCard(panel, "KeepCard", new Vector2(0, -340), new Vector2(560, 74), false);
        Text(panel, "Hint", "左右キーかスティックで選ぶ ・ クリック、Enter、Aボタンで決める", 22, ASH, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -420), new Vector2(1000, 34));

        Wire(window, "panel", dim.gameObject);
        Wire(window, "group", group);
        Wire(window, "eyebrowText", eyebrow);
        Wire(window, "titleText", title);
        Wire(window, "subText", sub);
        WireArray(window, "cards", cards);
        Wire(window, "keepCard", keep);
        // 閉じた状態で保存する(開くのは MeiManager)
        dim.gameObject.SetActive(false);
        return window;
    }

    static MeiCardView BuildCard(Transform parent, string name, Vector2 pos, Vector2 size, bool full)
    {
        RectTransform root = Place(Rect(name, parent), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        RectTransform lift = Rect("Lift", root);
        Stretch(lift);
        Image frame = Img(lift, "Frame", FRAME);
        Stretch(frame.rectTransform);
        frame.rectTransform.offsetMin = new Vector2(-3, -3);
        frame.rectTransform.offsetMax = new Vector2(3, 3);
        Image bg = Img(lift, "Bg", PANEL);
        Stretch(bg.rectTransform);
        bg.raycastTarget = true;

        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        // 見た目は MeiCardView が変えるので、ボタンの色の変化は切る
        button.transition = Selectable.Transition.None;

        MeiCardView view = root.gameObject.AddComponent<MeiCardView>();
        Wire(view, "button", button);
        Wire(view, "frame", frame);
        Wire(view, "lift", lift);

        if (!full)
        {
            TextMeshProUGUI label = Text(lift, "Name", "今の大銘を残す", 28, PAPER, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, true);
            Wire(view, "nameText", label);
            return view;
        }

        TextMeshProUGUI kind = Text(lift, "Kind", "小銘", 20, ASH, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -18), new Vector2(200, 30), true);
        TextMeshProUGUI nameText = Text(lift, "Name", "大\n断", 100, PAPER, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(200, 250), true);
        nameText.lineSpacing = -30;
        nameText.gameObject.AddComponent<ZapTextEffect>();
        TextMeshProUGUI desc = Text(lift, "Desc", "線の幅が広がる", 26, PAPER, TextAlignmentOptions.TopLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(22, 110), new Vector2(316, 80));
        TextMeshProUGUI detail = Text(lift, "Detail", "", 20, ASH, TextAlignmentOptions.TopLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(22, 54), new Vector2(316, 56));
        Image[] pips = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            pips[i] = Img(lift, "Pip" + i, new Color(0.17f, 0.17f, 0.19f));
            pips[i].rectTransform.SetFrom(new Vector2(0, 0), new Vector2(0, 0), new Vector2(22 + i * 46, 24), new Vector2(38, 10));
        }
        Wire(view, "kindText", kind);
        Wire(view, "nameText", nameText);
        Wire(view, "descText", desc);
        Wire(view, "detailText", detail);
        WireArray(view, "pips", pips);
        return view;
    }

    static T LoadOrCreate<T>(string name) where T : ScriptableObject
    {
        string path = $"{TUNING_FOLDER}/{name}.asset";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[MeiSceneBuilder] {path} を作った");
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

    static void Wire(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[MeiSceneBuilder] {target.GetType().Name}.{field} が見つからない"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireArray(Object target, string field, Object[] values)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[MeiSceneBuilder] {target.GetType().Name}.{field} が見つからない"); return; }
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetBool(Object target, string field, bool value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) return;
        p.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
