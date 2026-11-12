#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>既存の3体に吹き飛び部品を追加する。既存の確認シーンやSharedを変更しない。</summary>
public static class EnemyLaunchAssetSetup
{
    const string ROOT = "Assets/_Project/";
    public const string TEST_SCENE = ROOT + "Scenes/Dev/Sanjo/EnemyLaunchTest.unity";
    static readonly string[] NAMES = { "FloatingShieldEnemy", "FloatingShadowEnemy", "FloatingWerewolfEnemy" };

    [MenuItem("Hitofuri/Sanjo/Open Enemy Launch Test")]
    public static void OpenTest()
    {
        EditorSceneManager.OpenScene(TEST_SCENE);
        EditorApplication.ExecuteMenuItem("Window/General/Game");
    }
    [MenuItem("Hitofuri/Sanjo/Setup Enemy Launch")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        foreach (string name in NAMES)
        {
            string path = ROOT + "Prefabs/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try { ConfigureEnemy(root, name); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        CreateScene();
        AssetDatabase.SaveAssets();
        foreach (string name in NAMES)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/" + name + ".prefab");
            if (prefab.GetComponent<Enemy>() == null || prefab.GetComponent<Enemy>().Tuning == null) throw new Exception(name + " missing Enemy/Tuning");
            if (prefab.GetComponentsInChildren<Rigidbody>().Length != 0) throw new Exception(name + " unexpected Rigidbody");
            if (prefab.GetComponentInChildren<Animator>().applyRootMotion) throw new Exception(name + " root motion enabled");
        }
        Debug.Log("ENEMY_LAUNCH_ASSET_BUILD_PASS");
    }

    // 既存の素材再生成ツールからも呼び、後で再生成した際に吹き飛び部品が消えないようにする。
    public static void ConfigureEnemy(GameObject root, string name)
    {
        string kind = name.Contains("Shield") ? "Shield" : name.Contains("Werewolf") ? "Werewolf" : "Shadow";
        string tuningPath = ROOT + "Data/Tuning/" + kind + "EnemyTuning.asset";
        var tuning = AssetDatabase.LoadAssetAtPath<EnemyTuning>(tuningPath);
        if (tuning == null)
        {
            tuning = ScriptableObject.CreateInstance<EnemyTuning>();
            var setup = new SerializedObject(tuning);
            setup.FindProperty("hasShield").boolValue = kind == "Shield";
            setup.FindProperty("weight").floatValue = kind == "Shield" ? .6f : 1f;
            setup.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(tuning, tuningPath);
        }
        var pivot = root.transform.Find("ReactionPivot");
        if (pivot == null)
        {
            pivot = new GameObject("ReactionPivot").transform; pivot.SetParent(root.transform, false);
            var visual = root.transform.Find("Visual");
            if (visual == null) throw new Exception(name + " missing Visual");
            visual.SetParent(pivot, false);
        }
        var actions = root.GetComponent<EnemyActionAnimation>();
        var actionData = new SerializedObject(actions);
        actionData.FindProperty("reactionPivot").objectReferenceValue = pivot;
        actionData.FindProperty("reactionTuning").objectReferenceValue = tuning;
        actionData.ApplyModifiedPropertiesWithoutUndo();
        var enemy = root.GetComponent<Enemy>();
        if (enemy == null) enemy = root.AddComponent<Enemy>();
        var enemyData = new SerializedObject(enemy);
        enemyData.FindProperty("tuning").objectReferenceValue = tuning;
        enemyData.FindProperty("bodyCollider").objectReferenceValue = root.GetComponent<Collider>();
        enemyData.FindProperty("groundMask").intValue = 1;
        enemyData.ApplyModifiedPropertiesWithoutUndo();
        EnemyPursuitAssetSetup.EnsurePursuit(root);
    }

    static void CreateScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var prefabs = NAMES.Select(name => AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/" + name + ".prefab")).ToArray();
        var actors = new Enemy[3];
        for (int i = 0; i < actors.Length; i++)
        {
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i]);
            actor.transform.position = new Vector3((1 - i) * 3.2f, 0, -2f);
            actors[i] = actor.GetComponent<Enemy>();
        }
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name = "TestPlatform";
        floor.transform.position = new Vector3(0, -.2f, 0);floor.transform.localScale = new Vector3(12, .4f, 10);
        string matPath = ROOT + "Materials/EnemyLaunchTestFloor.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor", new Color(.18f, .18f, .18f));AssetDatabase.CreateAsset(mat, matPath); }
        floor.GetComponent<Renderer>().sharedMaterial = mat;
        var system = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/GameOverSystem.prefab"));
        var camera = new GameObject("TestCamera").AddComponent<Camera>();camera.tag = "MainCamera";
        camera.transform.position = new Vector3(3, 9, 16);camera.transform.LookAt(new Vector3(0, .4f, 1.2f));
        camera.orthographic = true;camera.orthographicSize = 7.2f;camera.nearClipPlane = .03f;camera.farClipPlane = 200;
        camera.clearFlags = CameraClearFlags.SolidColor;camera.backgroundColor = new Color(.085f, .09f, .105f);
        var light = new GameObject("PreviewDirectionalOnly").AddComponent<Light>();light.type = LightType.Directional;light.intensity = 1.5f;light.transform.rotation = Quaternion.Euler(45, 155, 0);
        var point = new GameObject("PreviewPointOnly").AddComponent<Light>();point.type = LightType.Point;point.intensity = 25;point.range = 25;point.transform.position = new Vector3(1, 4, 6);
        RenderSettings.ambientMode = AmbientMode.Flat;RenderSettings.ambientLight = new Color(.3f, .3f, .3f);
        var demo = new GameObject("TestControlsOnly").AddComponent<EnemyLaunchDemo>();
        var data = new SerializedObject(demo);
        var prefabArray = data.FindProperty("enemyPrefabs");prefabArray.arraySize = 3;
        var actorArray = data.FindProperty("actors");actorArray.arraySize = 3;
        for (int i = 0; i < 3; i++) { prefabArray.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];actorArray.GetArrayElementAtIndex(i).objectReferenceValue = actors[i]; }
        data.FindProperty("gameOver").objectReferenceValue = system.GetComponent<GameOverController>();data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), TEST_SCENE);
    }
}
#endif
