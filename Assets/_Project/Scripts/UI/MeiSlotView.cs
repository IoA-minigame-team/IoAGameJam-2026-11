using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 大銘の「席」。大銘はひとつしか刻めないことを、文で読ませずに見せるための部品。
/// 席はひとつだけで、新しい大銘を選びかけると、今の大銘を金の線が斬って暗くし、新しい大銘が横から入ってくる。
/// 銘の選択画面、ゲーム中の HUD、タイトルの遊び方の図で使い回す。動きは実時間(止まっている間も動く)。
/// </summary>
public sealed class MeiSlotView : MonoBehaviour
{
    [Tooltip("席の枠。空のときは薄くする")]
    [SerializeField] Image frame;
    [Tooltip("今の大銘の名前")]
    [SerializeField] TMP_Text currentText;
    [Tooltip("選びかけの大銘の名前。横から席に入ってくる")]
    [SerializeField] TMP_Text incomingText;
    [Tooltip("今の大銘を横切る金の線。左端を軸(Pivot X = 0)にして横に伸ばす")]
    [SerializeField] RectTransform strike;
    [Tooltip("空の席に出す文字")]
    [SerializeField] string emptyLabel = "空";

    [Header("動き")]
    [Tooltip("線が走りきるまでの秒数")]
    [SerializeField, Min(0.01f)] float strikeDuration = 0.15f;
    [Tooltip("新しい大銘が席に入りきるまでの秒数")]
    [SerializeField, Min(0.01f)] float enterDuration = 0.2f;
    [Tooltip("新しい大銘が入ってくる前の位置のずれ(右から入る)")]
    [SerializeField] float enterOffset = 60f;
    [Tooltip("斬られた直後の今の大銘の透明度(この後、新しい大銘が入るのに合わせて消える)")]
    [SerializeField, Range(0f, 1f)] float struckAlpha = 0.25f;

    [Header("色")]
    [SerializeField] Color frameFilled = new Color(0.91f, 0.71f, 0.29f, 1f);
    [SerializeField] Color frameEmpty = new Color(1f, 1f, 1f, 0.2f);
    [SerializeField] Color nameColor = new Color(1f, 0.89f, 0.63f, 1f);
    [SerializeField] Color emptyColor = new Color(0.56f, 0.553f, 0.533f, 0.6f);

    [Header("遊び方の図(繰り返し見せる)")]
    [Tooltip("オンにすると、今の大銘 → 斬られる → 入れ替わる を繰り返す")]
    [SerializeField] bool demoLoop;
    [SerializeField] string demoCurrent = "返し";
    [SerializeField] string demoIncoming = "震";
    [Tooltip("繰り返し1回の秒数")]
    [SerializeField, Min(0.5f)] float demoCycle = 2.4f;

    string current;
    string incoming;
    // 選びかけ(incoming)を見せ始めてからの実時間
    float previewTime;
    float demoTime;
    Vector2 incomingHome;
    bool initialized;

    void Awake() => Init();

    void Init()
    {
        if (initialized) return;
        initialized = true;
        if (incomingText != null) incomingHome = incomingText.rectTransform.anchoredPosition;
    }

    void OnEnable()
    {
        Init();
        if (demoLoop)
        {
            demoTime = 0f;
            Show(demoCurrent);
        }
        Apply();
    }

    /// <summary>席に今の大銘を出す。null か空文字なら空の席にする。選びかけの表示は消す</summary>
    public void Show(string currentName)
    {
        Init();
        current = string.IsNullOrEmpty(currentName) ? null : currentName;
        incoming = null;
        Apply();
    }

    /// <summary>選びかけの大銘を見せる。null なら今の大銘のままに戻す(「残す」を選んでいるとき)</summary>
    public void Preview(string incomingName)
    {
        Init();
        string next = string.IsNullOrEmpty(incomingName) ? null : incomingName;
        if (next == incoming) return;
        incoming = next;
        previewTime = 0f;
        Apply();
    }

    void Update()
    {
        if (demoLoop)
        {
            demoTime += Time.unscaledDeltaTime;
            // 前半は今の大銘だけ、後半で選びかけを見せ、一巡したら戻す
            bool showIncoming = demoTime % demoCycle > demoCycle * 0.45f;
            Preview(showIncoming ? demoIncoming : null);
        }
        if (incoming == null) return;
        previewTime += Time.unscaledDeltaTime;
        Apply();
    }

    void Apply()
    {
        bool hasCurrent = current != null;
        bool hasIncoming = incoming != null;

        // 線は今の大銘があるときだけ走る。空の席なら斬るものがないので、すぐ入ってくる
        float strikeT = hasIncoming && hasCurrent ? Mathf.Clamp01(previewTime / strikeDuration) : 0f;
        float enterStart = hasCurrent ? strikeDuration * 0.6f : 0f;
        float enterT = hasIncoming ? Mathf.Clamp01((previewTime - enterStart) / enterDuration) : 0f;
        float eased = 1f - (1f - enterT) * (1f - enterT);

        if (frame != null) frame.color = hasCurrent || hasIncoming ? frameFilled : frameEmpty;

        if (currentText != null)
        {
            currentText.text = hasCurrent ? current : emptyLabel;
            Color c = hasCurrent ? nameColor : emptyColor;
            // 斬られたら暗くなり、新しい大銘が入るのに合わせて席から消える(席にはひとつしか残らない)。
            // 空の席の「空」も、入ってくる大銘に場所を譲って消える
            float fade = hasCurrent ? Mathf.Lerp(1f, struckAlpha, strikeT) * (1f - eased) : 1f - eased;
            c.a *= fade;
            currentText.color = c;
        }

        if (strike != null)
        {
            // 線も今の大銘と一緒に消える
            strike.gameObject.SetActive(strikeT > 0f && eased < 1f);
            strike.localScale = new Vector3(strikeT, 1f - eased, 1f);
        }

        if (incomingText != null)
        {
            incomingText.gameObject.SetActive(hasIncoming);
            if (hasIncoming)
            {
                incomingText.text = incoming;
                Color c = nameColor;
                c.a = eased;
                incomingText.color = c;
                incomingText.rectTransform.anchoredPosition = incomingHome + new Vector2((1f - eased) * enterOffset, 0f);
            }
        }
    }
}
