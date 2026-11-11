using UnityEngine;

// 人狼の見た目を使うPrefabも、ゲーム上の役割はLampEater（灯喰らい）として扱う。
public enum EnemyBehaviourRole { Shadow, LampEater, ShieldBearer }

/// <summary>追跡先の判断だけを所有する。共有状態は読むだけ。Root移動はEnemy、姿勢はイベントで依頼する。</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Enemy)), DefaultExecutionOrder(20)]
public sealed class EnemyPursuit : MonoBehaviour
{
    public enum PursuitTarget { None, Player, Sword }
    Enemy enemy;
    bool swordPlanted, guardRequested;
    Vector3 swordPosition;
    float spawnHold;
    bool attackBusy;
    /// <summary>出現待ちが終わり、復帰済みで、主人公を選んでいるときだけ攻撃開始を許可する。</summary>
    public bool CanInitiateAttack => spawnHold <= 0 && !attackBusy && enemy != null && enemy.CanPursue && Target == PursuitTarget.Player;
    public PursuitTarget Target { get; private set; }
    public Vector3 TargetPosition { get; private set; }
    public bool IsSwarming { get; private set; }
    public bool IsBlocked { get; private set; }
    public EnemyBehaviourRole Role => enemy != null && enemy.Tuning != null ? enemy.Tuning.BehaviourRole : EnemyBehaviourRole.Shadow;

    void Awake() => enemy = GetComponent<Enemy>();
    void OnEnable()
    {
        GameEvents.SwordPlanted += OnSwordPlanted;
        GameEvents.SlashStarted += OnSlashStarted;
        GameEvents.GameOver += OnGameOver;
        EnemyMotionEvents.Reset += OnReset;
        EnemyMotionEvents.Launched += OnLaunched;
        EnemyMotionEvents.AttackStarted += OnAttackStarted;
        EnemyMotionEvents.AttackEnded += OnAttackEnded;
        attackBusy = false;RefreshContext();spawnHold = enemy != null && enemy.Tuning != null ? enemy.Tuning.SpawnHoldDuration : 0;
    }
    void OnDisable()
    {
        GameEvents.SwordPlanted -= OnSwordPlanted;
        GameEvents.SlashStarted -= OnSlashStarted;
        GameEvents.GameOver -= OnGameOver;
        EnemyMotionEvents.Reset -= OnReset;
        EnemyMotionEvents.Launched -= OnLaunched;
        EnemyMotionEvents.AttackStarted -= OnAttackStarted;
        EnemyMotionEvents.AttackEnded -= OnAttackEnded;
        attackBusy = false;SetSwarming(false);SetGuard(false);Target = PursuitTarget.None;
    }
    void RefreshContext()
    {
        // 後から出現した敵も、過去のSwordPlantedを聞いていなくても現在の状態から開始する。
        swordPlanted = GameContext.IsSwordPlanted;swordPosition = GameContext.SwordPosition;SelectTarget();
    }
    void OnSwordPlanted(Vector3 position) { swordPlanted = true;swordPosition = position;SetSwarming(false);SelectTarget(); }
    void OnSlashStarted(SlashStartedInfo info)
    {
        // 実際のPlayerはこの通知より後でIsSwordPlantedをfalseにする。古い共有値を読み直して刀へ戻さない。
        swordPlanted = false;SetSwarming(false);SelectTarget();
    }
    void OnGameOver() { attackBusy = false;SetSwarming(false);SetGuard(false);Target = PursuitTarget.None; }
    void OnLaunched(ILaunchable source, Vector3 velocity)
    {
        if (ReferenceEquals(source, enemy)) { SetSwarming(false);SetGuard(false); }
    }
    void OnAttackStarted(ILaunchable source) { if (ReferenceEquals(source, enemy)) attackBusy = true; }
    void OnAttackEnded(ILaunchable source) { if (ReferenceEquals(source, enemy)) attackBusy = false; }
    void OnReset(ILaunchable source)
    {
        if (!ReferenceEquals(source, enemy)) return;
        spawnHold = enemy.Tuning.SpawnHoldDuration;SetSwarming(false);SetGuard(false);RefreshContext();
        attackBusy = false;
    }

    void SelectTarget()
    {
        // 毎回最新の銘倍率を読む。刀のイベント後に倍率が変わっても、次の更新で反映する。
        Target = PursuitTarget.None;
        if (enemy == null || enemy.Tuning == null || enemy.IsStoppedByGameOver) return;
        float radius = enemy.Tuning.LureRadius * Mathf.Max(0, GameContext.MeiStats.LureRadiusMultiplier);
        if (Role == EnemyBehaviourRole.LampEater && swordPlanted && FlatDistance(enemy.Position, swordPosition) <= radius)
        {
            Target = PursuitTarget.Sword;TargetPosition = swordPosition;return;
        }
        var player = GameContext.PlayerTransform;
        if (player != null) { Target = PursuitTarget.Player;TargetPosition = player.position; }
    }
    void Update()
    {
        if (enemy == null || enemy.Tuning == null) return;
        if (TimeController.IsPaused || TimeController.IsHitStopping) return;
        SelectTarget();IsBlocked = false;
        spawnHold = Mathf.Max(0, spawnHold - Time.deltaTime);
        // CanPursueには見た目の復帰待ちも含まれる。姿勢が傾いたまま追跡へ戻さない。
        if (!enemy.CanPursue || spawnHold > 0 || Target == PursuitTarget.None)
        {
            SetSwarming(false);SetGuard(false);return;
        }
        var tuning = enemy.Tuning;
        Vector3 direction = TargetPosition - enemy.Position;direction.y = 0;
        float distance = direction.magnitude;
        enemy.FaceOnGround(direction, tuning.TurnSpeed * Time.deltaTime);
        SetGuard(Role == EnemyBehaviourRole.ShieldBearer);
        // 攻撃中のRoot移動はEnemyAutoAttackが担当する。追跡移動を重ねない。
        if (attackBusy) { SetSwarming(false);return; }
        float stop = Target == PursuitTarget.Sword ? tuning.SwordStopDistance : tuning.PlayerStopDistance;
        float step = Mathf.Min(tuning.MoveSpeed * Time.deltaTime, Mathf.Max(0, distance - stop));
        if (step > .000001f)
        {
            Vector3 desired = direction.normalized;
            bool moved = enemy.TryMoveOnGround(desired * step);
            // 縁で足場が切れる場合の簡単な迂回。経路探索ではなく、床のある方向を少しずつ探す。
            if (!moved) moved = enemy.TryMoveOnGround(Quaternion.Euler(0, 35, 0) * desired * step);
            if (!moved) moved = enemy.TryMoveOnGround(Quaternion.Euler(0, -35, 0) * desired * step);
            if (!moved) moved = enemy.TryMoveOnGround(Quaternion.Euler(0, 70, 0) * desired * step);
            if (!moved) moved = enemy.TryMoveOnGround(Quaternion.Euler(0, -70, 0) * desired * step);
            IsBlocked = !moved;
        }
        SetSwarming(Target == PursuitTarget.Sword && FlatDistance(enemy.Position, swordPosition) <= tuning.SwarmCountRadius);
    }
    void SetGuard(bool value)
    {
        if (guardRequested == value) return;
        guardRequested = value;EnemyMotionEvents.RaiseGuardChanged(enemy, value);
    }
    void SetSwarming(bool value)
    {
        if (IsSwarming == value) return;
        IsSwarming = value;EnemyMotionEvents.RaiseSwarmMembershipChanged(enemy, value);
    }
    static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0;return Vector3.Distance(a, b); }
}

