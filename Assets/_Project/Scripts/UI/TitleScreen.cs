using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// タイトル画面(TitleScene、増永)。置くもの:タイトル、開始、操作説明(仕様書「画面とUI」)。
/// 開くと、奥に立つ主人公が画面を斬り、その斬り跡からロゴと「始める」が現れる。「始める」で画面が光って、ゲームのシーンへ移る。
/// 主人公は髙橋さんの Player の Prefab を見た目だけ借りて置いている(操作のスクリプトは切ってある)。
/// </summary>
public sealed class TitleScreen : MonoBehaviour
{
    [SerializeField] FeedbackTuning tuning;
    [Tooltip("「始める」で移るシーンの名前。Build Settings に入っている必要がある")]
    [SerializeField] string gameSceneName = "InGame";
    [SerializeField] Button startButton;
    [SerializeField] TMP_Text bestText;
    [Header("主人公が画面を斬る")]
    [Tooltip("奥に立つ主人公の Animator。空でも動く(斬る動きが出ないだけ)")]
    [SerializeField] Animator heroAnimator;
    [Tooltip("斬る動き、納める動き、立ち姿のステート名。髙橋さんの Player.controller の名前に合わせる")]
    [SerializeField] string slashStateName = "SlashDash";
    [SerializeField] string sheatheStateName = "Sheathe";
    [SerializeField] string idleStateName = "Locomotion";
    [Tooltip("画面を横切る斬り跡。左端を軸(Pivot X = 0)にして横に伸ばす")]
    [SerializeField] RectTransform screenSlash;
    [Tooltip("主人公が斬り始めるまでの秒数。それまでは主人公だけが見えている")]
    [SerializeField, Min(0f)] float slashTime = 0.7f;
    [Tooltip("斬り始めてから、斬り跡が画面を走りきるまでの秒数")]
    [SerializeField, Min(0.01f)] float slashSweep = 0.18f;
    [Tooltip("斬ってから刀を納めるまでの秒数")]
    [SerializeField, Min(0f)] float sheatheDelay = 0.6f;
    [Tooltip("斬った瞬間の光の強さ(0〜1)")]
    [SerializeField, Range(0f, 1f)] float slashFlash = 0.7f;

    [Header("ロゴと説明")]
    [Tooltip("ロゴを横切る斜めの線。左端を軸(Pivot X = 0)にして横に伸ばす")]
    [SerializeField] RectTransform logoSlash;
    [Tooltip("ロゴ。線が走った後に現れる")]
    [SerializeField] CanvasGroup logoGroup;
    [Tooltip("ロゴの後に、この順で浮かび上がる")]
    [SerializeField] CanvasGroup[] revealInOrder;
    [Header("遊び方")]
    [SerializeField] Button howToButton;
    [Tooltip("遊び方の欄。開いている間だけ表示する")]
    [SerializeField] GameObject howToPanel;
    [SerializeField] Button howToCloseButton;

    [Tooltip("始めるときに画面いっぱいに光る板")]
    [SerializeField] CanvasGroup flash;
    [Tooltip("画面全体(始めるときに消す)")]
    [SerializeField] CanvasGroup content;

    // 動きを切り替える速さ(秒)。短いほどきびきび切り替わる
    const float CROSS_FADE = 0.08f;
    // 斬り跡が走りきってから、ロゴが出始めるまでの間(秒)
    const float LOGO_DELAY = 0.05f;

    float time;
    bool leaving;
    bool slashed;
    bool sheathed;

    void Start()
    {
        startButton.onClick.AddListener(OnStart);
        if (howToButton != null) howToButton.onClick.AddListener(() => ShowHowTo(true));
        if (howToCloseButton != null) howToCloseButton.onClick.AddListener(() => ShowHowTo(false));
        if (howToPanel != null) howToPanel.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(startButton.gameObject);

        bestText.text = RecordStore.HasRecord
            ? $"自己ベスト　一振り最多 {RecordStore.BestMaxSwing}体（{tuning.TitleFor(RecordStore.BestMaxSwing)}）　得点 {RecordStore.BestScore:N0}"
            : "まだ記録はありません。一振りで落とした最多の数が記録になります。";

        logoGroup.alpha = 0f;
        foreach (CanvasGroup g in revealInOrder) g.alpha = 0f;
        if (flash != null) flash.alpha = 0f;
        if (logoSlash != null) logoSlash.localScale = new Vector3(0f, 1f, 1f);
        if (screenSlash != null) screenSlash.localScale = new Vector3(0f, 1f, 1f);
    }

    void Update()
    {
        if (leaving) return;
        time += Time.unscaledDeltaTime;

        // 主人公が斬る → 斬り跡が画面を走って光る → ロゴが現れる → 説明が順に浮かぶ
        if (!slashed && time >= slashTime)
        {
            slashed = true;
            PlayHero(slashStateName);
            Feedback.PlaySe(SeId.Slash, Vector3.zero);
        }
        if (slashed && !sheathed && time >= slashTime + sheatheDelay)
        {
            sheathed = true;
            StartCoroutine(SheatheThenIdle());
        }

        // 斬り始めからの経過(0 より前は、まだ斬っていない)
        float sinceSlash = time - slashTime;
        float sweep = Mathf.Clamp01(sinceSlash / slashSweep);
        if (screenSlash != null) screenSlash.localScale = new Vector3(sweep, 1f, 1f);
        // 斬り跡が走りきった瞬間にいちばん光り、すぐ消える
        float sinceSweep = sinceSlash - slashSweep;
        if (flash != null) flash.alpha = sinceSweep < 0f ? 0f : slashFlash * Mathf.Clamp01(1f - sinceSweep / 0.35f);
        // 斬り跡そのものも、少し遅れて薄れる
        if (screenSlash != null && sinceSweep > 0.25f) screenSlash.localScale = new Vector3(1f, Mathf.Clamp01(1f - (sinceSweep - 0.25f) / 0.3f), 1f);

        float sinceLogo = sinceSweep - LOGO_DELAY;
        if (logoSlash != null) logoSlash.localScale = new Vector3(Mathf.Clamp01(sinceLogo / 0.3f), 1f, 1f);
        logoGroup.alpha = Mathf.Clamp01(sinceLogo / 0.4f);
        for (int i = 0; i < revealInOrder.Length; i++)
        {
            revealInOrder[i].alpha = Mathf.Clamp01((sinceLogo - 0.4f - i * 0.15f) / 0.5f);
        }
    }

    void PlayHero(string stateName)
    {
        if (heroAnimator == null || string.IsNullOrEmpty(stateName)) return;
        heroAnimator.CrossFadeInFixedTime(stateName, CROSS_FADE);
    }

    IEnumerator SheatheThenIdle()
    {
        PlayHero(sheatheStateName);
        yield return new WaitForSecondsRealtime(sheatheDelay);
        PlayHero(idleStateName);
    }

    /// <summary>遊び方の欄を開く、閉じる。ゲームパッドでも操作できるよう、選択中のボタンも移す</summary>
    void ShowHowTo(bool show)
    {
        if (howToPanel == null || leaving) return;
        howToPanel.SetActive(show);
        Feedback.PlaySe(SeId.UiMove, Vector3.zero);
        GameObject select = show ? howToCloseButton.gameObject : howToButton.gameObject;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(select);
    }

    void OnStart()
    {
        if (leaving) return;
        leaving = true;
        Feedback.PlaySe(SeId.Slash, Vector3.zero);
        Feedback.PlaySe(SeId.Thunder, Vector3.zero);
        StartCoroutine(Leave());
    }

    IEnumerator Leave()
    {
        float t = 0f;
        while (t < 0.55f)
        {
            t += Time.unscaledDeltaTime;
            if (flash != null) flash.alpha = 1f - t / 0.55f;
            if (content != null) content.alpha = 1f - t / 0.4f;
            yield return null;
        }
        SceneManager.LoadScene(gameSceneName);
    }
}
