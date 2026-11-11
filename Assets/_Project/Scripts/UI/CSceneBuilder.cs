#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// エディタ専用。メニュー「Hitofuri/増永/…」から、増永の画面と仕組みを今開いているシーンに組み立てる。
/// 今あるのは「スコアとゲーム中のUI」と「揺れ、SE、エフェクト」の2つ(銘の選択画面やタイトルは、それぞれのタスクで足す)。
/// 手で UI を並べる手間を省き、参照のつなぎ忘れを防ぐため。作った後は位置や色をインスペクターで自由に直してよい。
/// エディタ専用のフォルダ(Editor/)が無いので、#if UNITY_EDITOR で囲んでビルドに入らないようにしている。
/// </summary>
public static class CSceneBuilder
{
    // 調整値のアセットの置き場。決まった置き場が無いので、今は自分の Dev フォルダに置く(髙橋さんと相談して移す)
    const string TUNING_FOLDER = "Assets/_Project/Scenes/Dev/Masunaga";
    // 素材の置き場。どれも元からあるフォルダを使う
    const string AUDIO_FOLDER = "Assets/_Project/Audio";
    const string ART_FOLDER = "Assets/_Project/Art";
    const string MATERIAL_FOLDER = "Assets/_Project/Materials";
    const string PREFAB_FOLDER = "Assets/_Project/Prefabs";

    // テスト用カメラの位置。本番と同じく、斜め上から島を見下ろす
    static readonly Vector3 TEST_CAMERA_POSITION = new Vector3(0f, 11f, -5f);
    static readonly Vector3 TEST_CAMERA_TARGET = new Vector3(0f, 0f, 4f);

    // SE のファイル名(se_〜)と SeId の対応。音を差し替えるときは、同じ名前のファイルで上書きすればよい
    static readonly Dictionary<string, SeId> SE_FILES = new Dictionary<string, SeId>
    {
        { "se_sword_plant", SeId.SwordPlant }, { "se_slash", SeId.Slash }, { "se_hit_kan", SeId.Hit },
        { "se_sheathe", SeId.Sheathe }, { "se_whiff", SeId.Whiff }, { "se_player_damaged", SeId.PlayerDamaged },
        { "se_edge_cross", SeId.EdgeCross }, { "se_fall", SeId.Fall }, { "se_teeter", SeId.Teeter },
        { "se_thunder", SeId.Thunder }, { "se_zap", SeId.Zap }, { "se_mei_open", SeId.MeiOpen },
        { "se_mei_pick", SeId.MeiPick }, { "se_callout", SeId.Callout }, { "se_ui_move", SeId.UiMove },
        { "se_wave_start", SeId.WaveStart }, { "se_shin", SeId.Shin }, { "se_shield_block", SeId.ShieldBlock },
    };

    static readonly Color INK = new Color(0.043f, 0.043f, 0.051f, 1f);
    static readonly Color PANEL = new Color(0.085f, 0.085f, 0.1f, 1f);
    static readonly Color PAPER = new Color(0.925f, 0.92f, 0.9f, 1f);
    static readonly Color ASH = new Color(0.56f, 0.553f, 0.533f, 1f);
    static readonly Color GOLD = new Color(0.914f, 0.71f, 0.29f, 1f);
    static readonly Color GOLD_HI = new Color(1f, 0.886f, 0.627f, 1f);
    static readonly Color FRAME = new Color(1f, 1f, 1f, 0.14f);

    // ================= メニュー =================

    [MenuItem("Hitofuri/増永/スコアとゲーム中のUIを作る")]
    static void BuildGame()
    {
        FeedbackTuning feedbackTuning = LoadOrCreate<FeedbackTuning>("FeedbackTuning");

        GameObject systems = NewObject("C_Systems");
        ScoreCounter score = systems.AddComponent<ScoreCounter>();
        Wire(score, "tuning", feedbackTuning);

        Canvas canvas = NewCanvas("C_GameCanvas");
        GameHud hud = BuildHud(canvas.transform);
        Wire(hud, "tuning", feedbackTuning);
        Wire(hud, "score", score);

        CTestDriver driver = NewObject("C_TestDriver").AddComponent<CTestDriver>();
        Wire(driver, "score", score);

        Selection.activeGameObject = canvas.gameObject;
        CheckFont();
        Debug.Log("[CSceneBuilder] スコアとゲーム中のUIを作った。再生して 1、2、3、0 キーで試せる");
    }

    [MenuItem("Hitofuri/増永/揺れ、SE、エフェクトを作る")]
    static void BuildFeedback()
    {
        FeedbackTuning tuning = LoadOrCreate<FeedbackTuning>("FeedbackTuning");
        int seCount = AssignSe(tuning);
        BuildEffects(tuning);

        // 2つ置くと Feedback.Service を取り合うので、もう置いてあれば作らない
        FeedbackService service = Object.FindFirstObjectByType<FeedbackService>();
        if (service == null)
        {
            GameObject go = NewObject("C_Feedback");
            service = go.AddComponent<FeedbackService>();
            CinemachineImpulseSource impulse = go.AddComponent<CinemachineImpulseSource>();
            SetUpImpulse(impulse);
            Wire(service, "tuning", tuning);
            Wire(service, "impulseSource", impulse);
        }
        SetUpTestCamera();

        Selection.activeGameObject = service.gameObject;
        Debug.Log($"[CSceneBuilder] 揺れ、SE({seCount} 個)、エフェクトを作った。再生して 4(揺れ)、5(SE)、6(エフェクト)、1(一振り)で試せる");
    }

    // ================= ゲーム中の画面 =================

    static GameHud BuildHud(Transform canvas)
    {
        RectTransform root = Rect("HUD", canvas);
        Stretch(root);

        // 得点と連鎖(左上)
        Text(root, "ScoreLabel", "得点", 22, ASH, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -36), new Vector2(200, 30), true);
        TextMeshProUGUI score = Text(root, "Score", "0", 76, PAPER, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -60), new Vector2(600, 90), true);
        score.gameObject.AddComponent<ZapTextEffect>();
        TextMeshProUGUI chain = Text(root, "Chain", "連鎖 0　×1.0", 30, PAPER, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -150), new Vector2(500, 44));
        chain.rectTransform.pivot = new Vector2(0, 0.5f);

        // ウェーブ(右上)
        Text(root, "WaveLabel", "波", 22, ASH, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -36), new Vector2(200, 30), true);
        TextMeshProUGUI wave = Text(root, "Wave", "第一波", 56, PAPER, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -62), new Vector2(400, 70), true);
        wave.characterSpacing = 12;
        wave.gameObject.AddComponent<ZapTextEffect>();
        TextMeshProUGUI waveCount = Text(root, "WaveCount", "1 / 5", 26, ASH, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -136), new Vector2(200, 36));

        // 一振りのカウント(上の中央)
        RectTransform swing = Place(Rect("Swing", root), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(500, 260));
        CanvasGroup swingGroup = swing.gameObject.AddComponent<CanvasGroup>();
        swingGroup.blocksRaycasts = false;
        Text(swing, "Label", "一振り", 28, ASH, TextAlignmentOptions.Top, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(400, 40), true);
        TextMeshProUGUI swingCount = Text(swing, "Count", "0", 150, PAPER, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(400, 170), true);
        swingCount.gameObject.AddComponent<ZapTextEffect>();
        Text(swing, "Unit", "体", 40, PAPER, TextAlignmentOptions.Left, new Vector2(0.5f, 1), new Vector2(0, 0.5f), new Vector2(120, -150), new Vector2(100, 60));
        TextMeshProUGUI swingPoints = Text(swing, "Points", "+0", 38, PAPER, TextAlignmentOptions.Top, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -210), new Vector2(400, 50), true);

        // 称号(中央)
        RectTransform callout = Place(Rect("Callout", root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(900, 320));
        CanvasGroup calloutGroup = callout.gameObject.AddComponent<CanvasGroup>();
        calloutGroup.blocksRaycasts = false;
        TextMeshProUGUI calloutText = Text(callout, "Title", "一閃", 220, PAPER, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(900, 250), true);
        calloutText.characterSpacing = 20;
        calloutText.gameObject.AddComponent<ZapTextEffect>();
        RectTransform calloutSlash = Slash(callout, "Slash", new Vector2(-420, 20), 840, -6);
        calloutSlash.anchorMin = calloutSlash.anchorMax = new Vector2(0.5f, 0.5f);
        TextMeshProUGUI calloutSub = Text(callout, "Sub", "一振りで五体", 26, ASH, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(600, 40));

        // ウェーブの帯(中央より少し下)
        RectTransform banner = Place(Rect("Banner", root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(1200, 220));
        CanvasGroup bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.blocksRaycasts = false;
        Img(banner, "LineTop", FRAME).rectTransform.SetFrom(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(800, 2));
        TextMeshProUGUI bannerText = Text(banner, "Text", "第一波", 96, PAPER, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1100, 120), true);
        bannerText.characterSpacing = 40;
        bannerText.gameObject.AddComponent<ZapTextEffect>();
        TextMeshProUGUI bannerSub = Text(banner, "Sub", "", 26, ASH, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -62), new Vector2(1100, 40));
        Img(banner, "LineBottom", FRAME).rectTransform.SetFrom(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(800, 2));

        GameHud hud = root.gameObject.AddComponent<GameHud>();
        Wire(hud, "scoreText", score);
        Wire(hud, "chainText", chain);
        Wire(hud, "chainPunch", chain.rectTransform);
        Wire(hud, "waveText", wave);
        Wire(hud, "waveCountText", waveCount);
        Wire(hud, "swingGroup", swingGroup);
        Wire(hud, "swingCountText", swingCount);
        Wire(hud, "swingPointsText", swingPoints);
        Wire(hud, "swingPunch", swingCount.rectTransform);
        Wire(hud, "calloutGroup", calloutGroup);
        Wire(hud, "calloutText", calloutText);
        Wire(hud, "calloutSubText", calloutSub);
        Wire(hud, "calloutSlash", calloutSlash);
        Wire(hud, "bannerGroup", bannerGroup);
        Wire(hud, "bannerText", bannerText);
        Wire(hud, "bannerSubText", bannerSub);
        return hud;
    }

    // ================= 揺れ、SE、エフェクト =================

    static void SetUpImpulse(CinemachineImpulseSource impulse)
    {
        // コードで AddComponent したときは、インスペクターで足したときの初期値(Reset)が入らないことがあるので、自分で入れる。
        // Impulse Type を Uniform にするのは、Legacy のままだと揺れの形と長さ(Feedback.Shake の duration)が効かないため
        impulse.ImpulseDefinition = new CinemachineImpulseDefinition
        {
            ImpulseChannel = 1,
            ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump,
            ImpulseDuration = 0.2f,
            ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform,
        };
        impulse.DefaultVelocity = Vector3.down;
    }

    /// <summary>テスト用のカメラ。揺れは Cinemachine のカメラにしか効かないので、まだ無いシーンにだけ作る</summary>
    static void SetUpTestCamera()
    {
        // 本番のカメラ(髙橋さんの CinemachineCamera)には触らない
        if (Object.FindFirstObjectByType<CinemachineCamera>() != null)
        {
            Debug.LogWarning("[CSceneBuilder] シーンに CinemachineCamera があるので、カメラには触らなかった。揺れを出すには、そのカメラに CinemachineImpulseListener を付ける");
            return;
        }
        Camera main = Camera.main;
        if (main == null)
        {
            Debug.LogWarning("[CSceneBuilder] Main Camera が無いので、テスト用のカメラを作らなかった");
            return;
        }
        if (main.GetComponent<CinemachineBrain>() == null) Undo.AddComponent<CinemachineBrain>(main.gameObject);

        GameObject go = NewObject("C_TestVcam");
        go.transform.position = TEST_CAMERA_POSITION;
        go.transform.LookAt(TEST_CAMERA_TARGET);
        go.AddComponent<CinemachineCamera>();
        CinemachineImpulseListener listener = go.AddComponent<CinemachineImpulseListener>();
        listener.ChannelMask = 1;
        listener.Gain = 1f;
        listener.UseCameraSpace = true;
    }

    /// <summary>Audio フォルダの se_〜 を FeedbackTuning に入れる。入れた数を返す</summary>
    static int AssignSe(FeedbackTuning tuning)
    {
        SerializedObject so = new SerializedObject(tuning);
        SerializedProperty entries = so.FindProperty("seEntries");
        int assigned = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AUDIO_FOLDER }))
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip == null || !SE_FILES.TryGetValue(clip.name, out SeId id)) continue;

            SerializedProperty entry = FindEntry(entries, id);
            if (entry == null)
            {
                entry = AddEntry(entries, id);
                entry.FindPropertyRelative("volume").floatValue = DefaultVolume(id);
                // 当たりの音は音階で高さを決めるので、ランダムなずれは小さくする
                entry.FindPropertyRelative("pitchJitter").floatValue = id == SeId.Hit ? 0.01f : 0.03f;
            }
            // 既にある項目は音量などを残し、音だけ入れ直す(インスペクターで直した値を消さないため)
            SerializedProperty clips = entry.FindPropertyRelative("clips");
            clips.arraySize = 1;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
            assigned++;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tuning);
        AssetDatabase.SaveAssets();
        return assigned;
    }

    // 同梱の SE はどれも同じ大きさにそろえてあるので、聞こえ方の釣り合いはここの音量でとる(後はインスペクターで直す)
    static float DefaultVolume(SeId id)
    {
        switch (id)
        {
            case SeId.Hit: return 0.75f;
            case SeId.Thunder: return 0.8f;
            case SeId.Callout: return 0.7f;
            case SeId.EdgeCross: return 0.55f;
            case SeId.Sheathe: return 0.45f;
            case SeId.Fall: return 0.3f;
            case SeId.Teeter: return 0.4f;
            case SeId.UiMove: return 0.3f;
            case SeId.MeiOpen: return 0.5f;
            case SeId.MeiPick: return 0.55f;
            case SeId.Zap: return 0.4f;
            default: return 0.6f;
        }
    }

    /// <summary>エフェクトの素材(テクスチャ、マテリアル)と Prefab を作り、FeedbackTuning に入れる。もうある素材は作り直さない</summary>
    static void BuildEffects(FeedbackTuning tuning)
    {
        // ぼかした点と輪。どの粒子もこの2枚で描く
        Texture2D dot = MakeTexture("vfx_soft_dot", r => Mathf.Pow(Mathf.Clamp01(1f - r), 2f));
        Texture2D ring = MakeTexture("vfx_ring", r => Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) / 0.12f));
        Material dotMaterial = MakeParticleMaterial("VfxSoftDot", dot);
        Material ringMaterial = MakeParticleMaterial("VfxRing", ring);

        SerializedObject so = new SerializedObject(tuning);
        SerializedProperty entries = so.FindProperty("vfxEntries");
        SetVfx(entries, VfxId.LightRing, MakeLightRingPrefab(ringMaterial, dotMaterial), 1f);
        SetVfx(entries, VfxId.SlashTrail, MakeSlashTrailPrefab(dotMaterial), 1f);
        SetVfx(entries, VfxId.Hit, MakeHitPrefab(dotMaterial), 1f);
        SetVfx(entries, VfxId.FallSpark, MakeFallSparkPrefab(dotMaterial), 2f);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tuning);
        AssetDatabase.SaveAssets();
    }

    static void SetVfx(SerializedProperty entries, VfxId id, GameObject prefab, float lifetime)
    {
        SerializedProperty entry = FindEntry(entries, id);
        if (entry == null)
        {
            entry = AddEntry(entries, id);
            entry.FindPropertyRelative("lifetime").floatValue = lifetime;
        }
        entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
    }

    // SeEntry と VfxEntry はどちらも id を持つので、同じ探し方で済む
    static SerializedProperty FindEntry(SerializedProperty entries, System.Enum id)
    {
        int value = System.Convert.ToInt32(id);
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("id").intValue == value) return entry;
        }
        return null;
    }

    static SerializedProperty AddEntry(SerializedProperty entries, System.Enum id)
    {
        // 増やした要素は1つ前の要素の値を写し取るので、呼んだ側で全部の値を入れ直す
        entries.arraySize++;
        SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
        entry.FindPropertyRelative("id").intValue = System.Convert.ToInt32(id);
        return entry;
    }

    static Texture2D MakeTexture(string name, System.Func<float, float> alphaOfRadius)
    {
        string path = $"{ART_FOLDER}/{name}.png";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        const int SIZE = 128;
        Texture2D tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
        for (int y = 0; y < SIZE; y++)
        {
            for (int x = 0; x < SIZE; x++)
            {
                float dx = (x + 0.5f) / SIZE * 2f - 1f, dy = (y + 0.5f) / SIZE * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alphaOfRadius(r)));
            }
        }
        tex.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, path.Substring("Assets/".Length)), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>URP の粒子用シェーダーで、加算(重なるほど明るく光る)のマテリアルを作る</summary>
    static Material MakeParticleMaterial(string name, Texture2D texture)
    {
        string path = $"{MATERIAL_FOLDER}/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Material m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        // インスペクターで Surface Type = Transparent、Blending Mode = Additive を選ぶのと同じ設定
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.SetFloat("_SrcBlend", (float)BlendMode.One);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        if (texture != null) m.SetTexture("_BaseMap", texture);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    /// <summary>粒子を1つ作り、一度だけ噴き出す設定にする。細かい見た目は呼んだ側で決める</summary>
    static ParticleSystem NewBurst(Transform parent, string name, Material material, float duration, short count)
    {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        // 再生中に duration を変えると警告が出るので、先に止める
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.duration = duration;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        return ps;
    }

    static GameObject SavePrefab(GameObject root, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>刀を置いたとき、地面に広がる金の輪</summary>
    static GameObject MakeLightRingPrefab(Material ringMaterial, Material dotMaterial)
    {
        string path = $"{PREFAB_FOLDER}/VfxLightRing.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        ParticleSystem ring = NewBurst(null, "VfxLightRing", ringMaterial, 0.5f, 1);
        ParticleSystem.MainModule main = ring.main;
        main.startLifetime = 0.45f;
        main.startSpeed = 0f;
        main.startSize = 3.5f;
        main.startColor = GOLD_HI;
        ParticleSystem.ShapeModule shape = ring.shape;
        shape.enabled = false;
        ParticleSystem.SizeOverLifetimeModule size = ring.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.1f, 1f, 1f));
        ParticleSystem.ColorOverLifetimeModule color = ring.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(Fade(GOLD_HI, GOLD));
        // 地面に寝かせた板として出す
        ring.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;

        // 輪の内側に、少しだけ昇る光の粒
        ParticleSystem motes = NewBurst(ring.transform, "Motes", dotMaterial, 0.5f, 10);
        ParticleSystem.MainModule motesMain = motes.main;
        motesMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        motesMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        motesMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
        motesMain.startColor = GOLD_HI;
        ParticleSystem.ShapeModule motesShape = motes.shape;
        motesShape.shapeType = ParticleSystemShapeType.Hemisphere;
        motesShape.radius = 0.6f;
        motes.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // 半球を上へ向ける

        return SavePrefab(ring.gameObject, path);
    }

    /// <summary>斬り抜けの向き(Z の正の向き)へ走る光の筋。髙橋さんは斬り抜けの向きを向けて出す</summary>
    static GameObject MakeSlashTrailPrefab(Material material)
    {
        string path = $"{PREFAB_FOLDER}/VfxSlashTrail.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        ParticleSystem ps = NewBurst(null, "VfxSlashTrail", material, 0.3f, 16);
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(28f, 40f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(GOLD_HI, Color.white);
        // 円すいは Z の正の向きへ噴き出すので、角度を細くして一本の筋に見せる
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 3f;
        shape.radius = 0.05f;
        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(Fade(Color.white, GOLD));
        // 速さの向きに引き伸ばして、点ではなく線に見せる
        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.04f;
        renderer.lengthScale = 2f;

        return SavePrefab(ps.gameObject, path);
    }

    /// <summary>当たった瞬間の白い閃きと、四方へ散る火花</summary>
    static GameObject MakeHitPrefab(Material material)
    {
        string path = $"{PREFAB_FOLDER}/VfxHit.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        ParticleSystem flash = NewBurst(null, "VfxHit", material, 0.2f, 1);
        ParticleSystem.MainModule main = flash.main;
        main.startLifetime = 0.1f;
        main.startSpeed = 0f;
        main.startSize = 1.6f;
        main.startColor = Color.white;
        ParticleSystem.ShapeModule shape = flash.shape;
        shape.enabled = false;
        ParticleSystem.SizeOverLifetimeModule size = flash.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        ParticleSystem sparks = NewBurst(flash.transform, "Sparks", material, 0.3f, 14);
        ParticleSystem.MainModule sparksMain = sparks.main;
        sparksMain.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        sparksMain.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
        sparksMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        sparksMain.startColor = new ParticleSystem.MinMaxGradient(GOLD_HI, Color.white);
        ParticleSystem.ShapeModule sparksShape = sparks.shape;
        sparksShape.shapeType = ParticleSystemShapeType.Sphere;
        sparksShape.radius = 0.1f;
        ParticleSystem.ColorOverLifetimeModule sparksColor = sparks.colorOverLifetime;
        sparksColor.enabled = true;
        sparksColor.color = new ParticleSystem.MinMaxGradient(Fade(Color.white, GOLD));
        ParticleSystemRenderer sparksRenderer = sparks.GetComponent<ParticleSystemRenderer>();
        sparksRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        sparksRenderer.velocityScale = 0.03f;
        sparksRenderer.lengthScale = 1.5f;

        return SavePrefab(flash.gameObject, path);
    }

    /// <summary>落ちた敵が霧の中で光の粒になって消える</summary>
    static GameObject MakeFallSparkPrefab(Material material)
    {
        string path = $"{PREFAB_FOLDER}/VfxFallSpark.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        ParticleSystem ps = NewBurst(null, "VfxFallSpark", material, 1.5f, 28);
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(GOLD_HI, Color.white);
        main.gravityModifier = -0.15f;   // 少しずつ昇っていく
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(Fade(Color.white, GOLD));

        return SavePrefab(ps.gameObject, path);
    }

    /// <summary>色を変えながら、最後に透明になって消えるグラデーション</summary>
    static Gradient Fade(Color from, Color to)
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    // ================= 小さな道具 =================

    static T LoadOrCreate<T>(string name) where T : ScriptableObject
    {
        string path = $"{TUNING_FOLDER}/{name}.asset";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CSceneBuilder] {path} を作った");
        return asset;
    }

    static GameObject NewObject(string name)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    static Canvas NewCanvas(string name)
    {
        GameObject go = NewObject(name);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static void SetFrom(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size) => Place(rt, anchor, pivot, pos, size);

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box, bool bold = false)
    {
        RectTransform rt = Place(Rect(name, parent), anchor, pivot, pos, box);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        if (bold) t.fontStyle = FontStyles.Bold;
        return t;
    }

    static Image Img(Transform parent, string name, Color color)
    {
        Image img = Rect(name, parent).gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>金の斜めの線。左端を軸にして、スクリプトから横に伸ばす</summary>
    static RectTransform Slash(Transform parent, string name, Vector2 pos, float width, float angle)
    {
        Image img = Img(parent, name, GOLD_HI);
        RectTransform rt = img.rectTransform;
        Place(rt, new Vector2(0, 1), new Vector2(0, 0.5f), pos, new Vector2(width, 6));
        rt.localRotation = Quaternion.Euler(0, 0, angle);
        return rt;
    }

    // private の [SerializeField] にエディタから参照を入れる(インスペクターでドラッグするのと同じ)
    static void Wire(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[CSceneBuilder] {target.GetType().Name}.{field} が見つからない"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CheckFont()
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font != null && font.HasCharacter('斬', true, true)) return;
        Debug.LogWarning("[CSceneBuilder] TextMeshPro の既定のフォントに日本語が入っていないので、文字が□になる。" +
            "日本語フォント(同梱の ShipporiMinchoB1-ExtraBold.ttf など)を右クリック → Create → TextMeshPro → Font Asset で作り、" +
            "Project Settings → TextMesh Pro → Settings の Default Font Asset に入れる");
    }
}
#endif
