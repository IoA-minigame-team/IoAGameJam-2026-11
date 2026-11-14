#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// エディタ専用。メニュー「Hitofuri/増永/ポーズ画面を作る」から、ポーズ画面(PauseMenu)を今開いているシーンに組み立てる。
/// 手で UI を並べる手間を省き、参照のつなぎ忘れを防ぐため。作った後は位置や色をインスペクターで自由に直してよい。
/// エディタ専用のフォルダ(Editor/)が無いので、#if UNITY_EDITOR で囲んでビルドに入らないようにしている。
/// </summary>
public static class PauseMenuBuilder
{
    // 色は CSceneBuilder と TitleResultSceneBuilder に揃える
    static readonly Color INK = new Color(0.043f, 0.043f, 0.051f, 1f);
    static readonly Color PAPER = new Color(0.925f, 0.92f, 0.9f, 1f);
    static readonly Color ASH = new Color(0.56f, 0.553f, 0.533f, 1f);
    static readonly Color GOLD = new Color(0.914f, 0.71f, 0.29f, 1f);
    // 暗幕の濃さ。後ろの止まった場面がうっすら見える程度
    const float DIM_ALPHA = 0.8f;
    // HUD(並び順 0)より上に出す
    const int SORTING_ORDER = 10;

    [MenuItem("Hitofuri/増永/ポーズ画面を作る")]
    static void Build()
    {
        GameObject existing = GameObject.Find("C_PauseCanvas");
        if (existing != null)
        {
            Debug.LogWarning("[PauseMenuBuilder] C_PauseCanvas がもうあるので作らない。作り直すときは消してから実行する");
            Selection.activeGameObject = existing;
            return;
        }

        Canvas canvas = NewCanvas("C_PauseCanvas");
        PauseMenu menu = canvas.gameObject.AddComponent<PauseMenu>();

        // ゲーム中に出しておく三本線のボタン。左上は得点、右上は波で埋まっているので上部の中央に置く
        Button open = NewButton(canvas.transform, "PauseButton", "", 0, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(72, 60));
        for (int i = 0; i < 3; i++)
        {
            Image bar = Img(open.transform, $"Bar{i}", PAPER);
            Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 14 - i * 14), new Vector2(36, 5));
        }
        // ゲーム中にゲームパッドの十字キーで選ばれないようにする
        Navigation none = new Navigation { mode = Navigation.Mode.None };
        open.navigation = none;

        RectTransform panel = Rect("Panel", canvas.transform);
        Stretch(panel);
        CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
        Image dim = panel.gameObject.AddComponent<Image>();
        // 暗幕でクリックを受け止め、後ろの三本線のボタンに届かないようにする
        dim.color = new Color(INK.r, INK.g, INK.b, DIM_ALPHA);

        TextMeshProUGUI logo = Text(panel, "Logo", "ヒトフリ", 180, PAPER, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(1000, 220), true);
        logo.characterSpacing = 6;
        logo.gameObject.AddComponent<ZapTextEffect>();
        Text(panel, "Caption", "一時停止", 30, ASH, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -340), new Vector2(600, 44), true).characterSpacing = 30;

        Button resume = NewButton(panel, "ResumeButton", "再開", 40, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(380, 84));
        Button retry = NewButton(panel, "RetryButton", "やり直し", 40, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(380, 84));
        Button title = NewButton(panel, "TitleButton", "タイトルへ", 40, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -210), new Vector2(380, 84));
        // 縦に並べ、上下の端で反対側へ回り込まないようにする
        Vertical(resume, null, retry);
        Vertical(retry, resume, title);
        Vertical(title, retry, null);

        Text(panel, "HowTo", "移動は WASD か 左スティック。ボタンは左クリック・Space・Aボタンのどれでも同じ。", 26, PAPER, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 130), new Vector2(1400, 40));
        Text(panel, "Hint", "Esc・P・Start で再開", 22, ASH, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(800, 34));
        panel.gameObject.SetActive(false);

        Wire(menu, "panel", panel.gameObject);
        Wire(menu, "group", group);
        Wire(menu, "resumeButton", resume);
        Wire(menu, "retryButton", retry);
        Wire(menu, "titleButton", title);
        Wire(menu, "openButton", open);

        Selection.activeGameObject = canvas.gameObject;
        Debug.Log("[PauseMenuBuilder] ポーズ画面を作った。再生して Esc、P、Start か、上部中央の三本線で開ける");
    }

    // ================= 部品 =================

    static Canvas NewCanvas(string name)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SORTING_ORDER;
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

    // タイトルと結果画面のボタンと同じ見た目(薄い金の地に金の縁、選ぶと金が濃くなる)
    static Button NewButton(Transform parent, string name, string label, float fontSize, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        RectTransform rt = Place(Rect(name, parent), anchor, pivot, pos, size);
        Image bg = rt.gameObject.AddComponent<Image>();
        bg.color = Color.white;
        Outline outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = GOLD;
        outline.effectDistance = new Vector2(2, -2);

        Button button = rt.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(GOLD.r, GOLD.g, GOLD.b, 0.12f);
        colors.highlightedColor = new Color(GOLD.r, GOLD.g, GOLD.b, 0.32f);
        colors.selectedColor = new Color(GOLD.r, GOLD.g, GOLD.b, 0.32f);
        colors.pressedColor = new Color(GOLD.r, GOLD.g, GOLD.b, 0.5f);
        colors.disabledColor = new Color(GOLD.r, GOLD.g, GOLD.b, 0.05f);
        button.colors = colors;

        if (!string.IsNullOrEmpty(label))
        {
            TextMeshProUGUI t = Text(rt, "Label", label, fontSize, PAPER, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, true);
            t.characterSpacing = 20;
        }
        return button;
    }

    static void Vertical(Button button, Button up, Button down)
    {
        Navigation nav = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down };
        button.navigation = nav;
    }

    static void Wire(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[PauseMenuBuilder] {target.GetType().Name}.{field} が見つからない"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
