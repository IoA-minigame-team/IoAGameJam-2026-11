#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>担当者の実Playerから作ったPrefabを、B担当の島と進行へ組み込む制作ツール。</summary>
public static class PlayerIntegrationAssetSetup
{
    const string ROOT="Assets/_Project/";
    public const string TEST_SCENE=ROOT+"Scenes/Dev/Sanjo/PlayerIntegrationTest.unity";
    public const string ROUTE_SCENE=ROOT+"Scenes/Dev/Sanjo/PlayerIntegrationRouteTest.unity";
    public const string PLAYER=ROOT+"Prefabs/Player.prefab";
    public const string ISLAND=ROOT+"Prefabs/GameJamIsland.prefab";
    public const string ROUTE_PLAN=ROOT+"Data/Tuning/PlayerIntegrationRoutePlan.asset";
    [MenuItem("Hitofuri/Sanjo/Open Real Player Test")]
    public static void OpenTest(){EditorSceneManager.OpenScene(TEST_SCENE);EditorApplication.ExecuteMenuItem("Window/General/Game");}
    [MenuItem("Hitofuri/Sanjo/Open Real Player Route Test")]
    public static void OpenRoute(){EditorSceneManager.OpenScene(ROUTE_SCENE);EditorApplication.ExecuteMenuItem("Window/General/Game");}
    public static void Build()
    {
        AssetDatabase.Refresh();
        foreach(var name in new[]{"Shield","Shadow","Werewolf"})
        {
            string path=ROOT+"Prefabs/Floating"+name+"Enemy.prefab";
            var content=PrefabUtility.LoadPrefabContents(path);
            try
            {
                EnemyPursuitAssetSetup.EnsurePursuit(content);
                var data=new SerializedObject(content.GetComponent<Enemy>());data.FindProperty("movementObstacleMask").intValue=4;data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(content,path);
                var tuning=AssetDatabase.LoadAssetAtPath<EnemyTuning>(ROOT+"Data/Tuning/"+name+"EnemyTuning.asset");EditorUtility.SetDirty(tuning);
            }
            finally{PrefabUtility.UnloadPrefabContents(content);}
        }
        CreateIsland();CreateRoutePlan();AssetDatabase.SaveAssets();
        CreateScene(false);CreateScene(true);InstallMain();AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(TEST_SCENE);
        Debug.Log("PLAYER_INTEGRATION_ASSET_BUILD_PASS");
    }
    static void CreateIsland()
    {
        string modelPath=ROOT+"Art/GameJamIsland.fbx";
        var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
        PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);root.name="GameJamIsland";
        var fence=AssetDatabase.LoadAssetAtPath<Material>(ROOT+"Materials/GameJamFence.mat");
        if(fence==null){fence=new Material(Shader.Find("Universal Render Pipeline/Lit"));fence.SetColor("_BaseColor",new Color(.035f,.037f,.04f));AssetDatabase.CreateAsset(fence,ROOT+"Materials/GameJamFence.mat");}
        // FBXに埋め込まれた素材名と、既存のUnity側素材を対応させる。
        foreach(var mesh in root.GetComponentsInChildren<MeshFilter>())
        {
            var renderer=mesh.GetComponent<MeshRenderer>();
            if(mesh.name.StartsWith("FencePanel"))
            {
                renderer.sharedMaterial=fence;mesh.gameObject.layer=2;
                var box=mesh.gameObject.AddComponent<BoxCollider>();box.center=mesh.sharedMesh.bounds.center;box.size=mesh.sharedMesh.bounds.size;
                continue;
            }
        }
        // モデルの素材スロットを、既存のUnity側素材へ明示的に差し替える。
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            if(renderer.name.StartsWith("FencePanel"))continue;
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath).GetComponentsInChildren<MeshRenderer>().First(r=>r.name==renderer.name);
            renderer.sharedMaterials=original.sharedMaterials.Select(mat=>
            {
                string name=mat.name.Split('.')[0];
                var mapped=AssetDatabase.LoadAssetAtPath<Material>(ROOT+"Materials/FloatingStage"+name.Replace("_","")+".mat");
                return mapped!=null?mapped:AssetDatabase.LoadAssetAtPath<Material>(ROOT+"Materials/"+name+".mat");
            }).ToArray();
        }
        var floor=root.GetComponentsInChildren<MeshFilter>().Single(m=>m.name.StartsWith("DarkMeadow_Walkable_Ground"));
        floor.gameObject.AddComponent<MeshCollider>().sharedMesh=floor.sharedMesh;
        var old=AssetDatabase.LoadAssetAtPath<GameObject>(ROOT+"Prefabs/FloatingMeadowStage.prefab");
        var fogSource=old.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name.Contains("StageEdgeFog"));
        if(fogSource!=null)
        {
            var fog=Object.Instantiate(fogSource.gameObject,root.transform);fog.name="GameJamEdgeFog";
            fog.transform.localScale=Vector3.Scale(fogSource.transform.localScale,new Vector3(1.35f,1,1.35f));
        }
        Physics.SyncTransforms();
        var bounds=floor.GetComponent<MeshCollider>().bounds;
        // パスはコマンド引数で指定でき、プロジェクト外へ置く確認レポートだけに使う。
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-sanjoIntegrationOutput");
        if(index>=0&&index+1<args.Length){Directory.CreateDirectory(args[index+1]);File.WriteAllText(Path.Combine(args[index+1],"island-unity.txt"),"Ground world bounds: "+bounds+"\nFences: "+root.GetComponentsInChildren<BoxCollider>().Length);}
        PrefabUtility.SaveAsPrefabAsset(root,ISLAND);
    }
    static void CreateRoutePlan()
    {
        var plan=AssetDatabase.LoadAssetAtPath<EnemyWavePlan>(ROUTE_PLAN);
        if(plan==null){plan=Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyWavePlan>(ROOT+"Data/Tuning/EnemyWavePlan.asset"));AssetDatabase.CreateAsset(plan,ROUTE_PLAN);}
        var data=new SerializedObject(plan);var waves=data.FindProperty("waves");waves.arraySize=1;
        var wave=waves.GetArrayElementAtIndex(0);wave.FindPropertyRelative("label").stringValue="Real Player route: one Shadow";
        var batches=wave.FindPropertyRelative("batches");batches.arraySize=1;var batch=batches.GetArrayElementAtIndex(0);
        batch.FindPropertyRelative("prefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(ROOT+"Prefabs/FloatingShadowEnemy.prefab");
        batch.FindPropertyRelative("count").intValue=1;batch.FindPropertyRelative("areas").intValue=(int)EnemySpawnArea.East;
        batch.FindPropertyRelative("delay").floatValue=0;data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(plan);
    }
    public static bool Ground(MeshCollider ground,Vector3 position,out Vector3 result)
    {
        result=position;if(!ground.Raycast(new Ray(new Vector3(position.x,30,position.z),Vector3.down),out var hit,70))return false;
        result=hit.point;return true;
    }
    public static float Edge(MeshCollider ground,Vector3 direction)
    {
        float last=0;for(float r=.1f;r<100;r+=.1f){if(!Ground(ground,direction*r,out _))break;last=r;}return last;
    }
    static GameObject PlacePlayer(MeshCollider ground,Vector3 position,Camera camera)
    {
        if(!Ground(ground,position,out var floor))throw new Exception("No player floor");
        var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER));
        player.name="Player";var cc=player.GetComponent<CharacterController>();player.transform.position=floor+Vector3.up*(cc.height*.5f-cc.center.y+cc.skinWidth);
        var data=new SerializedObject(player.GetComponent<PlayerController>());data.FindProperty("cameraTransform").objectReferenceValue=camera.transform;data.ApplyModifiedPropertiesWithoutUndo();return player;
    }
    static void ConfigureSystem(GameObject system,MeshCollider ground,bool route)
    {
        var flow=system.GetComponent<WaveController>();var spawner=system.GetComponent<EnemySpawner>();
        var plan=AssetDatabase.LoadAssetAtPath<EnemyWavePlan>(route?ROUTE_PLAN:ROOT+"Data/Tuning/EnemyWavePlan.asset");
        foreach(var component in new Component[]{flow,spawner}){var data=new SerializedObject(component);data.FindProperty("plan").objectReferenceValue=plan;data.ApplyModifiedPropertiesWithoutUndo();}
        var points=spawner.Points;
        for(int i=0;i<points.Length;i++)
        {
            if(route&&i>0){points[i].gameObject.SetActive(false);continue;}
            Vector3 position;
            if(route)position=Vector3.right*(Edge(ground,Vector3.right)-7);
            else if(i<8){float angle=i*Mathf.PI/4;var dir=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));position=dir*Mathf.Max(0,Edge(ground,dir)-2.5f);}
            else{float angle=(i-8)*Mathf.PI/2;position=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*4;}
            if(!Ground(ground,position,out var floor))throw new Exception("Spawn point has no ground: "+i);
            points[i].transform.position=floor;points[i].transform.rotation=Quaternion.LookRotation(-new Vector3(position.x,0,position.z));
            if(route){var data=new SerializedObject(points[i]);data.FindProperty("radius").floatValue=0;data.FindProperty("areas").intValue=(int)EnemySpawnArea.East;data.ApplyModifiedPropertiesWithoutUndo();}
        }
    }
    static Camera CreateCamera(Vector3 playerPosition)
    {
        var camera=new GameObject("IntegrationCamera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=13;
        camera.nearClipPlane=.03f;camera.farClipPlane=200;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.085f,.10f);
        camera.transform.position=playerPosition+new Vector3(0,18,12);camera.transform.LookAt(playerPosition);camera.gameObject.AddComponent<IntegrationCameraFollow>();return camera;
    }
    static void CreateScene(bool route)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var stage=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ISLAND));var ground=stage.GetComponentInChildren<MeshCollider>();Physics.SyncTransforms();
        var start=route?Vector3.right*(Edge(ground,Vector3.right)-11):Vector3.zero;Ground(ground,start,out var position);
        var camera=CreateCamera(position);PlacePlayer(ground,start,camera);
        var system=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT+"Prefabs/WaveGameSystem.prefab"));ConfigureSystem(system,ground,route);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT+"Prefabs/GameOverSystem.prefab"));
        var hud=new GameObject("IntegrationEventObserver").AddComponent<PlayerIntegrationHUD>();var data=new SerializedObject(hud);data.FindProperty("flow").objectReferenceValue=system.GetComponent<WaveController>();data.ApplyModifiedPropertiesWithoutUndo();
        var sun=new GameObject("IntegrationSun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.8f;sun.transform.rotation=Quaternion.Euler(45,155,0);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.4f,.4f,.4f);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),route?ROUTE_SCENE:TEST_SCENE);
    }
    static void InstallMain()
    {
        EditorSceneManager.OpenScene(ROOT+"Scenes/Main/InGame.unity");
        var old=SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(r=>r.name=="FloatingMeadowStage");if(old!=null)old.SetActive(false);
        var stage=SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(r=>r.name=="GameJamIsland");
        if(stage==null)stage=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ISLAND));
        var ground=stage.GetComponentInChildren<MeshCollider>();Physics.SyncTransforms();
        var camera=Camera.main;if(camera==null)camera=CreateCamera(Vector3.zero);
        else{camera.orthographic=true;camera.orthographicSize=13;if(camera.GetComponent<IntegrationCameraFollow>()==null)camera.gameObject.AddComponent<IntegrationCameraFollow>();camera.transform.position=new Vector3(0,18,12);camera.transform.LookAt(Vector3.zero);}
        if(Object.FindFirstObjectByType<PlayerController>()==null)PlacePlayer(ground,Vector3.zero,camera);
        var flow=Object.FindFirstObjectByType<WaveController>();ConfigureSystem(flow.gameObject,ground,false);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
}
#endif

