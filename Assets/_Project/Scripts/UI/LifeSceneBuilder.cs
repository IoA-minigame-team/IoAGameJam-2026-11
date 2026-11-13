#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// エディタ専用。メニュー「Hitofuri/増永/被弾の残り回数を作る」から、ゲーム中の画面(C_GameCanvas)の左上、連鎖の下に被弾の残り回数を足す。
/// 得点の画面の組み立て(CSceneBuilder)とは別のファイルにして、ほかの PR とぶつからないようにした。
/// エディタ専用のフォルダ(Editor/)が無いので、#if UNITY_EDITOR で囲んでビルドに入らないようにしている。
/// </summary>
public static class LifeSceneBuilder
{
    const string TUNING_PATH = "Assets/_Project/Scenes/Dev/Masunaga/FeedbackTuning.asset";
    const string GAME_CANVAS_NAME = "C_GameCanvas";
    const string HUD_NAME = "HUD";
    const string LIFE_NAME = "Life";
    const string TEST_NAME = "C_LifeTestDriver";

    static readonly Color ASH = new Color(0.56f, 0.553f, 0.533f, 1f);

    [MenuItem("Hitofuri/増永/被弾の残り回数を作る")]
    static void BuildLife()
    {
        FeedbackTuning tuning = AssetDatabase.LoadAssetAtPath<FeedbackTuning>(TUNING_PATH);
        if (tuning == null)
        {
            Debug.LogWarning("[LifeSceneBuilder] FeedbackTuning が見つからない。先に「スコアとゲーム中のUIを作る」を押す");
            return;
        }
        GameObject canvas = GameObject.Find(GAME_CANVAS_NAME);
        if (canvas == null)
        {
            Debug.LogWarning("[LifeSceneBuilder] C_GameCanvas が無い。先に「スコアとゲーム中のUIを作る」を押す");
            return;
        }
        Transform parent = canvas.transform.Find(HUD_NAME);
        if (parent == null) parent = canvas.transform;
        if (parent.Find(LIFE_NAME) != null)
        {
            Debug.LogWarning("[LifeSceneBuilder] 被弾の残り回数はもう置いてあるので、何も作らなかった");
            return;
        }

        // 連鎖(左上、y -150)のすぐ下に置く
        RectTransform root = Place(Rect(LIFE_NAME, parent), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -186), new Vector2(400, 70));
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Create " + LIFE_NAME);

        TextMeshProUGUI label = Place(Rect("Label", root), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(200, 30)).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = "命";
        label.fontSize = 22;
        label.color = ASH;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false;

        RectTransform row = Place(Rect("Row", root), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(0, -50), new Vector2(400, 40));

        LifeHud hud = root.gameObject.AddComponent<LifeHud>();
        Wire(hud, "tuning", tuning);
        Wire(hud, "row", row);

        // テスト用のキー。本番のシーンには置かない
        if (Object.FindFirstObjectByType<LifeTestDriver>() == null && !IsMainScene())
        {
            GameObject test = new GameObject(TEST_NAME);
            Undo.RegisterCreatedObjectUndo(test, "Create " + TEST_NAME);
            LifeTestDriver driver = test.AddComponent<LifeTestDriver>();
            Wire(driver, "tuning", tuning);
        }

        Selection.activeGameObject = root.gameObject;
        Debug.Log("[LifeSceneBuilder] 被弾の残り回数を置いた。再生して K(被弾したことにする)、L(元に戻す)で試せる");
    }

    // 本番のシーン(Scenes/Main)にはテスト用のキーを置かない
    static bool IsMainScene() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().path.Contains("/Scenes/Main/");

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

    // private の [SerializeField] にエディタから参照を入れる(インスペクターでドラッグするのと同じ)
    static void Wire(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[LifeSceneBuilder] {target.GetType().Name}.{field} が見つからない"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
