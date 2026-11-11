using UnityEngine;

/// <summary>主人公に近づくと既存の攻撃姿勢と短い踏み込みを自動再生する。被弾の計算は変更しない。</summary>
[RequireComponent(typeof(EnemyPursuit)), DefaultExecutionOrder(30)]
public sealed class EnemyAutoAttack : MonoBehaviour
{
    Enemy enemy;EnemyPursuit pursuit;
    Vector3 direction;
    float elapsed,cooldown;
    public bool IsAttacking { get; private set; }
    void Awake() { enemy=GetComponent<Enemy>();pursuit=GetComponent<EnemyPursuit>(); }
    void OnEnable() { GameEvents.GameOver+=Cancel;EnemyMotionEvents.Launched+=OnLaunched;EnemyMotionEvents.Reset+=OnReset; }
    void OnDisable() { GameEvents.GameOver-=Cancel;EnemyMotionEvents.Launched-=OnLaunched;EnemyMotionEvents.Reset-=OnReset;Cancel(); }
    void OnLaunched(ILaunchable source,Vector3 velocity) { if(ReferenceEquals(source,enemy))Cancel(); }
    void OnReset(ILaunchable source) { if(ReferenceEquals(source,enemy)){Cancel();cooldown=0;} }
    void Update()
    {
        if(enemy==null||enemy.Tuning==null||TimeController.IsPaused||TimeController.IsHitStopping)return;
        if(!enemy.CanPursue){Cancel();return;}
        float dt=Time.deltaTime;cooldown=Mathf.Max(0,cooldown-dt);
        if(IsAttacking)
        {
            if(pursuit.Target!=EnemyPursuit.PursuitTarget.Player){Cancel();return;}
            float previous=elapsed;elapsed+=dt;var tuning=enemy.Tuning;
            float step=Mathf.Max(0,Mathf.Min(elapsed,tuning.AttackWindup+tuning.AttackLungeDuration)-Mathf.Max(previous,tuning.AttackWindup));
            if(step>0)enemy.TryMoveOnGround(direction*(step*tuning.AttackLungeSpeed));
            if(elapsed>=tuning.AttackDuration){Cancel();cooldown=tuning.AttackCooldown;}
            return;
        }
        var player=GameContext.PlayerTransform;
        if(cooldown>0||player==null||!pursuit.CanInitiateAttack)return;
        direction=player.position-enemy.Position;direction.y=0;
        if(direction.magnitude>enemy.Tuning.AttackRange)return;
        direction=direction.sqrMagnitude>.0001f?direction.normalized:transform.forward;
        elapsed=0;IsAttacking=true;EnemyMotionEvents.RaiseAttackStarted(enemy);
    }
    void Cancel()
    {
        if(!IsAttacking)return;
        IsAttacking=false;EnemyMotionEvents.RaiseAttackEnded(enemy);
    }
}
