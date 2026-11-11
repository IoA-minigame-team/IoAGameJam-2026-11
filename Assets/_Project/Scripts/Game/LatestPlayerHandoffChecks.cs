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
using Object=UnityEngine.Object;

/// <summary>実入力から実主人公を通す通し確認。主要経路ではLaunchや結果イベントを代わりに呼ばない。</summary>
[InitializeOnLoad]
public static class LatestPlayerHandoffChecks
{
    static bool running;
    const string KEY="Sanjo.LatestPlayerHandoffChecks";
    static readonly string OUTPUT=OutputDirectory();
    static readonly List<string> results=new List<string>(),events=new List<string>();
    static readonly List<int> damage=new List<int>(),starts=new List<int>(),clears=new List<int>();
    static IEnumerator scenario;static Until until;static double deadline,realStart,nextCapture,startTime;
    static int frame,hits,falls,attacks,slashId,fallSlashId,gameovers,glows;static string label;
    static PlayerController player;static PlayerHealth health;static WaveController flow;static MeshCollider ground;
    static Gamepad pad;static Vector2 stick;static bool plant;static bool sawFlying,sawReaction,sawFalling,sawAttackAnimation;
    static Enemy routeEnemy;static Vector3 lastHitVelocity;
    static Animator heroAnimator;static TrailRenderer[] trails;static IMeiStats originalStats;
    static bool sawPlant,sawFree,sawSlash,sawTrail;static Vector3 baselineVelocity;
    static float lastSlashLength,baselineLength;static PlayerTuning playerTuning;static bool editorConnectionValid;

    sealed class Until
    {
        public readonly Func<bool> Condition;public readonly double Timeout;
        public Until(Func<bool> condition,float seconds){Condition=condition;Timeout=Time.timeAsDouble+seconds;}
    }
    static LatestPlayerHandoffChecks(){EditorApplication.playModeStateChanged+=OnState;}
    static string OutputDirectory()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-sanjoHandoffOutput");
        return Path.GetFullPath(i>=0&&i+1<args.Length?args[i+1]:Path.Combine(Path.GetTempPath(),"HitofuriRealPlayerChecks"))+Path.DirectorySeparatorChar;
    }
    public static void Run()
    {
        // Playへ切り替える前にも点検結果を書くため、出力先は入口で作る。
        Directory.CreateDirectory(OUTPUT);
        EditorSceneManager.OpenScene(LatestPlayerHandoffSetup.ROUTE_SCENE);
        var actor=Object.FindFirstObjectByType<PlayerController>();
        if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(actor.gameObject)!=PlayerIntegrationAssetSetup.PLAYER)throw new Exception("Scene does not reference the actual Player prefab");
        var findings=EnemyStageTools.Validate(Object.FindFirstObjectByType<WaveController>());editorConnectionValid=!findings.Any(x=>x.StartsWith("NG"));
        File.WriteAllText(OUTPUT+"scene-validation.txt",string.Join("\n",findings));
        if(!editorConnectionValid)throw new Exception("Spawn tool reported invalid scene connections");
        SessionState.SetBool(KEY+".spawnValid",editorConnectionValid);
        SessionState.SetBool(KEY+".source",true);SessionState.SetBool(KEY,true);EditorApplication.EnterPlaymode();
    }
    static void OnState(PlayModeStateChange change)
    {
        // 手動停止は失敗終了と分ける。Editorを閉じず、次の通常Playへ検証を持ち越さない。
        if (change == PlayModeStateChange.ExitingPlayMode)
        {
            if (running || SessionState.GetBool(KEY, false)) Cleanup();
            return;
        }
        if(change!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(KEY,false))return;
        running = true;
        player=Object.FindFirstObjectByType<PlayerController>();health=player.GetComponent<PlayerHealth>();flow=Object.FindFirstObjectByType<WaveController>();ground=Object.FindFirstObjectByType<MeshCollider>();
        heroAnimator=player.GetComponentInChildren<Animator>();trails=player.GetComponentsInChildren<TrailRenderer>(true);originalStats=GameContext.MeiStats;
        playerTuning=(PlayerTuning)new SerializedObject(player).FindProperty("tuning").objectReferenceValue;
        sawPlant=sawFree=sawSlash=sawTrail=false;
        results.Clear();events.Clear();damage.Clear();starts.Clear();clears.Clear();frame=hits=falls=attacks=slashId=fallSlashId=gameovers=glows=0;
        sawFlying=sawReaction=sawFalling=sawAttackAnimation=false;stick=Vector2.zero;plant=false;
        GameEvents.WaveStarted+=OnStarted;GameEvents.WaveCleared+=OnCleared;GameEvents.PlayerDamaged+=OnDamage;
        GameEvents.SwordPlanted+=OnSword;GameEvents.SlashStarted+=OnSlash;GameEvents.EnemyHit+=OnHit;
        GameEvents.GlowChanged+=OnGlow;GameEvents.EnemyFell+=OnFell;GameEvents.GameOver+=OnGameOver;
        EnemyMotionEvents.AttackStarted+=OnAttack;
        pad=InputSystem.AddDevice<Gamepad>("IntegrationTestGamepad");Send(Vector2.zero,false);
        realStart=EditorApplication.timeSinceStartup;startTime=Time.timeAsDouble;nextCapture=startTime;deadline=0;until=null;
        Directory.CreateDirectory(OUTPUT+"frames");label="Real player: wait for pursuit and automatic attack";scenario=Scenario();EditorApplication.update+=Update;
    }
    static void Log(string value)=>events.Add((Time.timeAsDouble-startTime).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"\t"+value);
    static void OnStarted(int wave){starts.Add(wave);Log("WaveStarted("+wave+")");}
    static void OnCleared(int wave){clears.Add(wave);Log("WaveCleared("+wave+")");}
    static void OnDamage(int remaining){damage.Add(remaining);Log("PlayerDamaged("+remaining+")");}
    static void OnSword(Vector3 position)=>Log("SwordPlanted "+position.ToString("F2"));
    static void OnSlash(SlashStartedInfo info){slashId=info.SlashId;lastSlashLength=Flat(info.Start,info.End);Log("SlashStarted id="+info.SlashId+" from="+info.Start+" to="+info.End);}
    static void OnHit(EnemyHitInfo info){hits++;lastHitVelocity=info.Velocity;Log("EnemyHit id="+info.SlashId+" v="+info.Velocity.ToString("F2"));}
    static void OnGlow(ILaunchable enemy,float ratio){glows++;Log("GlowChanged "+ratio.ToString("F2"));}
    static void OnFell(EnemyFellInfo info){falls++;fallSlashId=info.SlashId;Log("EnemyFell id="+info.SlashId+" at="+info.Position.ToString("F2"));}
    static void OnGameOver(){gameovers++;Log("GameOver");}
    static void OnAttack(ILaunchable enemy){attacks++;Log("EnemyMotionEvents.AttackStarted "+enemy.Transform.name);}
    static double Wait(float seconds)=>Time.timeAsDouble+seconds;
    static void Check(bool pass,string description){if(!pass)throw new Exception(description);results.Add(description);}
    static float Flat(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    static void Send(Vector2 input,bool held)
    {
        stick=input;plant=held;if(pad!=null)InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=stick}.WithButton(GamepadButton.South,plant));
    }
    static Vector2 InputFor(Vector3 direction)
    {
        var cam=Camera.main.transform;return new Vector2(Vector3.Dot(direction,Vector3.ProjectOnPlane(cam.right,Vector3.up).normalized),Vector3.Dot(direction,Vector3.ProjectOnPlane(cam.forward,Vector3.up).normalized));
    }
    static IEnumerator Scenario()
    {
        yield return new Until(()=>flow.Spawner.LastSpawned!=null,4);routeEnemy=flow.Spawner.LastSpawned;
        Check(SessionState.GetBool(KEY+".source",false),"Edit-mode scene references actual Player.prefab from merged GitHub player visual change");
        Check(SessionState.GetBool(KEY+".spawnValid",false),"Spawn tool validates real prefab, matching wave plans, area assignments and floor under spawn points");
        Check(player.GetComponent<SlashLineView>()!=null&&health!=null,"Real PlayerController, PlayerHealth and SlashLineView are active");
        Check(GameContext.PlayerTransform==player.transform,"Real player registers its own transform in shared GameContext");
        Check(player.GetComponent<PlayerVisual>()!=null&&player.GetComponent<SwordTrailView>()!=null,"New GitHub player visual and sword trail components are present");
        Check(heroAnimator!=null&&heroAnimator.runtimeAnimatorController!=null&&player.GetComponentsInChildren<SkinnedMeshRenderer>().Length>0,"Actual rigged player model and animation controller are connected");
        Check(GameContext.MeiStats is DefaultMeiStats,"Production scene currently uses DefaultMeiStats, not an implemented upgrade provider");
        Vector3 start=routeEnemy.Position;float originalDistance=Flat(start,player.transform.position);
        yield return new Until(()=>attacks>0,7);
        Check(Flat(routeEnemy.Position,player.transform.position)<originalDistance-.5f,"Enemy pursues actual player before automatic attack");
        yield return new Until(()=>damage.Count>0,4);
        Check(damage.SequenceEqual(new[]{2})&&health.DamageTaken==1,"Actual overlap contact causes exactly one PlayerDamaged(2)");
        Check(sawAttackAnimation,"Automatic attack enters the existing Attack animator state");
        Check(routeEnemy.GetComponent<EnemyAutoAttack>().IsAttacking,"Contact occurs during automatic attack lunge");
        label="Real input: plant sword, move along the cape, release";Vector3 planted=player.transform.position;
        Send(Vector2.zero,true);yield return new Until(()=>GameContext.IsSwordPlanted,2);
        Check(player.IsLineVisible&&Flat(GameContext.SwordPosition,planted)<.2f,"Gamepad South input plants real sword and enables line");
        float target=PlayerIntegrationAssetSetup.Edge(ground,Vector3.right)-2.5f;
        Send(InputFor(Vector3.right),true);yield return new Until(()=>player.transform.position.x>=target,2.8f);Send(Vector2.zero,true);
        Check(GameContext.IsSwordPlanted&&player.CurrentState==PlayerController.State.FreeHand,"Actual free-hand movement preserves planted sword before automatic return timeout");
        Check(player.transform.position.x-planted.x>7,"Real CharacterController traverses more than 7m of enlarged terrain");
        Send(Vector2.zero,false);yield return new Until(()=>hits>0,2);
        Check(slashId==1&&hits==1&&routeEnemy.LastSlashId==1,"Release input resolves real slash and calls ILaunchable once with slashId 1");
        Check(glows==1&&routeEnemy.GlowRatio>0,"Launch emits one shared GlowChanged result");
        Check(lastHitVelocity.x>8&&Mathf.Abs(lastHitVelocity.z)<.1f,"Actual SlashResolver supplies forward initial velocity "+lastHitVelocity.ToString("F2"));
        label="Custom knockback: open edge, falling, shared WaveCleared";
        yield return new Until(()=>clears.Count==1,7);
        Check(sawFlying&&sawReaction&&sawFalling,"Flight, launch reaction and falling occur without direct test Launch or root teleport");
        Check(falls==1&&fallSlashId==slashId,"EnemyFell preserves real slashId until it leaves the island");
        Check(starts.SequenceEqual(new[]{1})&&clears.SequenceEqual(new[]{1})&&flow.State==WaveController.WaveState.Completed,"One-wave route completes once through shared WaveStarted and WaveCleared");
        Check(flow.Spawner.AliveCount==0&&flow.PendingCount==0,"Wave clears after all scheduled spawns and real falls finish");
        int hitIndex=events.FindIndex(e=>e.Contains("EnemyHit")),fallIndex=events.FindIndex(e=>e.Contains("EnemyFell")),clearIndex=events.FindIndex(e=>e.Contains("WaveCleared"));
        Check(hitIndex>=0&&fallIndex>hitIndex&&clearIndex>fallIndex,"Recorded shared event ordering is hit, fall, wave clear");
        Check(damage.Count==1&&gameovers==0,"Successful slash path does not fabricate damage or game over");
        baselineVelocity=lastHitVelocity;baselineLength=lastSlashLength;
        Check(sawPlant&&sawFree&&sawSlash&&sawTrail,"Real Plant, FreeLocomotion, SlashDash and sword-trail emission were observed");
        File.WriteAllText(OUTPUT+"baseline-power.txt","Provider="+GameContext.MeiStats.GetType().Name+"\nLaunchVelocity="+baselineVelocity+"\nFeedbackProvider="+(Feedback.Service!=null?Feedback.Service.GetType().Name:"none"));
        yield return Wait(.5f);

        label="Contract test only: inject two-times stats after planting";Log("PHASE editor-only IMeiStats probe. Not production upgrade implementation.");
        EditorSceneManager.LoadSceneInPlayMode(LatestPlayerHandoffSetup.ROUTE_SCENE,new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return Wait(.2f);BindNewScene();
        yield return new Until(()=>flow.Spawner.LastSpawned!=null,4);routeEnemy=flow.Spawner.LastSpawned;
        int damageBefore=damage.Count;yield return new Until(()=>damage.Count>damageBefore,7);
        Vector3 probePlanted=player.transform.position;Send(Vector2.zero,true);yield return new Until(()=>GameContext.IsSwordPlanted,2);
        float endX=PlayerIntegrationAssetSetup.Edge(ground,Vector3.right)-2.5f;
        Send(InputFor(Vector3.right),true);yield return Wait(.4f);
        GameContext.MeiStats=new ProbeStats(originalStats);Check(GameContext.MeiStats.LaunchPowerMultiplier==2,"Probe changes only the shared stats input during live free-hand movement");
        yield return new Until(()=>player.transform.position.x>=endX,2);Send(Vector2.zero,true);
        int hitsBefore=hits;Send(Vector2.zero,false);yield return new Until(()=>hits>hitsBefore,2);
        // 入力をサンプルしたフレーム差で線の長さが変わるため、実イベントの長さ補正を除いて倍率を比較する。
        float baselinePower=baselineVelocity.magnitude/(1+playerTuning.LengthBonusPerMeter*baselineLength);
        float probePower=lastHitVelocity.magnitude/(1+playerTuning.LengthBonusPerMeter*lastSlashLength);
        Check(Mathf.Abs(probePower/baselinePower-2)<.002f,"Actual player consumes current LaunchPowerMultiplier exactly once: length-normalized velocity ratio "+(probePower/baselinePower).ToString("F3"));
        Check(Mathf.Abs(routeEnemy.GlowRatio-.4f)<.01f,"Enemy consumes current GlowGainMultiplier exactly once: glow ratio 0.4");
        File.WriteAllText(OUTPUT+"power-contract.txt","TEST ONLY. Production provider absent.\nBaseline velocity="+baselineVelocity+"\nProbe velocity="+lastHitVelocity+"\nProbe glow ratio="+routeEnemy.GlowRatio);
        yield return new Until(()=>flow.State==WaveController.WaveState.Completed,7);
        Check(fallSlashId==1&&falls==2,"Second real slash preserves new player's slashId through actual fall");
        GameContext.MeiStats=originalStats;
        yield return new Until(()=>player.CurrentState==PlayerController.State.Armed,3);
        var lampPosition=player.transform.position+Vector3.left*12;PlayerIntegrationAssetSetup.Ground(ground,lampPosition,out lampPosition);
        var lamp=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/FloatingWerewolfEnemy.prefab"),lampPosition,Quaternion.identity).GetComponent<Enemy>();
        Send(Vector2.zero,true);yield return new Until(()=>GameContext.IsSwordPlanted,2);yield return Wait(.1f);
        Check(lamp.GetComponent<EnemyPursuit>().Target==EnemyPursuit.PursuitTarget.Player,"Lamp at 12m stays with player under default 10m lure range");
        GameContext.MeiStats=new ProbeStats(originalStats);yield return Wait(.1f);
        Check(lamp.GetComponent<EnemyPursuit>().Target==EnemyPursuit.PursuitTarget.Sword,"Live LureRadiusMultiplier switches same lamp to sword without new event");
        GameContext.MeiStats=originalStats;Send(Vector2.zero,false);label="PASS: latest player connected; upgrade provider still pending";yield return Wait(.4f);
    }
    static void BindNewScene()
    {
        player=Object.FindFirstObjectByType<PlayerController>();health=player.GetComponent<PlayerHealth>();flow=Object.FindFirstObjectByType<WaveController>();ground=Object.FindFirstObjectByType<MeshCollider>();
        heroAnimator=player.GetComponentInChildren<Animator>();trails=player.GetComponentsInChildren<TrailRenderer>(true);routeEnemy=null;
    }
    sealed class ProbeStats : IMeiStats
    {
        readonly IMeiStats original;public ProbeStats(IMeiStats source){original=source;}
        public float LaunchPowerMultiplier=>2;public float GlowGainMultiplier=>2;public float LureRadiusMultiplier=>2;
        public float LineWidthMultiplier=>original.LineWidthMultiplier;public float FreeHandSpeedMultiplier=>original.FreeHandSpeedMultiplier;
        public float AutoReturnTimeMultiplier=>original.AutoReturnTimeMultiplier;public float SlashSpeedMultiplier=>original.SlashSpeedMultiplier;
        public int MaxDamageOffset=>original.MaxDamageOffset;public bool HasReturnSlash=>original.HasReturnSlash;
    }
    static void Update()
    {
        // 同じEditor更新で停止が通知された場合も、破棄されたシーンへ触れない。
        if (!running || !EditorApplication.isPlaying) return;
        if(!EditorApplication.isPlaying)return;
        try
        {
            if(heroAnimator!=null)
            {
                var state=heroAnimator.GetCurrentAnimatorStateInfo(0);sawPlant|=state.IsName("Plant");sawFree|=state.IsName("FreeLocomotion");sawSlash|=state.IsName("SlashDash");
                sawTrail|=trails.Any(t=>t!=null&&t.emitting);
            }
            if(EditorApplication.timeSinceStartup-realStart>180)throw new Exception("Overall timeout");
            if(routeEnemy!=null)
            {
                sawFlying|=routeEnemy.State==Enemy.MotionState.Flying;sawFalling|=routeEnemy.State==Enemy.MotionState.Falling;
                sawReaction|=routeEnemy.GetComponent<EnemyActionAnimation>().IsReacting;
                sawAttackAnimation|=routeEnemy.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Attack");
            }
            if(Time.timeAsDouble>=nextCapture&&frame<600){Capture();nextCapture=Time.timeAsDouble+.10;}
            if(Time.timeAsDouble<deadline)return;
            if(until!=null){if(!until.Condition()){if(Time.timeAsDouble>until.Timeout)throw new Exception("Timed out: "+label+" Player="+player.transform.position+" State="+player.CurrentState+" Flow="+flow.State+" Damage="+string.Join(",",damage));return;}until=null;}
            if(!scenario.MoveNext()){Finish(null);return;}deadline=scenario.Current is double time?time:0;until=scenario.Current as Until;
        }
        catch(Exception error){Finish(error);}
    }
    static void Capture()
    {
        if(player==null||Camera.main==null)return;
        var rt=RenderTexture.GetTemporary(1200,700,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;var captureCamera=Camera.main;var oldTarget=captureCamera.targetTexture;
        var view=Object.FindFirstObjectByType<InkReviewInterface>();var scaler=view!=null?view.Canvas.GetComponent<UnityEngine.UI.CanvasScaler>():null;
        bool scalerEnabled=scaler!=null&&scaler.enabled;float scale=view!=null?view.Canvas.scaleFactor:1;
        try
        {
            captureCamera.targetTexture=rt;
            if(scaler!=null){scaler.enabled=false;view.Canvas.scaleFactor=.75f;foreach(var text in view.Canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true))text.SetAllDirty();}
            Canvas.ForceUpdateCanvases();RenderPipeline.SubmitRenderRequest(captureCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
            var texture=new Texture2D(1200,700,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1200,700),0,0);texture.Apply();
            File.WriteAllBytes(OUTPUT+"frames/"+frame.ToString("D4")+".png",texture.EncodeToPNG());
            File.AppendAllText(OUTPUT+"frames/timeline.tsv",frame+"\t"+(Time.timeAsDouble-startTime).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"\t"+label+"\t"+flow.State+"\t"+hits+"\t"+falls+"\t"+health.Remaining+"\n");frame++;Object.DestroyImmediate(texture);
        }
        finally{captureCamera.targetTexture=oldTarget;if(scaler!=null){view.Canvas.scaleFactor=scale;scaler.enabled=scalerEnabled;}Canvas.ForceUpdateCanvases();RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
    }
    static void SaveCameraImage(string path)
    {
        var rt=RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(Camera.main,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
            var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
    }
    /// <summary>通常完了と手動停止で共有する後始末。ここでは結果の保存やEditor終了を行わない。</summary>
    static void Cleanup()
    {
        EditorApplication.update-=Update;SessionState.EraseBool(KEY);GameContext.MeiStats=originalStats;
        GameEvents.WaveStarted-=OnStarted;GameEvents.WaveCleared-=OnCleared;GameEvents.PlayerDamaged-=OnDamage;
        GameEvents.SwordPlanted-=OnSword;GameEvents.SlashStarted-=OnSlash;GameEvents.EnemyHit-=OnHit;
        GameEvents.GlowChanged-=OnGlow;GameEvents.EnemyFell-=OnFell;GameEvents.GameOver-=OnGameOver;EnemyMotionEvents.AttackStarted-=OnAttack;
        if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        pad = null; stick = Vector2.zero; plant = false;
        scenario = null; until = null; player = null; health = null; flow = null; ground = null; routeEnemy = null; heroAnimator = null; trails = null; originalStats = null;
        SessionState.EraseBool(KEY + ".source"); SessionState.EraseBool(KEY + ".spawnValid");
        running = false;
    }

    static void Finish(Exception error)
    {
        if (!running) return;
        Cleanup();
        File.WriteAllText(OUTPUT+"playmode-result.txt",(error==null?"PASS":"FAIL: "+error)+"\n"+string.Join("\n",results));
        File.WriteAllText(OUTPUT+"shared-events.tsv",string.Join("\n",events));
        if(error==null)Debug.Log("LATEST_PLAYER_HANDOFF_PASS "+results.Count+" checks");else Debug.LogException(error);
        EditorApplication.Exit(error==null?0:1);
    }
}
#endif

