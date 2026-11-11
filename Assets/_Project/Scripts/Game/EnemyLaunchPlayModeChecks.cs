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

/// <summary>ILaunchableとイベントを実際のPlayモードで検証する。Editor専用。</summary>
[InitializeOnLoad]
public static class EnemyLaunchPlayModeChecks
{
    static bool running;
    const string KEY = "Sanjo.EnemyLaunchChecks";
    static readonly string OUTPUT = OutputDirectory();
    static EnemyLaunchDemo demo;
    static IEnumerator scenario;
    static readonly List<string> results = new List<string>();
    static readonly List<EnemyFellInfo> falls = new List<EnemyFellInfo>();
    static int teeters, glows, launches, gameOvers, frame;
    static double deadline, realDeadline, realStart, nextCapture, gameStart;
    static string label;
    static object pauseOwner;
    static IMeiStats previousMei;

    static EnemyLaunchPlayModeChecks() { EditorApplication.playModeStateChanged += OnState; }
    static string OutputDirectory()
    {
        var args = Environment.GetCommandLineArgs();int index = Array.IndexOf(args, "-sanjoLaunchOutput");
        return Path.GetFullPath(index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Path.GetTempPath(), "HitofuriEnemyLaunchChecks")) + Path.DirectorySeparatorChar;
    }
    public static void Run()
    {
        SessionState.SetBool(KEY, true);EditorSceneManager.OpenScene(EnemyLaunchAssetSetup.TEST_SCENE);EditorApplication.EnterPlaymode();
    }
    static void OnState(PlayModeStateChange state)
    {
        // 手動停止は失敗終了と分ける。Editorを閉じず、次の通常Playへ検証を持ち越さない。
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (running || SessionState.GetBool(KEY, false)) Cleanup();
            return;
        }
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(KEY, false)) return;
        running = true;
        demo = Object.FindFirstObjectByType<EnemyLaunchDemo>();
        results.Clear();falls.Clear();teeters = glows = launches = gameOvers = frame = 0;
        previousMei = GameContext.MeiStats;
        GameEvents.EnemyFell += OnFell;GameEvents.EnemyTeetered += OnTeeter;
        GameEvents.GlowChanged += OnGlow;GameEvents.GameOver += OnGameOver;EnemyMotionEvents.Launched += OnLaunch;
        gameStart = Time.timeAsDouble;nextCapture = gameStart;realStart = EditorApplication.timeSinceStartup;
        deadline = realDeadline = 0;label = "Spawn -> Idle";
        Directory.CreateDirectory(OUTPUT + "frames");
        scenario = Scenario();EditorApplication.update += Update;
    }
    static void OnFell(EnemyFellInfo info) => falls.Add(info);
    static void OnTeeter(ILaunchable enemy) => teeters++;
    static void OnGlow(ILaunchable enemy, float value) => glows++;
    static void OnLaunch(ILaunchable enemy, Vector3 velocity) => launches++;
    static void OnGameOver() => gameOvers++;
    static Animator Animator(Enemy enemy) => enemy.GetComponentInChildren<Animator>();
    static EnemyActionAnimation Actions(Enemy enemy) => enemy.GetComponent<EnemyActionAnimation>();
    static Transform Pivot(Enemy enemy) => enemy.transform.Find("ReactionPivot");
    static bool State(Enemy enemy, string state) => Animator(enemy).GetCurrentAnimatorStateInfo(0).IsName(state);
    static double Wait(float seconds) => Time.timeAsDouble + seconds;
    static void Check(bool passed, string name)
    {
        if (!passed) throw new Exception(name);
        results.Add(name);
    }

    static IEnumerator Scenario()
    {
        yield return Wait(1.4f);
        Check(demo.Actors.Length == 3 && demo.Actors.All(e => e is ILaunchable && State(e, "Idle")), "Three prefabs implement ILaunchable and finish Spawn");
        Check(demo.Actors.All(e => e.GetComponent<Rigidbody>() == null && !Animator(e).applyRootMotion), "No Rigidbody or Animator root motion");
        foreach (var e in demo.Actors) Actions(e).SetDefending(true);
        yield return Wait(.55f);
        Check(demo.Actors.All(e => State(e, "Defense")), "Defense still works on all three");
        foreach (var e in demo.Actors) Actions(e).PlayAttack();
        yield return Wait(.25f);
        Check(demo.Actors.All(e => State(e, "Attack")), "Attack still works on all three");
        yield return Wait(1.1f);
        Check(demo.Actors.All(e => State(e, "Defense")), "Attack returns to guarding");
        Check(demo.Actors[0].IsShieldFacing(-Vector3.forward) && !demo.Actors[0].IsShieldFacing(Vector3.forward), "Shield front and rear follow slash direction contract");
        Check(!demo.Actors[1].IsShieldFacing(-Vector3.forward) && !demo.Actors[2].IsShieldFacing(-Vector3.forward), "Non-shield enemies never block");
        Check(Mathf.Abs(demo.Actors[0].Weight - .6f) < .001f, "Shield publishes 0.6 weight multiplier");

        demo.ResetTest();
        var one = demo.Actors[1];Vector3 home = one.Position;int glowBefore = glows, launchBefore = launches;
        ILaunchable target = one;
        target.Launch(Vector3.forward * 4f, 501);
        label = "One enemy hit: event filters by target";
        Check(one.Velocity == Vector3.forward * 4f && one.LastSlashId == 501, "Launch preserves supplied initial velocity and slashId");
        Check(glows == glowBefore + 1 && launches == launchBefore + 1, "One Launch emits one glow and one launch event");
        Check(Mathf.Abs(one.GlowMultiplier - 1.3f) < .001f && Mathf.Abs(one.GlowRatio - .2f) < .001f, "Glow gain follows 30 percent and 150 percent cap");
        Check(!one.GetComponent<Collider>().enabled, "Flying collider cannot damage player");
        Check(Actions(one).IsReacting && !Actions(demo.Actors[0]).IsReacting && !Actions(demo.Actors[2]).IsReacting, "Event receiver only reacts to its own ILaunchable");
        yield return Wait(.18f);
        Check(State(one, "Idle") && Actions(one).IsReacting, "Immediate hit overrides pending Spawn");
        Check(Quaternion.Angle(Pivot(one).localRotation, Quaternion.identity) > 5 && Pivot(one).localPosition.y > .1f, "Launch event produces actual lean and visual hop");
        Check(one.Position.y == home.y && one.Position.z > home.z, "Visual hop does not move gameplay root vertically");
        var block = new MaterialPropertyBlock();one.GetComponentInChildren<Renderer>().GetPropertyBlock(block);
        Check(Mathf.Abs(block.GetFloat("_GlowRatio") - .2f) < .001f, "Shared GlowChanged updates only instance material properties");
        Actions(one).PlayAttack();
        yield return Wait(.15f);
        Check(State(one, "Idle"), "Attack cannot overwrite flying reaction");
        yield return Wait(1.2f);
        Check(one.State == Enemy.MotionState.Idle && Mathf.Abs(one.Position.z - home.z - 2f) < .025f, "Friction gives analytic stopping distance v squared over 2a");
        Check(one.LastSlashId == 501 && one.GetComponent<Collider>().enabled && falls.Count == 0, "Stopping retains slashId and restores collider without death");
        yield return Wait(.5f);
        Check(!Actions(one).IsReacting && Quaternion.Angle(Pivot(one).localRotation, Quaternion.identity) < .15f, "Recovery returns visual pivot to rest");

        label = "All three: shield is heavier, supplied velocity is not multiplied twice";
        demo.LaunchSoft();
        Check(Mathf.Abs(demo.Actors[0].Velocity.z - 2.4f) < .001f && !demo.Actors[0].IsShieldFacing(-Vector3.forward), "Caller applies shield weight once; flying shield cannot block");
        yield return Wait(1.6f);
        Check(Mathf.Abs(demo.Actors[0].Position.z + 2f - .72f) < .025f, "Shield stops after 0.72 metres at supplied 2.4m per second");
        Check(Mathf.Abs(demo.Actors[1].Position.z) < .025f && Mathf.Abs(demo.Actors[2].Position.z) < .025f, "Unweighted enemies stop after 2 metres");

        label = "Gain multiplier read on each hit, cap, and event unsubscribe";
        demo.ResetTest();one = demo.Actors[1];
        GameContext.MeiStats = new TestMei(previousMei);
        one.Launch(Vector3.zero, 601);
        Check(Mathf.Abs(one.GlowMultiplier - 1.6f) < .001f, "Mei glow gain multiplier read when Launch is called");
        GameContext.MeiStats = previousMei;
        one.Launch(Vector3.zero, 602);
        Check(Mathf.Abs(one.GlowMultiplier - 1.9f) < .001f, "Changed Mei multiplier is not cached");
        for (int i = 0; i < 10; i++) one.Launch(Vector3.zero, 603 + i);
        Check(Mathf.Abs(one.GlowMultiplier - 2.5f) < .001f && one.GlowRatio == 1f, "Glow is capped at multiplier 2.5 and ratio 1");
        one.Launch(new Vector3(float.NaN, 0, 0), 999);
        Check(one.LastSlashId == 612, "Invalid velocity cannot corrupt enemy state");
        demo.ResetTest();var disabledActions = Actions(demo.Actors[0]);disabledActions.enabled = false;
        demo.Actors[0].Launch(Vector3.forward, 700);
        Check(!disabledActions.IsReacting, "Disabled animation receiver unsubscribes from launch events");
        disabledActions.enabled = true;demo.ResetTest();

        label = "Pause via shared TimeController";
        demo.LaunchSoft();yield return Wait(.15f);
        var positions = demo.Actors.Select(e => e.Position).ToArray();
        pauseOwner = new object();TimeController.RequestPause(pauseOwner);
        realDeadline = EditorApplication.timeSinceStartup + .25;
        yield return null;
        Check(demo.Actors.Select((e, i) => Vector3.Distance(e.Position, positions[i]) < .001f).All(v => v), "Shared pause stops trajectory without writing timeScale");
        TimeController.ReleasePause(pauseOwner);pauseOwner = null;

        label = "GameOver freezes movement and reaction";
        demo.EndGameForTest();positions = demo.Actors.Select(e => e.Position).ToArray();
        var rotations = demo.Actors.Select(e => Pivot(e).localRotation).ToArray();
        var armRotations = demo.Actors.Select(e => Animator(e).GetComponentsInChildren<Transform>().Single(t => t.name == "L_Arm").localRotation).ToArray();
        int beforeGameOver = gameOvers;
        GameEvents.RaisePlayerDamaged(0);GameEvents.RaisePlayerDamaged(-1);
        yield return Wait(.2f);
        Check(gameOvers == beforeGameOver && beforeGameOver == 1, "GameOver still emits once for repeated zero damage");
        Check(demo.Actors.Select((e, i) => e.IsStoppedByGameOver && Vector3.Distance(e.Position, positions[i]) < .001f && Quaternion.Angle(Pivot(e).localRotation, rotations[i]) < .1f).All(v => v), "GameOver stops both root motion and visual reaction");
        Check(demo.Actors.Select((e, i) => Quaternion.Angle(Animator(e).GetComponentsInChildren<Transform>().Single(t => t.name == "L_Arm").localRotation, armRotations[i]) < .1f).All(v => v), "GameOver also preserves procedural arm pose after Animator evaluation");
        foreach (var e in demo.Actors) e.Launch(Vector3.forward * 20, 800);
        Check(demo.Actors.All(e => e.LastSlashId == demo.LastIssuedSlashId), "Launch ignored after GameOver");
        demo.ResetTest();
        Check(demo.Actors.All(e => !e.IsStoppedByGameOver && e.GlowMultiplier == 1 && e.LastSlashId == -1), "Reset clears only enemy-owned motion and glow state");

        label = "Edge stop -> EnemyTeetered -> balance -> recover";
        teeters = 0;demo.ShowTeeter();
        yield return Wait(.2f);
        Check(teeters == 3 && demo.Actors.All(e => e.State == Enemy.MotionState.Teetering && Actions(e).IsReacting), "Three edge stops emit one EnemyTeetered each");
        yield return Wait(1.3f);
        Check(teeters == 3 && falls.Count == 0 && demo.Actors.All(e => e.State == Enemy.MotionState.Idle), "Teeter finishes without duplicate events or death");

        label = "Out of bounds -> custom gravity -> EnemyFell with original slashId";
        falls.Clear();demo.LaunchOverEdge();int fallSlash = demo.LastIssuedSlashId;
        yield return Wait(.35f);
        Check(demo.Actors.All(e => e.State == Enemy.MotionState.Falling && e.Position.y < 0), "No ground starts falling under custom gravity");
        Check(demo.Actors.All(e => e.LastSlashId == fallSlash && !e.GetComponent<Collider>().enabled), "Falling keeps original slashId and collider disabled");
        yield return Wait(1.1f);
        Check(falls.Count == 3 && falls.All(info => info.SlashId == fallSlash && info.Position.y <= -2.9f), "EnemyFell contains correct slashId and actual fall position once per enemy");
        Check(demo.Actors.All(e => e == null), "Enemies are destroyed only after falling off stage");

        label = "Production floating island MeshCollider integration";
        var stage = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/FloatingMeadowStage.prefab"));stage.transform.position = new Vector3(100, 0, 0);
        var probe = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/FloatingShadowEnemy.prefab")).GetComponent<Enemy>();probe.transform.position = new Vector3(100, 0, 0);
        Physics.SyncTransforms();probe.Launch(Vector3.forward * .5f, 901);
        yield return Wait(.35f);
        Check(probe != null && probe.State == Enemy.MotionState.Idle, "Production island MeshCollider is recognised as ground");
        Object.Destroy(probe.gameObject);Object.Destroy(stage);
        demo.ResetTest();label = "Reset: three prefabs ready";
        yield return Wait(1.4f);
        Check(demo.Actors.All(e => e != null && State(e, "Idle") && e.State == Enemy.MotionState.Idle), "Destroyed enemies can be recreated and finish Spawn");
    }

    static void Update()
    {
        // 同じEditor更新で停止が通知された場合も、破棄されたシーンへ触れない。
        if (!running || !EditorApplication.isPlaying) return;
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup - realStart > 150) throw new Exception("Play-mode test timed out");
            if (Time.timeAsDouble >= nextCapture && frame < 600) { Capture();nextCapture = Time.timeAsDouble + .05; }
            if (Time.timeAsDouble < deadline || EditorApplication.timeSinceStartup < realDeadline) return;
            realDeadline = 0;
            if (!scenario.MoveNext()) { Finish(null);return; }
            deadline = scenario.Current is double time ? time : 0;
        }
        catch (Exception error) { Finish(error); }
    }
    static void Capture()
    {
        var rt = RenderTexture.GetTemporary(1200, 700, 24, RenderTextureFormat.ARGB32);var previous = RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(Camera.main, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt;var image = new Texture2D(1200, 700, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1200, 700), 0, 0);image.Apply();
            File.WriteAllBytes(OUTPUT + "frames/" + frame.ToString("D4") + ".png", image.EncodeToPNG());
            File.AppendAllText(OUTPUT + "frames/timeline.tsv", frame + "\t" + (Time.timeAsDouble - gameStart).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "\t" + label + "\n");
            frame++;Object.DestroyImmediate(image);
        }
        finally { RenderTexture.active = previous;RenderTexture.ReleaseTemporary(rt); }
    }
    /// <summary>通常完了と手動停止で共有する後始末。ここでは結果の保存やEditor終了を行わない。</summary>
    static void Cleanup()
    {
        EditorApplication.update -= Update;SessionState.EraseBool(KEY);
        GameEvents.EnemyFell -= OnFell;GameEvents.EnemyTeetered -= OnTeeter;GameEvents.GlowChanged -= OnGlow;
        GameEvents.GameOver -= OnGameOver;EnemyMotionEvents.Launched -= OnLaunch;
        if (pauseOwner != null) TimeController.ReleasePause(pauseOwner);
        GameContext.MeiStats = previousMei;
        scenario = null; demo = null; pauseOwner = null; previousMei = null;
        running = false;
    }

    static void Finish(Exception error)
    {
        if (!running) return;
        Cleanup();
        File.WriteAllText(OUTPUT + "playmode-result.txt", (error == null ? "PASS" : "FAIL: " + error) + "\n" + string.Join("\n", results));
        if (error == null) Debug.Log("ENEMY_LAUNCH_PLAYMODE_PASS " + results.Count + " checks");else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    sealed class TestMei : IMeiStats
    {
        readonly IMeiStats original;
        public TestMei(IMeiStats original) { this.original = original; }
        public float GlowGainMultiplier => 2f;
        public float LineWidthMultiplier => original.LineWidthMultiplier;
        public float LaunchPowerMultiplier => original.LaunchPowerMultiplier;
        public float FreeHandSpeedMultiplier => original.FreeHandSpeedMultiplier;
        public float AutoReturnTimeMultiplier => original.AutoReturnTimeMultiplier;
        public float SlashSpeedMultiplier => original.SlashSpeedMultiplier;
        public float LureRadiusMultiplier => original.LureRadiusMultiplier;
        public int MaxDamageOffset => original.MaxDamageOffset;
        public bool HasReturnSlash => original.HasReturnSlash;
    }
}
#endif
