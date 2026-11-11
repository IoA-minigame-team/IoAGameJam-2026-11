using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 銘の選択画面のカード1枚。選ばれている間は金の縁取りにして少し持ち上げる。
/// マウスを乗せる、矢印キーやスティックで動かす、のどちらでも「選択中」になるよう EventSystem の選択に合わせる。
/// </summary>
public sealed class MeiCardView : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    [SerializeField] Button button;
    [SerializeField] Image frame;
    [SerializeField] TMP_Text kindText;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text descText;
    [SerializeField] TMP_Text detailText;
    [Tooltip("小銘の段数。三段なら3つ並べる")]
    [SerializeField] Image[] pips;
    [SerializeField] RectTransform lift;

    [Header("色")]
    [SerializeField] Color frameNormal = new Color(1f, 1f, 1f, 0.14f);
    [SerializeField] Color frameSelected = new Color(0.91f, 0.71f, 0.29f, 1f);
    [SerializeField] Color nameNormal = new Color(0.93f, 0.92f, 0.9f);
    [SerializeField] Color nameSelected = new Color(1f, 0.89f, 0.63f);
    [SerializeField] Color pipOn = new Color(0.93f, 0.92f, 0.9f);
    [SerializeField] Color pipNext = new Color(0.91f, 0.71f, 0.29f);
    [SerializeField] Color pipOff = new Color(0.17f, 0.17f, 0.19f);

    MeiSelectWindow window;
    int index;
    bool selected;
    float liftNow;

    void Awake()
    {
        button.onClick.AddListener(() => { if (window != null) window.Choose(index); });
    }

    /// <summary>候補を表示する</summary>
    public void Bind(MeiSelectWindow owner, int candidateIndex, MeiCandidate candidate)
    {
        window = owner;
        index = candidateIndex;

        if (kindText != null) kindText.text = candidate.IsKeep ? "" : candidate.Kind == MeiChoiceKind.Minor ? "小銘" : "大銘";
        // 名前は縦書きに見せたいので、1文字ずつ改行する(TextMeshPro は縦書きに対応していないため)
        if (nameText != null) nameText.text = candidate.IsKeep ? $"今の大銘「{candidate.DisplayName}」を残す" : string.Join("\n", ToChars(candidate.DisplayName));
        if (descText != null) descText.text = candidate.IsKeep ? "" : candidate.Description;
        if (detailText != null) detailText.text = candidate.Detail;

        if (pips != null)
        {
            for (int i = 0; i < pips.Length; i++)
            {
                bool show = candidate.Kind == MeiChoiceKind.Minor && i < candidate.MaxStage;
                pips[i].gameObject.SetActive(show);
                if (!show) continue;
                pips[i].color = i < candidate.CurrentStage ? pipOn : i == candidate.CurrentStage ? pipNext : pipOff;
            }
        }
        selected = false;
        Apply();
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        Apply();
        Feedback.PlaySe(SeId.UiMove, Vector3.zero);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // マウスを乗せたら選択も移す。キーボードとマウスで「選択中」がずれないようにする
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
    }

    void Update()
    {
        if (lift == null) return;
        liftNow = Mathf.MoveTowards(liftNow, selected ? 10f : 0f, Time.unscaledDeltaTime * 80f);
        lift.anchoredPosition = new Vector2(0f, liftNow);
    }

    void Apply()
    {
        if (frame != null) frame.color = selected ? frameSelected : frameNormal;
        if (nameText != null) nameText.color = selected ? nameSelected : nameNormal;
    }

    static string[] ToChars(string s)
    {
        string[] chars = new string[s.Length];
        for (int i = 0; i < s.Length; i++) chars[i] = s[i].ToString();
        return chars;
    }
}
