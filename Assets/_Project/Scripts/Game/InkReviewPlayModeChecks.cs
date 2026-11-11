#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class InkReviewPlayModeChecks
{
    const string KEY="Sanjo.InkReviewChecks";
    static readonly string OUTPUT=OutputDirectory();
    static readonly List<string> checks=new List<string>(),events=new List<string>();
    static readonly List<int> damages=new List<int>();
    static readonly Dictionary<EnemyBehaviourRole,int> attacks=new Dictionary<EnemyBehaviourRole,int>();
    static readonly List<ILaunchable> hitActors=new List<ILaunchable>();
    static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
    static InkReviewSession session;static InkReviewInterface view;static PlayerController player;
    static Gamepad pad;static Mouse mouse;static Vector2 mousePosition;static double realStart,waitUntil,startTime,nextCapture;
    static Until condition;static string label;static int frame,gameovers,firstSpawnCount;
    static int titleRequests;
    static InputSettings.BackgroundBehavior previousBackground;
    static InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
    sealed class Until
    {
        public readonly Func<bool> Predicate;public readonly double Timeout;
        public Until(Func<bool> predicate,float seconds){Predicate=predicate;Timeout=EditorApplication.timeSinceStartup+seconds;}
    }
    static InkReviewPlayModeChecks(){EditorApplication.playModeStateChanged+=OnState;}
    static string OutputDirectory()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-sanjoInkOutput");
        return Path.GetFullPath(i>=0&&i+1<args.Length?args[i+1]:Path.Combine(Path.GetTempPath(),"HitofuriInkReview"))+Path.DirectorySeparatorChar;
    }
    public static void Run(){SessionState.SetBool(KEY,true);EditorSceneManager.OpenScene(InkReviewAssetSetup.REVIEW_SCENE);EditorApplication.EnterPlaymode();}
    static void OnState(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(KEY,false))return;
        Bind();Application.runInBackground=true;checks.Clear();events.Clear();damages.Clear();attacks.Clear();hitActors.Clear();stack.Clear();frame=gameovers=firstSpawnCount=titleRequests=0;
        previousBackground=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        previousEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        pad=InputSystem.AddDevice<Gamepad>("InkReviewTestPad");mouse=InputSystem.AddDevice<Mouse>("InkReviewTestMouse");MoveMouse(new Vector2(5,Screen.height*.5f),false);SendPad(Vector3.zero,false);
        GameEvents.WaveStarted+=OnStarted;GameEvents.WaveCleared+=OnCleared;GameEvents.PlayerDamaged+=OnDamage;GameEvents.GameOver+=OnGameOver;
        GameEvents.SwordPlanted+=OnSword;GameEvents.EnemyHit+=OnHit;EnemyMotionEvents.AttackStarted+=OnAttack;
        Directory.CreateDirectory(OUTPUT+"frames");realStart=EditorApplication.timeSinceStartup;startTime=Time.unscaledTimeAsDouble;nextCapture=startTime;waitUntil=0;condition=null;
        label="Wave announcement before simultaneous spawn";stack.Push(Scenario());EditorApplication.update+=Update;
    }
    static void Bind(){session=Object.FindFirstObjectByType<InkReviewSession>();view=session.GetComponent<InkReviewInterface>();player=Object.FindFirstObjectByType<PlayerController>();}
    static void Log(string text)=>events.Add((Time.unscaledTimeAsDouble-startTime).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"\t"+text);
    static void OnStarted(int wave)=>Log("WaveStarted("+wave+")");
    static void OnCleared(int wave)=>Log("WaveCleared("+wave+")");
    static void OnDamage(int remaining){damages.Add(remaining);Log("PlayerDamaged("+remaining+")");}
    static void OnGameOver(){gameovers++;Log("GameOver");}
    static void OnTitleRequested(){titleRequests++;Log("GameScreenEvents.TitleRequested");}
    static void OnSword(Vector3 position)=>Log("SwordPlanted "+position);
    static void OnHit(EnemyHitInfo info){hitActors.Add(info.Enemy);Log("EnemyHit "+info.Enemy.Transform.name+" slash="+info.SlashId);}
    static void OnAttack(ILaunchable source)
    {
        var enemy=source as Enemy;if(enemy==null)return;var role=enemy.Tuning.BehaviourRole;
        attacks.TryGetValue(role,out var count);attacks[role]=count+1;Log("AttackStarted "+role);
    }
    static double Wait(float seconds)=>Time.unscaledTimeAsDouble+seconds;
    static void Check(bool value,string text){if(!value)throw new Exception(text);checks.Add(text);}
    static void SendPad(Vector3 direction,bool held)
    {
        var camera=Camera.main.transform;var stick=new Vector2(Vector3.Dot(direction,Vector3.ProjectOnPlane(camera.right,Vector3.up).normalized),Vector3.Dot(direction,Vector3.ProjectOnPlane(camera.forward,Vector3.up).normalized));
        InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=stick}.WithButton(GamepadButton.South,held));
    }
    static void MoveMouse(Vector2 position,bool down){mousePosition=position;InputSystem.QueueStateEvent(mouse,new MouseState{position=position,buttons=(ushort)(down?1:0)});}
    static IEnumerator Click(Button button)
    {
        if(button==null||!button.gameObject.activeInHierarchy)throw new Exception("UI button is not active");
        var canvas=button.GetComponentInParent<Canvas>();Vector2 point=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,button.transform.position);
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Log("CLICK "+button.name+" screen="+Screen.width+"x"+Screen.height+" point="+point+" raycast="+string.Join(",",hits.Select(h=>h.gameObject.name)));
        MoveMouse(point,false);yield return Wait(.13f);MoveMouse(point,true);yield return Wait(.09f);MoveMouse(point,false);yield return Wait(.2f);
        MoveMouse(new Vector2(5,Screen.height*.5f),false);yield return Wait(.08f);
    }
    static IEnumerator Scenario()
    {
        yield return new Until(()=>view.IsBannerVisible,5);
        Check(view.BannerLabel=="第一波"&&session.Flow.Spawner.TotalSpawned==0,"First-wave banner appears before any enemy is spawned");
        Check(TimeController.IsPaused,"Announcement owns a shared TimeController pause");CaptureNamed("wave-announcement");
        yield return Wait(.5f);Check(session.Flow.Spawner.TotalSpawned==0,"No pop occurs while the opening banner is still visible");
        yield return new Until(()=>session.Flow.Spawner.TotalSpawned==3,10);yield return Wait(.2f);
        Check(firstSpawnCount==3,"Three types are spawned in the same update budget");
        Check(session.Flow.Spawner.OwnedEnemies.Select(e=>e.Tuning.BehaviourRole).Distinct().Count()==3,"Shadow, Shield and LampEater are present together");
        Check(view.RemainingLabel.Contains("03")&&session.Flow.PendingCount==0,"Remaining UI includes all three live enemies and separates queued spawns");
        Check(!session.DamageEnabled&&!player.GetComponent<PlayerHealth>().enabled,"Review scene clearly starts with contact damage stopped for observation");
        var font=view.Canvas.GetComponentInChildren<Text>().font;
        Check("影盾持灯喰第一波終幕再挑戦".All(c=>font.HasCharacter(c)),"Bundled Japanese font supports required labels without OS font fallback");
        CaptureNamed("three-enemies");label="All three pursue and automatically attack real player";
        yield return new Until(()=>attacks.Count==3,15);
        Check(session.Flow.Spawner.OwnedEnemies.All(e=>e.GetComponent<EnemyPursuit>().Target==EnemyPursuit.PursuitTarget.Player),"All three pursue actual shared player when sword is held");
        Check(attacks.Values.All(n=>n>0),"Each type automatically starts an attack through its own event");
        Check(player.GetComponent<PlayerHealth>().DamageTaken==0,"Safe review permits repeated attacks without consuming player health");
        yield return Click(view.DamageButton);Check(session.DamageEnabled,"Actual mouse click enables contact damage via uGUI button");
        Check(!GameContext.IsSwordPlanted,"Clicking UI does not also plant the sword");
        yield return Click(view.DamageButton);Check(!session.DamageEnabled,"Actual mouse click can stop review contact damage again");
        SendPad(Vector3.zero,true);yield return new Until(()=>GameContext.IsSwordPlanted,3);yield return Wait(.15f);
        Check(session.Flow.Spawner.OwnedEnemies.Single(e=>e.Tuning.BehaviourRole==EnemyBehaviourRole.LampEater).GetComponent<EnemyPursuit>().Target==EnemyPursuit.PursuitTarget.Sword,"Real plant input switches LampEater toward sword");
        Check(session.Flow.Spawner.OwnedEnemies.Where(e=>e.Tuning.BehaviourRole!=EnemyBehaviourRole.LampEater).All(e=>e.GetComponent<EnemyPursuit>().Target==EnemyPursuit.PursuitTarget.Player),"Shadow and Shield continue choosing player");
        SendPad(Vector3.right,true);yield return Wait(.7f);SendPad(Vector3.zero,true);CaptureNamed("sword-lure");
        SendPad(Vector3.zero,false);yield return new Until(()=>!GameContext.IsSwordPlanted,3);yield return Wait(.2f);
        Check(session.Flow.Spawner.OwnedEnemies.Where(e=>e!=null).All(e=>e.GetComponent<EnemyPursuit>().Target==EnemyPursuit.PursuitTarget.Player),"Real slash event restores player target");

        label="Fixed front-guard arrangement with real player input";
        int oldId=session.GetInstanceID();yield return Click(view.GuardButton);
        yield return new Until(()=>Object.FindFirstObjectByType<InkReviewSession>().GetInstanceID()!=oldId,8);Bind();
        Check(session.GuardPractice,"Guard-layout button reloads a dedicated fixed-facing arrangement");
        yield return new Until(()=>session.Flow.Spawner.TotalSpawned==3,10);
        var shield=session.Flow.Spawner.OwnedEnemies.Single(e=>e.Tuning.BehaviourRole==EnemyBehaviourRole.ShieldBearer);
        var shadow=session.Flow.Spawner.OwnedEnemies.Single(e=>e.Tuning.BehaviourRole==EnemyBehaviourRole.Shadow);
        yield return new Until(()=>shield.GetComponent<EnemyActionAnimation>().IsDefending,6);
        Check(shield.transform.forward.x<-.9f&&!shield.GetComponent<EnemyPursuit>().enabled,"Guard fixture fixes front direction while ordinary review remains live AI");
        hitActors.Clear();Vector3 swordStart=player.transform.position;SendPad(Vector3.zero,true);yield return new Until(()=>GameContext.IsSwordPlanted,3);
        yield return new Until(()=>player.CurrentState==PlayerController.State.FreeHand,2);
        SendPad(Vector3.forward,true);yield return new Until(()=>player.transform.position.z>=swordStart.z+2,2);
        SendPad(Vector3.right,true);yield return new Until(()=>player.transform.position.x>=5,3);
        SendPad(Vector3.back,true);yield return new Until(()=>player.transform.position.z<=swordStart.z,2);
        SendPad(Vector3.zero,false);yield return new Until(()=>player.CurrentState==PlayerController.State.Recovery||player.CurrentState==PlayerController.State.Armed,3);
        Check(player.LastShieldBlockedCount==1,"Actual player slash reports one front shield block");
        Check(!hitActors.Any(e=>ReferenceEquals(e,shield))&&hitActors.Any(e=>ReferenceEquals(e,shadow)),"Front shield is not launched, and shadow behind it is hit by same real slash");
        Check(shield.State==Enemy.MotionState.Idle&&shield.GlowRatio==0,"Guard keeps shield stationary with no false hit or glow gain");CaptureNamed("front-guard");
        label="Actual contact game over and replay UI";damages.Clear();int priorGameovers=gameovers;
        yield return Click(view.DamageButton);yield return new Until(()=>gameovers>priorGameovers,18);
        Check(damages.SequenceEqual(new[]{2,1,0}),"Real PlayerHealth reaches GameOver from contact after damage is enabled");
        Check(view.IsFinished&&view.IsGameOverVisible&&player.CurrentState==PlayerController.State.Disabled,"GameOver opens game-side action panel and disables actual player");
        Check(view.ReplayButton.gameObject.activeInHierarchy&&view.RetryWaveButton.gameObject.activeInHierarchy&&view.TitleButton.gameObject.activeInHierarchy,"Game-side panel offers replay, current wave, and title request");
        Check(!view.TitleButton.interactable&&!GameScreenEvents.HasTitleHandler,"Unconnected title destination is visibly unavailable rather than navigating to a fabricated title");CaptureNamed("game-over");
        string beforeTitle=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        GameScreenEvents.TitleRequested+=OnTitleRequested;yield return Wait(.1f);Check(view.TitleButton.interactable,"Title button becomes available when external screen owner subscribes");
        yield return Click(view.TitleButton);
        Check(titleRequests==1&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().path==beforeTitle,"Actual title button emits one request and does not construct or load a title screen itself");
        GameScreenEvents.TitleRequested-=OnTitleRequested;yield return Wait(.1f);
        oldId=session.GetInstanceID();yield return Click(view.ReplayButton);yield return new Until(()=>Object.FindFirstObjectByType<InkReviewSession>().GetInstanceID()!=oldId,8);Bind();
        Check(!session.GuardPractice&&player.GetComponent<PlayerHealth>().DamageTaken==0&&!GameContext.IsSwordPlanted,"Replay recreates actual player health and sword state and returns to first wave");
        yield return new Until(()=>session.Flow.Spawner.TotalSpawned==3,10);
        yield return Click(view.PauseButton);Check(view.IsPaused&&TimeController.IsPaused,"In-game pause button requests shared pause without opening another screen");
        var enemy=session.Flow.Spawner.OwnedEnemies[0];var stopped=enemy.Position;yield return Wait(.3f);
        Check(Vector3.Distance(stopped,enemy.Position)<.001f,"Manual in-game pause keeps enemy root still");
        yield return Click(view.PauseButton);yield return Wait(.1f);
        Check(!view.IsPaused&&!TimeController.IsPaused&&!GameContext.IsSwordPlanted,"Same in-game button resumes without click-through sword input");

        label="Normal five-wave game HUD, without title or result screens";
        oldId=session.GetInstanceID();EditorSceneManager.LoadSceneInPlayMode(PlayerIntegrationAssetSetup.TEST_SCENE,new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return new Until(()=>Object.FindFirstObjectByType<InkReviewSession>().GetInstanceID()!=oldId,8);Bind();
        Check(!session.IsReview&&session.Flow.TotalWaves==5&&session.DamageEnabled,"Normal game scene preserves five waves and actual contact damage");
        yield return new Until(()=>session.Flow.State==WaveController.WaveState.WaitingForEnemies,12);
        // 別の進行fixture。敵を柵のない縁へ置いて実際に落とし、完了イベントは偽造しない。
        Log("PHASE independent wave progression fixture at open edge");
        var floor=Object.FindFirstObjectByType<MeshCollider>();float openEdge=PlayerIntegrationAssetSetup.Edge(floor,Vector3.right);
        PlayerIntegrationAssetSetup.Ground(floor,Vector3.right*(openEdge-1.5f),out var openPosition);
        foreach(var actor in session.Flow.Spawner.OwnedEnemies.ToArray()){actor.transform.position=openPosition;actor.Launch(Vector3.right*15,8001);}
        yield return new Until(()=>session.Flow.CurrentWaveNumber==2&&view.IsBannerVisible,12);
        Check(view.BannerLabel=="第二波"&&session.Flow.Spawner.AliveCount==0,"Second-wave banner is visible before its first pop");
        Check(view.RemainingLabel.Contains("05")&&session.Flow.PendingCount==5,"Second-wave remaining count includes all five pending enemies");CaptureNamed("wave-two");
        yield return new Until(()=>session.Flow.SpawnedThisWave>0,10);
        Check(view.RemainingLabel.Contains("05")&&session.Flow.PendingCount+session.Flow.Spawner.AliveCount==5,"Staggered spawning does not inflate or reduce remaining count");
        int count=gameovers;Object.FindFirstObjectByType<GameOverController>().TryEndGame();yield return Wait(.2f);
        Check(gameovers==count+1&&view.IsGameOverVisible,"Shared GameOver opens game-side controls during second wave");
        oldId=session.GetInstanceID();yield return Click(view.RetryWaveButton);yield return new Until(()=>Object.FindFirstObjectByType<InkReviewSession>().GetInstanceID()!=oldId,8);Bind();
        yield return new Until(()=>session.Flow.CurrentWaveNumber==2&&view.IsBannerVisible,8);
        Check(session.StartingWave==2&&session.Flow.PendingCount==5&&view.BannerLabel=="第二波","Current-wave retry restores all five tickets and announces second wave before pop");
        Check(player.GetComponent<PlayerHealth>().DamageTaken==0&&player.CurrentState==PlayerController.State.Armed&&!GameContext.IsSwordPlanted,"Current-wave retry starts with fresh actual player and sword state");
        Check(view.Canvas.transform.Find("TitleAndResult")==null&&view.Canvas.GetComponentsInChildren<Button>(true).All(b=>b.name!="SwitchModeButton"),"No title screen or title start controls are created in game canvas");
        label="PASS: gameplay UI and three-enemy review";yield return Wait(.2f);
    }
    static void Update()
    {
        if(!EditorApplication.isPlaying)return;
        try
        {
            if(EditorApplication.timeSinceStartup-realStart>240)throw new Exception("Overall UI test timeout");
            if(firstSpawnCount==0&&session!=null&&session.Flow.Spawner.TotalSpawned>0)firstSpawnCount=session.Flow.Spawner.TotalSpawned;
            if(Time.unscaledTimeAsDouble>=nextCapture&&frame<700){CaptureNamed(null);nextCapture=Time.unscaledTimeAsDouble+.18;}
            if(Time.unscaledTimeAsDouble<waitUntil)return;
            if(condition!=null){if(!condition.Predicate()){if(EditorApplication.timeSinceStartup>condition.Timeout)throw new Exception("Timeout: "+label+" | "+view.BannerLabel+" | "+player.CurrentState+" | "+session.Flow.State);return;}condition=null;}
            while(stack.Count>0)
            {
                var iterator=stack.Peek();if(!iterator.MoveNext()){stack.Pop();continue;}
                var current=iterator.Current;if(current is IEnumerator nested){stack.Push(nested);continue;}
                waitUntil=current is double time?time:0;condition=current as Until;return;
            }
            Finish(null);
        }
        catch(Exception error){Finish(error);}
    }
    static void CaptureNamed(string name)
    {
        if(Camera.main==null||view==null||view.Canvas==null)return;
        var rt=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;var camera=Camera.main;var oldTarget=camera.targetTexture;
        float oldScale=view.Canvas.scaleFactor;var scaler=view.Canvas.GetComponent<CanvasScaler>();bool scalerEnabled=scaler.enabled;
        try
        {
            camera.targetTexture=rt;scaler.enabled=false;view.Canvas.scaleFactor=1;
            foreach(var text in view.Canvas.GetComponentsInChildren<Text>(true))text.SetAllDirty();Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(Camera.main,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
            var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();
            if(name!=null)File.WriteAllBytes(OUTPUT+name+".png",texture.EncodeToPNG());
            else{File.WriteAllBytes(OUTPUT+"frames/"+frame.ToString("D4")+".png",texture.EncodeToPNG());File.AppendAllText(OUTPUT+"frames/timeline.tsv",frame+"\t"+(Time.unscaledTimeAsDouble-startTime).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"\t"+label+"\n");frame++;}
            Object.DestroyImmediate(texture);
        }
        finally{camera.targetTexture=oldTarget;view.Canvas.scaleFactor=oldScale;scaler.enabled=scalerEnabled;Canvas.ForceUpdateCanvases();RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
    }
    static void Finish(Exception error)
    {
        EditorApplication.update-=Update;SessionState.SetBool(KEY,false);
        GameEvents.WaveStarted-=OnStarted;GameEvents.WaveCleared-=OnCleared;GameEvents.PlayerDamaged-=OnDamage;GameEvents.GameOver-=OnGameOver;
        GameEvents.SwordPlanted-=OnSword;GameEvents.EnemyHit-=OnHit;EnemyMotionEvents.AttackStarted-=OnAttack;
        GameScreenEvents.TitleRequested-=OnTitleRequested;
        if(pad!=null)InputSystem.RemoveDevice(pad);if(mouse!=null)InputSystem.RemoveDevice(mouse);
        InputSystem.settings.backgroundBehavior=previousBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorInput;
        File.WriteAllText(OUTPUT+"playmode-result.txt",(error==null?"PASS":"FAIL: "+error)+"\n"+string.Join("\n",checks));File.WriteAllText(OUTPUT+"shared-events.tsv",string.Join("\n",events));
        if(error==null)Debug.Log("INK_REVIEW_PLAYMODE_PASS "+checks.Count+" checks");else Debug.LogException(error);EditorApplication.Exit(error==null?0:1);
    }
}
#endif
