using TMPro;
using UnityEngine;

/// <summary>
/// 斬り抜け演出の締めに出す大文字(「一網打尽」)。鍔が鳴る瞬間に SlashCinematic が Play を呼ぶ。
/// 大きく出て一気に縮んで着地し、着地で少し揺れ、後ろを斬り線が走り、少し残ってから消える。
/// 演出用の Canvas(SlashCinematicRig の子)に付ける。HUD とは別の Canvas にして、演出の部品を一か所にまとめるため。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class SlashFinishText : MonoBehaviour
{
    [SerializeField] PlayerTuning tuning;
    [Tooltip("文字。墨色にして紙色の縁取りを付ける")]
    [SerializeField] TMP_Text label;
    [Tooltip("文字の後ろを走る斬り線。左端を軸(Pivot X = 0)にして横に伸ばす")]
    [SerializeField] RectTransform slash;
    [Tooltip("文字の縁取り。景色の明るさに関係なく読めるようにする")]
    [SerializeField] Color outlineColor = new Color(0.96f, 0.93f, 0.86f, 1f);
    [SerializeField, Range(0f, 1f)] float outlineWidth = 0.22f;

    CanvasGroup group;
    RectTransform labelRect;
    Vector2 labelHome;
    // Play からの経過(実時間)。負なら出していない
    float time = -1f;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        if (label != null)
        {
            labelRect = label.rectTransform;
            labelHome = labelRect.anchoredPosition;
            // 実行時に1回だけ設定する。フォントの共有マテリアルを書き換えないよう、インスタンスのマテリアルに入れる
            label.outlineColor = outlineColor;
            label.outlineWidth = outlineWidth;
        }
        Hide();
    }

    /// <summary>最初から出し直す。出している途中で呼ばれても叩きつけからやり直す</summary>
    public void Play()
    {
        if (tuning == null || label == null) return;
        label.text = tuning.FinishTextLabel;
        time = 0f;
        Apply();
    }

    public void Hide()
    {
        time = -1f;
        if (group != null) group.alpha = 0f;
        if (slash != null) slash.localScale = new Vector3(0f, 1f, 1f);
        if (labelRect != null)
        {
            labelRect.anchoredPosition = labelHome;
            labelRect.localScale = Vector3.one;
        }
    }

    void Update()
    {
        if (time < 0f) return;
        // ヒットストップとスローの最中に出すので、実時間で進める
        time += Time.unscaledDeltaTime;
        Apply();
    }

    void Apply()
    {
        float slam = tuning.FinishTextSlamTime;
        float landed = time - slam;
        float end = slam + tuning.FinishTextHold + tuning.FinishTextFade;
        if (time >= end)
        {
            Hide();
            return;
        }

        // 叩きつけ: 終わりほど速く縮む(イーズイン)。着地の瞬間がいちばん勢いがあるように見せる
        float p = Mathf.Clamp01(time / slam);
        float scale = Mathf.Lerp(tuning.FinishTextStartScale, 1f, p * p);
        labelRect.localScale = new Vector3(scale, scale, 1f);

        // 着地の揺れ: 着地からだんだん小さくなる
        Vector2 shake = Vector2.zero;
        if (landed >= 0f && landed < tuning.FinishTextShakeTime)
        {
            float strength = tuning.FinishTextShakeAmount * (1f - landed / tuning.FinishTextShakeTime);
            shake = Random.insideUnitCircle * strength;
        }
        labelRect.anchoredPosition = labelHome + shake;

        // 斬り線: 着地と同時に左から走る
        if (slash != null) slash.localScale = new Vector3(Mathf.Clamp01(landed / tuning.FinishTextSlashTime), 1f, 1f);

        // 出始めは素早く濃くし、残す時間の後に薄れる
        float fadeIn = Mathf.Clamp01(time / (slam * 0.5f));
        float fadeOut = Mathf.Clamp01((end - time) / tuning.FinishTextFade);
        group.alpha = Mathf.Min(fadeIn, fadeOut);
    }
}
