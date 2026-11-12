#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class FenceRoundChecks
{
    static Enemy actor;static WaveController randomFlow;static GameObject wall;static double deadline;static int stage;static string output;static bool active;
    static readonly System.Collections.Generic.List<string> results=new System.Collections.Generic.List<string>();
    const string KEY="Sanjo.FenceRound";
    static FenceRoundChecks()=>EditorApplication.playModeStateChanged+=State;
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);results.Add(message);}
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();output=args[Array.IndexOf(args,"-fenceOutput")+1];Directory.CreateDirectory(output);SessionState.SetString(KEY+".out",output);
        // 元のアセットは保存せず、一時コピーでPrefabオーバーライドを保存・再読込する。
        const string temp="Assets/_Project/Scenes/Dev/Sanjo/FencePersistenceAudit.unity";
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(temp)!=null)throw new Exception("Temporary audit scene already exists");
        EditorSceneManager.OpenScene(LatestPlayerHandoffSetup.OBSERVATION_SCENE);
        var point=Object.FindFirstObjectByType<WaveController>().Spawner.Points[0];var position=point.transform.position+Vector3.right*.31f;
        EnemyStageTools.MovePoint(point,position);EnemyStageTools.SetPointActive(point,false);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),temp,true);
        EditorSceneManager.OpenScene(temp);point=Object.FindFirstObjectByType<WaveController>().Spawner.Points[0];
        if(point.gameObject.activeSelf||Vector3.Distance(point.transform.position,position)>.001f)throw new Exception("Prefab point overrides lost on reload");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);AssetDatabase.DeleteAsset(temp);
        SessionState.SetBool(KEY,true);EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.ExitingPlayMode){EditorApplication.update-=Tick;SessionState.EraseBool(KEY);active=false;return;}
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(KEY,false))return;
        output=SessionState.GetString(KEY+".out","");results.Clear();results.Add("Prefab point position and enabled state survive save/reload");active=true;stage=0;
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=Vector3.down*.1f;floor.transform.localScale=new Vector3(30,.2f,30);
        wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=2;wall.transform.position=Vector3.up;wall.transform.localScale=new Vector3(.2f,4,10);
        deadline=EditorApplication.timeSinceStartup+15;EditorApplication.update+=Tick;
        actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/FloatingShadowEnemy.prefab"),new Vector3(-.3f,0,0),Quaternion.identity).GetComponent<Enemy>();
        actor.GetComponent<EnemyPursuit>().enabled=false;actor.GetComponent<EnemyAutoAttack>().enabled=false;Physics.SyncTransforms();
    }
    static void Tick()
    {
        if(!active||!EditorApplication.isPlaying)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Fence test timeout");
            if(stage==0)
            {
                Check(!actor.TryMoveOnGround(Vector3.right),"Penetrating fence cannot be crossed");
                Check(actor.TryMoveOnGround(Vector3.left*.3f),"Pursuit can separate from existing fence overlap");
                actor.transform.position=new Vector3(-.58f,0,0);Physics.SyncTransforms();
                Check(actor.TryMoveOnGround(Vector3.left*.2f),"Pursuit can leave exact fence contact");
                actor.transform.position=new Vector3(-.3f,0,0);Physics.SyncTransforms();actor.Launch(Vector3.left*4,99);stage=1;return;
            }
            if(stage==1)
            {
                if(actor.State!=Enemy.MotionState.Idle||!actor.CanPursue)return;
                Check(actor.Position.x<-1,"Knockback can move away from overlapping fence: x="+actor.Position.x+" slash="+actor.LastSlashId);
                Check(actor.BodyCollider.enabled,"Body hit detection returns after fence knockback stops");
                Check(Physics.OverlapSphere(actor.Position+Vector3.up,.7f,1<<actor.gameObject.layer,QueryTriggerInteraction.Collide).Contains(actor.BodyCollider),"Stopped enemy is present in native hit queries");
                var targets=new System.Collections.Generic.List<SlashTarget>();
                SlashResolver.Resolve(actor.Position-Vector3.forward*2,actor.Position+Vector3.forward*2,Vector3.forward,AssetDatabase.LoadAssetAtPath<PlayerTuning>("Assets/_Project/Data/Tuning/PlayerTuning.asset"),GameContext.MeiStats,1<<actor.gameObject.layer,targets,out _);
                Check(targets.Count==1&&ReferenceEquals(targets[0].Enemy,actor),"Actual player SlashResolver detects the stopped fence enemy");
                // 頭上の柵は水平掃引では届かないが、上り坂の高さ補正を加えると接触する配置。
                wall.SetActive(false);
                var ramp=GameObject.CreatePrimitive(PrimitiveType.Cube);ramp.transform.position=new Vector3(5,.6f,0);ramp.transform.localScale=new Vector3(4,.2f,4);ramp.transform.rotation=Quaternion.Euler(0,0,20);
                var overhead=GameObject.CreatePrimitive(PrimitiveType.Cube);overhead.layer=2;overhead.transform.position=new Vector3(4.6f,3.4f,0);overhead.transform.localScale=new Vector3(.4f,.3f,4);
                Physics.SyncTransforms();Physics.Raycast(new Vector3(4,4,0),Vector3.down,out var rampHit,6,1,QueryTriggerInteraction.Ignore);actor.transform.position=rampHit.point;Physics.SyncTransforms();
                EnemyCapsuleGeometry.GetWorld(actor.BodyCollider,out var low,out var high,out float bodyRadius);
                Check(!Physics.CheckCapsule(low,high,bodyRadius,4,QueryTriggerInteraction.Ignore),"Slope test starts outside the overhead fence");
                var start=actor.Position;Check(!actor.TryMoveOnGround(Vector3.right*.6f)&&actor.Position==start,"Pursuit sweeps floor-height displacement before entering an overhead fence");
                overhead.SetActive(false);Check(actor.TryMoveOnGround(Vector3.right*.6f)&&actor.Position.y>start.y+.15f,"The same slope is traversable without the overhead fence");
                // 候補の型と抽選回数を実際に検査する。
                var batch=new EnemyWavePlan.Batch();var field=typeof(EnemyWavePlan.Batch).GetField("randomPrefabs",BindingFlags.Instance|BindingFlags.NonPublic);
                var candidates=new[]{"Shadow","Shield","Werewolf"}.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Floating"+n+"Enemy.prefab")).ToArray();field.SetValue(batch,candidates);
                var previous=UnityEngine.Random.state;UnityEngine.Random.InitState(9182);var picked=new System.Collections.Generic.HashSet<GameObject>();
                for(int i=0;i<90;i++)picked.Add(batch.PickPrefab());UnityEngine.Random.state=previous;
                Check(picked.SetEquals(candidates),"Random selection chooses all three configured enemy prefabs");
                // phase=0での手動終了通知をスイート自身へ渡す。次の通常Playを起動するKEYが残らないことを確認する。
                var suite=typeof(EnemyCheckCancellationChecks);var flags=BindingFlags.Static|BindingFlags.NonPublic;
                const string suiteKey="Sanjo.CheckCancellationSuite";
                SessionState.SetBool(suiteKey,true);SessionState.SetInt(suiteKey+".phase",0);
                var tick=(EditorApplication.CallbackFunction)Delegate.CreateDelegate(typeof(EditorApplication.CallbackFunction),suite.GetMethod("Tick",flags));
                EditorApplication.update+=tick;
                suite.GetMethod("OnState",flags).Invoke(null,new object[]{PlayModeStateChange.ExitingPlayMode});
                Check(!SessionState.GetBool(suiteKey,false),"Cancellation suite clears its own KEY on an early manual-stop notification");
                var callbacks=typeof(EditorApplication).GetField("update",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).GetValue(null) as Delegate;
                Check(callbacks==null||callbacks.GetInvocationList().All(d=>d.Method.DeclaringType!=suite),"Cancelled suite leaves no Editor update callback");
                // 抽選メソッドだけでなく、実際のWaveController→Spawnerまで通す。既存の計画は変更しない。
                var randomPlan=ScriptableObject.CreateInstance<EnemyWavePlan>();var planData=new SerializedObject(randomPlan);
                var waves=planData.FindProperty("waves");waves.arraySize=1;var batches=waves.GetArrayElementAtIndex(0).FindPropertyRelative("batches");batches.arraySize=1;
                var entry=batches.GetArrayElementAtIndex(0);entry.FindPropertyRelative("count").intValue=30;entry.FindPropertyRelative("interval").floatValue=.01f;entry.FindPropertyRelative("areas").intValue=(int)EnemySpawnArea.AnyEdge;
                var pool=entry.FindPropertyRelative("randomPrefabs");pool.arraySize=3;for(int i=0;i<3;i++)pool.GetArrayElementAtIndex(i).objectReferenceValue=candidates[i];
                planData.FindProperty("initialDelay").floatValue=0;planData.ApplyModifiedPropertiesWithoutUndo();
                var container=new GameObject("RandomPoolRuntimeAudit");container.SetActive(false);var spawner=container.AddComponent<EnemySpawner>();randomFlow=container.AddComponent<WaveController>();
                var spawnData=new SerializedObject(spawner);spawnData.FindProperty("plan").objectReferenceValue=randomPlan;
                var spawnPoints=spawnData.FindProperty("points");spawnPoints.arraySize=30;
                for(int i=0;i<30;i++){var node=new GameObject("RandomPoint"+i);node.transform.SetParent(container.transform);node.transform.position=new Vector3(-10+(i%5)*5,0,-11+(i/5)*4.4f);spawnPoints.GetArrayElementAtIndex(i).objectReferenceValue=node.AddComponent<EnemySpawnPoint>();}
                spawnData.ApplyModifiedPropertiesWithoutUndo();var flowData=new SerializedObject(randomFlow);flowData.FindProperty("plan").objectReferenceValue=randomPlan;flowData.FindProperty("spawner").objectReferenceValue=spawner;flowData.ApplyModifiedPropertiesWithoutUndo();
                UnityEngine.Random.InitState(9182);container.SetActive(true);stage=2;deadline=EditorApplication.timeSinceStartup+15;return;
            }
            if(stage==2)
            {
                if(randomFlow.State==WaveController.WaveState.Faulted)throw new Exception(randomFlow.LastError);
                if(randomFlow.Spawner.TotalSpawned!=30)return;
                Check(randomFlow.Spawner.OwnedEnemies.Select(e=>e.Tuning.BehaviourRole).Distinct().Count()==3,"Actual WaveController random pool spawns Shadow, Shield and LampEater");
                Check(randomFlow.PendingCount==0&&randomFlow.State==WaveController.WaveState.WaitingForEnemies,"Random wave preserves pending and alive-enemy completion rules");
                Finish(null);
            }
        }
        catch(Exception error){Finish(error);}
    }
    static void Finish(Exception error){EditorApplication.update-=Tick;SessionState.EraseBool(KEY);active=false;File.WriteAllText(Path.Combine(output,"result.txt"),(error==null?"PASS":"FAIL "+error)+"\n"+string.Join("\n",results));if(error!=null)Debug.LogException(error);EditorApplication.Exit(error==null?0:1);}
}
#endif
