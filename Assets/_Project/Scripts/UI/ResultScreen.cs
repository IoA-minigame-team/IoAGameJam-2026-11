using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// リザルト画面(ResultScene、増永)。一振り最多と称号を最も大きく出し、得点、リトライ、タイトルへを置く(仕様書「画面とUI」)。
/// 値は ScoreCounter がゲームの終わりに GameResult に入れたものを読む。
/// </summary>
public sealed class ResultScreen : MonoBehaviour
{
    [SerializeField] FeedbackTuning tuning;
    [Tooltip("「もう一度」で移るシーンの名前。Build Settings に入っている必要がある")]
    [SerializeField] string gameSceneName = "InGame";
    [SerializeField] string titleSceneName = "TitleScene";

    [Header("確認用(このシーンを直接再生したときだけ使う仮の結果)")]
    [SerializeField] bool testCleared = true;
    [SerializeField, Min(0)] int testScore = 12340;
    [SerializeField, Min(0)] int testMaxSwing = 12;
    [SerializeField, Min(0)] int testMaxChain = 6;
    [SerializeField, Min(0)] int testFalls = 87;
    [SerializeField, Min(1)] int testWave = 5;
    [Tooltip("直接再生したとき、自己ベスト更新の印を出すか(印の動きを確かめるため)")]
    [SerializeField] bool testNewBest = true;

    [SerializeField] TMP_Text headText;
    [SerializeField] TMP_Text subText;
    [SerializeField] TMP_Text maxSwingText;
    [SerializeField] TMP_Text titleText;
    [Tooltip("称号を横切る線。左端を軸(Pivot X = 0)にして横に伸ばす")]
    [SerializeField] RectTransform titleSlash;
    [SerializeField] GameObject newBestBadge;
    [SerializeField] TMP_Text scoreText;
    [SerializeField] TMP_Text chainText;
    [SerializeField] TMP_Text fallsText;
    [SerializeField] TMP_Text majorText;
    [SerializeField] Button retryButton;
    [SerializeField] Button titleButton;

    float time;
    bool thundered;

    void Start()
    {
        // ゲームを通らずにこのシーンを直接再生したときは結果が無いので、見た目を確かめられるよう仮の結果を入れる
        if (!GameResult.HasResult)
        {
            GameResult.Set(testCleared, testScore, testMaxSwing, testMaxChain, testFalls, testWave, MajorMeiId.None, testNewBest);
        }

        retryButton.onClick.AddListener(() => SceneManager.LoadScene(gameSceneName));
        titleButton.onClick.AddListener(() => SceneManager.LoadScene(titleSceneName));
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(retryButton.gameObject);

        headText.text = GameResult.Cleared ? "島を守り抜いた" : "斬られた";
        subText.text = GameResult.Cleared ? $"全{KanjiNumber.Of(tuning.TotalWaves)}波" : $"第{KanjiNumber.Of(Mathf.Max(1, GameResult.WaveReached))}波で倒れた";
        titleText.text = tuning.TitleFor(GameResult.MaxSwing);
        scoreText.text = GameResult.Score.ToString("N0");
        chainText.text = GameResult.MaxChain.ToString();
        fallsText.text = GameResult.Falls.ToString();
        majorText.text = MajorName(GameResult.Major);
        newBestBadge.SetActive(GameResult.IsNewBest);
        maxSwingText.text = "0";
        if (titleSlash != null) titleSlash.localScale = new Vector3(0f, 1f, 1f);
    }

    void Update()
    {
        time += Time.unscaledDeltaTime;
        // 一振り最多を 0 から数え上げ、称号に線を走らせ、雷を鳴らす
        int shown = Mathf.RoundToInt(Mathf.Lerp(0, GameResult.MaxSwing, Mathf.Clamp01(time / 0.6f)));
        maxSwingText.text = shown.ToString();
        if (titleSlash != null) titleSlash.localScale = new Vector3(Mathf.Clamp01((time - 0.5f) / 0.3f), 1f, 1f);
        if (!thundered && time > 0.5f)
        {
            thundered = true;
            Feedback.PlaySe(SeId.Thunder, Vector3.zero);
        }
    }

    // 大銘の名前。銘の調整値(MeiTuning)に頼らず出せるよう、ここに持つ(4つだけで、仕様書「銘」の名前と同じ)
    static string MajorName(MajorMeiId id)
    {
        switch (id)
        {
            case MajorMeiId.Zanko: return "残光";
            case MajorMeiId.Kaeshi: return "返し";
            case MajorMeiId.Shin: return "震";
            case MajorMeiId.Inryoku: return "引力";
            default: return "なし";
        }
    }
}
