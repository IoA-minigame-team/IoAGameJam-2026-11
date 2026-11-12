#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>検証中に実際のPlay終了遷移を起こし、ドメイン再読み込みより前に後始末を点検する。</summary>
[InitializeOnLoad]
public static class EnemyCheckCancellationChecks
{
    const string KEY="Sanjo.CheckCancellationSuite";
    static readonly Type[] RUNNERS={typeof(EnemyActionPlayModeChecks),typeof(EnemyLaunchPlayModeChecks),typeof(EnemyPursuitPlayModeChecks),typeof(EnemyReviewRegressionChecks),typeof(EnemyWavePlayModeChecks),typeof(InkReviewPlayModeChecks),typeof(LatestPlayerHandoffChecks),typeof(PlayerIntegrationPlayModeChecks)};
    static double due;
    static readonly BindingFlags FLAGS=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static EnemyCheckCancellationChecks(){EditorApplication.playModeStateChanged+=OnState;}
    static string Output
    {
        get{var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-sanjoCancelOutput");return i>=0?args[i+1]:Path.Combine(Path.GetTempPath(),"HitofuriCancellationChecks");}
    }
    static Type Runner=>RUNNERS[SessionState.GetInt(KEY+".index",0)];
    static string RunnerKey=>(string)Runner.GetField("KEY",FLAGS).GetRawConstantValue();
    public static void Run()
    {
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"result.txt"),"RUNNING\n");
        SessionState.SetBool(KEY,true);SessionState.SetInt(KEY+".index",0);SessionState.SetInt(KEY+".phase",0);SessionState.SetInt(KEY+".count",0);
        StartRunner();
    }
    static void StartRunner()
    {
        try
        {
            SessionState.SetInt(KEY+".background",(int)InputSystem.settings.backgroundBehavior);
            SessionState.SetInt(KEY+".editorInput",(int)InputSystem.settings.editorInputBehaviorInPlayMode);
            Runner.GetMethod("Run",FLAGS).Invoke(null,null);
        }
        catch(Exception error){Finish(error);}
    }
    static void OnState(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(KEY,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){due=EditorApplication.timeSinceStartup+.4;EditorApplication.update+=Tick;}
        if(state==PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update-=Tick;
            int stopPhase=SessionState.GetInt(KEY+".phase",0);
            if(stopPhase!=1&&stopPhase!=3)CancelSuite();
        }
        if(state!=PlayModeStateChange.EnteredEditMode)return;
        int phase=SessionState.GetInt(KEY+".phase",0);
        if(phase==1)
        {
            SessionState.SetInt(KEY+".phase",2);
            // KEYを立てずに通常のPlayを再開する。検証が勝手に再開しないことを実際に確かめる。
            EditorApplication.delayCall+=EditorApplication.EnterPlaymode;
        }
        else if(phase==3)
        {
            int next=SessionState.GetInt(KEY+".index",0)+1;
            if(next==RUNNERS.Length){Finish(null);return;}
            SessionState.SetInt(KEY+".index",next);SessionState.SetInt(KEY+".phase",0);EditorApplication.delayCall+=StartRunner;
        }
    }
    static void CancelSuite()
    {
        EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=AuditStop;
        EditorApplication.delayCall-=StartRunner;EditorApplication.delayCall-=EditorApplication.EnterPlaymode;
        SessionState.EraseBool(KEY);
        foreach(string suffix in new[]{".index",".phase",".count",".background",".editorInput"})SessionState.EraseInt(KEY+suffix);
        Debug.Log("検証スイートを手動停止しました。通常Playには持ち越しません。");
    }
    static void Tick()
    {
        if(!SessionState.GetBool(KEY,false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<due)return;
        try
        {
            int phase=SessionState.GetInt(KEY+".phase",0);
            if(phase==0)
            {
                Check((bool)Runner.GetField("running",FLAGS).GetValue(null),"normal EnteredPlayMode starts verification");
                // 停止要求を所有する検証は、停止中の手動終了でも要求を解放できるか調べる。
                var owner=Runner.GetField("pauseOwner",FLAGS);
                if(owner!=null){var request=new object();owner.SetValue(null,request);TimeController.RequestPause(request);}
                SessionState.SetInt(KEY+".phase",1);
                // この購読は全runnerのOnStateより後。ExitingPlayMode中、再読み込みで状態が消える前に点検する。
                EditorApplication.playModeStateChanged+=AuditStop;
                EditorApplication.ExitPlaymode();
            }
            else if(phase==2)
            {
                VerifyReleased("ordinary Play after cancellation");
                SessionState.SetInt(KEY+".phase",3);EditorApplication.ExitPlaymode();
            }
        }
        catch(Exception error){Finish(error);}
    }
    static void AuditStop(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.ExitingPlayMode)return;
        EditorApplication.playModeStateChanged-=AuditStop;
        try{VerifyReleased("before domain reload");}
        catch(Exception error){Finish(error);}
    }
    static void VerifyReleased(string phase)
    {
        Check(!SessionState.GetBool(RunnerKey,false)&&SessionState.GetBool(RunnerKey,true),phase+": SessionState KEY erased");
        Check(!(bool)Runner.GetField("running",FLAGS).GetValue(null),phase+": runner inactive");
        var callbackField=typeof(EditorApplication).GetField("update",FLAGS);
        if(callbackField==null)throw new Exception("Cannot inspect Editor update callbacks");
        var updates=callbackField.GetValue(null) as Delegate;
        Check(updates==null||updates.GetInvocationList().All(d=>d.Method.DeclaringType!=Runner),phase+": Editor update callback removed");
        foreach(Type bus in new[]{typeof(GameEvents),typeof(EnemyMotionEvents),typeof(GameScreenEvents)})
        {
            bool found=bus.GetFields(FLAGS).Select(f=>f.GetValue(null)).OfType<Delegate>().Any(d=>d.GetInvocationList().Any(h=>h.Method.DeclaringType==Runner));
            Check(!found,phase+": "+bus.Name+" subscriptions removed");
        }
        Check(!InputSystem.devices.Any(d=>d.name.StartsWith("IntegrationTest")||d.name.StartsWith("InkReviewTest")),phase+": test input devices removed");
        var pause=Runner.GetField("pauseOwner",FLAGS);
        if(pause!=null)Check(pause.GetValue(null)==null,phase+": owned pause released and cleared");
        if(Runner==typeof(InkReviewPlayModeChecks)&&phase=="before domain reload")
            Check((int)InputSystem.settings.backgroundBehavior==SessionState.GetInt(KEY+".background",-1)&&(int)InputSystem.settings.editorInputBehaviorInPlayMode==SessionState.GetInt(KEY+".editorInput",-1),phase+": original input settings restored");
    }
    static void Check(bool pass,string message)
    {
        if(!pass)throw new Exception(Runner.Name+": "+message);
        File.AppendAllText(Path.Combine(Output,"result.txt"),"PASS "+Runner.Name+": "+message+"\n");
        SessionState.SetInt(KEY+".count",SessionState.GetInt(KEY+".count",0)+1);
    }
    static void Finish(Exception error)
    {
        EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=AuditStop;SessionState.EraseBool(KEY);
        string path=Path.Combine(Output,"result.txt");
        string details=File.ReadAllText(path);if(details.StartsWith("RUNNING\n"))details=details.Substring("RUNNING\n".Length);
        File.WriteAllText(path,(error==null?"PASS":"FAIL: "+error)+"\n"+details);
        int count=SessionState.GetInt(KEY+".count",0);
        foreach(string suffix in new[]{".index",".phase",".count",".background",".editorInput"})SessionState.EraseInt(KEY+suffix);
        if(error!=null)Debug.LogException(error);else Debug.Log("ENEMY_CHECK_CANCELLATION_PASS "+count+" checks / 8 runners");
        EditorApplication.Exit(error==null?0:1);
    }
}
#endif
