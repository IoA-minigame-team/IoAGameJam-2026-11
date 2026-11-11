using UnityEngine;

/// <summary>
/// テスト用の仮の敵。三條さんの敵ができるまで、斬り戻しの当たり方と飛び方を確かめるために使う。本番では使わない。
/// 初速と摩擦だけで動き、止まったら元の位置に戻る。
/// </summary>
public sealed class PlayerTestEnemy : MonoBehaviour, ILaunchable
{
    // これより遅くなったら止まったとみなす(m/s)
    const float STOP_SPEED = 0.05f;

    [SerializeField, Min(0f)] float weight = 1f;
    [SerializeField, Min(0f)] float glowMultiplier = 1f;
    [Tooltip("吹き飛びの減速(m/s²)")]
    [SerializeField, Min(0f)] float friction = 8f;

    [Header("盾")]
    [SerializeField] bool hasShield;
    [Tooltip("自分の正面(青い軸)から何度までを盾の正面とするか")]
    [SerializeField, Range(0f, 180f)] float shieldHalfAngle = 60f;

    [Header("テスト")]
    [Tooltip("吹き飛びが止まってから元の位置に戻るまでの秒数。0 未満なら戻らない")]
    [SerializeField] float resetDelay = 1.5f;
    [Tooltip("吹き飛び中に切るコライダー。三條さんの敵でも吹き飛び中は主人公に当たらない前提なので、同じ形にしておく")]
    [SerializeField] Collider bodyCollider;
    // 大銘「返し」で、飛んでいる敵にもう一度当たるかを試すため。三條さんの敵が吹き飛び中に判定へ残るかは相談中
    [Tooltip("吹き飛び中もコライダーを残す。オンにすると返しで再ヒットするが、触れた主人公は被弾する")]
    [SerializeField] bool keepColliderWhileFlying;

    Vector3 velocity;
    Vector3 homePosition;
    Quaternion homeRotation;
    float resetTimer;
    bool isFlying;

    public Transform Transform => transform;
    public Vector3 Position => transform.position;
    public float Weight => weight;
    public float GlowMultiplier => glowMultiplier;

    void Awake()
    {
        homePosition = transform.position;
        homeRotation = transform.rotation;
        if (bodyCollider == null) bodyCollider = GetComponent<Collider>();
    }

    public bool IsShieldFacing(Vector3 slashDir)
    {
        if (!hasShield) return false;
        // 盾が斬り抜けてくる主人公の方を向いているか
        return Vector3.Angle(transform.forward, -slashDir) <= shieldHalfAngle;
    }

    public void Launch(Vector3 launchVelocity, int slashId)
    {
        velocity = launchVelocity;
        velocity.y = 0f;
        isFlying = true;
        if (bodyCollider != null && !keepColliderWhileFlying) bodyCollider.enabled = false;
        Debug.Log($"[PlayerTestEnemy] {name} Launch id={slashId} speed={velocity.magnitude:F2}", this);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (isFlying)
        {
            transform.position += velocity * dt;
            float speed = Mathf.Max(0f, velocity.magnitude - friction * dt);
            velocity = velocity.normalized * speed;

            if (speed <= STOP_SPEED)
            {
                velocity = Vector3.zero;
                isFlying = false;
                if (bodyCollider != null) bodyCollider.enabled = true;
                resetTimer = resetDelay;
            }
            return;
        }

        if (resetDelay >= 0f && resetTimer > 0f)
        {
            resetTimer -= dt;
            if (resetTimer <= 0f) ResetToHome();
        }
    }

    [ContextMenu("元の位置に戻す")]
    void ResetToHome()
    {
        velocity = Vector3.zero;
        isFlying = false;
        transform.SetPositionAndRotation(homePosition, homeRotation);
        if (bodyCollider != null) bodyCollider.enabled = true;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!hasShield) return;
        // 盾の向きをシーンビューで見えるようにする
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
    }
#endif
}
