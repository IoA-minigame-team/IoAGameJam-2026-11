#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>取り込まれた本物のPlayer.prefabを直接配置する。プレイヤー担当のPrefab・コードは書き換えない。</summary>
public static class LatestPlayerHandoffSetup
{
    const string ROOT="Assets/_Project/";
    public const string PLAYER=ROOT+"Prefabs/Player.prefab";
    public const string OBSERVATION_SCENE=ROOT+"Scenes/Dev/Sanjo/LatestPlayerObservation.unity";
    public const string ROUTE_SCENE=ROOT+"Scenes/Dev/Sanjo/LatestPlayerRouteTest.unity";
    [MenuItem("Hitofuri/Sanjo/最新主人公で三種を観察")]
    public static void OpenObservation(){OpenScene(OBSERVATION_SCENE);}
    [MenuItem("Hitofuri/Sanjo/最新主人公の通し確認")]
    public static void OpenRoute(){OpenScene(ROUTE_SCENE);}
    static void OpenScene(string path){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;EditorSceneManager.OpenScene(path);EditorApplication.ExecuteMenuItem("Window/General/Game");}
    public static bool OwnsScene(string path)=>path.StartsWith(ROOT+"Scenes/Dev/Sanjo/")||path==ROOT+"Scenes/Main/InGame.unity";
    public static void Build()
    {
        AssetDatabase.Refresh();var report=new StringBuilder();
        foreach(string path in new[]{ROOT+"Scenes/Main/InGame.unity",PlayerIntegrationAssetSetup.TEST_SCENE,PlayerIntegrationAssetSetup.ROUTE_SCENE,InkReviewAssetSetup.REVIEW_SCENE})
        {
            EditorSceneManager.OpenScene(path);ConnectCurrentScene();report.AppendLine(path+" -> "+PLAYER);EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
        EditorSceneManager.OpenScene(InkReviewAssetSetup.REVIEW_SCENE);EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),OBSERVATION_SCENE);
        EditorSceneManager.OpenScene(PlayerIntegrationAssetSetup.ROUTE_SCENE);EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ROUTE_SCENE);
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(OBSERVATION_SCENE);
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-sanjoHandoffOutput");
        if(index>=0&&index+1<args.Length){Directory.CreateDirectory(args[index+1]);File.WriteAllText(Path.Combine(args[index+1],"scene-connections.txt"),report+"Observation: "+OBSERVATION_SCENE+"\nRoute: "+ROUTE_SCENE);}
        Debug.Log("LATEST_PLAYER_CONNECTION_BUILD_PASS");
    }
    public static void ConnectCurrentScene()
    {
        var scene=SceneManager.GetActiveScene();if(!OwnsScene(scene.path))throw new Exception("三條の確認シーンまたはInGameで接続してください。");
        if(EditorApplication.isPlaying)throw new Exception("Playを停止してから接続してください。");
        var all=Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);if(all.Length!=1)throw new Exception("主人公は1体の状態で接続してください。現在 "+all.Length);
        var old=all[0];GameObject player;
        if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old.gameObject)==PLAYER)player=old.gameObject;
        else
        {
            player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER),scene);
            player.name="Player";player.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation);
            Undo.RegisterCreatedObjectUndo(player,"最新主人公を接続");
        }
        var camera=Camera.main;if(camera==null)throw new Exception("MainCameraがありません。");
        var controllerData=new SerializedObject(player.GetComponent<PlayerController>());controllerData.FindProperty("cameraTransform").objectReferenceValue=camera.transform;controllerData.ApplyModifiedProperties();
        var review=Object.FindFirstObjectByType<InkReviewSession>();
        if(review!=null)
        {
            var data=new SerializedObject(review);data.FindProperty("playerRoot").objectReferenceValue=player;
            data.FindProperty("contactDamageReceiver").objectReferenceValue=player.GetComponent<PlayerHealth>();data.ApplyModifiedProperties();
        }
        if(player!=old.gameObject)Undo.DestroyObjectImmediate(old.gameObject);
        if(Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).All(v=>!v.isGlobal))
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT+"Prefabs/PostProcessVolume.prefab"),scene);
        Undo.RecordObject(camera.GetUniversalAdditionalCameraData(),"主人公の剣の発光を接続");camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        EditorSceneManager.MarkSceneDirty(scene);
    }
}
#endif
