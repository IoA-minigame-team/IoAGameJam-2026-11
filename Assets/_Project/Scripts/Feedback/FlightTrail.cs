using UnityEngine;

/// <summary>
/// 吹き飛んだ敵の後ろに黒い雷を走らせる(演出「飛ぶ:速さに応じた残光」)。
/// FeedbackService が EnemyHit を受けたときに敵に付ける。敵の移動は三條さんのコードなので、ここは位置の変化から速さを測るだけで、敵を動かさない。
/// 遅くなったら自分で止まる。
/// </summary>
public sealed class FlightTrail : MonoBehaviour
{
    // 遅い状態がこの秒数続いたら止める。一瞬の減速で途切れないようにする
    const float STOP_AFTER_SLOW = 0.3f;

    FeedbackTuning tuning;
    Vector3 lastPosition;
    float boltTimer;
    float slowTime;

    public void Begin(FeedbackTuning feedbackTuning)
    {
        tuning = feedbackTuning;
        lastPosition = transform.position;
        boltTimer = 0f;
        slowTime = 0f;
        enabled = true;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (tuning == null || dt <= 0f) return;   // ヒットストップ中は何もしない

        Vector3 position = transform.position;
        Vector3 velocity = (position - lastPosition) / dt;
        velocity.y = 0f;
        lastPosition = position;
        float speed = velocity.magnitude;

        if (speed < tuning.TrailMinSpeed)
        {
            slowTime += dt;
            if (slowTime > STOP_AFTER_SLOW) enabled = false;
            return;
        }
        slowTime = 0f;

        boltTimer -= dt;
        if (boltTimer > 0f) return;
        boltTimer = tuning.TrailBoltInterval;

        // 速いほど長く、太く、ギザギザに
        float k = Mathf.Clamp01(speed / tuning.TrailFullSpeed);
        Vector3 dir = velocity / speed;
        Vector3 start = position + Vector3.up * tuning.TrailHeight - dir * 0.2f;
        Vector3 end = start - dir * Mathf.Lerp(0.5f, 2.4f, k) + Vector3.down * 0.15f;
        BlackBolt.Spawn(start, end, Mathf.Lerp(0.035f, 0.07f, k), 0.07f, 0.22f + 0.2f * k);
    }
}
