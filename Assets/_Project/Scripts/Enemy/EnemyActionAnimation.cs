using UnityEngine;

/// <summary>
/// 敵の見た目だけを担当する。防御成立・攻撃ダメージ・移動・吹き飛びは敵本体側に任せる。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyActionAnimation : MonoBehaviour
{
    static readonly int IDLE = Animator.StringToHash("Idle");
    static readonly int DEFENSE = Animator.StringToHash("Defense");
    static readonly int ATTACK = Animator.StringToHash("Attack");
    static readonly int SPAWN = Animator.StringToHash("Spawn");
    static readonly int DEFENDING = Animator.StringToHash("Defending");

    [SerializeField] Animator animator;
    [SerializeField, Min(0f)] float transitionDuration = .08f;
    bool spawnPending;

    public bool IsDefending { get; private set; }
    public bool IsStoppedByGameOver { get; private set; }

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        GameEvents.GameOver += OnGameOver;
        IsStoppedByGameOver = false;
        spawnPending = true;
        if (animator != null) animator.speed = 1f;
    }

    void OnDisable()
    {
        GameEvents.GameOver -= OnGameOver;
    }

    void LateUpdate()
    {
        // 初回のAnimator初期化を待って出現を再生する。毎フレームの参照検索は行わない。
        if (!spawnPending) return;
        spawnPending = false;
        PlaySpawn();
    }

    public void SetDefending(bool defending)
    {
        if (!CanPlay()) return;
        IsDefending = defending;
        animator.SetBool(DEFENDING, defending);
        animator.CrossFadeInFixedTime(defending ? DEFENSE : IDLE, transitionDuration);
    }

    public void PlayAttack()
    {
        if (!CanPlay()) return;
        animator.CrossFadeInFixedTime(ATTACK, transitionDuration, 0, 0f);
    }

    public void PlaySpawn()
    {
        if (!CanPlay()) return;
        IsDefending = false;
        animator.SetBool(DEFENDING, false);
        animator.Play(SPAWN, 0, 0f);
    }

    public void ResetPresentation()
    {
        IsStoppedByGameOver = false;
        if (animator == null) return;
        animator.speed = 1f;
        PlaySpawn();
    }

    bool CanPlay()
    {
        return !IsStoppedByGameOver && animator != null && animator.isActiveAndEnabled;
    }

    void OnGameOver()
    {
        IsStoppedByGameOver = true;
        if (animator != null) animator.speed = 0f;
    }
}
