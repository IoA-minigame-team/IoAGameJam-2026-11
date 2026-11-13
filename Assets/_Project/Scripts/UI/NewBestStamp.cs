using UnityEngine;

/// <summary>
/// リザルトの「自己ベスト更新」の印を、判子のように押す(仕様書「スコアと記録」の任せること、増永)。
/// ResultScreen が自己ベストのときだけ印を表示するので、表示されたら、一振り最多を数え上げ終わるのを待ってから、
/// 大きく傾いた状態から勢いよく縮めて押し付け、押した瞬間に音と小さな揺れを出す。
/// 動きは実時間で回す(リザルトは時間が止まったまま来ることがあるため)。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class NewBestStamp : MonoBehaviour
{
    // 押す前と押したあとの傾き(度)
    const float START_ANGLE = -18f;
    const float END_ANGLE = -6f;
    // 押した瞬間の揺れの強さと時間
    const float SHAKE_STRENGTH = 0.35f;
    const float SHAKE_TIME = 0.15f;
    // 押したあと、少し跳ね返る大きさ
    const float BOUNCE = 0.08f;
    const float BOUNCE_TIME = 0.2f;

    [SerializeField] FeedbackTuning tuning;

    CanvasGroup group;
    float time;
    bool stamped;

    void Awake() => group = GetComponent<CanvasGroup>();

    void OnEnable()
    {
        time = 0f;
        stamped = false;
        group.alpha = 0f;
    }

    void Update()
    {
        if (tuning == null) return;
        time += Time.unscaledDeltaTime;
        float t = time - tuning.BestStampDelay;
        if (t < 0f) return;

        float k = Mathf.Clamp01(t / tuning.BestStampTime);
        // 加速しながら縮める(上から叩きつける感じ)
        float eased = k * k;
        group.alpha = Mathf.Clamp01(k * 3f);
        float scale = Mathf.Lerp(tuning.BestStampScale, 1f, eased);
        float angle = Mathf.Lerp(START_ANGLE, END_ANGLE, eased);

        if (k >= 1f)
        {
            if (!stamped)
            {
                stamped = true;
                Feedback.PlaySe(SeId.Callout, Vector3.zero);
                Feedback.Shake(SHAKE_STRENGTH, SHAKE_TIME);
            }
            float after = t - tuning.BestStampTime;
            float bounce = after < BOUNCE_TIME ? Mathf.Sin(after / BOUNCE_TIME * Mathf.PI) * BOUNCE : 0f;
            scale = 1f + bounce;
            angle = END_ANGLE;
        }
        transform.localScale = new Vector3(scale, scale, 1f);
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}
