#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

/// <summary>通常の戦闘に加え、部品の再有効化・凹凸・編集途中の不完全な設定を再現する。</summary>
[InitializeOnLoad]
public static class EnemyAdditionalAuditChecks
{
    const string KEY="Sanjo.AdditionalAudit";
    static readonly BindingFlags FLAGS=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> results=new List<string>();
    static readonly List<string> failures=new List<string>();
    static IEnumerator scenario;
    static double due,started;
    static bool running;
    static string output;
    static WaveController flow;
    static InkReviewInterface view;
    static Enemy actor;
    static string Output(){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-sanjoAuditOutput");return i>=0?a[i+1]:Path.Combine(Path.GetTempPath(),"HitofuriAdditionalAudit");}
    static EnemyAdditionalAuditChecks()=>EditorApplication.playModeStateChanged+=OnState;
    static void Check(bool pass,string message){results.Add((pass?"PASS ":"FAIL ")+message);if(!pass)failures.Add(message);}
    static void Field(object target,string name,object value)=>target.GetType().GetField(name,FLAGS).SetValue(target,value);
    static void SetPlan(Component target,EnemyWavePlan plan){var data=new SerializedObject(target);data.FindProperty("plan").objectReferenceValue=plan;data.ApplyModifiedPropertiesWithoutUndo();}
    public static void Run()
    {
        output=Output();Directory.CreateDirectory(output);results.Clear();failures.Clear();
        EditorSceneManager.OpenScene(LatestPlayerHandoffSetup.OBSERVATION_SCENE);
        flow=Object.FindFirstObjectByType<WaveController>();var original=flow.Plan;var points=flow.Spawner.Points;
        for(int i=0;i<5;i++)
        {
            var plan=Object.Instantiate(original);SetPlan(flow,plan);SetPlan(flow.Spawner,plan);
            if(i==0)Field(plan,"waves",Array.Empty<EnemyWavePlan.Wave>());
            if(i==1)Field(plan,"waves",new[]{new EnemyWavePlan.Wave()});
            if(i==2)Field(plan,"waves",new EnemyWavePlan.Wave[]{null});
            if(i==3){var wave=new EnemyWavePlan.Wave();Field(wave,"batches",new EnemyWavePlan.Batch[]{null});Field(plan,"waves",new[]{wave});}
            if(i==4)Field(flow.Spawner,"points",null);
            try{Check(EnemyStageTools.Validate(flow).Any(m=>m.StartsWith("NG")),"Tool reports invalid empty/null configuration without exception, case "+i);}
            catch(Exception error){Check(false,"Tool reports invalid empty/null configuration without exception, case "+i+": "+error.GetType().Name);}
            Field(flow.Spawner,"points",points);SetPlan(flow,original);SetPlan(flow.Spawner,original);Object.DestroyImmediate(plan);
        }
        File.WriteAllLines(Path.Combine(output,"editor-results.txt"),results);
        SessionState.SetBool(KEY,true);EditorApplication.EnterPlaymode();
    }
    static void OnState(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.ExitingPlayMode){if(running||SessionState.GetBool(KEY,false))Cleanup();return;}
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(KEY,false))return;
        running=true;output=Output();results.Clear();results.AddRange(File.ReadAllLines(Path.Combine(output,"editor-results.txt")));failures.Clear();failures.AddRange(results.Where(s=>s.StartsWith("FAIL")));
        flow=Object.FindFirstObjectByType<WaveController>();view=Object.FindFirstObjectByType<InkReviewInterface>();flow.enabled=false;
        started=EditorApplication.timeSinceStartup;due=0;scenario=Scenario();EditorApplication.update+=Update;
    }
    static double Wait(float seconds)=>Time.timeAsDouble+seconds;
    static IEnumerator Scenario()
    {
        yield return Wait(.1f);
        view.TogglePause();Check(TimeController.IsPaused,"UI pause initially owns a shared pause");
        view.enabled=false;Check(!TimeController.IsPaused,"Disabled UI releases its pause");
        view.enabled=true;Check(TimeController.IsPaused&&view.IsPaused,"Reenabled paused UI restores actual shared pause");
        // 再開直後の2フレームはクリックの貫通防止で止める仕様。その期間を過ぎてから確認する。
        view.TogglePause();yield return Wait(.1f);Check(!TimeController.IsPaused,"Pause button resumes after reenable");

        // バーはコードの見かけで判断せず、Unity Imageが実際に作るメッシュの横幅を測る。
        Field(flow,"<PendingCount>k__BackingField",1);Field(view,"totalThisWave",3);yield return Wait(.1f);
        var bar=(Image)typeof(InkReviewInterface).GetField("remainingBar",FLAGS).GetValue(view);
        using(var vertices=new VertexHelper())
        {
            typeof(Image).GetMethod("OnPopulateMesh",FLAGS,null,new[]{typeof(VertexHelper)},null).Invoke(bar,new object[]{vertices});
            var mesh=new Mesh();vertices.FillMesh(mesh);mesh.RecalculateBounds();
            float visible=mesh.bounds.size.x*bar.transform.localScale.x/bar.rectTransform.rect.width;
            Check(Mathf.Abs(visible-1f/3)<.01f,"Actual progress mesh matches one-third remaining: "+visible.ToString("F3"));Object.Destroy(mesh);
        }
        Field(flow,"<PendingCount>k__BackingField",0);
        GameEvents.RaiseWaveStarted(1);Check(TimeController.IsPaused,"Wave banner owns a pause");
        view.enabled=false;view.enabled=true;
        Check(TimeController.IsPaused&&view.IsBannerVisible,"Reenabled banner still pauses enemy spawning");
        GameEvents.RaiseGameOver();yield return Wait(.1f);Check(!TimeController.IsPaused,"GameOver releases UI pause");

        // WaveControllerは無効中にGameOverを聞けなかった。再有効化時に終了状態を復元する必要がある。
        int spawned=flow.Spawner.TotalSpawned;flow.enabled=true;
        Check(flow.State==WaveController.WaveState.GameOver,"Reenabled wave controller synchronizes missed GameOver");
        yield return Wait(.85f);
        Check(flow.Spawner.TotalSpawned==spawned,"No enemy is spawned after GameOver when wave controller returns");flow.enabled=false;

        actor=CreateEnemy(new Vector3(100,0,100));GameEvents.RaiseGameOver();
        var animation=actor.GetComponent<EnemyActionAnimation>();animation.enabled=false;animation.enabled=true;
        Check(actor.IsStoppedByGameOver&&animation.IsStoppedByGameOver&&actor.GetComponentInChildren<Animator>().speed==0,"Reenabled enemy presentation stays stopped after GameOver");
        Object.Destroy(actor.gameObject);actor=null;

        // 同じ初速と摩擦で、斜面上でも水平の停止距離は変えず足元だけを地面に合わせる。
        var slope=GameObject.CreatePrimitive(PrimitiveType.Cube);slope.name="AuditSlope";
        slope.transform.position=new Vector3(100,0,100);slope.transform.rotation=Quaternion.Euler(0,0,20);slope.transform.localScale=new Vector3(20,.2f,10);
        Physics.SyncTransforms();Physics.Raycast(new Vector3(100,20,100),Vector3.down,out var startHit,40,1,QueryTriggerInteraction.Ignore);
        actor=CreateEnemy(startHit.point);actor.Launch(Vector3.right*4,9101);double end=Time.timeAsDouble+3;
        while(actor.State!=Enemy.MotionState.Idle&&Time.timeAsDouble<end)yield return Wait(.04f);
        Physics.SyncTransforms();bool grounded=Physics.Raycast(actor.Position+Vector3.up*10,Vector3.down,out var endHit,20,1,QueryTriggerInteraction.Ignore);
        Check(actor.State==Enemy.MotionState.Idle&&Mathf.Abs(actor.Position.x-startHit.point.x-2)<.03f,"Slope flight keeps expected horizontal friction stopping distance");
        Check(grounded&&Mathf.Abs(actor.Position.y-endHit.point.y)<.01f,"Stopped enemy root follows uneven ground: vertical error "+Mathf.Abs(actor.Position.y-endHit.point.y).ToString("F3"));
        Object.Destroy(actor.gameObject);actor=null;

        // 進行と出現が別の計画を持っている場合は、始める前に原因を示して停止する。
        Object.FindFirstObjectByType<GameOverController>().ResetRound();
        var root=new GameObject("MismatchedPlans");var mismatch=root.AddComponent<WaveController>();var spawner=root.AddComponent<EnemySpawner>();
        var copy=Object.Instantiate(flow.Plan);SetPlan(mismatch,flow.Plan);SetPlan(spawner,copy);
        var data=new SerializedObject(mismatch);data.FindProperty("spawner").objectReferenceValue=spawner;data.FindProperty("startAutomatically").boolValue=false;data.ApplyModifiedPropertiesWithoutUndo();
        Check(!mismatch.StartRun()&&mismatch.State==WaveController.WaveState.Faulted,"Mismatched wave/spawn plans are rejected before run starts");
    }
    static Enemy CreateEnemy(Vector3 position)
    {
        var enemy=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/FloatingShadowEnemy.prefab"),position,Quaternion.identity).GetComponent<Enemy>();
        enemy.GetComponent<EnemyPursuit>().enabled=false;enemy.GetComponent<EnemyAutoAttack>().enabled=false;return enemy;
    }
    static void Update()
    {
        if(!running||!EditorApplication.isPlaying)return;
        try{if(EditorApplication.timeSinceStartup-started>90)throw new Exception("Additional audit timeout");if(Time.timeAsDouble<due)return;if(!scenario.MoveNext()){Finish(null);return;}due=scenario.Current is double value?value:0;}
        catch(Exception error){Finish(error);}
    }
    static void Cleanup(){EditorApplication.update-=Update;SessionState.EraseBool(KEY);scenario=null;flow=null;view=null;actor=null;running=false;}
    static void Finish(Exception error)
    {
        if(!running)return;Cleanup();bool pass=error==null&&failures.Count==0;
        File.WriteAllText(Path.Combine(output,"result.txt"),(pass?"PASS":"FAIL")+"\n"+string.Join("\n",results)+(error!=null?"\n"+error:""));
        Debug.Log("ENEMY_ADDITIONAL_AUDIT_"+(pass?"PASS":"FAIL")+" "+results.Count+" checks");if(error!=null)Debug.LogException(error);EditorApplication.Exit(pass?0:1);
    }
}
#endif
