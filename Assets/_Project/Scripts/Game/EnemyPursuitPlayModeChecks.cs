#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>共有GameEventsの実引数とライフサイクル、追跡、群がり、5ウェーブをPlayモードで検証する。</summary>
[InitializeOnLoad]
public static class EnemyPursuitPlayModeChecks
{
    const string KEY = "Sanjo.EnemyPursuitChecks";
    static readonly string OUTPUT = OutputDirectory();
    static readonly List<string> results = new List<string>();
    static readonly List<int> swarm = new List<int>(), starts = new List<int>(), clears = new List<int>();
    static EnemyPursuitDemo demo;
    static IEnumerator scenario;
    static Until until;
    static double deadline, realDeadline, realStart, gameStart, nextCapture;
    static int frame, fallCount;
    static bool sawSlashWithOldContext;
    static IMeiStats previousMei;
    static object pauseOwner;
    static string label;
    sealed class Until
    {
        public readonly Func<bool> Condition;public readonly double Timeout;
        public Until(Func<bool> condition, float seconds) { Condition = condition;Timeout = Time.timeAsDouble + seconds; }
    }
    static EnemyPursuitPlayModeChecks() { EditorApplication.playModeStateChanged += OnState; }
    static string OutputDirectory()
    {
        var args = Environment.GetCommandLineArgs();int i = Array.IndexOf(args, "-sanjoPursuitOutput");
        return Path.GetFullPath(i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(Path.GetTempPath(), "HitofuriEnemyPursuitChecks")) + Path.DirectorySeparatorChar;
    }
    public static void Run() { SessionState.SetBool(KEY, true);EditorSceneManager.OpenScene(EnemyPursuitAssetSetup.TEST_SCENE);EditorApplication.EnterPlaymode(); }
    static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(KEY, false)) return;
        demo = Object.FindFirstObjectByType<EnemyPursuitDemo>();previousMei = GameContext.MeiStats;
        GameEvents.SwarmCountChanged += OnSwarm;GameEvents.SlashStarted += OnSlash;
        GameEvents.WaveStarted += OnStarted;GameEvents.WaveCleared += OnCleared;GameEvents.EnemyFell += OnFell;
        results.Clear();swarm.Clear();starts.Clear();clears.Clear();frame = fallCount = 0;
        gameStart = Time.timeAsDouble;realStart = EditorApplication.timeSinceStartup;nextCapture = gameStart;deadline = realDeadline = 0;until = null;
        label = "Three roles: follow shared player";Directory.CreateDirectory(OUTPUT + "frames");
        scenario = Scenario();EditorApplication.update += Update;
    }
    static void OnSwarm(int count) => swarm.Add(count);
    static void OnSlash(SlashStartedInfo info) { if (GameContext.IsSwordPlanted) sawSlashWithOldContext = true; }
    static void OnStarted(int wave) => starts.Add(wave);
    static void OnCleared(int wave) => clears.Add(wave);
    static void OnFell(EnemyFellInfo info) => fallCount++;
    static EnemyPursuit Brain(Enemy e) => e.GetComponent<EnemyPursuit>();
    static EnemyActionAnimation Action(Enemy e) => e.GetComponent<EnemyActionAnimation>();
    static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0;return Vector3.Distance(a,b); }
    static double Wait(float seconds) => Time.timeAsDouble + seconds;
    static void Check(bool value, string name) { if (!value) throw new Exception(name);results.Add(name); }

    static IEnumerator Scenario()
    {
        yield return new Until(() => demo.Roster != null, 2);
        yield return new Until(() => Action(demo.Roster[0]).IsDefending, 4);
        yield return Wait(.1f);
        var shield = demo.Roster[0];var shadow = demo.Roster[1];var wolf = demo.Roster[2];
        Check(Brain(shield).Role == EnemyBehaviourRole.ShieldBearer && Brain(shadow).Role == EnemyBehaviourRole.Shadow && Brain(wolf).Role == EnemyBehaviourRole.LampEater, "Three prefabs have explicit roles: Shield / Shadow / LampEater visual");
        Check(demo.Roster.All(e => Brain(e).Target == EnemyPursuit.PursuitTarget.Player), "With sword in hand all three select shared player");
        Vector3[] p = demo.Roster.Select(e => e.Position).ToArray();
        yield return Wait(.6f);
        float[] travelled = demo.Roster.Select((e,i) => Flat(e.Position,p[i])).ToArray();
        Check(travelled[0] > .1f && travelled[0] < travelled[1] && travelled[1] < travelled[2], "All move; shield is slow and werewolf is moderately faster; distances="+string.Join(",",travelled.Select(x=>x.ToString("F3"))));
        Check(Action(shield).IsDefending, "Shield requests its guard through an event after pop finishes");
        var dir = demo.TestPlayer.position - shield.Position;dir.y = 0;
        Check(shield.IsShieldFacing(-dir) && !shield.IsShieldFacing(dir), "Shield faces the player and preserves front/rear ILaunchable contract");

        label = "SwordPlanted(Vector3): only LampEater switches";
        Vector3 sword = new Vector3(-6,0,0);demo.PlantSword(sword);
        Check(Brain(wolf).Target == EnemyPursuit.PursuitTarget.Sword && Brain(shadow).Target == EnemyPursuit.PursuitTarget.Player && Brain(shield).Target == EnemyPursuit.PursuitTarget.Player, "Shared SwordPlanted switches only the LampEater");
        float before = Flat(wolf.Position,sword);
        yield return Wait(.7f);
        Check(Flat(wolf.Position,sword) < before, "LampEater physically approaches planted sword");
        yield return new Until(() => Brain(wolf).IsSwarming, 4);
        Check(demo.SwarmCount == 1 && swarm.Last() == 1, "Arrival emits shared SwarmCountChanged(1)");
        int messages = swarm.Count;yield return Wait(.3f);
        Check(swarm.Count == messages, "Unchanged swarm count is not broadcast every frame");
        label = "Player moves: Shadow and Shield follow, LampEater stays with sword";
        demo.MovePlayer(new Vector3(6,0,4));float shadowDistance = Flat(shadow.Position,demo.TestPlayer.position);
        yield return Wait(.7f);
        Check(Flat(shadow.Position,demo.TestPlayer.position) < shadowDistance && Brain(wolf).Target == EnemyPursuit.PursuitTarget.Sword && Flat(wolf.Position,sword) <= wolf.Tuning.SwarmCountRadius, "Player relocation changes player followers without stealing sword target");

        wolf.transform.position = new Vector3(15,0,0);Physics.SyncTransforms();yield return Wait(.08f);
        Check(Brain(wolf).Target == EnemyPursuit.PursuitTarget.Player, "Outside 10m lure range LampEater falls back to player");
        GameContext.MeiStats = new LureMei(previousMei);yield return Wait(.08f);
        Check(Brain(wolf).Target == EnemyPursuit.PursuitTarget.Sword, "Current MeiStats LureRadiusMultiplier immediately expands target range");
        GameContext.MeiStats = previousMei;yield return Wait(.08f);
        Check(Brain(wolf).Target == EnemyPursuit.PursuitTarget.Player, "Lure multiplier is read on use, not cached");
        wolf.transform.position = new Vector3(-4,0,-2);Physics.SyncTransforms();yield return Wait(.08f);
        Check(Brain(wolf).Target == EnemyPursuit.PursuitTarget.Sword, "Entering lure range selects sword again");
        sawSlashWithOldContext = false;demo.PullSword();
        Check(sawSlashWithOldContext && Brain(wolf).Target == EnemyPursuit.PursuitTarget.Player && demo.SwarmCount == 0, "SlashStarted overrides old true context immediately and clears swarm");

        label = "Late spawn reads shared sword context and joins swarm";
        demo.ResetRoster();wolf = demo.Roster[2];shield = demo.Roster[0];
        sword = new Vector3(-4,0,-1.5f);demo.PlantSword(sword);yield return new Until(() => demo.SwarmCount == 1, 4);
        Check(demo.SwarmCount == 1, "Fresh roster reaches sword after pop hold");
        var late = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/FloatingWerewolfEnemy.prefab"), sword + Vector3.right * 1.3f, Quaternion.identity).GetComponent<Enemy>();
        Check(Brain(late).Target == EnemyPursuit.PursuitTarget.Sword, "Spawn after SwordPlanted missed event still reads current context");
        yield return new Until(() => demo.SwarmCount == 2, 4);Check(demo.SwarmCount == 2, "Two arriving LampEaters aggregate into shared count 2");
        Brain(late).enabled = false;Check(demo.SwarmCount == 1, "OnDisable removes member and unsubscribes pursuit");
        Brain(late).enabled = true;yield return new Until(() => demo.SwarmCount == 2, 4);Check(demo.SwarmCount == 2, "OnEnable rejoins once without duplicate subscriptions");
        Object.Destroy(late.gameObject);yield return Wait(.08f);Check(demo.SwarmCount == 1, "Destroy removes the exact swarm member");

        label = "Launch: pursuit yields to custom knockback, then resumes";
        Vector3 launchPosition = wolf.Position;wolf.Launch(Vector3.right * 4f, 9001);
        Check(demo.SwarmCount == 0, "Launch removes swarm member synchronously even before next Update");
        yield return Wait(.3f);
        Check(wolf.State == Enemy.MotionState.Flying && Mathf.Abs(wolf.Position.x-launchPosition.x-1.02f) < .12f && wolf.LastSlashId == 9001, "Pursuit cannot add displacement during ILaunchable flight");
        yield return Wait(1.3f);Check(wolf.State == Enemy.MotionState.Idle && Brain(wolf).Target == EnemyPursuit.PursuitTarget.Sword, "Ground stop restores pursuit target");
        shield.Launch(Vector3.back * 2f, 9002);Check(!Action(shield).IsDefending, "Launch clears guard reaction immediately");
        yield return new Until(() => Action(shield).IsDefending, 4);Check(Action(shield).IsDefending, "Guard request survives visual recovery and resumes");

        label = "Shared TimeController pause and GameOver";
        p = demo.Roster.Select(e => e.Position).ToArray();pauseOwner = new object();TimeController.RequestPause(pauseOwner);
        realDeadline = EditorApplication.timeSinceStartup + .25;yield return null;
        Check(demo.Roster.Select((e,i) => Flat(e.Position,p[i]) < .001f).All(v => v), "Shared pause stops all pursuit without changing timeScale directly");
        TimeController.ReleasePause(pauseOwner);pauseOwner = null;
        demo.EndGame();p = demo.Roster.Select(e => e.Position).ToArray();yield return Wait(.3f);
        Check(demo.Roster.Select((e,i) => Flat(e.Position,p[i]) < .001f && Brain(e).Target == EnemyPursuit.PursuitTarget.None).All(v => v) && demo.SwarmCount == 0, "GameOver clears swarm and halts all three target behaviours");
        demo.ResetRoster();GameContext.PlayerTransform = null;yield return Wait(1.3f);
        Check(demo.Roster.All(e => Brain(e).Target == EnemyPursuit.PursuitTarget.None), "No shared target means no invented target or movement");
        GameContext.PlayerTransform = demo.TestPlayer;yield return Wait(.08f);Check(demo.Roster.All(e => Brain(e).Target == EnemyPursuit.PursuitTarget.Player), "Later player registration is picked up without Find in Update");

        label = "Edge safety while chasing a player beyond the platform";
        var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);platform.transform.position = new Vector3(100,-.2f,0);platform.transform.localScale = new Vector3(6,.4f,6);
        var probe = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/FloatingShadowEnemy.prefab"),new Vector3(102,0,0),Quaternion.identity).GetComponent<Enemy>();
        demo.MovePlayer(new Vector3(110,0,0));Physics.SyncTransforms();yield return Wait(2.2f);
        Check(probe.State == Enemy.MotionState.Idle && Physics.Raycast(probe.Position+Vector3.up,Vector3.down,2,1,QueryTriggerInteraction.Ignore) && probe.Position.x < 103, "Pursuit samples ground and does not walk itself off an edge");
        Object.Destroy(probe.gameObject);Object.Destroy(platform);demo.MovePlayer(new Vector3(0,0,5));

        label = "Five waves with active target behaviours";
        starts.Clear();clears.Clear();fallCount = 0;demo.StartWaves();demo.Flow.AutoAdvance = false;
        demo.PlantSword(demo.Flow.Spawner.Points[1].transform.position);
        for (int wave = 1; wave <= 5; wave++)
        {
            yield return new Until(() => demo.Flow.State == WaveController.WaveState.WaitingForEnemies, 8);
            label = "Wave " + wave + ": spawned enemies select player or sword";
            Check(demo.Flow.Spawner.OwnedEnemies.All(e => Brain(e) != null), "Wave " + wave + " spawns live pursuit components");
            Check(demo.Flow.Spawner.OwnedEnemies.Where(e => Brain(e).Role != EnemyBehaviourRole.LampEater).All(e => Brain(e).Target == EnemyPursuit.PursuitTarget.Player), "Wave " + wave + " Shadow and Shield select shared player");
            if (wave == 3) Check(demo.Flow.Spawner.OwnedEnemies.All(e => Brain(e).Role != EnemyBehaviourRole.LampEater), "Wave three preserves its Shadow and Shield composition");
            else if (wave >= 2) Check(demo.Flow.Spawner.OwnedEnemies.Where(e => Brain(e).Role == EnemyBehaviourRole.LampEater).All(e => Brain(e).Target != EnemyPursuit.PursuitTarget.None), "Wave " + wave + " LampEaters have a valid shared target");
            yield return Wait(.35f);demo.ThrowWaveEnemies();
            yield return new Until(() => demo.Flow.State == WaveController.WaveState.Intermission || demo.Flow.State == WaveController.WaveState.Completed, 3);
            Check(clears.Count == wave && clears.Last() == wave, "Wave " + wave + " clears once through real EnemyFell");
            if (wave < 5) Check(demo.Flow.AdvanceNow(), "Advance after wave " + wave);
        }
        Check(starts.SequenceEqual(new[]{1,2,3,4,5}) && clears.SequenceEqual(new[]{1,2,3,4,5}) && fallCount == 30 && demo.Flow.State == WaveController.WaveState.Completed, "Active-AI five-wave integration preserves all 30 falls and shared event ordering");
        demo.ResetRoster();demo.PlantSword(new Vector3(-6,0,0));label = "Ready: move player, plant sword, or pull sword";yield return Wait(1.5f);
    }

    static void Update()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup-realStart>210) throw new Exception("Pursuit tests timed out");
            if (Time.timeAsDouble>=nextCapture && frame<1200) { Capture();nextCapture=Time.timeAsDouble+.08; }
            if (Time.timeAsDouble<deadline || EditorApplication.timeSinceStartup<realDeadline) return;realDeadline=0;
            if (until!=null) { if (!until.Condition()) { if (Time.timeAsDouble>until.Timeout) throw new Exception("Timed out: "+label);return; }until=null; }
            if (!scenario.MoveNext()) { Finish(null);return; }deadline=scenario.Current is double time?time:0;until=scenario.Current as Until;
        }
        catch(Exception error) { Finish(error); }
    }
    static void Capture()
    {
        var rt=RenderTexture.GetTemporary(1200,700,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(Camera.main,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
            var image=new Texture2D(1200,700,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,700),0,0);image.Apply();
            File.WriteAllBytes(OUTPUT+"frames/"+frame.ToString("D4")+".png",image.EncodeToPNG());
            File.AppendAllText(OUTPUT+"frames/timeline.tsv",frame+"\t"+(Time.timeAsDouble-gameStart).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"\t"+label+"\t"+demo.SwarmCount+"\n");
            frame++;Object.DestroyImmediate(image);
        }
        finally { RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt); }
    }
    static void Finish(Exception error)
    {
        EditorApplication.update-=Update;SessionState.SetBool(KEY,false);GameEvents.SwarmCountChanged-=OnSwarm;GameEvents.SlashStarted-=OnSlash;
        GameEvents.WaveStarted-=OnStarted;GameEvents.WaveCleared-=OnCleared;GameEvents.EnemyFell-=OnFell;
        if(pauseOwner!=null) TimeController.ReleasePause(pauseOwner);GameContext.MeiStats=previousMei;
        File.WriteAllText(OUTPUT+"playmode-result.txt",(error==null?"PASS":"FAIL: "+error)+"\n"+string.Join("\n",results));
        File.WriteAllText(OUTPUT+"shared-events.txt","SwarmCountChanged: "+string.Join(",",swarm)+"\nWaveStarted: "+string.Join(",",starts)+"\nWaveCleared: "+string.Join(",",clears)+"\nEnemyFell: "+fallCount);
        if(error==null) Debug.Log("ENEMY_PURSUIT_PLAYMODE_PASS "+results.Count+" checks");else Debug.LogException(error);EditorApplication.Exit(error==null?0:1);
    }
    sealed class LureMei : IMeiStats
    {
        readonly IMeiStats original;public LureMei(IMeiStats value){original=value;}public float LureRadiusMultiplier=>3;
        public float LineWidthMultiplier=>original.LineWidthMultiplier;public float LaunchPowerMultiplier=>original.LaunchPowerMultiplier;
        public float FreeHandSpeedMultiplier=>original.FreeHandSpeedMultiplier;public float AutoReturnTimeMultiplier=>original.AutoReturnTimeMultiplier;
        public float SlashSpeedMultiplier=>original.SlashSpeedMultiplier;public float GlowGainMultiplier=>original.GlowGainMultiplier;
        public int MaxDamageOffset=>original.MaxDamageOffset;public bool HasReturnSlash=>original.HasReturnSlash;
    }
}
#endif
