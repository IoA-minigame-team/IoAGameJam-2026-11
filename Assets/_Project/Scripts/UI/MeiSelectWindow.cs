using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 銘の選択画面(ゲーム画面に重ねて出す)。開いている間は TimeController に時間を止めてもらう。
/// 何を出すかは MeiManager が決め、ここは並べて選ばせるだけ。マウスでもゲームパッドでも選べるよう、
/// uGUI の Button と EventSystem の選択(矢印キー、スティック、決定ボタン)をそのまま使う。
/// </summary>
public sealed class MeiSelectWindow : MonoBehaviour
{
    [Tooltip("開いている間だけ表示する部分。このコンポーネント自体は常に有効にしておく")]
    [SerializeField] GameObject panel;
    [SerializeField] CanvasGroup group;
    [SerializeField] TMP_Text eyebrowText;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text subText;
    [SerializeField] MeiCardView[] cards;
    [Tooltip("「今の大銘を残す」")]
    [SerializeField] MeiCardView keepCard;

    readonly List<MeiCandidate> current = new List<MeiCandidate>();
    Action<MeiCandidate> onChosen;
    float openTime;

    public bool IsOpen { get; private set; }

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
    }

    /// <summary>eyebrow は見出しの上の小さな一言(「十五体を落とした」など)</summary>
    public void Open(MeiChoiceKind kind, string eyebrow, List<MeiCandidate> candidates, Action<MeiCandidate> chosen)
    {
        current.Clear();
        current.AddRange(candidates);
        onChosen = chosen;

        eyebrowText.text = eyebrow;
        titleText.text = kind == MeiChoiceKind.Minor ? "銘を刻む" : "大銘をひとつ";
        subText.text = kind == MeiChoiceKind.Minor
            ? "小銘は重ねられる。ひとつにつき三段まで。"
            : "大銘はひとつだけ。新しく刻むと、前の大銘は消える。";

        // 候補をカードに、「残す」は専用のボタンに割り当てる
        int cardIndex = 0;
        GameObject first = null;
        keepCard.gameObject.SetActive(false);
        for (int i = 0; i < current.Count; i++)
        {
            if (current[i].IsKeep)
            {
                keepCard.gameObject.SetActive(true);
                keepCard.Bind(this, i, current[i]);
                continue;
            }
            if (cardIndex >= cards.Length) continue;
            cards[cardIndex].gameObject.SetActive(true);
            cards[cardIndex].Bind(this, i, current[i]);
            if (first == null) first = cards[cardIndex].gameObject;
            cardIndex++;
        }
        for (int i = cardIndex; i < cards.Length; i++) cards[i].gameObject.SetActive(false);
        // 大銘の候補が「残す」だけのときも、ゲームパッドで選べるよう「残す」を選んだ状態にする
        if (first == null && keepCard.gameObject.activeSelf) first = keepCard.gameObject;

        TimeController.RequestPause(this);
        IsOpen = true;
        panel.SetActive(true);
        openTime = 0f;
        group.alpha = 0f;

        // 最初のカードを選んだ状態で開く(ゲームパッドだけでもすぐ選べるように)
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first);
        Feedback.PlaySe(SeId.MeiOpen, Vector3.zero);
    }

    /// <summary>カードから呼ばれる。index は Open で渡した候補の番号</summary>
    public void Choose(int index)
    {
        if (!IsOpen || index < 0 || index >= current.Count) return;
        MeiCandidate chosen = current[index];
        Close();
        Feedback.PlaySe(SeId.MeiPick, Vector3.zero);
        onChosen?.Invoke(chosen);
    }

    void Close()
    {
        IsOpen = false;
        panel.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        TimeController.ReleasePause(this);
    }

    void Update()
    {
        if (!IsOpen) return;
        // 時間が止まっているので実時間でフェードインさせる
        openTime += Time.unscaledDeltaTime;
        group.alpha = Mathf.Clamp01(openTime / 0.2f);
    }

    void OnEnable()
    {
        // 開いたまま GameObject ごと無効化されたときは、OnDisable でパネルを閉じられないので、戻ってきたときに閉じる
        if (!IsOpen && panel != null && panel.activeSelf) panel.SetActive(false);
    }

    void OnDisable()
    {
        // 開いたまま無効化されても、時間が止まり続けたり、パネルだけ残ったりしないようにする
        if (!IsOpen) return;
        IsOpen = false;
        // コンポーネントだけ無効化されたときはすぐ閉じる。GameObject ごと無効化中に子の SetActive を呼ぶと Unity がエラーを出すので、そのときは OnEnable に任せる
        if (panel != null && gameObject.activeInHierarchy) panel.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        TimeController.ReleasePause(this);
    }
}
