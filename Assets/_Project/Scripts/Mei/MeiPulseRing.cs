using UnityEngine;

/// <summary>
/// 大銘の発動を見せる、寿命つきの輪と光の線。震(広がる輪)と引力(縮む輪、刀へ走る線)で使う。
/// 刀を刺したときの光の輪(VfxId.LightRing)と見分けられるよう、Prefab ではなく LineRenderer をコードで描いて形と色を変えている。
/// 終わったら自分で消える。
/// </summary>
public sealed class MeiPulseRing : MonoBehaviour
{
    const int RING_SEGMENTS = 64;
    // 地面に埋もれさせないための浮かせる高さ(m)
    const float LIFT = 0.05f;

    LineRenderer line;
    Vector3 center;
    Vector3 lineEnd;
    float fromRadius;
    float toRadius;
    float fromWidth;
    float duration;
    float elapsed;
    Color color;
    bool isStreak;

    /// <summary>center を中心に、半径 fromRadius から toRadius へ duration 秒で変わる輪を出す</summary>
    public static void SpawnRing(Material material, Vector3 center, float fromRadius, float toRadius, float width, float duration, Color color)
    {
        MeiPulseRing ring = Create(material, "MeiPulseRing", RING_SEGMENTS, true);
        ring.center = center;
        ring.fromRadius = fromRadius;
        ring.toRadius = toRadius;
        ring.Begin(width, duration, color, false);
    }

    /// <summary>from から to へ向かって縮みながら消える光の線を出す(引き寄せる敵から刀へ)</summary>
    public static void SpawnStreak(Material material, Vector3 from, Vector3 to, float width, float duration, Color color)
    {
        MeiPulseRing streak = Create(material, "MeiPulseStreak", 2, false);
        streak.center = from;
        streak.lineEnd = to;
        streak.Begin(width, duration, color, true);
    }

    static MeiPulseRing Create(Material material, string name, int points, bool loop)
    {
        GameObject go = new GameObject(name);
        MeiPulseRing pulse = go.AddComponent<MeiPulseRing>();
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.positionCount = points;
        line.numCapVertices = loop ? 0 : 2;
        if (material != null) line.sharedMaterial = material;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        pulse.line = line;
        return pulse;
    }

    void Begin(float width, float seconds, Color tint, bool streak)
    {
        fromWidth = width;
        duration = Mathf.Max(0.01f, seconds);
        color = tint;
        isStreak = streak;
        elapsed = 0f;
        Draw(0f);
    }

    void Update()
    {
        // ゲームの中の出来事なので game time で進める(ポーズ中とヒットストップ中は止まって見える)
        elapsed += Time.deltaTime;
        float k = Mathf.Clamp01(elapsed / duration);
        Draw(k);
        if (k >= 1f) Destroy(gameObject);
    }

    void Draw(float k)
    {
        // 最初に一気に動いて、最後はゆっくり止まる動きにすると、衝撃や吸いこみらしく見える
        float eased = 1f - (1f - k) * (1f - k) * (1f - k);
        Color c = new Color(color.r, color.g, color.b, color.a * (1f - k));
        line.startColor = c;
        line.endColor = c;
        line.widthMultiplier = fromWidth * Mathf.Lerp(1f, 0.3f, k);

        if (isStreak)
        {
            // 敵の側の端が刀へ追いつくように縮める
            Vector3 tail = Vector3.Lerp(center, lineEnd, eased);
            line.SetPosition(0, tail + Vector3.up * LIFT);
            line.SetPosition(1, lineEnd + Vector3.up * LIFT);
            return;
        }

        float radius = Mathf.Lerp(fromRadius, toRadius, eased);
        for (int i = 0; i < RING_SEGMENTS; i++)
        {
            float a = i / (float)RING_SEGMENTS * Mathf.PI * 2f;
            line.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, LIFT, Mathf.Sin(a) * radius));
        }
    }
}
