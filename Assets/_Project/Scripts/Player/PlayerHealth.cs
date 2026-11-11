using UnityEngine;

/// <summary>
/// 主人公の被弾。敵に触れたら被弾し、残りの被弾回数を PlayerDamaged で流す。
/// ゲームオーバーの判定と発行は三條さんの担当なので、ここでは残り回数を知らせるだけにする。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerHealth : MonoBehaviour
{
    // レイヤーが決まるまでは敵と主人公が同じレイヤーのこともあり、自分のコライダーも拾うので少し多めに取る
    const int OVERLAP_BUFFER_SIZE = 8;

    [SerializeField] PlayerTuning tuning;
    [SerializeField] PlayerController controller;
    [Tooltip("触れると被弾する敵のレイヤー。吹き飛び中の敵は三條さん側で外してもらう前提")]
    [SerializeField] LayerMask enemyMask;
    [Tooltip("体より少し外側まで判定を広げる量(m)。CharacterController は敵の手前で止まるので、半径ちょうどでは触れたことにならない")]
    [SerializeField, Min(0f)] float contactMargin = 0.1f;

    readonly Collider[] overlapBuffer = new Collider[OVERLAP_BUFFER_SIZE];
    CharacterController characterController;
    float invincibleRemaining;
    // 被弾で残りが 0 になった(本当の終わり)。銘で上限が下がっただけのときと見分けるため
    bool isDefeated;

    /// <summary>被弾した回数</summary>
    public int DamageTaken { get; private set; }

    /// <summary>残りの被弾回数。銘の増減(MaxDamageOffset)込み</summary>
    public int Remaining => Mathf.Max(0, MaxDamage - DamageTaken);

    /// <summary>被弾後の無敵の最中か。点滅などの見た目で使う</summary>
    public bool IsDamageInvincible => invincibleRemaining > 0f;

    // 銘で上限が下がっても 1 未満にはしない
    int MaxDamage => Mathf.Max(1, tuning.MaxDamageCount + GameContext.MeiStats.MaxDamageOffset);

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (controller == null) controller = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (invincibleRemaining > 0f) invincibleRemaining -= Time.deltaTime;

        KeepOneAfterMeiChange();

        if (!CanTakeDamage()) return;
        if (!IsTouchingEnemy()) return;

        DamageTaken++;
        if (Remaining <= 0) isDefeated = true;
        invincibleRemaining = tuning.DamageInvincibleTime;

        GameEvents.RaisePlayerDamaged(Remaining);
        Feedback.PlaySe(SeId.PlayerDamaged, transform.position);
    }

    // 捨て身などで上限が下がり、被弾していないのに残りが 0 になったら、最低1回は残す。
    // 銘を選んだだけで負けにしないため(仕様にない所。朝会で共有)。被弾の演出は出さないので PlayerDamaged は流さない
    void KeepOneAfterMeiChange()
    {
        if (isDefeated) return;
        if (DamageTaken >= MaxDamage) DamageTaken = MaxDamage - 1;
    }

    bool CanTakeDamage()
    {
        if (IsDamageInvincible) return false;
        if (Remaining <= 0) return false;
        if (controller != null && (controller.IsSlashInvincible || controller.CurrentState == PlayerController.State.Disabled)) return false;
        // 止まっている間は敵も動かないので、その間に被弾させない
        if (TimeController.IsPaused || TimeController.IsHitStopping) return false;
        return true;
    }

    bool IsTouchingEnemy()
    {
        float radius = characterController.radius + characterController.skinWidth + contactMargin;
        float halfLine = Mathf.Max(0f, characterController.height * 0.5f - characterController.radius);
        Vector3 center = transform.TransformPoint(characterController.center);
        Vector3 top = center + Vector3.up * halfLine;
        Vector3 bottom = center - Vector3.up * halfLine;

        int count = Physics.OverlapCapsuleNonAlloc(top, bottom, radius, overlapBuffer, enemyMask, QueryTriggerInteraction.Collide);
        bool touching = false;
        for (int i = 0; i < count; i++)
        {
            // 自分と、手に持っている刀のコライダーは数えない
            if (!overlapBuffer[i].transform.IsChildOf(transform)) touching = true;
            overlapBuffer[i] = null;
        }
        return touching;
    }
}
