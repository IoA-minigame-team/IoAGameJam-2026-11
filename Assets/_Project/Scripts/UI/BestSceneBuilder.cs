#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// エディタ専用。メニュー「Hitofuri/増永/色反転と自己ベストの演出を作る」から、今開いているシーンに次を足す。
/// ・ゲーム中の画面(C_GameCanvas)があれば:大量撃破の色反転(ScreenInvert)と、ゲーム中の自己ベストの知らせ(BestRecordHud)
/// ・リザルト(ResultScreen)があれば:「自己ベスト更新」の印を判子のように押す動き(NewBestStamp)
/// ほかの組み立て(CSceneBuilder、TitleResultSceneBuilder)とは別のファイルにして、ほかの PR とぶつからないようにした。
/// エディタ専用のフォルダ(Editor/)が無いので、#if UNITY_EDITOR で囲んでビルドに入らないようにしている。
/// </summary>
public static class BestSceneBuilder
{
    const string TUNING_PATH = "Assets/_Project/Scenes/Dev/Masunaga/FeedbackTuning.asset";
    const string SHADER_NAME = "Hitofuri/UiInvert";
    const string MATERIAL_PATH = "Assets/_Project/Materials/UiInvert.mat";
    const string GAME_CANVAS_NAME = "C_GameCanvas";
    const string HUD_NAME = "HUD";
    const string INVERT_NAME = "ScreenInvert";
    const string BEST_NAME = "BestRecord";
    // テスト用のシーンで比べる自己ベスト。CTestDriver の 1(5体)、0(20体)で超えられる数にする
    const int TEST_BEST = 6;

    static readonly Color PAPER = new Color(0.925f, 0.92f, 0.9f, 1f);
    static readonly Color GOLD_HI = new Color(1f, 0.886f, 0.627f, 1f);

    [MenuItem("Hitofuri/増永/色反転と自己ベストの演出を作る")]
    static void Build()
    {
        FeedbackTuning tuning = AssetDatabase.LoadAssetAtPath<FeedbackTuning>(TUNING_PATH);
        if (tuning == null)
        {
            Debug.LogWarning("[BestSceneBuilder] FeedbackTuning が見つからない。先に「スコアとゲーム中のUIを作る」を押す");
            return;
        }
        bool built = BuildGame(tuning);
        built |= BuildResult(tuning);
        if (!built) Debug.LogWarning("[BestSceneBuilder] C_GameCanvas も ResultScreen も無いか、もう置いてあるので、何も作らなかった");
    }

    // ================= ゲーム中 =================

    static bool BuildGame(FeedbackTuning tuning)
    {
        GameObject canvas = GameObject.Find(GAME_CANVAS_NAME);
        ScoreCounter score = Object.FindFirstObjectByType<ScoreCounter>();
        if (canvas == null || score == null) return false;
        if (canvas.transform.Find(INVERT_NAME) != null) return false;

        Material material = LoadOrCreateMaterial();
        if (material == null) return false;

        // 色反転は一番奥(最初の子)に置き、HUD の文字は反転させない
        RectTransform invert = Rect(INVERT_NAME, canvas.transform);
        invert.SetSiblingIndex(0);
        invert.anchorMin = Vector2.zero;
        invert.anchorMax = Vector2.one;
        invert.offsetMin = invert.offsetMax = Vector2.zero;
        Image image = invert.gameObject.AddComponent<Image>();
        image.material = material;
        image.raycastTarget = false;
        image.enabled = false;
        ScreenInvert screenInvert = invert.gameObject.AddComponent<ScreenInvert>();
        Wire(screenInvert, "tuning", tuning);
        Wire(screenInvert, "score", score);
        Undo.RegisterCreatedObjectUndo(invert.gameObject, "Create " + INVERT_NAME);

        // 自己ベストの知らせは、一振りのカウント(上の中央、下端が y -380 あたり)のすぐ下
        Transform hud = canvas.transform.Find(HUD_NAME);
        Transform parent = hud != null ? hud : canvas.transform;
        RectTransform best = Rect(BEST_NAME, parent);
        best.anchorMin = best.anchorMax = new Vector2(0.5f, 1f);
        best.pivot = new Vector2(0.5f, 0.5f);
        best.anchoredPosition = new Vector2(0f, -425f);
        best.sizeDelta = new Vector2(900f, 90f);
        CanvasGroup group = best.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        TextMeshProUGUI title = Text(best, "Title", "自己ベスト更新", 46, GOLD_HI, new Vector2(0f, 20f), new Vector2(900f, 56f));
        title.characterSpacing = 16;
        title.gameObject.AddComponent<ZapTextEffect>();
        TextMeshProUGUI sub = Text(best, "Sub", "", 24, PAPER, new Vector2(0f, -26f), new Vector2(900f, 34f));
        BestRecordHud bestHud = best.gameObject.AddComponent<BestRecordHud>();
        Wire(bestHud, "tuning", tuning);
        Wire(bestHud, "score", score);
        Wire(bestHud, "group", group);
        Wire(bestHud, "titleText", title);
        Wire(bestHud, "subText", sub);
        // テスト用のシーンでは、保存した記録が無くても出せるよう、比べる数を決めておく。本番のシーンでは -1(保存した記録と比べる)
        if (!IsMainScene()) SetInt(bestHud, "testBest", TEST_BEST);
        Undo.RegisterCreatedObjectUndo(best.gameObject, "Create " + BEST_NAME);

        Selection.activeGameObject = best.gameObject;
        Debug.Log(IsMainScene()
            ? "[BestSceneBuilder] 色反転と自己ベストの知らせを置いた"
            : $"[BestSceneBuilder] 色反転と自己ベストの知らせを置いた。再生して 0(20体落とす一振り)で、10体で1回、20体で2回反転し、{TEST_BEST}体を超えたところで自己ベスト更新が出る");
        return true;
    }

    static Material LoadOrCreateMaterial()
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
        if (m != null) return m;
        Shader shader = Shader.Find(SHADER_NAME);
        if (shader == null)
        {
            Debug.LogError("[BestSceneBuilder] UiInvert のシェーダーが見つからない。Materials/UiInvert.shader があるか確かめる");
            return null;
        }
        m = new Material(shader);
        AssetDatabase.CreateAsset(m, MATERIAL_PATH);
        AssetDatabase.SaveAssets();
        return m;
    }

    // ================= リザルト =================

    static bool BuildResult(FeedbackTuning tuning)
    {
        ResultScreen screen = Object.FindFirstObjectByType<ResultScreen>();
        if (screen == null) return false;
        SerializedObject so = new SerializedObject(screen);
        GameObject badge = so.FindProperty("newBestBadge").objectReferenceValue as GameObject;
        if (badge == null || badge.GetComponent<NewBestStamp>() != null) return false;

        // 判子のように中心で縮んで傾くよう、見た目の位置は変えずに軸を真ん中へ移す
        RectTransform rt = (RectTransform)badge.transform;
        Undo.RecordObject(rt, "Center badge pivot");
        Vector2 size = rt.sizeDelta;
        Vector2 shift = new Vector2((0.5f - rt.pivot.x) * size.x, (0.5f - rt.pivot.y) * size.y);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition += shift;

        if (badge.GetComponent<CanvasGroup>() == null) Undo.AddComponent<CanvasGroup>(badge);
        NewBestStamp stamp = Undo.AddComponent<NewBestStamp>(badge);
        Wire(stamp, "tuning", tuning);
        Selection.activeGameObject = badge;
        Debug.Log("[BestSceneBuilder] 自己ベスト更新の印を押す動きを置いた。ResultScene を再生すると、数え上げのあとに印が押される");
        return true;
    }

    // ================= 小さな道具 =================

    // 本番のシーン(Scenes/Main)では、テスト用の値を入れない
    static bool IsMainScene() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().path.Contains("/Scenes/Main/");

    static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, Vector2 pos, Vector2 box)
    {
        RectTransform rt = Rect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    // private の [SerializeField] にエディタから参照を入れる(インスペクターでドラッグするのと同じ)
    static void Wire(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[BestSceneBuilder] {target.GetType().Name}.{field} が見つからない"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetInt(Object target, string field, int value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) return;
        p.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
