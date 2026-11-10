#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>開発用の実動作検証。ビルドには含めず、テストシーンだけを開く。</summary>
[InitializeOnLoad]
public static class EnemyActionPlayModeChecks
{
    const string KEY = "Sanjo.EnemyActionChecks";
    static readonly string WORK = OutputDirectory();
    static EnemyActionAnimation[] actors;
    static Animator[] animators;
    static Vector3[] roots;
    static Quaternion[] idleArm;
    static EnemyActionDemo demo;
    static GameOverController manager;
    static List<string> results;
    static int step, count, frame;
    static double deadline, start, nextCapture;
    static string label;

    static string OutputDirectory()
    {
        // 他の開発PCでも使えるよう、記録先はコマンド引数かOSの一時フォルダーにする。
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-sanjoCheckOutput");
        string path = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Path.GetTempPath(), "HitofuriEnemyActionChecks");
        return Path.GetFullPath(path) + Path.DirectorySeparatorChar;
    }

    static EnemyActionPlayModeChecks()
    {
        EditorApplication.playModeStateChanged += OnState;
    }

    public static void Run()
    {
        SessionState.SetBool(KEY, true);
        EditorSceneManager.OpenScene(EnemyActionAssetSetup.TEST_SCENE);
        EditorApplication.EnterPlaymode();
    }

    static void OnState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(KEY, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            actors = Object.FindObjectsByType<EnemyActionAnimation>(FindObjectsSortMode.None).OrderBy(x => x.transform.position.x).ToArray();
            animators = actors.Select(x => x.GetComponentInChildren<Animator>()).ToArray();
            roots = actors.Select(x => x.transform.position).ToArray();
            demo = Object.FindFirstObjectByType<EnemyActionDemo>();
            manager = Object.FindFirstObjectByType<GameOverController>();
            results = new List<string>(); step = 0; count = 0; frame = 0;
            start = Time.timeAsDouble; deadline = start + 1.5; nextCapture = start;
            label = "Spawn -> Idle";
            Directory.CreateDirectory(WORK + "frames");
            GameEvents.GameOver += CountGameOver;
            EditorApplication.update += Update;
        }
    }

    static void CountGameOver() { count++; }
    static void Check(bool pass, string message)
    {
        if (!pass) throw new Exception(message);
        results.Add(message);
    }
    static Transform Bone(Animator animator, string name) => animator.GetComponentsInChildren<Transform>().Single(x => x.name == name);
    static bool State(Animator animator, string name) => animator.GetCurrentAnimatorStateInfo(0).IsName(name);
    static void After(double seconds, string nextLabel) { deadline = Time.timeAsDouble + seconds; label = nextLabel; step++; }

    static void Update()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            double now = Time.timeAsDouble;
            if (now >= nextCapture && frame < 300)
            {
                Capture(); nextCapture = now + .05;
            }
            if (now < deadline) return;
            switch (step)
            {
                case 0:
                    Check(actors.Length == 3 && animators.All(a => State(a, "Idle")), "All three: automatic Spawn -> Idle");
                    idleArm = animators.Select(a => Bone(a, "L_Arm").localRotation).ToArray();
                    demo.SetDefense(true); After(.65, "Defense: hold"); break;
                case 1:
                    Check(animators.All(a => State(a, "Defense")), "All three: Defense state");
                    Check(animators.Select((a, i) => Quaternion.Angle(idleArm[i], Bone(a, "L_Arm").localRotation) > 15).All(v => v), "Defense changes actual arm bones");
                    After(.6, "Defense: hold"); break;
                case 2:
                    Check(animators.All(a => State(a, "Defense")), "Defense holds until released");
                    demo.Attack(); After(.25, "Attack"); break;
                case 3:
                    Check(animators.All(a => State(a, "Attack")), "All three: Attack state");
                    After(1.1, "Attack -> Defense"); break;
                case 4:
                    Check(animators.All(a => State(a, "Defense")), "Attack returns to Defense when guarding");
                    demo.SetDefense(false); After(.25, "Idle"); break;
                case 5:
                    Check(animators.All(a => State(a, "Idle")), "Defense off -> Idle");
                    demo.Attack(); After(.3, "Attack"); break;
                case 6:
                    Check(animators.All(a => State(a, "Attack")), "Attack from Idle");
                    After(1.05, "Attack -> Idle"); break;
                case 7:
                    Check(animators.All(a => State(a, "Idle")), "Attack returns to Idle");
                    demo.Spawn(); After(.06, "Spawn / Pop"); break;
                case 8:
                    Check(animators.All(a => State(a, "Spawn")), "All three: Spawn state");
                    Check(animators.All(a => Bone(a, "Motion").localScale.x < .6f), "Spawn begins at small visual scale");
                    After(1.25, "Spawn -> Idle"); break;
                case 9:
                    Check(animators.All(a => State(a, "Idle") && Mathf.Abs(Bone(a, "Motion").localScale.x - 1) < .02f), "Spawn restores normal scale");
                    Check(actors.Select((a, i) => Vector3.Distance(a.transform.position, roots[i]) < .001f).All(x => x), "Visual actions never move gameplay root");
                    demo.SimulateDamage(); demo.SimulateDamage();
                    Check(count == 0 && !manager.IsGameOver, "Remaining 2 and 1 do not end game");
                    demo.SimulateDamage();
                    Check(count == 1 && manager.IsGameOver, "Remaining 0 emits GameOver once");
                    GameEvents.RaisePlayerDamaged(0); GameEvents.RaisePlayerDamaged(-1);
                    Check(count == 1 && !manager.TryEndGame(), "Repeated or negative damage cannot duplicate GameOver");
                    Check(animators.All(a => a.speed == 0) && actors.All(a => a.IsStoppedByGameOver), "All three animators stop on shared GameOver");
                    demo.Attack(); After(.5, "GAME OVER: animations stopped"); break;
                case 10:
                    Check(animators.All(a => State(a, "Idle")), "Attack ignored after GameOver");
                    demo.ResetDemo();
                    Check(!manager.IsGameOver && animators.All(a => a.speed == 1), "Reset starts a fresh presentation and round");
                    manager.enabled = false; GameEvents.RaisePlayerDamaged(0);
                    Check(count == 1, "Disabled controller unsubscribes damage");
                    manager.enabled = true;
                    GameEvents.RaisePlayerDamaged(0);
                    Check(count == 2, "Reenabled controller handles next damage once");
                    demo.ResetDemo();
                    var second = new GameObject("SecondControllerTest").AddComponent<GameOverController>();
                    GameEvents.RaisePlayerDamaged(0);
                    Check(count == 3 && second.IsGameOver, "Two scene controllers do not duplicate GameOver");
                    Object.Destroy(second.gameObject);
                    demo.ResetDemo();
                    After(1.25, "Reset: Spawn -> Idle"); break;
                case 11:
                    Check(animators.All(a => State(a, "Idle")), "Reset animations return to Idle");
                    Finish(null); break;
            }
        }
        catch (Exception error) { Finish(error); }
    }

    static void Capture()
    {
        var camera = Camera.main;
        var rt = RenderTexture.GetTemporary(1200, 700, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        try
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = rt;
            var image = new Texture2D(1200, 700, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1200, 700), 0, 0); image.Apply();
            File.WriteAllBytes(WORK + "frames/" + frame.ToString("D4") + ".png", image.EncodeToPNG());
            File.AppendAllText(WORK + "frames/timeline.tsv", frame + "\t" + (Time.timeAsDouble - start).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "\t" + label + "\n");
            frame++; Object.DestroyImmediate(image);
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); }
    }

    static void Finish(Exception error)
    {
        EditorApplication.update -= Update;
        GameEvents.GameOver -= CountGameOver;
        SessionState.SetBool(KEY, false);
        string report = (error == null ? "PASS" : "FAIL: " + error) + "\n" + string.Join("\n", results);
        File.WriteAllText(WORK + "playmode-result.txt", report);
        if (error != null) Debug.LogException(error); else Debug.Log("ENEMY_ACTION_PLAYMODE_PASS " + results.Count + " checks");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
#endif
