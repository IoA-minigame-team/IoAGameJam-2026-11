#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// エディタ専用。メニュー「Hitofuri/増永/刀の光を作る」から、刀の光(SwordLightView)を今開いているシーンに置く。
/// 本番のシーン(InGame)にも、テスト用のものを混ぜずに置けるよう、テスト用のキーは別のメニューにした。
/// 得点や音のメニュー(CSceneBuilder)とは別のファイルにして、このタスクだけで直せるようにした。
/// エディタ専用のフォルダ(Editor/)が無いので、#if UNITY_EDITOR で囲んでビルドに入らないようにしている。
/// </summary>
public static class SwordLightSceneBuilder
{
    // 調整値のアセットの置き場(得点や音と同じものを使う)
    const string TUNING_PATH = "Assets/_Project/Scenes/Dev/Masunaga/FeedbackTuning.asset";
    const string MATERIAL_PATH = "Assets/_Project/Materials/VfxSwordRing.mat";
    const string VIEW_NAME = "C_SwordLight";
    const string TEST_NAME = "C_SwordLightTest";

    [MenuItem("Hitofuri/増永/刀の光を作る")]
    static void BuildSwordLight()
    {
        FeedbackTuning tuning = AssetDatabase.LoadAssetAtPath<FeedbackTuning>(TUNING_PATH);
        if (tuning == null)
        {
            Debug.LogWarning($"[SwordLightSceneBuilder] {TUNING_PATH} が無い。先にメニュー「揺れ、SE、エフェクトを作る」か「スコアとゲーム中のUIを作る」を押す");
            return;
        }
        AssignRingMaterial(tuning);

        // 2つ置くと光が二重になるので、もう置いてあれば作らない
        if (Object.FindFirstObjectByType<SwordLightView>() != null)
        {
            Debug.LogWarning("[SwordLightSceneBuilder] このシーンにはもう SwordLightView があるので、置かなかった(輪のマテリアルだけ入れ直した)");
            return;
        }

        GameObject root = new GameObject(VIEW_NAME);
        Undo.RegisterCreatedObjectUndo(root, "Create " + VIEW_NAME);
        SwordLightView view = root.AddComponent<SwordLightView>();

        GameObject lightObject = new GameObject("Light");
        lightObject.transform.SetParent(root.transform, false);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        // 置く前は消しておく(再生すると SwordLightView が点ける)
        light.enabled = false;

        LineRenderer range = NewRing(root.transform, "RangeRing", tuning.RingMaterial);
        LineRenderer timer = NewRing(root.transform, "TimerRing", tuning.RingMaterial);

        Wire(view, "tuning", tuning);
        Wire(view, "swordLight", light);
        Wire(view, "rangeRing", range);
        Wire(view, "timerRing", timer);

        Selection.activeGameObject = root;
        Debug.Log("[SwordLightSceneBuilder] 刀の光を置いた。テスト用のシーンでは、メニュー「刀の光のテスト用キーを置く」も押す");
    }

    [MenuItem("Hitofuri/増永/刀の光のテスト用キーを置く")]
    static void BuildTestDriver()
    {
        if (Object.FindFirstObjectByType<SwordLightTestDriver>() != null) return;
        GameObject go = new GameObject(TEST_NAME);
        Undo.RegisterCreatedObjectUndo(go, "Create " + TEST_NAME);
        go.AddComponent<SwordLightTestDriver>();
        Selection.activeGameObject = go;
        Debug.Log("[SwordLightSceneBuilder] テスト用キーを置いた。再生して P(置く / 斬り戻す)、G(群がり +1)で試せる。本番のシーンには置かない");
    }

    /// <summary>輪のマテリアル(金、加算)を作り、FeedbackTuning に入れる。もうあれば作り直さない</summary>
    static void AssignRingMaterial(FeedbackTuning tuning)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
        if (m == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("[SwordLightSceneBuilder] URP の Particles/Unlit のシェーダーが見つからないので、輪のマテリアルを作らなかった");
                return;
            }
            m = new Material(shader);
            // インスペクターで Surface Type = Transparent、Blending Mode = Additive を選ぶのと同じ設定
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, MATERIAL_PATH);
        }
        SerializedObject so = new SerializedObject(tuning);
        so.FindProperty("ringMaterial").objectReferenceValue = m;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tuning);
        AssetDatabase.SaveAssets();
    }

    static LineRenderer NewRing(Transform parent, string name, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.enabled = false;
        return line;
    }

    // private の [SerializeField] にエディタから参照を入れる(インスペクターでドラッグするのと同じ)
    static void Wire(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[SwordLightSceneBuilder] {target.GetType().Name}.{field} が見つからない"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
