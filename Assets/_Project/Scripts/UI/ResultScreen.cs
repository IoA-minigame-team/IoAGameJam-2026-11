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
    [Header("果てなく(エンドレス)のとき、得点を大きく出すために書き換える見出し。空でも動く")]
    [Tooltip("大きな数字の上の見出し(「一振り最多」)")]
    [SerializeField] TMP_Text bigLabelText;
    [Tooltip("大きな数字の横の単位(「体」)")]
    [SerializeField] TMP_Text bigUnitText;
    [Tooltip("右の欄の「得点」の見出し。果てなくでは一振り最多を出す")]
    [SerializeField] TMP_Text scoreLabelText;
    [SerializeField] Button retryButton;
    [SerializeField] Button titleButton;

    float time;
    bool thundered;
    bool endless;
    // 大きな数字で数え上げる値。5波では一振り最多、果てなくでは得点
    int bigValue;

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
        bigValue = GameResult.MaxSwing;
        endless = GameContext.Mode == GameMode.Endless;
        if (endless) ShowEndless();
        maxSwingText.text = "0";
        if (titleSlash != null) titleSlash.localScale = new Vector3(0f, 1f, 1f);
    }

    void Update()
    {
        time += Time.unscaledDeltaTime;
        // 一振り最多を 0 から数え上げ、称号に線を走らせ、雷を鳴らす
        int shown = Mathf.RoundToInt(Mathf.Lerp(0, bigValue, Mathf.Clamp01(time / 0.6f)));
        maxSwingText.text = endless ? shown.ToString("N0") : shown.ToString();
        if (titleSlash != null) titleSlash.localScale = new Vector3(Mathf.Clamp01((time - 0.5f) / 0.3f), 1f, 1f);
        if (!thundered && time > 0.5f)
        {
            thundered = true;
            Feedback.PlaySe(SeId.Thunder, Vector3.zero);
        }
    }

    /// <summary>
    /// 果てなくは得点で競うので、大きな数字を得点にし、一振り最多は右の欄へ移す。
    /// 称号は一振り最多のまま出す(一振り最多は果てなくでも見せたい記録のため)
    /// </summary>
    void ShowEndless()
    {
        subText.text = $"果てなく　第{KanjiNumber.Of(Mathf.Max(1, GameResult.WaveReached))}波で倒れた";
        bigValue = GameResult.Score;
        if (bigLabelText != null) bigLabelText.text = "得点";
        if (bigUnitText != null) bigUnitText.gameObject.SetActive(false);
        if (scoreLabelText != null) scoreLabelText.text = "一振り最多";
        scoreText.text = $"{GameResult.MaxSwing}体";
        // 得点は桁が多く、一振り最多用の大きさでは右の欄に重なるので、枠に収まるまで縮める
        maxSwingText.textWrappingMode = TextWrappingModes.NoWrap;
        maxSwingText.enableAutoSizing = true;
        maxSwingText.fontSizeMin = 60f;
        maxSwingText.fontSizeMax = maxSwingText.fontSize;
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
