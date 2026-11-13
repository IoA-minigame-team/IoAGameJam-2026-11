using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 大量撃破のときの色反転(仕様書「演出」の「できれば」、増永)。
/// 一振りで落とした数が FeedbackTuning の invertAt(断ち)に届いた瞬間に一度、invertBigAt(一刀)に届いた瞬間に二度、画面の色を反転する。
/// 画面いっぱいの Image に UiInvert のマテリアルを付け、色のアルファで反転の強さを決める。
/// HUD より奥(キャンバスの一番上の子)に置くので、反転するのはゲームの画面だけで、得点などの文字は読めたままになる。
/// 動きは実時間で回す。大量撃破の瞬間はヒットストップやスローで時間が止まりやすいため。
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class ScreenInvert : MonoBehaviour
{
    [SerializeField] FeedbackTuning tuning;
    [Tooltip("一振りで落とした数を知らせてくれる得点係(同じ C の担当)")]
    [SerializeField] ScoreCounter score;

    Image image;
    int flashCount;
    // 反転を始めてからの実時間。マイナスは止まっている
    float time = -1f;

    void Awake()
    {
        image = GetComponent<Image>();
        image.raycastTarget = false;
        SetAmount(0f);
    }

    void OnEnable()
    {
        if (score != null) score.SwingProgressed += OnSwingProgressed;
    }

    void OnDisable()
    {
        if (score != null) score.SwingProgressed -= OnSwingProgressed;
        time = -1f;
        SetAmount(0f);
    }

    void OnSwingProgressed(SwingProgress progress)
    {
        if (tuning == null) return;
        // 届いた瞬間だけ。それより多く落ちても、同じ一振りで何度も光らせない
        if (progress.Count == tuning.InvertBigAt) Flash(2);
        else if (progress.Count == tuning.InvertAt) Flash(1);
    }

    /// <summary>count 回反転する</summary>
    public void Flash(int count)
    {
        flashCount = Mathf.Max(1, count);
        time = 0f;
    }

    void Update()
    {
        if (time < 0f || tuning == null) return;
        time += Time.unscaledDeltaTime;

        // 1回の反転は「止める → 戻る」。2回目以降は間(invertGap)を空けて同じことをする
        float one = tuning.InvertHold + tuning.InvertFade;
        float period = one + tuning.InvertGap;
        int index = Mathf.FloorToInt(time / period);
        if (index >= flashCount)
        {
            time = -1f;
            SetAmount(0f);
            return;
        }
        float local = time - index * period;
        if (local >= one)
        {
            SetAmount(0f);
            return;
        }
        float fade = Mathf.Clamp01((local - tuning.InvertHold) / tuning.InvertFade);
        SetAmount(tuning.InvertStrength * (1f - fade * fade));
    }

    void SetAmount(float amount)
    {
        if (image == null) return;
        image.enabled = amount > 0.001f;
        image.color = new Color(1f, 1f, 1f, amount);
    }
}
