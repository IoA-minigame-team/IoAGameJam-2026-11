#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>レビューの2条件を実Prefabで再現する。設定変更はPlay内の複製だけに行い、アセットを保存しない。</summary>
[InitializeOnLoad]
public static class EnemyReviewRegressionChecks
{
    static bool running;
    const string KEY = "Sanjo.EnemyReviewRegression";
    const string PREFAB = "Assets/_Project/Prefabs/FloatingShadowEnemy.prefab";
    static readonly List<string> checks = new List<string>();
    static readonly List<string> failures = new List<string>();
    static IEnumerator scenario;
    static Until pending;
    static double waitUntil, realStart;
    static Enemy observed;
    static EnemyActionAnimation presentation;
    static bool prematureAttack, recoveryWindow, driftDuringRecovery;
    static int attacks;
    static Vector3 recoveryPosition;
    static bool haveRecoveryPosition;
    static string output;
    static Transform previousPlayer;
    static GameObject player;

    sealed class Until
    {
        public readonly Func<bool> Condition;
        public readonly double Deadline;
        public Until(Func<bool> condition, float timeout) { Condition = condition; Deadline = Time.timeAsDouble + timeout; }
    }

    static EnemyReviewRegressionChecks() => EditorApplication.playModeStateChanged += OnState;

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(KEY, true);
        EditorApplication.EnterPlaymode();
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
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-sanjoReviewOutput");
        output = index >= 0 ? args[index + 1] : Path.Combine(Path.GetTempPath(), "HitofuriEnemyReviewChecks");
        Directory.CreateDirectory(output);
        checks.Clear(); failures.Clear(); attacks = 0;
        prematureAttack = recoveryWindow = driftDuringRecovery = haveRecoveryPosition = false;
        previousPlayer = GameContext.PlayerTransform;
        player = new GameObject("ReviewPlayerTarget");
        GameContext.PlayerTransform = player.transform;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "ReviewFloor"; floor.transform.position = Vector3.down * .1f; floor.transform.localScale = new Vector3(60, .2f, 60);
        EnemyMotionEvents.AttackStarted += OnAttack;
        realStart = EditorApplication.timeSinceStartup; waitUntil = 0; pending = null;
        scenario = Scenario(); EditorApplication.update += Update;
    }

    static Enemy CreateEnemy(Vector3 position, bool autoAttack)
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB), position, Quaternion.identity);
        var enemy = root.GetComponent<Enemy>();
        var tuning = Object.Instantiate(enemy.Tuning);
        var values = new SerializedObject(tuning);
        values.FindProperty("moveSpeed").floatValue = 0;
        values.FindProperty("spawnHoldDuration").floatValue = 0;
        values.FindProperty("attackRange").floatValue = 0;
        values.ApplyModifiedPropertiesWithoutUndo();
        var data = new SerializedObject(enemy); data.FindProperty("tuning").objectReferenceValue = tuning; data.ApplyModifiedPropertiesWithoutUndo();
        var visual = new SerializedObject(root.GetComponent<EnemyActionAnimation>());
        visual.FindProperty("reactionTuning").objectReferenceValue = tuning; visual.ApplyModifiedPropertiesWithoutUndo();
        root.GetComponent<EnemyPursuit>().enabled = autoAttack;
        root.GetComponent<EnemyAutoAttack>().enabled = autoAttack;
        Physics.SyncTransforms(); return enemy;
    }

    static void SetRange(Enemy enemy, float range)
    {
        var data = new SerializedObject(enemy.Tuning); data.FindProperty("attackRange").floatValue = range; data.ApplyModifiedPropertiesWithoutUndo();
    }

    static void OnAttack(ILaunchable source)
    {
        if (!ReferenceEquals(source, observed)) return;
        attacks++; prematureAttack |= presentation.IsReacting;
    }

    static void Check(bool pass, string message)
    {
        checks.Add((pass ? "PASS " : "FAIL ") + message);
        if (!pass) failures.Add(message);
    }

    static double Wait(float seconds) => Time.timeAsDouble + seconds;

    static IEnumerator Scenario()
    {
        observed = CreateEnemy(Vector3.zero, true);
        presentation = observed.GetComponent<EnemyActionAnimation>();
        player.transform.position = Vector3.left * .5f;
        yield return Wait(1.2f);
        observed.Launch(Vector3.left * 4, 501); SetRange(observed, 2);
        yield return new Until(() => observed.State == Enemy.MotionState.Idle, 3);
        yield return new Until(() => !presentation.IsReacting, 3);
        Check(recoveryWindow, "An actual landed-but-visually-recovering interval was exercised");
        Check(!prematureAttack, "No AttackStarted is emitted before visual recovery finishes");
        Check(!driftDuringRecovery, "No pursuit or attack lunge moves the root during visual recovery");
        yield return new Until(() => attacks > 0, 3);
        yield return Wait(.12f);
        Check(observed.GetComponent<EnemyAutoAttack>().IsAttacking, "Automatic attack resumes after recovery");
        Check(presentation.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Attack"), "Resumed attack displays the actual Attack animator state");
        SetRange(observed, 0); observed.GetComponent<EnemyAutoAttack>().enabled = false;
        player.transform.position = new Vector3(20, 0, 20);

        // 表示を無効にしても、待機だけが残って敵が永久停止しないことも確認する。
        observed.Launch(Vector3.right * 3, 502);
        yield return new Until(() => observed.State == Enemy.MotionState.Idle, 3);
        presentation.enabled = false;
        Check(observed.CanPursue, "Disabling presentation releases its recovery wait");
        presentation.enabled = true; observed.ResetState();
        Check(observed.CanPursue, "Reset releases recovery state for a reused enemy");
        Object.Destroy(observed.gameObject); observed = null; presentation = null;
        yield return Wait(.1f);

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "ReviewFence"; wall.layer = 2;
        wall.transform.position = new Vector3(0, 1.5f, 0); wall.transform.localScale = new Vector3(.2f, 4, 20);
        float[] radii = { .48f, 1.2f, .2f, .5f, .4f };
        Vector3[] scales = { Vector3.one, Vector3.one, Vector3.one, new Vector3(2, 1, 1.5f), Vector3.one };
        for (int i = 0; i < radii.Length; i++)
        {
            var enemy = CreateEnemy(new Vector3(-4, 0, 0), false);
            var capsule = enemy.GetComponent<CapsuleCollider>(); capsule.radius = radii[i];
            if (i == 4) capsule.center += Vector3.right * .3f;
            enemy.transform.localScale = scales[i]; Physics.SyncTransforms();
            for (int n = 0; n < 100; n++) enemy.TryMoveOnGround(Vector3.right * .05f);
            Physics.SyncTransforms(); float gap = -.1f - capsule.bounds.max.x;
            Check(gap >= -.015f && gap < .08f, "Pursuit uses actual capsule radius/scale/center, case " + i + ", gap=" + gap.ToString("F3"));
            enemy.transform.position = new Vector3(-4, 0, 0); Physics.SyncTransforms();
            enemy.Launch(Vector3.right * 8, 600 + i);
            Check(!capsule.enabled, "Flight temporarily disables body collider, case " + i);
            yield return new Until(() => enemy.State == Enemy.MotionState.Idle, 4);
            Physics.SyncTransforms(); gap = -.1f - capsule.bounds.max.x;
            Check(gap >= -.015f && gap < .025f, "Flight stops at actual capsule surface even while collider is disabled, case " + i + ", gap=" + gap.ToString("F3"));
            Check(!Physics.ComputePenetration(capsule, capsule.transform.position, capsule.transform.rotation, wall.GetComponent<Collider>(), wall.transform.position, wall.transform.rotation, out _, out float depth) || depth < .015f, "Stopped collider does not penetrate fence, case " + i);
            Object.Destroy(enemy.gameObject); yield return Wait(.03f);
        }
    }

    static void Update()
    {
        // 同じEditor更新で停止が通知された場合も、破棄されたシーンへ触れない。
        if (!running || !EditorApplication.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup - realStart > 120) throw new Exception("Overall timeout");
            if (observed != null && presentation != null && presentation.IsReacting && observed.State == Enemy.MotionState.Idle)
            {
                recoveryWindow = true;
                if (!haveRecoveryPosition) { recoveryPosition = observed.Position; haveRecoveryPosition = true; }
                else if (Vector3.Distance(recoveryPosition, observed.Position) > .001f) driftDuringRecovery = true;
            }
            if (Time.timeAsDouble < waitUntil) return;
            if (pending != null)
            {
                if (!pending.Condition()) { if (Time.timeAsDouble > pending.Deadline) throw new Exception("Condition timed out"); return; }
                pending = null;
            }
            if (!scenario.MoveNext()) { Finish(null); return; }
            waitUntil = scenario.Current is double value ? value : 0;
            pending = scenario.Current as Until;
        }
        catch (Exception error) { Finish(error); }
    }

    /// <summary>通常完了と手動停止で共有する後始末。ここでは結果の保存やEditor終了を行わない。</summary>
    static void Cleanup()
    {
        EditorApplication.update -= Update; EnemyMotionEvents.AttackStarted -= OnAttack; GameContext.PlayerTransform = previousPlayer;
        SessionState.EraseBool(KEY);
        scenario = null; pending = null; observed = null; presentation = null; player = null; previousPlayer = null;
        running = false;
    }

    static void Finish(Exception error)
    {
        if (!running) return;
        Cleanup();
        string result = error == null && failures.Count == 0 ? "PASS" : "FAIL";
        File.WriteAllText(Path.Combine(output, "result.txt"), result + "\n" + string.Join("\n", checks) + (error != null ? "\n" + error : ""));
        Debug.Log("ENEMY_REVIEW_REGRESSION_" + result + " " + checks.Count + " checks, " + failures.Count + " failures");
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(result == "PASS" ? 0 : 1);
    }
}
#endif
