using UnityEngine;

/// <summary>三條担当の敵調整値。重さと光の初期値は仕様書、軌道・姿勢の値は調整用の仮値。</summary>
[CreateAssetMenu(fileName = "EnemyTuning", menuName = "Hitofuri/EnemyTuning")]
public sealed class EnemyTuning : ScriptableObject
{
    [Header("追跡。速度は調整用の初期値")]
    [SerializeField] EnemyBehaviourRole behaviourRole;
    [SerializeField, Min(0f)] float moveSpeed = 1.8f;
    [SerializeField, Min(0f)] float turnSpeed = 240f;
    [SerializeField, Min(0f)] float playerStopDistance = .75f;
    [SerializeField, Min(0f)] float swordStopDistance = 1.05f;
    [SerializeField, Min(0f)] float lureRadius = 10f;
    [SerializeField, Min(0f)] float swarmCountRadius = 1.8f;
    [SerializeField, Min(0f)] float spawnHoldDuration = 1.05f;
    [SerializeField, Min(0f)] float pursuitGroundMargin = .4f;
    [Header("自動攻撃。接触ダメージの判定はPlayerHealthが所有")]
    [SerializeField, Min(0f)] float attackRange = 2f;
    [SerializeField, Min(0f)] float attackWindup = .2f;
    [SerializeField, Min(.01f)] float attackLungeDuration = .25f;
    [SerializeField, Min(0f)] float attackLungeSpeed = 4f;
    [SerializeField, Min(.01f)] float attackDuration = 1f;
    [SerializeField, Min(0f)] float attackCooldown = 1.1f;
    [Header("種類と光")]
    [SerializeField] bool hasShield;
    [Tooltip("初速へ掛ける倍率。盾は0.6。他担当が初速を計算するのでEnemy内では掛けない")]
    [SerializeField, Min(.01f)] float weight = 1f;
    [SerializeField, Range(0f, 180f)] float shieldHalfAngle = 60f;
    [SerializeField, Min(0f)] float glowStep = .3f;
    [SerializeField, Min(.01f)] float maxGlow = 1.5f;
    [Header("自前の吹き飛び")]
    [Tooltip("地面での減速(m/s²)")]
    [SerializeField, Min(.01f)] float friction = 4f;
    [SerializeField, Min(.001f)] float stopSpeed = .05f;
    [SerializeField, Min(.001f)] float simulationStep = 1f / 60f;
    [Tooltip("1回の足場確認で進む距離の上限(m)。細い隙間の飛び越しを防ぐ")]
    [SerializeField, Min(.01f)] float probeStepDistance = .2f;
    [SerializeField, Min(.01f)] float groundProbeHeight = 1f;
    [SerializeField, Min(.01f)] float groundProbeDepth = 2f;
    [SerializeField, Min(0f)] float edgeProbeDistance = .6f;
    [SerializeField, Min(.01f)] float teeterDuration = .8f;
    [SerializeField, Min(.01f)] float fallGravity = 18f;
    [Tooltip("足場を失った高さから何m下がったらEnemyFellを通知するか")]
    [SerializeField, Min(.1f)] float fallDepth = 3f;
    [Header("見た目の反応。ColliderとRootには適用しない")]
    [SerializeField, Range(0f, 80f)] float launchLean = 36f;
    [SerializeField, Min(0f)] float launchHopHeight = .38f;
    [SerializeField, Min(.01f)] float launchHopDuration = .45f;
    [SerializeField, Min(.01f)] float reactionResponse = 14f;
    [SerializeField, Min(.01f)] float reactionReferenceSpeed = 8f;
    [SerializeField, Min(0f)] float fallSpinSpeed = 150f;
    [SerializeField, Range(0f, 40f)] float teeterAngle = 13f;
    [SerializeField, Min(.01f)] float teeterFrequency = 3f;

    public bool HasShield => hasShield;
    public EnemyBehaviourRole BehaviourRole => behaviourRole;
    public float MoveSpeed => Mathf.Max(0, moveSpeed);
    public float TurnSpeed => Mathf.Max(0, turnSpeed);
    public float PlayerStopDistance => Mathf.Max(0, playerStopDistance);
    public float SwordStopDistance => Mathf.Max(0, swordStopDistance);
    public float LureRadius => Mathf.Max(0, lureRadius);
    public float SwarmCountRadius => Mathf.Max(0, swarmCountRadius);
    public float SpawnHoldDuration => Mathf.Max(0, spawnHoldDuration);
    public float PursuitGroundMargin => Mathf.Max(0, pursuitGroundMargin);
    public float AttackRange => Mathf.Max(0, attackRange);
    public float AttackWindup => Mathf.Max(0, attackWindup);
    public float AttackLungeDuration => Mathf.Max(.01f, attackLungeDuration);
    public float AttackLungeSpeed => Mathf.Max(0, attackLungeSpeed);
    public float AttackDuration => Mathf.Max(attackWindup + attackLungeDuration, attackDuration);
    public float AttackCooldown => Mathf.Max(0, attackCooldown);
    public float Weight => Mathf.Max(.01f, weight);
    public float ShieldHalfAngle => shieldHalfAngle;
    public float GlowStep => Mathf.Max(0, glowStep);
    public float MaxGlow => Mathf.Max(.01f, maxGlow);
    public float Friction => Mathf.Max(.01f, friction);
    public float StopSpeed => Mathf.Max(.001f, stopSpeed);
    public float SimulationStep => Mathf.Max(.001f, simulationStep);
    public float ProbeStepDistance => Mathf.Max(.01f, probeStepDistance);
    public float GroundProbeHeight => Mathf.Max(.01f, groundProbeHeight);
    public float GroundProbeDepth => Mathf.Max(.01f, groundProbeDepth);
    public float EdgeProbeDistance => Mathf.Max(0, edgeProbeDistance);
    public float TeeterDuration => Mathf.Max(.01f, teeterDuration);
    public float FallGravity => Mathf.Max(.01f, fallGravity);
    public float FallDepth => Mathf.Max(.1f, fallDepth);
    public float LaunchLean => launchLean;
    public float LaunchHopHeight => Mathf.Max(0, launchHopHeight);
    public float LaunchHopDuration => Mathf.Max(.01f, launchHopDuration);
    public float ReactionResponse => Mathf.Max(.01f, reactionResponse);
    public float ReactionReferenceSpeed => Mathf.Max(.01f, reactionReferenceSpeed);
    public float FallSpinSpeed => Mathf.Max(0, fallSpinSpeed);
    public float TeeterAngle => teeterAngle;
    public float TeeterFrequency => Mathf.Max(.01f, teeterFrequency);
}
