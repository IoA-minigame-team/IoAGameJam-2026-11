using UnityEngine;

/// <summary>
/// 主人公が近いと「予備動作→踏み込み→終了→待ち時間」を自動で進める。
/// 攻撃開始はEnemyMotionEventsで表示へ知らせ、移動はEnemyに依頼する。
/// 被弾回数やダメージはここでは計算せず、主人公担当のPlayerHealthに任せる。
/// </summary>
[RequireComponent(typeof(EnemyPursuit)), DefaultExecutionOrder(30)]
public sealed class EnemyAutoAttack : MonoBehaviour
{
    Enemy enemy;
    EnemyPursuit pursuit;
    Vector3 direction;
    float elapsed;
    float cooldown;
    public bool IsAttacking { get; private set; }
    void Awake()
    {
        enemy = GetComponent<Enemy>();
        pursuit = GetComponent<EnemyPursuit>();
    }

    void OnEnable()
    {
        GameEvents.GameOver += Cancel;
        EnemyMotionEvents.Launched += OnLaunched;
        EnemyMotionEvents.Reset += OnReset;
    }

    void OnDisable()
    {
        GameEvents.GameOver -= Cancel;
        EnemyMotionEvents.Launched -= OnLaunched;
        EnemyMotionEvents.Reset -= OnReset;
        Cancel();
    }

    void OnLaunched(ILaunchable source, Vector3 velocity)
    {
        // 他の敵が吹き飛んでも、自分の攻撃までは中断しない。
        if (ReferenceEquals(source, enemy)) Cancel();
    }

    void OnReset(ILaunchable source)
    {
        if (!ReferenceEquals(source, enemy)) return;
        Cancel();
        cooldown = 0;
    }
    void Update()
    {
        if (enemy == null || enemy.Tuning == null || TimeController.IsPaused || TimeController.IsHitStopping) return;
        // Idleだけで判断しない。Enemy側で「姿勢の復帰待ち」も含めて判定する。
        if (!enemy.CanPursue)
        {
            Cancel();
            return;
        }

        float dt = Time.deltaTime;
        cooldown = Mathf.Max(0, cooldown - dt);
        if (IsAttacking)
        {
            // 灯喰らいが刀を選んだら主人公への踏み込みを止める。
            if (pursuit.Target != EnemyPursuit.PursuitTarget.Player)
            {
                Cancel();
                return;
            }
            float previous = elapsed;
            elapsed += dt;
            var tuning = enemy.Tuning;
            // 今フレームと「踏み込む時間帯」の重なりだけ移動する。
            // フレームが予備動作の境目をまたいでも、移動量を余分に加えない。
            float step = Mathf.Max(0, Mathf.Min(elapsed, tuning.AttackWindup + tuning.AttackLungeDuration)
                - Mathf.Max(previous, tuning.AttackWindup));
            if (step > 0) enemy.TryMoveOnGround(direction * (step * tuning.AttackLungeSpeed));
            if (elapsed >= tuning.AttackDuration)
            {
                Cancel();
                cooldown = tuning.AttackCooldown;
            }
            return;
        }

        var player = GameContext.PlayerTransform;
        if (cooldown > 0 || player == null || !pursuit.CanInitiateAttack) return;
        direction = player.position - enemy.Position;
        direction.y = 0;
        if (direction.magnitude > enemy.Tuning.AttackRange) return;
        // 攻撃中に主人公が移動しても、開始時に見せた方向へ踏み込む。
        direction = direction.sqrMagnitude > .0001f ? direction.normalized : transform.forward;
        elapsed = 0;
        IsAttacking = true;
        EnemyMotionEvents.RaiseAttackStarted(enemy);
    }
    void Cancel()
    {
        // 中断と自然終了は同じ出口。AttackEndedを二重に通知しない。
        if (!IsAttacking) return;
        IsAttacking = false;
        EnemyMotionEvents.RaiseAttackEnded(enemy);
    }
}
