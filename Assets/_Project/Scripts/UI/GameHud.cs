using TMPro;
using UnityEngine;

/// <summary>
/// ゲーム中の画面(仕様書「画面とUI」)。置くのは得点、連鎖倍率、ウェーブ数だけで、体力ゲージは出さない。
/// 一振りのカウント、称号、ウェーブの帯は、出来事が起きたときだけ短く出す。
/// 動きはすべて unscaled time で書く(ヒットストップや銘の選択で時間が止まっても、UIは動かす)。
/// </summary>
public sealed class GameHud : MonoBehaviour
{
    [SerializeField] FeedbackTuning tuning;
    [SerializeField] ScoreCounter score;

    [Header("得点と連鎖")]
    [SerializeField] TMP_Text scoreText;
    [SerializeField] TMP_Text chainText;
    [SerializeField] RectTransform chainPunch;

    [Header("ウェーブ")]
    [SerializeField] TMP_Text waveText;
    [SerializeField] TMP_Text waveCountText;

    [Header("一振りのカウント")]
    [SerializeField] CanvasGroup swingGroup;
    [SerializeField] TMP_Text swingCountText;
    [SerializeField] TMP_Text swingPointsText;
    [SerializeField] RectTransform swingPunch;

    [Header("称号")]
    [SerializeField] CanvasGroup calloutGroup;
    [SerializeField] TMP_Text calloutText;
    [SerializeField] TMP_Text calloutSubText;
    [Tooltip("称号を横切る線。左端を軸(Pivot X = 0)にして横に伸ばす")]
    [SerializeField] RectTransform calloutSlash;

    [Header("ウェーブの帯")]
    [SerializeField] CanvasGroup bannerGroup;
    [SerializeField] TMP_Text bannerText;
    [SerializeField] TMP_Text bannerSubText;

    float shownScore;
    float swingTimer;
    float swingPunchTimer;
    float chainPunchTimer;
    float calloutTimer = -1f;
    float bannerTimer = -1f;

    const float PUNCH_TIME = 0.3f;

    void OnEnable()
    {
        score.SwingProgressed += OnSwingProgressed;
        score.ChainUpdated += OnChainUpdated;
        GameEvents.WaveStarted += OnWaveStarted;
    }

    void OnDisable()
    {
        score.SwingProgressed -= OnSwingProgressed;
        score.ChainUpdated -= OnChainUpdated;
        GameEvents.WaveStarted -= OnWaveStarted;
    }

    void Start()
    {
        swingGroup.alpha = 0f;
        calloutGroup.alpha = 0f;
        bannerGroup.alpha = 0f;
        scoreText.text = "0";
        OnChainUpdated(0, 1f);
        SetWave(1);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // 得点は数字が追いかけるように増やす(一気に変わると何点入ったか分からないため)
        float k = 1f - Mathf.Exp(-tuning.ScoreCountSpeed * dt);
        shownScore = Mathf.Lerp(shownScore, score.Score, k);
        if (Mathf.Abs(score.Score - shownScore) < 0.5f) shownScore = score.Score;
        scoreText.text = Mathf.RoundToInt(shownScore).ToString("N0");

        // 一振りのカウント: 最後に落ちてから少し残して消す
        swingTimer -= dt;
        swingGroup.alpha = Mathf.MoveTowards(swingGroup.alpha, swingTimer > 0f ? 1f : 0f, dt * 5f);
        swingPunchTimer = Mathf.Max(0f, swingPunchTimer - dt);
        swingPunch.localScale = Vector3.one * (1f + 0.45f * Punch(swingPunchTimer));

        chainPunchTimer = Mathf.Max(0f, chainPunchTimer - dt);
        chainPunch.localScale = Vector3.one * (1f + 0.3f * Punch(chainPunchTimer));

        UpdateCallout(dt);
        UpdateBanner(dt);
    }

    // 1 から 0 へ、最初に速く戻る曲線。数字が「ポンッ」と弾む
    static float Punch(float timer) => Mathf.Pow(Mathf.Clamp01(timer / PUNCH_TIME), 2f);

    void OnSwingProgressed(SwingProgress progress)
    {
        swingCountText.text = progress.Count.ToString();
        swingPointsText.text = "+" + progress.SwingPoints.ToString("N0");
        swingTimer = tuning.SwingHideDelay;
        swingPunchTimer = PUNCH_TIME;

        if (!string.IsNullOrEmpty(progress.TitleReached))
        {
            ShowCallout(progress.TitleReached, $"一振りで{KanjiNumber.Of(progress.Count)}体");
        }
    }

    void OnChainUpdated(int chain, float multiplier)
    {
        chainText.text = $"連鎖 {chain}　×{multiplier:0.0}";
        if (chain > 0) chainPunchTimer = PUNCH_TIME;
    }

    void OnWaveStarted(int wave)
    {
        SetWave(wave);
        bannerText.text = $"第{KanjiNumber.Of(wave)}波";
        bannerSubText.text = tuning.WaveHint(wave);
        bannerTimer = 0f;
    }

    void SetWave(int wave)
    {
        waveText.text = $"第{KanjiNumber.Of(wave)}波";
        // 果てなくには終わりの波が無いので、全何波の代わりに遊び方の名前を出す
        waveCountText.text = GameContext.Mode == GameMode.Endless ? "果てなく" : $"{wave} / {tuning.TotalWaves}";
    }

    public void ShowCallout(string title, string sub)
    {
        calloutText.text = title;
        calloutSubText.text = sub;
        calloutTimer = 0f;
        Feedback.PlaySe(SeId.Callout, Vector3.zero);
    }

    void UpdateCallout(float dt)
    {
        if (calloutTimer < 0f) return;
        calloutTimer += dt;
        float t = calloutTimer, d = tuning.CalloutDuration;
        // 大きく出て縮み、しばらく残って消える
        calloutGroup.alpha = t < 0.12f ? t / 0.12f : Mathf.Clamp01((d - t) / 0.35f);
        calloutText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Mathf.Clamp01(t / 0.15f));
        if (calloutSlash != null) calloutSlash.localScale = new Vector3(Mathf.Clamp01((t - 0.08f) / 0.25f), 1f, 1f);
        if (t >= d) { calloutTimer = -1f; calloutGroup.alpha = 0f; }
    }

    void UpdateBanner(float dt)
    {
        if (bannerTimer < 0f) return;
        bannerTimer += dt;
        float t = bannerTimer, d = tuning.BannerDuration;
        bannerGroup.alpha = t < 0.25f ? t / 0.25f : Mathf.Clamp01((d - t) / 0.4f);
        if (t >= d) { bannerTimer = -1f; bannerGroup.alpha = 0f; }
    }
}
