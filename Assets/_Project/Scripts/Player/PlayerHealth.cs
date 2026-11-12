using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 主人公の被弾。敵に触れたら被弾し、残りの被弾回数を PlayerDamaged で流す。
/// 場外へ落ちたときも被弾1回として数え、開始位置へ戻す。
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
    // 演出の無敵。被弾後の無敵とは分け、点滅(IsDamageInvincible)を出さない
    readonly HashSet<object> invincibleOwners = new HashSet<object>();
    float grantedInvincibleRemaining;
    // 被弾で残りが 0 になった(本当の終わり)。銘で上限が下がっただけのときと見分けるため
    bool isDefeated;
    // 場外へ落ちたときに戻す位置。シーンの開始時に立っている所
    Vector3 spawnPosition;

    /// <summary>被弾した回数</summary>
    public int DamageTaken { get; private set; }

    /// <summary>残りの被弾回数。銘の増減(MaxDamageOffset)込み</summary>
    public int Remaining => Mathf.Max(0, MaxDamage - DamageTaken);

    /// <summary>被弾後の無敵の最中か。点滅などの見た目で使う</summary>
    public bool IsDamageInvincible => invincibleRemaining > 0f;

    /// <summary>演出などで無敵にされているか(被弾後の無敵は含まない)</summary>
    public bool IsGrantedInvincible => invincibleOwners.Count > 0 || grantedInvincibleRemaining > 0f;

    // 銘で上限が下がっても 1 未満にはしない
    int MaxDamage => Mathf.Max(1, tuning.MaxDamageCount + GameContext.MeiStats.MaxDamageOffset);

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (controller == null) controller = GetComponent<PlayerController>();
    }

    // Awake ではなく Start で覚える。他の部品が Awake で主人公を置き直すことがあるため(三條さんの InkReviewSession など)
    void Start()
    {
        spawnPosition = transform.position;
    }

    void Update()
    {
        if (invincibleRemaining > 0f) invincibleRemaining -= Time.deltaTime;
        if (grantedInvincibleRemaining > 0f) grantedInvincibleRemaining -= Time.deltaTime;

        KeepOneAfterMeiChange();

        if (CheckFallOut()) return;

        if (!CanTakeDamage()) return;
        if (!IsTouchingEnemy()) return;

        ApplyDamage();
    }

    // 場外へ落ちたら開始位置へ戻し、被弾1回として数える。
    // 無敵の最中でも数える。数えないと、被弾直後や演出の無敵の間に落ちた分だけ、減らずに戻れてしまうため
    bool CheckFallOut()
    {
        if (isDefeated || Remaining <= 0) return false;
        if (TimeController.IsPaused) return false;
        if (transform.position.y >= spawnPosition.y - tuning.FallOutDepth) return false;

        // 先に戻してから流す。最後の1回でゲームオーバーになっても、主人公が島の上で止まるようにするため
        if (controller != null) controller.Respawn(spawnPosition);
        ApplyDamage();
        return true;
    }

    void ApplyDamage()
    {
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

    /// <summary>解除するまで無敵にする。owner には呼んだ本人を渡し、ReleaseInvincible で解除する</summary>
    public void RequestInvincible(object owner)
    {
        if (owner != null) invincibleOwners.Add(owner);
    }

    /// <summary>RequestInvincible で無敵にした本人が解除する</summary>
    public void ReleaseInvincible(object owner)
    {
        if (owner != null) invincibleOwners.Remove(owner);
    }

    /// <summary>今から seconds 秒(ゲーム内の時間)だけ無敵にする。重なったら長い方を残す</summary>
    public void GrantInvincible(float seconds)
    {
        grantedInvincibleRemaining = Mathf.Max(grantedInvincibleRemaining, seconds);
    }

    bool CanTakeDamage()
    {
        if (IsDamageInvincible || IsGrantedInvincible) return false;
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
