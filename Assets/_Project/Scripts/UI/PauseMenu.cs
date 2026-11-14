using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ゲーム中のポーズ画面(InGame)。Esc、P、ゲームパッドの Start、画面上部の三本線のボタンで開閉する。
/// 開いている間は TimeController に停止を要求し、「再開」「やり直し」「タイトルへ」を選べる。
/// 組み立てはメニュー「Hitofuri/増永/ポーズ画面を作る」(PauseMenuBuilder)で行う。
/// </summary>
public sealed class PauseMenu : MonoBehaviour
{
    // 開くときのフェードの長さ(実時間の秒)。銘の選択画面と揃える
    const float FADE_DURATION = 0.2f;
    // 閉じてから停止を解くまでのフレーム数。Space や A の決定で閉じた同じフレームに、
    // 主人公が「刀を刺すボタンが押された」と読まないよう、少しだけ止めたままにする
    const int RELEASE_DELAY_FRAMES = 2;

    [Tooltip("開いている間だけ表示する、暗幕とボタンの入った板")]
    [SerializeField] GameObject panel;
    [Tooltip("panel のフェードに使う")]
    [SerializeField] CanvasGroup group;
    [SerializeField] Button resumeButton;
    [SerializeField] Button retryButton;
    [SerializeField] Button titleButton;
    [Tooltip("ゲーム中に画面に出しておく、開くためのボタン(三本線)。空でもキーで開ける")]
    [SerializeField] Button openButton;
    [Tooltip("「やり直し」で読み込むシーンの名前。Build Settings に入っている必要がある")]
    [SerializeField] string gameSceneName = "InGame";
    [Tooltip("「タイトルへ」で読み込むシーンの名前")]
    [SerializeField] string titleSceneName = "TitleScene";

    /// <summary>ポーズ画面が開いているか</summary>
    public bool IsOpen { get; private set; }

    // 自分が TimeController に停止を要求している間 true(閉じた直後の数フレームも含む)
    bool pausing;
    int releaseFrames;
    float openTime;
    bool gameOver;
    bool leaving;

    void Awake()
    {
        resumeButton.onClick.AddListener(Close);
        retryButton.onClick.AddListener(() => Leave(gameSceneName));
        titleButton.onClick.AddListener(() => Leave(titleSceneName));
        if (openButton != null) openButton.onClick.AddListener(Open);
        panel.SetActive(false);
    }

    void OnEnable()
    {
        GameEvents.GameOver += OnGameOver;
    }

    void OnDisable()
    {
        GameEvents.GameOver -= OnGameOver;
        // 開いたまま無効化されても、時間が止まり続けないようにする
        if (IsOpen)
        {
            IsOpen = false;
            if (panel != null && gameObject.activeInHierarchy) panel.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
        releaseFrames = 0;
        SetPausing(false);
    }

    void Update()
    {
        if (releaseFrames > 0)
        {
            releaseFrames--;
            if (releaseFrames == 0) SetPausing(false);
        }

        if (WasTogglePressed())
        {
            if (IsOpen) Close();
            else Open();
        }
        else if (IsOpen && Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
        {
            // B ボタン(取り消し)でも閉じられるようにする
            Close();
        }

        if (IsOpen)
        {
            // 時間が止まっているので実時間でフェードインさせる
            openTime += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(openTime / FADE_DURATION);
        }
        if (openButton != null)
        {
            // 開いている間は暗幕の上で目立たないよう隠し、開けない間(銘の選択中など)は押せなくする
            if (openButton.gameObject.activeSelf == IsOpen) openButton.gameObject.SetActive(!IsOpen);
            openButton.interactable = CanOpen();
        }
    }

    // Esc は WebGL の全画面解除と重なるので、全画面の人のために P でも開閉できるようにする
    static bool WasTogglePressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame)) return true;
        Gamepad pad = Gamepad.current;
        return pad != null && pad.startButton.wasPressedThisFrame;
    }

    bool CanOpen()
    {
        if (gameOver || leaving) return false;
        // 銘の選択画面など、他の誰かが止めている間は重ねて開かない(自分の停止だけなら開いてよい)
        return pausing || !TimeController.IsPaused;
    }

    /// <summary>ポーズ画面を開く</summary>
    public void Open()
    {
        if (IsOpen || !CanOpen()) return;
        IsOpen = true;
        releaseFrames = 0;
        SetPausing(true);
        panel.SetActive(true);
        openTime = 0f;
        group.alpha = 0f;
        // ゲームパッドだけでもすぐ選べるよう、誤って進行を捨てにくい「再開」を選んだ状態で開く
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        Feedback.PlaySe(SeId.UiMove, Vector3.zero);
    }

    /// <summary>ポーズ画面を閉じて再開する</summary>
    public void Close()
    {
        if (!IsOpen || leaving) return;
        IsOpen = false;
        panel.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        releaseFrames = RELEASE_DELAY_FRAMES;
        Feedback.PlaySe(SeId.UiMove, Vector3.zero);
    }

    void Leave(string sceneName)
    {
        // 2度押しで読み込みを重ねない
        if (!IsOpen || leaving) return;
        leaving = true;
        Feedback.PlaySe(SeId.UiMove, Vector3.zero);
        // 次のシーンへ停止を持ち込まない。遊び方(GameContext.Mode)はそのまま残るので、果てなくのやり直しは果てなくで始まる
        SetPausing(false);
        SceneManager.LoadScene(sceneName);
    }

    void OnGameOver()
    {
        // ゲームオーバー後は結果画面へ移るので、もう開かない。開いていたら閉じる
        gameOver = true;
        if (IsOpen) Close();
    }

    void SetPausing(bool value)
    {
        if (pausing == value) return;
        pausing = value;
        if (value) TimeController.RequestPause(this);
        else TimeController.ReleasePause(this);
    }
}
