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
public static class PlayerIntegrationPlayModeChecks
{
    static bool running;
    const string KEY="Sanjo.PlayerIntegrationChecks";
    static readonly string OUTPUT=OutputDirectory();
    static readonly List<string> results=new List<string>(),events=new List<string>();
    static readonly List<int> damage=new List<int>(),starts=new List<int>(),clears=new List<int>();
    static IEnumerator scenario;static Until until;static double deadline,realStart,nextCapture,startTime;
    static int frame,hits,falls,attacks,slashId,fallSlashId,gameovers,glows;static string label;
    static PlayerController player;static PlayerHealth health;static WaveController flow;static MeshCollider ground;
    static Gamepad pad;static Vector2 stick;static bool plant;static bool sawFlying,sawReaction,sawFalling,sawAttackAnimation;
    static Enemy routeEnemy;static Vector3 lastHitVelocity;
    sealed class Until
    {
        public readonly Func<bool> Condition;public readonly double Timeout;
        public Until(Func<bool> condition,float seconds){Condition=condition;Timeout=Time.timeAsDouble+seconds;}
    }
    static PlayerIntegrationPlayModeChecks(){EditorApplication.playModeStateChanged+=OnState;}
    static string OutputDirectory()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-sanjoIntegrationOutput");
        return Path.GetFullPath(i>=0&&i+1<args.Length?args[i+1]:Path.Combine(Path.GetTempPath(),"HitofuriRealPlayerChecks"))+Path.DirectorySeparatorChar;
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene(PlayerIntegrationAssetSetup.ROUTE_SCENE);
        var actor=Object.FindFirstObjectByType<PlayerController>();
        if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(actor.gameObject)!=PlayerIntegrationAssetSetup.PLAYER)throw new Exception("Scene does not reference the actual Player prefab");
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
    static void OnSlash(SlashStartedInfo info){slashId=info.SlashId;Log("SlashStarted id="+info.SlashId+" from="+info.Start+" to="+info.End);}
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
        Check(SessionState.GetBool(KEY+".source",false),"Edit-mode scene references actual PlayerIntegrationActor prefab extracted from Kapi PlayerTest");
        Check(player.GetComponent<SlashLineView>()!=null&&health!=null,"Real PlayerController, PlayerHealth and SlashLineView are active");
        Check(GameContext.PlayerTransform==player.transform,"Real player registers its own transform in shared GameContext");
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
        yield return Wait(.6f);
        // 通し確認が完了した後の独立した検証区間。柵の実験中にHPを消費させない。
        health.enabled=false;
        var overviewCamera=Camera.main;var follow=overviewCamera.GetComponent<IntegrationCameraFollow>();follow.enabled=false;
        Vector3 cameraPosition=overviewCamera.transform.position;Quaternion cameraRotation=overviewCamera.transform.rotation;float cameraSize=overviewCamera.orthographicSize;
        overviewCamera.transform.position=new Vector3(30,44,36);overviewCamera.transform.LookAt(ground.bounds.center);overviewCamera.orthographicSize=34;
        SaveCameraImage(OUTPUT+"island-overview.png");overviewCamera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);overviewCamera.orthographicSize=cameraSize;follow.enabled=true;

        label="Three roles: actual player and real planted sword";Log("PHASE three-role target fixtures (player health disabled after completed route)");
        var controllerCollider=player.GetComponent<CharacterController>();controllerCollider.enabled=false;
        PlayerIntegrationAssetSetup.Ground(ground,Vector3.zero,out var centreFloor);player.transform.position=centreFloor+Vector3.up*1.08f;controllerCollider.enabled=true;
        string[] names={"Shadow","Shield","Werewolf"};Vector3[] offsets={new Vector3(-4,0,-4),new Vector3(4,0,-4),new Vector3(0,0,6)};
        var roster=new Enemy[3];
        for(int i=0;i<3;i++){PlayerIntegrationAssetSetup.Ground(ground,offsets[i],out var location);roster[i]=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Floating"+names[i]+"Enemy.prefab"),location,Quaternion.identity).GetComponent<Enemy>();}
        Physics.SyncTransforms();var initial=roster.Select(e=>e.Position).ToArray();yield return Wait(1.5f);
        Check(roster.All(e=>e.GetComponent<EnemyPursuit>().Target==EnemyPursuit.PursuitTarget.Player),"All three enemy prefabs select the actual player's shared transform");
        Check(roster.Select((e,i)=>Flat(e.Position,player.transform.position)<Flat(initial[i],player.transform.position)-.1f).All(v=>v),"All three physically approach actual player after spawn hold");
        Check(roster[1].GetComponent<EnemyActionAnimation>().IsDefending,"Shield uses its existing guard motion while approaching actual player");
        Send(Vector2.zero,true);yield return new Until(()=>GameContext.IsSwordPlanted,2);yield return Wait(.1f);
        Check(roster[2].GetComponent<EnemyPursuit>().Target==EnemyPursuit.PursuitTarget.Sword&&roster.Take(2).All(e=>e.GetComponent<EnemyPursuit>().Target==EnemyPursuit.PursuitTarget.Player),"Actual SwordPlanted switches only LampEater to sword");
        Send(Vector2.zero,false);yield return new Until(()=>!GameContext.IsSwordPlanted,2);yield return Wait(.1f);
        Check(roster.All(e=>e.GetComponent<EnemyPursuit>().Target==EnemyPursuit.PursuitTarget.Player),"Actual SlashStarted restores all three player targets despite event/context ordering");
        foreach(var actor in roster){actor.GetComponent<EnemyPursuit>().enabled=false;actor.GetComponent<EnemyAutoAttack>().enabled=false;}
        var shield=roster[1];var shadow=roster[0];
        PlayerIntegrationAssetSetup.Ground(ground,Vector3.right,out var shieldFloor);shield.transform.SetPositionAndRotation(shieldFloor,Quaternion.LookRotation(Vector3.left));
        PlayerIntegrationAssetSetup.Ground(ground,Vector3.right*3,out var shadowFloor);shadow.transform.position=shadowFloor;Physics.SyncTransforms();
        var targets=new List<SlashTarget>();var playerData=new SerializedObject(player);var playerTuning=(PlayerTuning)playerData.FindProperty("tuning").objectReferenceValue;
        SlashResolver.Resolve(centreFloor+Vector3.left*5+Vector3.up,centreFloor+Vector3.right*5+Vector3.up,Vector3.right,playerTuning,GameContext.MeiStats,256,targets,out int blocked);
        Check(blocked==1&&targets.Any(t=>ReferenceEquals(t.Enemy,shadow))&&!targets.Any(t=>ReferenceEquals(t.Enemy,shield)),"Actual SlashResolver fully blocks front shield while continuing to shadow behind it");
        shield.transform.rotation=Quaternion.LookRotation(Vector3.right);Physics.SyncTransforms();
        SlashResolver.Resolve(centreFloor+Vector3.left*5+Vector3.up,centreFloor+Vector3.right*5+Vector3.up,Vector3.right,playerTuning,GameContext.MeiStats,256,targets,out blocked);
        Check(blocked==0&&targets.Any(t=>ReferenceEquals(t.Enemy,shield)),"Rear shield is launchable through unchanged ILaunchable interface");
        foreach(var actor in roster)Object.Destroy(actor.gameObject);yield return Wait(.1f);

        label="Fence checks: real CharacterController and custom enemy movement";
        var fence=Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).First(c=>c.name.StartsWith("FencePanel"));
        var normal=Vector3.ProjectOnPlane(fence.transform.up,Vector3.up).normalized;
        if(normal.sqrMagnitude<.5f)normal=Vector3.ProjectOnPlane(fence.transform.forward,Vector3.up).normalized;
        Vector3 centre=fence.bounds.center;centre.y=0;Vector3 fixture=centre-normal*2;
        if(!PlayerIntegrationAssetSetup.Ground(ground,fixture,out var foot)){normal=-normal;fixture=centre-normal*2;PlayerIntegrationAssetSetup.Ground(ground,fixture,out foot);}
        var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=foot+Vector3.up*1.08f;cc.enabled=true;Physics.SyncTransforms();
        Send(InputFor(normal),false);yield return Wait(.8f);Send(Vector2.zero,false);
        Check(Vector3.Dot(player.transform.position-centre,normal)<0,"Real CharacterController is stopped before crossing Blender fence");
        var blocker=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/FloatingShadowEnemy.prefab"),foot,Quaternion.LookRotation(normal)).GetComponent<Enemy>();
        blocker.GetComponent<EnemyPursuit>().enabled=false;blocker.GetComponent<EnemyAutoAttack>().enabled=false;Physics.SyncTransforms();
        Check(!blocker.TryMoveOnGround(normal*4),"Root-driven pursuit cannot cross solid fence");
        blocker.Launch(normal*6,7001);yield return Wait(1.7f);
        Check(blocker.State!=Enemy.MotionState.Falling&&Vector3.Dot(blocker.Position-centre,normal)<0,"Custom launch also stops at fence without Rigidbody force");
        Object.Destroy(blocker.gameObject);yield return Wait(.1f);
        cc.enabled=false;player.transform.position=centreFloor+Vector3.up*1.08f;cc.enabled=true;Physics.SyncTransforms();
        var testPoint=flow.Spawner.Points[0];Vector3 originalPoint=testPoint.transform.position;
        if(!PlayerIntegrationAssetSetup.Ground(ground,centre,out var fenceFloor))throw new Exception("Fence fixture has no ground");
        testPoint.transform.position=fenceFloor;Physics.SyncTransforms();
        Check(Flat(fenceFloor,player.transform.position)>flow.Plan.MinimumPlayerDistance,"Fence spawn fixture is clear of player distance constraint");
        Check(!flow.Spawner.TrySpawn(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/FloatingShadowEnemy.prefab"),EnemySpawnArea.East,out _),"Spawner rejects a supported floor position overlapping a Blender fence");
        testPoint.transform.position=originalPoint;

        label="Real player health: contact damage leads to one GameOver";Log("PHASE fresh real player health and real contact");
        player.gameObject.SetActive(false);
        var freshActor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerIntegrationAssetSetup.PLAYER));player=freshActor.GetComponent<PlayerController>();health=freshActor.GetComponent<PlayerHealth>();
        cc=freshActor.GetComponent<CharacterController>();cc.enabled=false;PlayerIntegrationAssetSetup.Ground(ground,Vector3.zero,out foot);freshActor.transform.position=foot+Vector3.up*1.08f;cc.enabled=true;
        var enemy=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/FloatingShadowEnemy.prefab"),foot+Vector3.right*3.2f,Quaternion.identity).GetComponent<Enemy>();Physics.SyncTransforms();
        int damageBefore=damage.Count;yield return new Until(()=>gameovers==1,12);
        Check(damage.Skip(damageBefore).SequenceEqual(new[]{2,1,0}),"Actual PlayerHealth emits remaining counts 2, 1, 0 from contact");
        Check(player.CurrentState==PlayerController.State.Disabled&&enemy.IsStoppedByGameOver,"Shared GameOver disables actual player and stops enemy root");
        Vector3 stopped=enemy.Position;int attackBefore=attacks;yield return Wait(1.2f);
        Check(gameovers==1&&damage.Count==damageBefore+3&&attacks==attackBefore&&Flat(stopped,enemy.Position)<.001f,"GameOver is not repeated and automatic attacks stay stopped");
        Check(Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None).All(e=>e.GetComponent<Rigidbody>()==null),"All tested enemy movement uses custom motion without Rigidbody");
        label="PASS: real player event integration";yield return Wait(.5f);
    }
    static void Update()
    {
        // 同じEditor更新で停止が通知された場合も、破棄されたシーンへ触れない。
        if (!running || !EditorApplication.isPlaying) return;
        if(!EditorApplication.isPlaying)return;
        try
        {
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
        var rt=RenderTexture.GetTemporary(1200,700,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(Camera.main,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
            var texture=new Texture2D(1200,700,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1200,700),0,0);texture.Apply();
            File.WriteAllBytes(OUTPUT+"frames/"+frame.ToString("D4")+".png",texture.EncodeToPNG());
            File.AppendAllText(OUTPUT+"frames/timeline.tsv",frame+"\t"+(Time.timeAsDouble-startTime).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"\t"+label+"\t"+flow.State+"\t"+hits+"\t"+falls+"\t"+health.Remaining+"\n");frame++;Object.DestroyImmediate(texture);
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
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
        EditorApplication.update-=Update;SessionState.EraseBool(KEY);
        GameEvents.WaveStarted-=OnStarted;GameEvents.WaveCleared-=OnCleared;GameEvents.PlayerDamaged-=OnDamage;
        GameEvents.SwordPlanted-=OnSword;GameEvents.SlashStarted-=OnSlash;GameEvents.EnemyHit-=OnHit;
        GameEvents.GlowChanged-=OnGlow;GameEvents.EnemyFell-=OnFell;GameEvents.GameOver-=OnGameOver;EnemyMotionEvents.AttackStarted-=OnAttack;
        if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        pad = null; stick = Vector2.zero; plant = false;
        scenario = null; until = null; player = null; health = null; flow = null; ground = null; routeEnemy = null;
        SessionState.EraseBool(KEY + ".source");
        running = false;
    }

    static void Finish(Exception error)
    {
        if (!running) return;
        Cleanup();
        File.WriteAllText(OUTPUT+"playmode-result.txt",(error==null?"PASS":"FAIL: "+error)+"\n"+string.Join("\n",results));
        File.WriteAllText(OUTPUT+"shared-events.tsv",string.Join("\n",events));
        if(error==null)Debug.Log("PLAYER_INTEGRATION_PLAYMODE_PASS "+results.Count+" checks");else Debug.LogException(error);
        EditorApplication.Exit(error==null?0:1);
    }
}
#endif

