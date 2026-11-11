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

/// <summary>本物の出現・落下・共有イベントで5ウェーブと異常系を確認する。Editor専用。</summary>
[InitializeOnLoad]
public static class EnemyWavePlayModeChecks
{
    static bool running;
    const string KEY = "Sanjo.EnemyWaveChecks";
    static readonly string OUTPUT = OutputDirectory();
    static readonly List<string> checks = new List<string>();
    static readonly List<int> starts = new List<int>(), clears = new List<int>(), fallsAtClear = new List<int>();
    static readonly List<string> spawnRecords = new List<string>();
    static EnemyWaveDemo demo;
    static WaveController flow;
    static EnemySpawner spawner;
    static EnemyWavePlan originalPlan, testPlan;
    static Transform originalPlayer;
    static GameObject testPlayer;
    static IEnumerator scenario;
    static Until until;
    static double deadline, realDeadline, realStart, gameStart, captureNext;
    static object pauseOwner;
    static int fallCount, gameOverCount, frame, previousSpawnCount;
    static bool pauseFirstClear, gameOverOnStart;
    static string label;

    sealed class Until
    {
        public readonly Func<bool> Condition;
        public readonly double Timeout;
        public readonly string Message;
        public Until(Func<bool> condition, float seconds, string message) { Condition = condition;Timeout = Time.timeAsDouble + seconds;Message = message; }
    }
    static EnemyWavePlayModeChecks() { EditorApplication.playModeStateChanged += OnState; }
    static string OutputDirectory()
    {
        var args = Environment.GetCommandLineArgs();int i = Array.IndexOf(args, "-sanjoWaveOutput");
        return Path.GetFullPath(i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(Path.GetTempPath(), "HitofuriEnemyWaveChecks")) + Path.DirectorySeparatorChar;
    }
    public static void Run()
    {
        SessionState.SetBool(KEY, true);EditorSceneManager.OpenScene(EnemyWaveAssetSetup.TEST_SCENE);EditorApplication.EnterPlaymode();
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
        demo = Object.FindFirstObjectByType<EnemyWaveDemo>();flow = demo.Flow;spawner = flow.Spawner;originalPlan = flow.Plan;
        originalPlayer = GameContext.PlayerTransform;
        GameEvents.WaveStarted += OnStarted;GameEvents.WaveCleared += OnCleared;GameEvents.EnemyFell += OnFell;GameEvents.GameOver += OnGameOver;
        starts.Clear();clears.Clear();fallsAtClear.Clear();checks.Clear();spawnRecords.Clear();
        fallCount = gameOverCount = frame = previousSpawnCount = 0;pauseFirstClear = true;gameOverOnStart = false;
        deadline = realDeadline = 0;until = null;gameStart = Time.timeAsDouble;captureNext = gameStart;realStart = EditorApplication.timeSinceStartup;
        label = "Wave 1: timed spawning";Directory.CreateDirectory(OUTPUT + "frames");
        File.WriteAllLines(OUTPUT + "spawn-points.tsv", new[]{"Name\tX\tY\tZ\tAreas\tRadius"}.Concat(spawner.Points.Select(p => p.name + "\t" + p.transform.position.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "\t" + p.transform.position.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "\t" + p.transform.position.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "\t" + p.Areas + "\t" + p.Radius)));
        scenario = Scenario();EditorApplication.update += Update;
    }
    static void OnStarted(int wave)
    {
        starts.Add(wave);
        if (gameOverOnStart) { gameOverOnStart = false;GameEvents.RaisePlayerDamaged(0); }
    }
    static void OnCleared(int wave)
    {
        clears.Add(wave);fallsAtClear.Add(fallCount);
        if (wave == 1 && pauseFirstClear)
        {
            pauseFirstClear = false;pauseOwner = new object();TimeController.RequestPause(pauseOwner);
        }
    }
    static void OnFell(EnemyFellInfo info) => fallCount++;
    static void OnGameOver() => gameOverCount++;
    static double Wait(float seconds) => Time.timeAsDouble + seconds;
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name);checks.Add(name); }
    static bool Waiting() => flow.State == WaveController.WaveState.WaitingForEnemies;
    static bool Cleared(int wave) => flow.CurrentWaveNumber == wave && (flow.State == WaveController.WaveState.Intermission || flow.State == WaveController.WaveState.Completed);

    static IEnumerator Scenario()
    {
        Check(flow.TotalWaves == 5, "Default plan contains five waves");
        Check(spawner.Points.Length == 12 && spawner.Points.Count(p => (p.Areas & EnemySpawnArea.Center) != 0) == 4, "Twelve editable spawn points: eight edges and four centre");
        Check(spawner.Points.All(p => Physics.Raycast(p.transform.position + Vector3.up * 2, Vector3.down, 5, 1, QueryTriggerInteraction.Ignore)), "All twelve authored points have real ground below");
        yield return new Until(() => flow.SpawnedThisWave == 1, 4, "First spawn");
        Check(starts.SequenceEqual(new[]{1}) && flow.PendingCount == 2, "WaveStarted fires once before staggered wave-one spawns finish");
        Check(spawner.OwnedEnemies.All(e => e.GetComponent<EnemyActionAnimation>() != null), "Spawned prefabs retain action and pop animation components");
        demo.FocusLastSpawn();yield return Wait(.25f);
        Check(spawner.LastSpawned.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Spawn"), "Spawned character actually plays pop animation");
        label = "Early fall: pending spawn tickets prevent premature clear";
        demo.ThrowSpawnedEnemies();demo.Overview();
        yield return Wait(.8f);
        Check(flow.CurrentWaveNumber == 1 && clears.Count == 0, "Falling early cannot clear a wave with pending tickets");
        yield return new Until(Waiting, 5, "Remaining first-wave spawns");
        Check(flow.SpawnedThisWave == 3 && spawner.AliveCount == 2 && fallCount == 1, "Wave one spawns exactly three; first fallen enemy is tracked");
        int alive = spawner.AliveCount;
        GameEvents.RaiseEnemyFell(new EnemyFellInfo(999999, Vector3.zero));fallCount--;
        Check(spawner.AliveCount == alive && clears.Count == 0, "Unrelated or fake EnemyFell does not clear owned enemies");
        demo.ThrowSpawnedEnemies();
        yield return new Until(() => Cleared(1), 3, "First wave clear");
        Check(clears.SequenceEqual(new[]{1}) && fallsAtClear[0] == 3, "WaveCleared occurs once after all actual EnemyFell notifications");
        label = "Intermission: shared pause holds the next wave";
        int totalBeforePause = spawner.TotalSpawned;
        realDeadline = EditorApplication.timeSinceStartup + .25;yield return null;
        Check(starts.Count == 1 && spawner.TotalSpawned == totalBeforePause && !flow.AdvanceNow(), "TimeController pause blocks auto and manual advancement");
        TimeController.ReleasePause(pauseOwner);pauseOwner = null;
        yield return new Until(() => flow.CurrentWaveNumber == 2, 4, "Automatic next wave");
        Check(starts.SequenceEqual(new[]{1,2}), "Auto advancement starts wave two after intermission");

        // 出現直後の安全間隔と追跡中の距離は別の検証。ここは共有ターゲットなしでウェーブを検証する。
        for (int wave = 2; wave <= 5; wave++)
        {
            label = "Wave " + wave + ": different spawn regions and mixed characters";
            yield return new Until(Waiting, 7, "Wave " + wave + " spawn completion");
            int[] totals = {3,5,4,7,11};
            Check(flow.SpawnedThisWave == totals[wave - 1] && spawner.AliveCount == totals[wave - 1], "Wave " + wave + " spawns configured total " + totals[wave - 1]);
            Check(spawner.OwnedEnemies.All(e => Physics.Raycast(e.Position + Vector3.up, Vector3.down, 3, 1, QueryTriggerInteraction.Ignore)), "Wave " + wave + " enemies spawn on supported ground");
            bool spaced = true;
            for (int i = 0; i < spawner.OwnedEnemies.Count; i++) for (int j = i + 1; j < spawner.OwnedEnemies.Count; j++)
                if (Vector3.Distance(spawner.OwnedEnemies[i].Position, spawner.OwnedEnemies[j].Position) < originalPlan.MinimumEnemyDistance - .01f) spaced = false;
            Check(spaced, "Wave " + wave + " enemies do not overlap spawn spacing");
            if (wave == 2)
            {
                Check(spawner.OwnedEnemies.All(e => e.GetComponent<EnemyPursuit>().Target == EnemyPursuit.PursuitTarget.None), "Wave-only scene remains still when shared targets are absent");
                Check(spawner.OwnedEnemies.Count(e => e.name.Contains("Werewolf")) == 2, "Lamp-eater visual slot uses replaceable werewolf prefab");
            }
            if (wave == 3)
                Check(spawner.OwnedEnemies.Where(e => e.Tuning.HasShield).Count() == 2 && spawner.OwnedEnemies.Where(e => e.Tuning.HasShield).All(e => spawner.Points.Where(p => (p.Areas & EnemySpawnArea.Center) != 0).Any(p => Vector3.Distance(e.Position, p.transform.position) <= p.Radius + .05f)), "Wave three shield characters spawn from configured centre points");
            flow.AutoAdvance = false;demo.ThrowSpawnedEnemies();
            yield return new Until(() => Cleared(flow.CurrentWaveNumber), 3, "Wave clear");
            Check(clears.Count == wave && clears[wave - 1] == wave, "Wave " + wave + " emits one matching completion");
            if (wave < 5) { Check(flow.AdvanceNow(), "Manual continuation works after wave " + wave); }
        }
        label = "All five waves completed; no false GameOver";
        Check(flow.State == WaveController.WaveState.Completed && starts.SequenceEqual(new[]{1,2,3,4,5}), "All five waves start once in order and finish Completed");
        Check(fallCount == 30 && spawner.ConfirmedFalls == 30 && spawner.AliveCount == 0 && flow.PendingCount == 0, "Thirty real falls account for the entire sequence");
        Check(fallsAtClear.SequenceEqual(new[]{3,8,12,19,30}) && gameOverCount == 0, "Clear event ordering preserves fall totals and victory is not GameOver");
        yield return Wait(.3f);
        Check(starts.Count == 5 && clears.Count == 5, "Completed state cannot emit a sixth wave or duplicate clear");

        label = "Blocked point: retry without consuming a spawn ticket";
        testPlan = Object.Instantiate(originalPlan);var data = new SerializedObject(testPlan);
        data.FindProperty("initialDelay").floatValue = 0;data.FindProperty("blockedTimeout").floatValue = .4f;data.FindProperty("retryInterval").floatValue = .05f;
        var waves = data.FindProperty("waves");waves.arraySize = 1;var batches = waves.GetArrayElementAtIndex(0).FindPropertyRelative("batches");batches.arraySize = 1;
        batches.GetArrayElementAtIndex(0).FindPropertyRelative("count").intValue = 1;data.ApplyModifiedPropertiesWithoutUndo();
        SetPlan(testPlan);
        for (int i = 1; i < spawner.Points.Length; i++) spawner.Points[i].enabled = false;
        var firstPoint = spawner.Points[0];var pointData = new SerializedObject(firstPoint);float oldRadius = firstPoint.Radius;
        pointData.FindProperty("radius").floatValue = 0;pointData.ApplyModifiedPropertiesWithoutUndo();
        testPlayer = new GameObject("BlockedSpawnPlayer");testPlayer.transform.position = firstPoint.transform.position;GameContext.PlayerTransform = testPlayer.transform;
        flow.RestartRun();yield return Wait(.15f);
        Check(flow.State == WaveController.WaveState.Spawning && flow.PendingCount == 1 && spawner.AliveCount == 0, "Blocked point keeps the ticket pending and never spawns on player");
        testPlayer.transform.position = Vector3.zero;
        yield return new Until(Waiting, 1, "Blocked spawn recovery");
        Check(spawner.AliveCount == 1 && flow.PendingCount == 0, "Retry spawns once the point becomes safe");
        GameContext.PlayerTransform = originalPlayer;Object.Destroy(testPlayer);testPlayer = null;

        label = "Missing ground: stop with a visible configuration error";
        Vector3 oldPosition = firstPoint.transform.position;firstPoint.transform.position = new Vector3(1000, 0, 1000);
        int clearsBefore = clears.Count;flow.RestartRun();
        yield return new Until(() => flow.State == WaveController.WaveState.Faulted, 2, "Missing ground timeout");
        Check(spawner.TotalSpawned == 0 && flow.PendingCount == 1 && clears.Count == clearsBefore && !string.IsNullOrEmpty(flow.LastError), "Unsafe ground times out without spawning or false completion");
        firstPoint.transform.position = oldPosition;pointData.FindProperty("radius").floatValue = oldRadius;pointData.ApplyModifiedPropertiesWithoutUndo();
        foreach (var point in spawner.Points) point.enabled = true;
        SetPlan(originalPlan);Object.Destroy(testPlan);testPlan = null;

        label = "Unexpected Destroy is not a wave kill";
        flow.RestartRun();yield return new Until(() => spawner.AliveCount > 0, 3, "Restart spawn");
        clearsBefore = clears.Count;int fallsBefore = fallCount;Object.Destroy(spawner.OwnedEnemies[0].gameObject);
        yield return new Until(() => flow.State == WaveController.WaveState.Faulted, 1, "Unexpected destroy detection");
        Check(clears.Count == clearsBefore && fallCount == fallsBefore && spawner.HasUnexpectedRemoval, "External Destroy faults the run rather than granting a clear");

        label = "GameOver cancels pending spawns and advancement";
        flow.RestartRun();yield return new Until(() => spawner.AliveCount > 0, 3, "GameOver setup spawn");
        int totalBefore = spawner.TotalSpawned;clearsBefore = clears.Count;
        GameEvents.RaisePlayerDamaged(0);yield return Wait(.8f);
        Check(flow.State == WaveController.WaveState.GameOver && spawner.TotalSpawned == totalBefore && flow.PendingCount > 0 && clears.Count == clearsBefore, "GameOver halts pending tickets and cannot clear the wave");
        Check(spawner.OwnedEnemies.All(e => e.IsStoppedByGameOver), "Existing enemy motion also observes shared GameOver");
        label = "Reentrant GameOver during WaveStarted";
        gameOverOnStart = true;flow.RestartRun();
        yield return new Until(() => flow.State == WaveController.WaveState.GameOver, 3, "Reentrant GameOver");
        Check(spawner.TotalSpawned == 0, "GameOver raised inside WaveStarted prevents the first spawn");

        label = "Restart: valid wave and pop locations restored";
        demo.RestartTest();yield return new Until(Waiting, 5, "Final valid restart");
        Check(flow.CurrentWaveNumber == 1 && flow.SpawnedThisWave == 3 && spawner.AliveCount == 3 && demo.StartedEvents == 1 && demo.ClearedEvents == 0, "Restart resets only owned run and emits one fresh WaveStarted");
        demo.FocusLastSpawn();yield return Wait(.3f);
    }

    static void SetPlan(EnemyWavePlan plan)
    {
        var flowData = new SerializedObject(flow);flowData.FindProperty("plan").objectReferenceValue = plan;flowData.ApplyModifiedPropertiesWithoutUndo();
        var spawnData = new SerializedObject(spawner);spawnData.FindProperty("plan").objectReferenceValue = plan;spawnData.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Update()
    {
        // 同じEditor更新で停止が通知された場合も、破棄されたシーンへ触れない。
        if (!running || !EditorApplication.isPlaying) return;
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup - realStart > 180) throw new Exception("Wave tests timed out");
            if (spawner.TotalSpawned > previousSpawnCount)
            {
                if (spawner.TotalSpawned - previousSpawnCount > flow.Plan.MaxSpawnsPerFrame) throw new Exception("Spawn frame budget exceeded");
                spawnRecords.Add(flow.CurrentWaveNumber + "\t" + Time.timeAsDouble.ToString("F3") + "\t" + spawner.LastSpawnPoint + "\t" + spawner.LastSpawned.name);
            }
            previousSpawnCount = spawner.TotalSpawned;
            if (Time.timeAsDouble >= captureNext && frame < 1000) { Capture();captureNext = Time.timeAsDouble + .08; }
            if (EditorApplication.timeSinceStartup < realDeadline || Time.timeAsDouble < deadline) return;
            realDeadline = 0;
            if (until != null)
            {
                if (!until.Condition()) { if (Time.timeAsDouble > until.Timeout) throw new Exception("Timed out: " + until.Message + "; state=" + flow.State + "; error=" + flow.LastError);return; }
                until = null;
            }
            if (!scenario.MoveNext()) { Finish(null);return; }
            deadline = scenario.Current is double time ? time : 0;until = scenario.Current as Until;
        }
        catch (Exception error) { Finish(error); }
    }
    static void Capture()
    {
        var rt = RenderTexture.GetTemporary(1200, 700, 24, RenderTextureFormat.ARGB32);var previous = RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(Camera.main, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt;var image = new Texture2D(1200, 700, TextureFormat.RGB24, false);image.ReadPixels(new Rect(0, 0, 1200, 700), 0, 0);image.Apply();
            File.WriteAllBytes(OUTPUT + "frames/" + frame.ToString("D4") + ".png", image.EncodeToPNG());
            File.AppendAllText(OUTPUT + "frames/timeline.tsv", frame + "\t" + (Time.timeAsDouble - gameStart).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "\t" + label + "\t" + flow.CurrentWaveNumber + "\t" + flow.State + "\t" + flow.PendingCount + "\t" + spawner.AliveCount + "\n");
            frame++;Object.DestroyImmediate(image);
        }
        finally { RenderTexture.active = previous;RenderTexture.ReleaseTemporary(rt); }
    }
    /// <summary>通常完了と手動停止で共有する後始末。ここでは結果の保存やEditor終了を行わない。</summary>
    static void Cleanup()
    {
        EditorApplication.update -= Update;SessionState.EraseBool(KEY);
        GameEvents.WaveStarted -= OnStarted;GameEvents.WaveCleared -= OnCleared;GameEvents.EnemyFell -= OnFell;GameEvents.GameOver -= OnGameOver;
        if (pauseOwner != null) TimeController.ReleasePause(pauseOwner);
        GameContext.PlayerTransform = originalPlayer;
        scenario = null; until = null; demo = null; flow = null; spawner = null; originalPlan = null; testPlan = null; testPlayer = null; originalPlayer = null; pauseOwner = null;
        running = false;
    }

    static void Finish(Exception error)
    {
        if (!running) return;
        Cleanup();
        File.WriteAllText(OUTPUT + "playmode-result.txt", (error == null ? "PASS" : "FAIL: " + error) + "\n" + string.Join("\n", checks));
        File.WriteAllLines(OUTPUT + "spawn-log.tsv", spawnRecords);
        if (error == null) Debug.Log("ENEMY_WAVE_PLAYMODE_PASS " + checks.Count + " checks");else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
#endif
