#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>元の主人公ソースを変えず、確認シーンと編集可能なuGUIをUnity APIで制作する。</summary>
public static class InkReviewAssetSetup
{
    const string ROOT="Assets/_Project/";
    public const string REVIEW_SCENE=ROOT+"Scenes/Dev/Sanjo/ThreeEnemyReview.unity";
    public const string UI_PREFAB=ROOT+"Prefabs/InkReviewInterface.prefab";
    const string PLAN=ROOT+"Data/Tuning/ThreeEnemyReviewPlan.asset";
    [MenuItem("Hitofuri/Sanjo/Open Three Enemy Review")]
    public static void Open(){EditorSceneManager.OpenScene(REVIEW_SCENE);EditorApplication.ExecuteMenuItem("Window/General/Game");}
    public static void Build()
    {
        AssetDatabase.Refresh();CreatePlan();AssetDatabase.SaveAssets();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var prefabRoot=new GameObject("InkReviewInterface");prefabRoot.AddComponent<InkReviewSession>();
        prefabRoot.AddComponent<InkReviewInterface>().Construct(AssetDatabase.LoadAssetAtPath<Font>(ROOT+"Art/ShipporiMinchoB1-Medium.ttf"));
        PrefabUtility.SaveAsPrefabAsset(prefabRoot,UI_PREFAB);Object.DestroyImmediate(prefabRoot);
        CreateReview();
        foreach(string path in new[]{PlayerIntegrationAssetSetup.TEST_SCENE,PlayerIntegrationAssetSetup.ROUTE_SCENE,ROOT+"Scenes/Main/InGame.unity"})
        {
            EditorSceneManager.OpenScene(path);Install(false);EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
        AssetDatabase.SaveAssets();Open();Debug.Log("INK_REVIEW_ASSET_BUILD_PASS");
    }
    static void CreatePlan()
    {
        var plan=AssetDatabase.LoadAssetAtPath<EnemyWavePlan>(PLAN);
        if(plan==null){plan=Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyWavePlan>(ROOT+"Data/Tuning/EnemyWavePlan.asset"));AssetDatabase.CreateAsset(plan,PLAN);}
        var data=new SerializedObject(plan);var waves=data.FindProperty("waves");waves.arraySize=1;
        var wave=waves.GetArrayElementAtIndex(0);wave.FindPropertyRelative("label").stringValue="Three simultaneous enemies";
        var batches=wave.FindPropertyRelative("batches");batches.arraySize=3;
        string[] names={"Shadow","Shield","Werewolf"};EnemySpawnArea[] areas={EnemySpawnArea.West,EnemySpawnArea.East,EnemySpawnArea.South};
        for(int i=0;i<3;i++)
        {
            var batch=batches.GetArrayElementAtIndex(i);batch.FindPropertyRelative("prefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(ROOT+"Prefabs/Floating"+names[i]+"Enemy.prefab");
            batch.FindPropertyRelative("count").intValue=1;batch.FindPropertyRelative("areas").intValue=(int)areas[i];batch.FindPropertyRelative("delay").floatValue=0;batch.FindPropertyRelative("interval").floatValue=.01f;
        }
        data.FindProperty("maxSpawnsPerFrame").intValue=3;data.FindProperty("initialDelay").floatValue=.1f;data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(plan);
    }
    static void CreateReview()
    {
        EditorSceneManager.OpenScene(PlayerIntegrationAssetSetup.TEST_SCENE);
        var flow=Object.FindFirstObjectByType<WaveController>();
        var plan=AssetDatabase.LoadAssetAtPath<EnemyWavePlan>(PLAN);
        foreach(var component in new Component[]{flow,flow.Spawner}){var data=new SerializedObject(component);data.FindProperty("plan").objectReferenceValue=plan;data.ApplyModifiedPropertiesWithoutUndo();}
        Vector3[] positions={new Vector3(-5.5f,0,2.5f),new Vector3(5.5f,0,2.5f),new Vector3(0,0,-3.8f)};
        EnemySpawnArea[] areas={EnemySpawnArea.West,EnemySpawnArea.East,EnemySpawnArea.South};
        var ground=Object.FindFirstObjectByType<MeshCollider>();Physics.SyncTransforms();
        for(int i=0;i<flow.Spawner.Points.Length;i++)
        {
            var point=flow.Spawner.Points[i];point.gameObject.SetActive(i<3);if(i>=3)continue;
            PlayerIntegrationAssetSetup.Ground(ground,positions[i],out var position);point.transform.position=position;
            var data=new SerializedObject(point);data.FindProperty("radius").floatValue=0;data.FindProperty("areas").intValue=(int)areas[i];data.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var renderer in ground.transform.root.GetComponentsInChildren<Renderer>())if(renderer.name.Contains("Grass"))renderer.enabled=false;
        Camera.main.orthographicSize=10.5f;
        Install(true);EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),REVIEW_SCENE);
    }
    static void Install(bool review)
    {
        foreach(var old in Object.FindObjectsByType<PlayerIntegrationHUD>(FindObjectsSortMode.None))old.enabled=false;
        var flow=Object.FindFirstObjectByType<WaveController>();var player=Object.FindFirstObjectByType<PlayerController>();var ground=Object.FindFirstObjectByType<MeshCollider>();
        var existing=Object.FindFirstObjectByType<InkReviewSession>();GameObject root;
        if(existing!=null)root=existing.gameObject;else root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UI_PREFAB));
        var session=new SerializedObject(root.GetComponent<InkReviewSession>());
        session.FindProperty("flow").objectReferenceValue=flow;session.FindProperty("threeEnemyReview").boolValue=review;
        session.FindProperty("playerRoot").objectReferenceValue=player.gameObject;session.FindProperty("contactDamageReceiver").objectReferenceValue=player.GetComponent<PlayerHealth>();session.FindProperty("ground").objectReferenceValue=ground;session.ApplyModifiedPropertiesWithoutUndo();
        var flowData=new SerializedObject(flow);flowData.FindProperty("startAutomatically").boolValue=false;flowData.ApplyModifiedPropertiesWithoutUndo();
        var camera=new SerializedObject(root.GetComponentInChildren<Canvas>());camera.FindProperty("m_Camera").objectReferenceValue=Camera.main;camera.ApplyModifiedPropertiesWithoutUndo();
        // 画面の右がワールド+Xになる向きに統一し、確認用のW/D/S手順と配置を一致させる。
        var follow=new SerializedObject(Camera.main.GetComponent<IntegrationCameraFollow>());follow.FindProperty("offset").vector3Value=new Vector3(0,18,-12);follow.ApplyModifiedPropertiesWithoutUndo();
        Camera.main.transform.position=player.transform.position+new Vector3(0,18,-12);Camera.main.transform.LookAt(player.transform.position);
        if(Object.FindFirstObjectByType<EventSystem>()==null)
        {
            var events=new GameObject("ReviewEventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
#endif
