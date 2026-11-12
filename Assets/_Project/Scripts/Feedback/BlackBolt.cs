using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 黒い雷1本。芯は黒、縁は金の LineRenderer を2本重ね、数フレームごとに折れ線を作り直してバチバチさせる。
/// BlackBolt.Spawn(始点, 終点, 太さ, 寿命, ギザギザの強さ) で出す。使い終わったものは消さずに使い回す(数が多いので)。
/// 飛ぶ敵の残光、大銘「残光」、タイトルの雷など、黒い雷はすべてこれで描く。
/// </summary>
public sealed class BlackBolt : MonoBehaviour
{
    const int POINTS = 14;
    // 縁の太さは芯の何倍か
    const float RIM_RATIO = 2.8f;
    // 折れ線を作り直す間隔(秒)。短いほど激しく揺れる
    const float REGEN_INTERVAL = 0.04f;

    static readonly List<BlackBolt> pool = new List<BlackBolt>();
    static Transform poolRoot;
    static Material coreMaterial;
    static Material rimMaterial;

    LineRenderer core;
    LineRenderer rim;
    readonly Vector3[] points = new Vector3[POINTS];
    Vector3 from;
    Vector3 to;
    float life;
    float maxLife;
    float jitter;
    float regenTimer;

    /// <summary>使うマテリアルを決める。FeedbackService が OnEnable で呼ぶ</summary>
    public static void Configure(Material core, Material rim)
    {
        coreMaterial = core;
        rimMaterial = rim;
    }

    public static void Spawn(Vector3 start, Vector3 end, float width, float lifetime, float jitterAmount)
    {
        // マテリアルが無いときは何も出さない(エフェクトを作る前でもエラーにしない)
        if (coreMaterial == null || rimMaterial == null) return;
        BlackBolt bolt = Take();
        bolt.Play(start, end, width, lifetime, jitterAmount);
    }

    static BlackBolt Take()
    {
        // シーンを読み直すと前のシーンの雷は消えているので、消えたものを外す
        pool.RemoveAll(b => b == null);
        foreach (BlackBolt b in pool)
        {
            if (!b.gameObject.activeSelf) return b;
        }

        if (poolRoot == null) poolRoot = new GameObject("BlackBolts").transform;
        GameObject go = new GameObject("BlackBolt");
        go.transform.SetParent(poolRoot, false);
        BlackBolt bolt = go.AddComponent<BlackBolt>();
        // LineRenderer は1つの GameObject に1つしか付けられないので、縁と芯を子に分ける
        bolt.rim = CreateLine("Rim", go.transform, rimMaterial);
        bolt.core = CreateLine("Core", go.transform, coreMaterial);
        pool.Add(bolt);
        return bolt;
    }

    static LineRenderer CreateLine(string name, Transform parent, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.positionCount = POINTS;
        line.useWorldSpace = true;
        line.numCapVertices = 2;
        line.sharedMaterial = material;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    void Play(Vector3 start, Vector3 end, float width, float lifetime, float jitterAmount)
    {
        from = start;
        to = end;
        life = maxLife = Mathf.Max(0.01f, lifetime);
        jitter = jitterAmount;
        core.widthMultiplier = width;
        rim.widthMultiplier = width * RIM_RATIO;
        regenTimer = 0f;
        gameObject.SetActive(true);
        Regenerate();
        Fade();
    }

    void Update()
    {
        // ゲームの中の出来事なので game time で進める(ヒットストップ中は止まって見える)
        float dt = Time.deltaTime;
        life -= dt;
        if (life <= 0f)
        {
            gameObject.SetActive(false);
            return;
        }
        regenTimer -= dt;
        if (regenTimer <= 0f)
        {
            regenTimer = REGEN_INTERVAL;
            Regenerate();
        }
        Fade();
    }

    void Fade()
    {
        float a = (life / maxLife) * Random.Range(0.7f, 1f);
        Color coreColor = new Color(0.02f, 0.02f, 0.03f, a);
        Color rimColor = new Color(0.91f, 0.71f, 0.29f, a * 0.8f);
        core.startColor = coreColor;
        core.endColor = coreColor;
        rim.startColor = rimColor;
        rim.endColor = rimColor;
    }

    /// <summary>始点と終点の間を、横にずれながら進む折れ線にする。両端は動かさない</summary>
    void Regenerate()
    {
        Vector3 d = to - from;
        Vector3 side = Vector3.Cross(Vector3.up, d.normalized);
        if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
        side.Normalize();

        float offset = 0f, offsetY = 0f;
        for (int i = 0; i < POINTS; i++)
        {
            float t = i / (float)(POINTS - 1);
            float envelope = Mathf.Sin(Mathf.PI * t);   // 両端で 0、真ん中で 1
            // 前の点のずれを少し引き継ぐと、ただのノイズより稲妻らしい折れ方になる
            offset = offset * 0.35f + Random.Range(-1f, 1f) * jitter;
            offsetY = offsetY * 0.4f + Random.Range(-0.5f, 0.5f) * jitter;
            points[i] = from + d * t + side * (offset * envelope) + Vector3.up * (offsetY * envelope * 0.6f);
        }
        core.SetPositions(points);
        rim.SetPositions(points);
    }
}
