#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class EnemyPursuitAssetSetup
{
    const string ROOT = "Assets/_Project/";
    public const string TEST_SCENE = ROOT + "Scenes/Dev/Sanjo/EnemyPursuitTest.unity";
    public static void EnsurePursuit(GameObject root)
    {
        if (root.GetComponent<EnemyPursuit>() == null) root.AddComponent<EnemyPursuit>();
        if (root.GetComponent<EnemyAutoAttack>() == null) root.AddComponent<EnemyAutoAttack>();
    }
    [MenuItem("Hitofuri/Sanjo/Open Enemy Pursuit Test")]
    public static void OpenTest() { EditorSceneManager.OpenScene(TEST_SCENE);EditorApplication.ExecuteMenuItem("Window/General/Game"); }
    [MenuItem("Hitofuri/Sanjo/Setup Enemy Pursuit")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        string[] names = { "Shield", "Shadow", "Werewolf" };float[] speeds = { .95f, 1.8f, 2.4f };
        EnemyBehaviourRole[] roles = { EnemyBehaviourRole.ShieldBearer, EnemyBehaviourRole.Shadow, EnemyBehaviourRole.LampEater };
        for (int i = 0; i < names.Length; i++)
        {
            var tuning = AssetDatabase.LoadAssetAtPath<EnemyTuning>(ROOT + "Data/Tuning/" + names[i] + "EnemyTuning.asset");
            var data = new SerializedObject(tuning);data.FindProperty("behaviourRole").enumValueIndex = (int)roles[i];
            data.FindProperty("moveSpeed").floatValue = speeds[i];data.FindProperty("turnSpeed").floatValue = i == 0 ? 120 : 300;
            data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(tuning);
        }
        string[] prefabs = { "FloatingShieldEnemy", "FloatingShadowEnemy", "FloatingWerewolfEnemy" };
        foreach (var name in prefabs)
        {
            string path = ROOT + "Prefabs/" + name + ".prefab";var content = PrefabUtility.LoadPrefabContents(path);
            try { EnsurePursuit(content);PrefabUtility.SaveAsPrefabAsset(content, path); }
            finally { PrefabUtility.UnloadPrefabContents(content); }
        }
        string systemPath = ROOT + "Prefabs/WaveGameSystem.prefab";var system = PrefabUtility.LoadPrefabContents(systemPath);
        try { if (system.GetComponent<EnemySwarmCounter>() == null) system.AddComponent<EnemySwarmCounter>();PrefabUtility.SaveAsPrefabAsset(system, systemPath); }
        finally { PrefabUtility.UnloadPrefabContents(system); }
        var plan = AssetDatabase.LoadAssetAtPath<EnemyWavePlan>(ROOT + "Data/Tuning/EnemyWavePlan.asset");
        var planData = new SerializedObject(plan);
        planData.FindProperty("waves").GetArrayElementAtIndex(1).FindPropertyRelative("label").stringValue = "2: Shadow + LampEater (werewolf visual)";
        planData.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(plan);
        AssetDatabase.SaveAssets();CreateScene(prefabs);AssetDatabase.SaveAssets();
        Debug.Log("ENEMY_PURSUIT_ASSET_BUILD_PASS");
    }
    static Material MarkerMaterial(string name, Color colour)
    {
        string path = ROOT + "Materials/" + name + ".mat";var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor", colour);AssetDatabase.CreateAsset(material, path); }
        return material;
    }
    static void CreateScene(string[] names)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var stage = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/FloatingMeadowStage.prefab"));
        foreach (var renderer in stage.GetComponentsInChildren<Renderer>()) if (renderer.name.Contains("Grass")) renderer.enabled = false;
        var waveSystem = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/WaveGameSystem.prefab"));
        var flow = waveSystem.GetComponent<WaveController>();flow.enabled = false;
        var flowData = new SerializedObject(flow);flowData.FindProperty("startAutomatically").boolValue = false;flowData.ApplyModifiedPropertiesWithoutUndo();
        var gameOver = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/GameOverSystem.prefab"));
        var player = new GameObject("TestPlayerMarker");player.transform.position = new Vector3(0,0,5);
        var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);marker.name = "PlayerVisualOnly";marker.transform.SetParent(player.transform, false);marker.transform.localPosition = Vector3.up;
        marker.GetComponent<Collider>().isTrigger = true;marker.GetComponent<Renderer>().sharedMaterial = MarkerMaterial("PursuitTestPlayer", new Color(.15f,.65f,.85f));
        var sword = GameObject.CreatePrimitive(PrimitiveType.Cube);sword.name = "TestSwordMarker";sword.transform.position = new Vector3(-6,0,0);sword.transform.localScale = new Vector3(.2f,2.4f,.2f);
        sword.GetComponent<Collider>().isTrigger = true;sword.GetComponent<Renderer>().sharedMaterial = MarkerMaterial("PursuitTestSword", new Color(.95f,.95f,.8f));sword.SetActive(false);
        var camera = new GameObject("PursuitReviewCamera").AddComponent<Camera>();camera.tag = "MainCamera";
        camera.transform.position = new Vector3(4, 17, 24);camera.transform.LookAt(new Vector3(0,0,1));camera.orthographic = true;camera.orthographicSize = 10.2f;
        camera.nearClipPlane = .03f;camera.farClipPlane = 200;camera.clearFlags = CameraClearFlags.SolidColor;camera.backgroundColor = new Color(.08f,.09f,.105f);
        var sun = new GameObject("ReviewDirectionalOnly").AddComponent<Light>();sun.type = LightType.Directional;sun.intensity = 1.7f;sun.transform.rotation = Quaternion.Euler(45,155,0);
        var point = new GameObject("ReviewPointOnly").AddComponent<Light>();point.type = LightType.Point;point.range = 40;point.intensity = 35;point.transform.position = new Vector3(0,6,6);
        RenderSettings.ambientMode = AmbientMode.Flat;RenderSettings.ambientLight = new Color(.4f,.4f,.4f);
        var demo = new GameObject("PursuitReviewControlsOnly").AddComponent<EnemyPursuitDemo>();var data = new SerializedObject(demo);
        var array = data.FindProperty("prefabs");array.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/" + names[i] + ".prefab");
        data.FindProperty("testPlayer").objectReferenceValue = player.transform;data.FindProperty("testSword").objectReferenceValue = sword;
        data.FindProperty("flow").objectReferenceValue = flow;data.FindProperty("counter").objectReferenceValue = waveSystem.GetComponent<EnemySwarmCounter>();
        data.FindProperty("gameOver").objectReferenceValue = gameOver.GetComponent<GameOverController>();data.FindProperty("stageGround").objectReferenceValue = stage.GetComponentInChildren<MeshCollider>();
        data.FindProperty("reviewCamera").objectReferenceValue = camera;
        data.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), TEST_SCENE);
    }
}
#endif
