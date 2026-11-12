using UnityEngine;

/// <summary>
/// 刀の光の見た目(仕様書「刀と光」の任せること、増永)。
/// ・刀を置くと、灯喰らいを引き寄せる範囲まで光の輪が広がる(SwordPlanted)
/// ・群がる灯喰らいが多いほど、光と輪が揺らぐ(SwarmCountChanged)
/// ・自動で斬り戻すまでの残り時間を、刀の周りで縮む輪で見せる(GameContext.AutoReturnRemaining / AutoReturnLimit)
/// ・刀を持っている間は、光が弱まって刀(主人公)とともに動く
/// 刀そのものの形とモデルは髙橋さんの担当なので、ここは光と輪だけを出す。刀の位置は GameContext から読むだけ。
/// </summary>
public sealed class SwordLightView : MonoBehaviour
{
    const int RING_SEGMENTS = 96;
    // 光の輪が広がりきるまでの秒数
    const float EXPAND_TIME = 0.4f;
    // 輪を地面に埋もれさせないための浮かせる高さ(m)
    const float RING_LIFT = 0.03f;
    // 残り時間の輪の、一番小さいときの大きさ(m)。0 にすると見えなくなるため
    const float TIMER_RING_MIN = 0.14f;
    // 輪の太さ(m)
    const float RANGE_RING_WIDTH = 0.06f;
    const float TIMER_RING_WIDTH = 0.1f;
    // 光の色(主人公と刀のアクセントの金に合わせる)
    static readonly Color GOLD = new Color(0.91f, 0.71f, 0.29f, 1f);

    [SerializeField] FeedbackTuning tuning;
    [SerializeField] Light swordLight;
    [Tooltip("灯喰らいを引き寄せる範囲を見せる輪")]
    [SerializeField] LineRenderer rangeRing;
    [Tooltip("自動で斬り戻すまでの残り時間を見せる輪")]
    [SerializeField] LineRenderer timerRing;

    bool planted;
    Vector3 plantedAt;
    float plantedTime;
    int swarm;
    float flicker;
    float flickerTimer;

    void Awake()
    {
        SetupRing(rangeRing, RANGE_RING_WIDTH);
        SetupRing(timerRing, TIMER_RING_WIDTH);
        if (swordLight != null)
        {
            swordLight.type = LightType.Point;
            swordLight.color = GOLD;
        }
        SetRingsVisible(false);
    }

    void OnEnable()
    {
        GameEvents.SwordPlanted += OnSwordPlanted;
        GameEvents.SlashStarted += OnSlashStarted;
        GameEvents.SwarmCountChanged += OnSwarmCountChanged;
        GameEvents.GameOver += OnGameOver;
    }

    void OnDisable()
    {
        GameEvents.SwordPlanted -= OnSwordPlanted;
        GameEvents.SlashStarted -= OnSlashStarted;
        GameEvents.SwarmCountChanged -= OnSwarmCountChanged;
        GameEvents.GameOver -= OnGameOver;
    }

    void OnSwordPlanted(Vector3 position)
    {
        planted = true;
        plantedAt = position;
        plantedTime = 0f;
        SetRingsVisible(true);
    }

    // 斬り戻しが始まったら刀は抜けているので、輪を消して持っているときの光に戻す
    void OnSlashStarted(SlashStartedInfo info) => Unplant();

    void OnSwarmCountChanged(int count) => swarm = Mathf.Max(0, count);

    void OnGameOver()
    {
        Unplant();
        if (swordLight != null) swordLight.enabled = false;
    }

    void Unplant()
    {
        planted = false;
        SetRingsVisible(false);
    }

    void Update()
    {
        if (tuning == null) return;
        float dt = Time.deltaTime;
        UpdateFlicker(dt);

        if (!planted)
        {
            FollowCarriedSword();
            return;
        }
        plantedTime += dt;

        // 刀の位置は髙橋さんが GameContext に書く。まだ書かれていなければ、刺さったときの位置を使う
        transform.position = GameContext.IsSwordPlanted ? GameContext.SwordPosition : plantedAt;
        SetLight(1f);
        DrawRangeRing();
        DrawTimerRing();
    }

    /// <summary>群がるほど、短い間隔で大きく揺らぐ。持っている間も少しだけ揺らす</summary>
    void UpdateFlicker(float dt)
    {
        float amplitude = Mathf.Min(tuning.FlickerMax, tuning.FlickerBase + tuning.FlickerPerSwarm * swarm);
        flickerTimer -= dt;
        if (flickerTimer > 0f) return;
        // 群がるほど揺らぐ間隔も短くし、ちらつきを速く見せる
        flickerTimer = Random.Range(0.03f, 0.08f) / (1f + swarm * 0.3f);
        flicker = Random.Range(-amplitude, amplitude);
    }

    /// <summary>刀を持っている間は、光が主人公とともに動く(仕様)。主人公がいないシーンでは光を消す</summary>
    void FollowCarriedSword()
    {
        bool hasPlayer = GameContext.PlayerTransform != null;
        if (swordLight != null) swordLight.enabled = hasPlayer;
        if (!hasPlayer) return;
        transform.position = GameContext.SwordPosition;
        SetLight(tuning.CarriedLightRatio);
    }

    void SetLight(float ratio)
    {
        if (swordLight == null) return;
        swordLight.enabled = true;
        swordLight.transform.localPosition = new Vector3(0f, tuning.SwordLightHeight, 0f);
        swordLight.range = tuning.SwordLightRange;
        swordLight.intensity = tuning.SwordLightIntensity * ratio * (1f + flicker);
    }

    /// <summary>光の輪:引き寄せる範囲まで一気に広がり、薄く残る。群がるほど大きさと明るさが揺らぐ</summary>
    void DrawRangeRing()
    {
        float lure = tuning.LureRadius * GameContext.MeiStats.LureRadiusMultiplier;
        float k = Mathf.Clamp01(plantedTime / EXPAND_TIME);
        float eased = 1f - (1f - k) * (1f - k) * (1f - k);
        float radius = Mathf.Lerp(0.05f, lure, eased) * (1f + flicker * tuning.RingWobble);
        DrawCircle(rangeRing, radius);
        SetAlpha(rangeRing, Mathf.Lerp(0.5f, 0.16f, k) * Mathf.Clamp(1f + flicker * 0.8f, 0.3f, 1.6f));
    }

    /// <summary>残り時間の輪:刀へ向かって縮む。残りわずかになったら点滅させる</summary>
    void DrawTimerRing()
    {
        float limit, remaining;
        if (GameContext.AutoReturnLimit > 0f)
        {
            // 髙橋さんが群がり数と銘を含めて計算した値をそのまま使う(こちらで同じ計算を持たない)
            limit = GameContext.AutoReturnLimit;
            remaining = GameContext.AutoReturnRemaining;
        }
        else
        {
            // 主人公がいないテスト用のシーンだけ、仮の値で見せる
            limit = Mathf.Max(tuning.AutoReturnMin,
                tuning.AutoReturnBase * GameContext.MeiStats.AutoReturnTimeMultiplier - tuning.AutoReturnPerSwarm * swarm);
            remaining = Mathf.Max(0f, limit - plantedTime);
        }
        float ratio = limit > 0f ? Mathf.Clamp01(remaining / limit) : 0f;
        DrawCircle(timerRing, Mathf.Max(TIMER_RING_MIN, tuning.TimerRingRadius * ratio));
        bool urgent = remaining < tuning.TimerUrgentSeconds;
        // 点滅は実時間で。ヒットストップ中も見えるようにする
        SetAlpha(timerRing, urgent ? 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 18f)) : 0.85f);
    }

    void SetRingsVisible(bool visible)
    {
        if (rangeRing != null) rangeRing.enabled = visible;
        if (timerRing != null) timerRing.enabled = visible;
    }

    void SetupRing(LineRenderer ring, float width)
    {
        if (ring == null) return;
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = RING_SEGMENTS;
        ring.widthMultiplier = width;
        if (tuning != null && tuning.RingMaterial != null) ring.sharedMaterial = tuning.RingMaterial;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
    }

    static void DrawCircle(LineRenderer ring, float radius)
    {
        if (ring == null) return;
        for (int i = 0; i < RING_SEGMENTS; i++)
        {
            float a = i / (float)RING_SEGMENTS * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, RING_LIFT, Mathf.Sin(a) * radius));
        }
    }

    static void SetAlpha(LineRenderer ring, float alpha)
    {
        if (ring == null) return;
        Color c = new Color(GOLD.r, GOLD.g, GOLD.b, alpha);
        ring.startColor = c;
        ring.endColor = c;
    }
}
