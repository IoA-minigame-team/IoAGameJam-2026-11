#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using Object = UnityEngine.Object;

/// <summary>開始位置の重なり、全身の出現検査、旧主人公の参照置換を実オブジェクトで確認する。</summary>
[InitializeOnLoad]
public static class EnemyObstacleSafetyChecks
{
    const string KEY = "Sanjo.EnemyObstacleSafety";
    const string PREFAB = "Assets/_Project/Prefabs/FloatingShadowEnemy.prefab";
    static readonly List<string> results = new List<string>();
    static readonly List<string> failures = new List<string>();
    static IEnumerator scenario;
    static bool running;
    static double until, started;
    static string output;
    static Transform previousPlayer;
    static bool editorPassed;

    static EnemyObstacleSafetyChecks() => EditorApplication.playModeStateChanged += OnState;
    static string Output()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-sanjoSafetyOutput");
        return i>=0?args[i+1]:Path.Combine(Path.GetTempPath(),"HitofuriObstacleSafety");
    }
    public static void Run()
    {
        output=Output();Directory.CreateDirectory(output);
        VerifyPlayerReplacement();
        PlayerIntegrationSceneAudit.Run();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(KEY+".editor",true);SessionState.SetBool(KEY,true);EditorApplication.EnterPlaymode();
    }
    static void VerifyPlayerReplacement()
    {
        EditorSceneManager.OpenScene(LatestPlayerHandoffSetup.OBSERVATION_SCENE);
        var original=Object.FindFirstObjectByType<PlayerController>();
        var legacy=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LatestPlayerHandoffSetup.PLAYER));
        PrefabUtility.UnpackPrefabInstance(legacy,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        legacy.name="LegacyPlayerFixture";Object.DestroyImmediate(original.gameObject);
        legacy.transform.SetPositionAndRotation(new Vector3(2,1.5f,3),Quaternion.Euler(0,35,0));
        var parent=new GameObject("PlayerParent").transform;legacy.transform.SetParent(parent,true);
        var constraint=new GameObject("ExternalPlayerReference").AddComponent<PositionConstraint>();
        constraint.AddSource(new ConstraintSource{sourceTransform=legacy.transform,weight=1});
        var session=Object.FindFirstObjectByType<InkReviewSession>();var data=new SerializedObject(session);
        data.FindProperty("playerRoot").objectReferenceValue=legacy;data.FindProperty("contactDamageReceiver").objectReferenceValue=legacy.GetComponent<PlayerHealth>();data.ApplyModifiedPropertiesWithoutUndo();
        LatestPlayerHandoffSetup.ConnectCurrentScene();
        var replacement=Object.FindFirstObjectByType<PlayerController>();data.Update();
        if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(replacement.gameObject)!=LatestPlayerHandoffSetup.PLAYER
            ||constraint.GetSource(0).sourceTransform!=replacement.transform
            ||data.FindProperty("playerRoot").objectReferenceValue!=replacement.gameObject
            ||data.FindProperty("contactDamageReceiver").objectReferenceValue!=replacement.GetComponent<PlayerHealth>()
            ||replacement.transform.parent!=parent||Vector3.Distance(replacement.transform.position,new Vector3(2,1.5f,3))>.001f)
            throw new Exception("Player replacement lost prefab identity, external references, parent or position");
        LatestPlayerHandoffSetup.ConnectCurrentScene();
        if(Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length!=1)throw new Exception("Repeated replacement duplicated player");
        // 新Prefabに存在しない子への参照を、黙って切断しないことも確認する。
        PrefabUtility.UnpackPrefabInstance(replacement.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        var anchor=new GameObject("LegacyOnlyAnchor").transform;anchor.SetParent(replacement.transform,false);
        constraint.SetSource(0,new ConstraintSource{sourceTransform=anchor,weight=1});
        bool refused=false;
        try{LatestPlayerHandoffSetup.ConnectCurrentScene();}
        catch(Exception error){refused=error.Message.Contains("置換先がない主人公参照");}
        data.Update();
        if(!refused||replacement==null||constraint.GetSource(0).sourceTransform!=anchor
            ||Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length!=1
            ||data.FindProperty("playerRoot").objectReferenceValue!=replacement.gameObject)
            throw new Exception("Unsupported reference did not safely preserve the old player");
        File.WriteAllText(Path.Combine(output,"player-replacement.txt"),"PASS: real Player.prefab; external Transform/UI/health references; parent and pose retained; repeat is idempotent.\nPASS: unmatched child reference refuses replacement and preserves original player and references. No scene saved.");
    }
    static void OnState(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.ExitingPlayMode){if(running||SessionState.GetBool(KEY,false))Cleanup();return;}
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(KEY,false))return;
        running=true;output=Output();Directory.CreateDirectory(output);results.Clear();failures.Clear();editorPassed=SessionState.GetBool(KEY+".editor",false);
        previousPlayer=GameContext.PlayerTransform;var target=new GameObject("SafetyPlayer");target.transform.position=new Vector3(20,0,0);GameContext.PlayerTransform=target.transform;
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=Vector3.down*.1f;floor.transform.localScale=new Vector3(60,.2f,60);
        started=EditorApplication.timeSinceStartup;until=0;scenario=Scenario();EditorApplication.update+=Update;
    }
    static Enemy CreateEnemy(Vector3 position)
    {
        var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB),position,Quaternion.identity).GetComponent<Enemy>();
        actor.GetComponent<EnemyPursuit>().enabled=false;actor.GetComponent<EnemyAutoAttack>().enabled=false;return actor;
    }
    static void Check(bool pass,string label){results.Add((pass?"PASS ":"FAIL ")+label);if(!pass)failures.Add(label);}
    static IEnumerator Scenario()
    {
        Check(editorPassed,"Player replacement and current saved scene audit passed");
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=2;wall.transform.position=Vector3.up*1.5f;wall.transform.localScale=new Vector3(.2f,4,20);
        for(int i=0;i<2;i++)
        {
            var actor=CreateEnemy(new Vector3(-.2f,0,0));var body=actor.GetComponent<CapsuleCollider>();Physics.SyncTransforms();
            Check(Physics.ComputePenetration(body,body.transform.position,body.transform.rotation,wall.GetComponent<Collider>(),wall.transform.position,wall.transform.rotation,out _,out _),"Fixture really starts overlapping the fence "+i);
            var start=actor.Position;
            if(i==0)Check(!actor.TryMoveOnGround(Vector3.right*3)&&actor.Position==start,"Pursuit does not cross fence from an overlapping start");
            actor.Launch(Vector3.right*8,701+i);Check(!body.enabled,"Flight overlap test uses a disabled body collider "+i);
            yield return Time.timeAsDouble+.15;
            Check(Vector3.Distance(actor.Position,start)<.001f&&actor.State==Enemy.MotionState.Idle,"Flight does not move through initially overlapping fence "+i);
            Object.Destroy(actor.gameObject);yield return Time.timeAsDouble+.03;
        }
        Object.Destroy(wall);yield return Time.timeAsDouble+.03;

        var prototype=CreateEnemy(new Vector3(12,0,12)).gameObject;
        var capsule=prototype.GetComponent<CapsuleCollider>();
        var spawner=new GameObject("SafetySpawner").AddComponent<EnemySpawner>();
        var point=new GameObject("SafetySpawnPoint").AddComponent<EnemySpawnPoint>();
        var pointData=new SerializedObject(point);pointData.FindProperty("radius").floatValue=0;pointData.ApplyModifiedPropertiesWithoutUndo();
        var plan=Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyWavePlan>("Assets/_Project/Data/Tuning/EnemyWavePlan.asset"));
        var planData=new SerializedObject(plan);planData.FindProperty("minimumPlayerDistance").floatValue=0;planData.FindProperty("minimumEnemyDistance").floatValue=0;planData.ApplyModifiedPropertiesWithoutUndo();
        var spawnParent=new GameObject("ScaledSpawnParent").transform;
        var settings=new SerializedObject(spawner);settings.FindProperty("plan").objectReferenceValue=plan;var points=settings.FindProperty("points");points.arraySize=1;points.GetArrayElementAtIndex(0).objectReferenceValue=point;
        settings.FindProperty("spawnedRoot").objectReferenceValue=spawnParent;settings.ApplyModifiedPropertiesWithoutUndo();
        var fence=GameObject.CreatePrimitive(PrimitiveType.Cube);fence.layer=2;
        for(int i=0;i<5;i++)
        {
            spawnParent.localScale=i==2?new Vector3(2,1,2):Vector3.one;
            capsule.direction=i==4?0:1;capsule.radius=i==1?1.2f:.48f;capsule.height=3;capsule.center=new Vector3(0,1.5f,i==3?.8f:0);
            fence.transform.localScale=i==0?new Vector3(2,.2f,2):i==4?new Vector3(4,4,.2f):new Vector3(.2f,4,4);
            fence.transform.position=i==0?new Vector3(0,2.7f,0):i==4?new Vector3(0,1.5f,1.2f):new Vector3(i==1?1.1f:i==2?.9f:1.1f,1.5f,0);
            var preview=Object.Instantiate(prototype,Vector3.zero,Quaternion.LookRotation(Vector3.right),spawnParent);Physics.SyncTransforms();
            var native=preview.GetComponent<CapsuleCollider>();
            Check(Physics.ComputePenetration(native,native.transform.position,native.transform.rotation,fence.GetComponent<Collider>(),fence.transform.position,fence.transform.rotation,out _,out _),"Native spawned capsule intersects fixture fence "+i);
            Check(!Physics.CheckSphere(Vector3.up,plan.GroundMargin,4,QueryTriggerInteraction.Ignore),"Old sphere-only check misses this collision "+i);
            Object.DestroyImmediate(preview);Physics.SyncTransforms();
            Check(!spawner.TrySpawn(prototype,EnemySpawnArea.Anywhere,out _)&&spawner.AliveCount==0,"Full capsule rejects upper-body/radius/scale/offset/direction collision "+i);
            var previousFence=fence.transform.position;fence.transform.position=new Vector3(15,10,15);Physics.SyncTransforms();
            bool spawned=spawner.TrySpawn(prototype,EnemySpawnArea.Anywhere,out var clear);
            Check(spawned&&clear!=null,"Same spawn succeeds after obstacle is removed "+i);
            spawner.ClearOwnedEnemies();fence.transform.position=previousFence;yield return Time.timeAsDouble+.03;
        }
    }
    static void Update()
    {
        if(!running||!EditorApplication.isPlaying)return;
        try{if(EditorApplication.timeSinceStartup-started>90)throw new Exception("Safety timeout");if(Time.timeAsDouble<until)return;if(!scenario.MoveNext()){Finish(null);return;}until=scenario.Current is double next?next:0;}
        catch(Exception error){Finish(error);}
    }
    static void Cleanup(){EditorApplication.update-=Update;SessionState.EraseBool(KEY);SessionState.EraseBool(KEY+".editor");GameContext.PlayerTransform=previousPlayer;previousPlayer=null;scenario=null;running=false;}
    static void Finish(Exception error)
    {
        if(!running)return;Cleanup();bool pass=error==null&&failures.Count==0;
        File.WriteAllText(Path.Combine(output,"result.txt"),(pass?"PASS":"FAIL")+"\n"+string.Join("\n",results)+(error!=null?"\n"+error:""));
        Debug.Log("ENEMY_OBSTACLE_SAFETY_"+(pass?"PASS":"FAIL")+" "+results.Count+" checks");if(error!=null)Debug.LogException(error);EditorApplication.Exit(pass?0:1);
    }
}
#endif
