using TMPro;
using UnityEngine;

/// <summary>
/// ゲーム中に一振り最多の自己ベストを超えた瞬間の知らせ(仕様書「スコアと記録」の任せること、増永)。
/// 一振りのカウント(上の中央)のすぐ下に「自己ベスト更新」と出し、その一振りの間は数を追いかけて書き換える。
/// 比べる記録は、ゲームが始まったときの RecordStore の値。ゲームの終わりに上書きされる前の値で比べるため Awake で覚える。
/// 初めて遊ぶとき(記録が無いとき)は、何をしても更新になるので出さない。
/// </summary>
public sealed class BestRecordHud : MonoBehaviour
{
    // 出た瞬間に大きくしてから戻す倍率と、その時間(秒)
    const float POP_SCALE = 1.35f;
    const float POP_TIME = 0.18f;
    // 消えるときに薄くしていく時間(秒)
    const float FADE_TIME = 0.35f;

    [SerializeField] FeedbackTuning tuning;
    [SerializeField] ScoreCounter score;
    [SerializeField] CanvasGroup group;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text subText;
    [Tooltip("テスト用。0 以上にすると、保存してある記録の代わりにこの数と比べる。本番のシーンでは -1 のままにする")]
    [SerializeField] int testBest = -1;

    int best;
    bool shownThisRun;
    int showingSlash = -1;
    float time = -1f;

    void Awake()
    {
        best = testBest >= 0 ? testBest : (RecordStore.HasRecord ? RecordStore.BestMaxSwing : -1);
        if (group != null) group.alpha = 0f;
    }

    void OnEnable()
    {
        if (score != null) score.SwingProgressed += OnSwingProgressed;
    }

    void OnDisable()
    {
        if (score != null) score.SwingProgressed -= OnSwingProgressed;
    }

    void OnSwingProgressed(SwingProgress progress)
    {
        if (best < 0 || progress.Count <= best) return;
        // 1回のゲームで最初に超えたときだけ鳴らして大きく出す。同じ一振りで数が増える間は、数だけ書き換える
        if (!shownThisRun)
        {
            shownThisRun = true;
            time = 0f;
            Feedback.PlaySe(SeId.Thunder, Vector3.zero);
        }
        if (time >= 0f && subText != null)
        {
            subText.text = $"一振り{KanjiNumber.Of(progress.Count)}体　これまでの最多 {KanjiNumber.Of(best)}体";
            // 数が増えるたびに出しておく時間を延ばす
            time = Mathf.Min(time, POP_TIME);
        }
    }

    void Update()
    {
        if (time < 0f || tuning == null || group == null) return;
        time += Time.unscaledDeltaTime;
        float total = tuning.BestBannerTime;
        if (time >= total)
        {
            time = -1f;
            group.alpha = 0f;
            return;
        }
        group.alpha = Mathf.Clamp01((total - time) / FADE_TIME);
        float pop = Mathf.Clamp01(time / POP_TIME);
        float scale = Mathf.Lerp(POP_SCALE, 1f, 1f - (1f - pop) * (1f - pop));
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}
