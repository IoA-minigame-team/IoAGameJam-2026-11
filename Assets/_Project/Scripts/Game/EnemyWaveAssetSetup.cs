#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>UnityのAPIで出現ポイント・データ・Prefab・確認シーンを配置する制作ツール。</summary>
public static class EnemyWaveAssetSetup
{
    const string ROOT = "Assets/_Project/";
    const string PLAN = ROOT + "Data/Tuning/EnemyWavePlan.asset";
    const string SYSTEM = ROOT + "Prefabs/WaveGameSystem.prefab";
    public const string TEST_SCENE = ROOT + "Scenes/Dev/Sanjo/EnemyWaveTest.unity";

    [MenuItem("Hitofuri/Sanjo/Open Enemy Wave Test")]
    public static void OpenTest() { EditorSceneManager.OpenScene(TEST_SCENE);EditorApplication.ExecuteMenuItem("Window/General/Game"); }
    [MenuItem("Hitofuri/Sanjo/Setup Enemy Waves")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        CreatePlan();AssetDatabase.SaveAssets();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // Scene切替で未参照アセットがアンロードされるため、保存後に永続アセットを取得し直す。
        var plan = AssetDatabase.LoadAssetAtPath<EnemyWavePlan>(PLAN);
        var stage = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/FloatingMeadowStage.prefab"));
        var ground = stage.GetComponentInChildren<MeshCollider>();Physics.SyncTransforms();
        CreateSystem(plan, ground);
        CreateReviewScene(stage, ground);
        InstallMain();
        AssetDatabase.SaveAssets();
        var systemAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SYSTEM);
        if (systemAsset.GetComponent<WaveController>().TotalWaves != 5) throw new Exception("WaveGameSystem is missing the five-wave plan reference");
        Debug.Log("ENEMY_WAVE_ASSET_BUILD_PASS");
    }
    static EnemyWavePlan CreatePlan()
    {
        var existing = AssetDatabase.LoadAssetAtPath<EnemyWavePlan>(PLAN);
        if (existing != null) return existing;
        var plan = ScriptableObject.CreateInstance<EnemyWavePlan>();
        var data = new SerializedObject(plan);var waves = data.FindProperty("waves");waves.arraySize = 5;
        string[] labels = { "1: Shadow introduction", "2: Shadow + lamp-eater slot (werewolf visual, AI pending)", "3: Shield from centre", "4: Mixed directions", "5: Largest mixed wave" };
        string[][] prefabs = { new[]{"FloatingShadowEnemy"}, new[]{"FloatingShadowEnemy","FloatingWerewolfEnemy"}, new[]{"FloatingShieldEnemy","FloatingShadowEnemy"}, new[]{"FloatingShadowEnemy","FloatingWerewolfEnemy","FloatingShieldEnemy"}, new[]{"FloatingShadowEnemy","FloatingWerewolfEnemy","FloatingShieldEnemy"} };
        int[][] counts = { new[]{3}, new[]{3,2}, new[]{2,2}, new[]{3,2,2}, new[]{4,4,3} };
        EnemySpawnArea[][] areas = { new[]{EnemySpawnArea.AnyEdge}, new[]{EnemySpawnArea.South|EnemySpawnArea.West,EnemySpawnArea.North|EnemySpawnArea.East}, new[]{EnemySpawnArea.Center,EnemySpawnArea.East}, new[]{EnemySpawnArea.North,EnemySpawnArea.South,EnemySpawnArea.Center}, new[]{EnemySpawnArea.AnyEdge,EnemySpawnArea.AnyEdge,EnemySpawnArea.Center} };
        float[][] delays = { new[]{0f}, new[]{0f,.5f}, new[]{0f,.4f}, new[]{0f,.5f,1f}, new[]{0f,.5f,1f} };
        for (int w = 0; w < 5; w++)
        {
            var wave = waves.GetArrayElementAtIndex(w);wave.FindPropertyRelative("label").stringValue = labels[w];
            var batches = wave.FindPropertyRelative("batches");batches.arraySize = counts[w].Length;
            for (int b = 0; b < counts[w].Length; b++)
            {
                var batch = batches.GetArrayElementAtIndex(b);
                batch.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/" + prefabs[w][b] + ".prefab");
                batch.FindPropertyRelative("count").intValue = counts[w][b];
                batch.FindPropertyRelative("areas").intValue = (int)areas[w][b];
                batch.FindPropertyRelative("delay").floatValue = delays[w][b];
                batch.FindPropertyRelative("interval").floatValue = w == 4 ? .5f : .7f;
            }
        }
        data.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.CreateAsset(plan, PLAN);return plan;
    }
    static void CreateSystem(EnemyWavePlan plan, MeshCollider ground)
    {
        // 再実行で手動調整したポイントを上書きしない。初回だけ既定の配置を作る。
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SYSTEM) != null)
        {
            var existingRoot = PrefabUtility.LoadPrefabContents(SYSTEM);
            try
            {
                foreach (var component in new Component[]{existingRoot.GetComponent<EnemySpawner>(), existingRoot.GetComponent<WaveController>()})
                {
                    var setup = new SerializedObject(component);
                    if (setup.FindProperty("plan").objectReferenceValue == null)
                    {
                        setup.FindProperty("plan").objectReferenceValue = plan;setup.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(existingRoot, SYSTEM);
            }
            finally { PrefabUtility.UnloadPrefabContents(existingRoot); }
            return;
        }
        var root = new GameObject("WaveGameSystem");
        var pointRoot = new GameObject("SpawnPoints").transform;pointRoot.SetParent(root.transform, false);
        var spawnedRoot = new GameObject("SpawnedEnemies").transform;spawnedRoot.SetParent(root.transform, false);
        var points = new System.Collections.Generic.List<EnemySpawnPoint>();
        Vector3 centre = ground.bounds.center;centre.y = ground.bounds.max.y;
        string[] names = { "EdgeEast", "EdgeNorthEast", "EdgeNorth", "EdgeNorthWest", "EdgeWest", "EdgeSouthWest", "EdgeSouth", "EdgeSouthEast" };
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4;var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            float last = 0, maximum = Mathf.Max(ground.bounds.size.x, ground.bounds.size.z);
            for (float radius = .5f; radius < maximum; radius += .5f)
            {
                if (!Ground(ground, centre + direction * radius, out _)) break;
                last = radius;
            }
            Vector3 candidate = centre + direction * Mathf.Max(0, last - 2.2f);
            if (!Ground(ground, candidate, out var position)) throw new Exception("Cannot place " + names[i]);
            var area = (direction.x > .3f ? EnemySpawnArea.East : direction.x < -.3f ? EnemySpawnArea.West : 0)
                | (direction.z > .3f ? EnemySpawnArea.North : direction.z < -.3f ? EnemySpawnArea.South : 0);
            points.Add(CreatePoint(pointRoot, names[i], position, centre, area));
        }
        Vector3[] offsets = { Vector3.right * 3, Vector3.forward * 3, Vector3.left * 3, Vector3.back * 3 };
        for (int i = 0; i < offsets.Length; i++)
        {
            if (!Ground(ground, centre + offsets[i], out var position)) throw new Exception("Cannot place centre point");
            points.Add(CreatePoint(pointRoot, "Center" + (i + 1), position, centre, EnemySpawnArea.Center));
        }
        var spawner = root.AddComponent<EnemySpawner>();var spawnData = new SerializedObject(spawner);
        spawnData.FindProperty("plan").objectReferenceValue = plan;spawnData.FindProperty("spawnedRoot").objectReferenceValue = spawnedRoot;
        var array = spawnData.FindProperty("points");array.arraySize = points.Count;
        for (int i = 0; i < points.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
        spawnData.ApplyModifiedPropertiesWithoutUndo();
        var flow = root.AddComponent<WaveController>();var flowData = new SerializedObject(flow);
        flowData.FindProperty("plan").objectReferenceValue = plan;flowData.FindProperty("spawner").objectReferenceValue = spawner;
        flowData.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, SYSTEM);Object.DestroyImmediate(root);
    }
    static bool Ground(MeshCollider ground, Vector3 position, out Vector3 result)
    {
        result = position;
        if (!ground.Raycast(new Ray(position + Vector3.up * 30, Vector3.down), out var hit, 60)) return false;
        result = hit.point;return true;
    }
    static EnemySpawnPoint CreatePoint(Transform parent, string name, Vector3 position, Vector3 centre, EnemySpawnArea area)
    {
        var point = new GameObject(name).AddComponent<EnemySpawnPoint>();point.transform.SetParent(parent, false);point.transform.position = position;
        var facing = centre - position;facing.y = 0;if (facing.sqrMagnitude > .0001f) point.transform.rotation = Quaternion.LookRotation(facing);
        var data = new SerializedObject(point);data.FindProperty("areas").intValue = (int)area;data.ApplyModifiedPropertiesWithoutUndo();return point;
    }
    static void CreateReviewScene(GameObject stage, MeshCollider ground)
    {
        foreach (var renderer in stage.GetComponentsInChildren<Renderer>()) if (renderer.name.Contains("Grass")) renderer.enabled = false;
        var system = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SYSTEM));
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/GameOverSystem.prefab"));
        var camera = new GameObject("WaveReviewCamera").AddComponent<Camera>();camera.tag = "MainCamera";
        camera.transform.position = new Vector3(18, 32, 35);camera.transform.LookAt(Vector3.zero);
        camera.orthographic = true;camera.orthographicSize = 24;camera.nearClipPlane = .03f;camera.farClipPlane = 200;
        camera.clearFlags = CameraClearFlags.SolidColor;camera.backgroundColor = new Color(.08f, .09f, .105f);
        var sun = new GameObject("ReviewDirectionalOnly").AddComponent<Light>();sun.type = LightType.Directional;sun.intensity = 1.7f;sun.transform.rotation = Quaternion.Euler(45, 155, 0);
        var light = new GameObject("ReviewPointOnly").AddComponent<Light>();light.type = LightType.Point;light.range = 50;light.intensity = 40;light.transform.position = new Vector3(0, 8, 0);
        RenderSettings.ambientMode = AmbientMode.Flat;RenderSettings.ambientLight = new Color(.4f, .4f, .4f);
        var demo = new GameObject("WaveReviewControlsOnly").AddComponent<EnemyWaveDemo>();var data = new SerializedObject(demo);
        data.FindProperty("flow").objectReferenceValue = system.GetComponent<WaveController>();data.FindProperty("reviewCamera").objectReferenceValue = camera;data.FindProperty("stageGround").objectReferenceValue = ground;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), TEST_SCENE);
    }
    static void InstallMain()
    {
        const string path = ROOT + "Scenes/Main/InGame.unity";
        EditorSceneManager.OpenScene(path);bool changed = false;
        if (Object.FindFirstObjectByType<MeshCollider>() == null)
        {
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/FloatingMeadowStage.prefab"));changed = true;
        }
        if (Object.FindFirstObjectByType<GameOverController>() == null)
        {
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/GameOverSystem.prefab"));changed = true;
        }
        if (Object.FindFirstObjectByType<WaveController>() == null)
        {
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SYSTEM));changed = true;
        }
        if (changed) EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
        EditorSceneManager.OpenScene(TEST_SCENE);
    }
}
#endif
