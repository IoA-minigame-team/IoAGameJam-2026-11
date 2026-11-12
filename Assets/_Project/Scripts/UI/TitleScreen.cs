using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// タイトル画面(TitleScene、増永)。置くもの:タイトル、開始、操作説明(仕様書「画面とUI」)。
/// 開くと、奥から主人公が走ってきて、止まった所で画面を斬る。その斬り跡からロゴと「始める」が現れる。「始める」で画面が光って、ゲームのシーンへ移る。
/// 主人公は髙橋さんの Player の Prefab を見た目だけ借りて置いている(操作のスクリプトは切ってある)。
/// </summary>
// 主人公の操作のスクリプトより先に動いて、それらを止めるため、早めに動かす
[DefaultExecutionOrder(-1000)]
public sealed class TitleScreen : MonoBehaviour
{
    // 主人公が見つからないときに探す名前(エディタのメニューが付ける名前)
    const string HERO_OBJECT_NAME = "C_TitleHero";

    [SerializeField] FeedbackTuning tuning;
    [Tooltip("「始める」で移るシーンの名前。Build Settings に入っている必要がある")]
    [SerializeField] string gameSceneName = "InGame";
    [SerializeField] Button startButton;
    [SerializeField] TMP_Text bestText;
    [Header("主人公が走ってきて、画面を斬る")]
    [Tooltip("主人公の根(位置を動かす)。シーンに置いた位置が、走り終わって止まる位置になる")]
    [SerializeField] Transform hero;
    [Tooltip("走り始める位置の、止まる位置からのずれ(m)。X で右へ、Z で奥へ。カメラが低いので、遠すぎると手前の草に隠れる")]
    [SerializeField] Vector3 runFrom = new Vector3(5f, 0f, 3f);
    [Tooltip("走ってくるのにかける秒数")]
    [SerializeField, Min(0.1f)] float runDuration = 0.9f;
    [Tooltip("止まってから斬るまでの秒数")]
    [SerializeField, Min(0f)] float slashPause = 0.12f;
    [Tooltip("走る動きの速さの値(Animator の Speed)。髙橋さんの Player.controller の走りに合わせる")]
    [SerializeField, Min(0f)] float runAnimSpeed = 8f;
    [SerializeField] string speedParam = "Speed";
    [Tooltip("奥に立つ主人公の Animator。空でも動く(斬る動きが出ないだけ)")]
    [SerializeField] Animator heroAnimator;
    [Tooltip("斬る動き、納める動き、立ち姿のステート名。髙橋さんの Player.controller の名前に合わせる")]
    [SerializeField] string slashStateName = "SlashDash";
    [SerializeField] string sheatheStateName = "Sheathe";
    [SerializeField] string idleStateName = "Locomotion";
    [Tooltip("画面を横切る斬り跡。左端を軸(Pivot X = 0)にして横に伸ばす")]
    [SerializeField] RectTransform screenSlash;
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
    // シーンを開いた直後は読み込みで1コマが長くなり、走る演出が一瞬で終わってしまう。1コマで進める時間をこれで切る(秒)
    const float MAX_STEP = 1f / 30f;
    // 開いてから走り始めるまでの間(秒)。画面が落ち着いてから走らせる
    const float INTRO_DELAY = 0.3f;
    // 斬り跡が走りきってから、ロゴが出始めるまでの間(秒)
    const float LOGO_DELAY = 0.05f;

    float time;
    float slashTime;
    bool leaving;
    bool arrived;
    Vector3 heroEnd;
    Quaternion heroEndRotation;
    Vector3 heroStart;
    Quaternion heroRunRotation;
    bool slashed;
    bool sheathed;

    void Awake()
    {
        // 参照が外れていても動くよう、名前で探す
        if (hero == null)
        {
            GameObject found = GameObject.Find(HERO_OBJECT_NAME);
            if (found != null) hero = found.transform;
        }
        if (hero == null)
        {
            Debug.LogWarning("[TitleScreen] 主人公(C_TitleHero)が見つからないので、走る演出を飛ばす");
            return;
        }
        if (heroAnimator == null) heroAnimator = hero.GetComponentInChildren<Animator>();
        if (heroAnimator == null) Debug.LogWarning("[TitleScreen] 主人公の Animator が見つからないので、走る動きと斬る動きが出ない");

        // タイトルでは見た目だけ使う。主人公の操作や見た目のスクリプトが動いていると、
        // 位置や動き(Animator)を毎フレーム上書きされて、走る演出も斬る演出も出ないので、ここで止める
        foreach (MonoBehaviour behaviour in hero.GetComponentsInChildren<MonoBehaviour>(true))
        {
            behaviour.enabled = false;
        }
        CharacterController body = hero.GetComponent<CharacterController>();
        if (body != null) body.enabled = false;
        // 刀の軌跡や狙いの線(ゲーム中に使う線)は、操作を止めると変な位置に伸びたまま黒く残るので、タイトルでは消す
        foreach (Renderer line in hero.GetComponentsInChildren<Renderer>(true))
        {
            if (line is LineRenderer || line is TrailRenderer) line.enabled = false;
        }
        if (heroAnimator != null)
        {
            heroAnimator.enabled = true;
            heroAnimator.applyRootMotion = false;
        }
    }

    void Start()
    {
        // 主人公の準備を最初にする(画面の部品の設定で何か失敗しても、走る演出は動くように)
        slashTime = INTRO_DELAY + runDuration + slashPause;
        if (hero != null)
        {
            heroEnd = hero.position;
            heroEndRotation = hero.rotation;
            heroStart = heroEnd + runFrom;
            Vector3 runDirection = heroEnd - heroStart;
            runDirection.y = 0f;
            heroRunRotation = runDirection.sqrMagnitude > 0.001f ? Quaternion.LookRotation(runDirection) : heroEndRotation;
            hero.SetPositionAndRotation(heroStart, heroRunRotation);
            SetHeroSpeed(runAnimSpeed);
            // 確認用。走り始めと止まる位置を Console に出す
            Debug.Log($"[TitleScreen] 主人公を {heroStart} から {heroEnd} へ {runDuration} 秒で走らせる");
        }

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
        time += Mathf.Min(Time.unscaledDeltaTime, MAX_STEP);

        // 主人公が走ってくる → 止まって斬る → 斬り跡が画面を走って光る → ロゴが現れる → 説明が順に浮かぶ
        MoveHero();
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

    /// <summary>走ってくる間は奥から止まる位置へ動かし、着いたら止まる向きへ振り向かせる</summary>
    void MoveHero()
    {
        if (hero == null) return;
        float runTime = time - INTRO_DELAY;
        if (runTime < 0f) return;
        if (runTime < runDuration)
        {
            // 最後だけ少し減速して、踏みとどまったように見せる
            float t = runTime / runDuration;
            float eased = 1f - (1f - t) * (1f - t) * 0.35f - 0.65f * (1f - t);
            hero.SetPositionAndRotation(Vector3.Lerp(heroStart, heroEnd, Mathf.Clamp01(eased)), heroRunRotation);
            return;
        }
        if (!arrived)
        {
            arrived = true;
            SetHeroSpeed(0f);
        }
        float turn = Mathf.Clamp01((runTime - runDuration) / Mathf.Max(0.01f, slashPause));
        hero.SetPositionAndRotation(heroEnd, Quaternion.Slerp(heroRunRotation, heroEndRotation, turn));
    }

    void SetHeroSpeed(float speed)
    {
        if (heroAnimator == null || string.IsNullOrEmpty(speedParam)) return;
        heroAnimator.SetFloat(speedParam, speed);
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
        // 後ろのロゴやボタンが透けて読みにくくならないよう、開いている間はタイトルの文字を隠す
        if (content != null) content.alpha = show ? 0f : 1f;
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
