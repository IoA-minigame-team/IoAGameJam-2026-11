using UnityEngine;

/// <summary>
/// ILaunchableの受け口と吹き飛び軌道を所有する。追跡AIや攻撃判定はここへ混ぜない。
/// 見た目へはイベントだけを送り、具体的なアニメーションクラスを参照しない。
/// </summary>
[DisallowMultipleComponent]
public sealed class Enemy : MonoBehaviour, ILaunchable
{
    public enum MotionState { Idle, Flying, Teetering, Falling, Fallen }
    [SerializeField] EnemyTuning tuning;
    [SerializeField] Collider bodyCollider;
    [Tooltip("地面の非Trigger Colliderがあるレイヤー。初期値はDefault")]
    [SerializeField] LayerMask groundMask = 1;
    [Tooltip("柵などの障害物。初期値はIgnore Raycastレイヤー。地面の判定とは分ける")]
    [SerializeField] LayerMask movementObstacleMask = 4;
    Vector3 velocity;
    Vector3 lastDirection = Vector3.forward;
    float glow;
    float teeterRemaining;
    float fallStartHeight;
    bool stoppedByGameOver;
    bool colliderWasEnabled;
    int slashId = -1;

    public Transform Transform => transform;
    public Vector3 Position => transform.position;
    public float Weight => tuning != null ? tuning.Weight : 1f;
    public float GlowMultiplier => 1f + glow;
    public float GlowRatio => tuning != null ? glow / tuning.MaxGlow : 0f;
    public Vector3 Velocity => velocity;
    public int LastSlashId => slashId;
    public MotionState State { get; private set; }
    public bool IsStoppedByGameOver => stoppedByGameOver;
    public EnemyTuning Tuning => tuning;
    public bool CanPursue => isActiveAndEnabled && tuning != null && State == MotionState.Idle && !stoppedByGameOver
        && !TimeController.IsPaused && !TimeController.IsHitStopping;

    /// <summary>追跡側から受け取る地上移動。Rootを動かす責任はEnemyへ集約する。</summary>
    public bool TryMoveOnGround(Vector3 displacement)
    {
        if (!CanPursue || !IsFinite(displacement.x) || !IsFinite(displacement.z)) return false;
        displacement.y = 0;
        if (displacement.sqrMagnitude < .000001f) return true;
        var direction = displacement.normalized;
        if (ObstacleDistance(direction,displacement.magnitude,out _)) return false;
        int steps = Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / tuning.ProbeStepDistance));
        Vector3 destination = transform.position;
        for (int i = 1; i <= steps; i++)
        {
            var candidate = transform.position + displacement * (i / (float)steps);
            if (!TryGround(candidate, out var hit) || !HasGround(candidate + direction * tuning.PursuitGroundMargin)) return false;
            destination = new Vector3(candidate.x, hit.point.y, candidate.z);
        }
        transform.position = destination;return true;
    }
    public void FaceOnGround(Vector3 direction, float degrees)
    {
        if (!CanPursue) return;
        direction.y = 0;
        if (direction.sqrMagnitude > .000001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), degrees);
    }
    bool ObstacleDistance(Vector3 direction,float distance,out float available)
    {
        const float BODY_RADIUS = .48f;
        available=distance;
        if(!Physics.Raycast(transform.position+Vector3.up,direction,out var hit,distance+BODY_RADIUS,movementObstacleMask,QueryTriggerInteraction.Ignore))return false;
        available=Mathf.Max(0,hit.distance-BODY_RADIUS);return true;
    }

    void Awake()
    {
        if (bodyCollider == null) bodyCollider = GetComponent<Collider>();
        colliderWasEnabled = bodyCollider != null && bodyCollider.enabled;
        if (tuning == null) Debug.LogError("EnemyTuningを設定してください。吹き飛び処理を開始できません。", this);
    }

    void OnEnable() => GameEvents.GameOver += OnGameOver;
    void OnDisable() => GameEvents.GameOver -= OnGameOver;

    public bool IsShieldFacing(Vector3 slashDir)
    {
        if (tuning == null || !tuning.HasShield || State == MotionState.Flying || State == MotionState.Falling || State == MotionState.Fallen) return false;
        slashDir.y = 0f;
        var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (slashDir.sqrMagnitude < .000001f || forward.sqrMagnitude < .000001f) return false;
        // slashDirは刀が進む向き。敵から見た攻撃元はその逆向き。
        return Vector3.Angle(forward, -slashDir) <= tuning.ShieldHalfAngle;
    }

    /// <summary>
    /// 吹き飛び開始の共通窓口。呼出し側は1命中につき1回だけ呼ぶ。
    /// GameEvents.EnemyHitは呼出し側が後から流す結果通知なので、受信してLaunchし直さない。
    /// 重さ・光による初速補正は呼出し側、移動と落下までのslashId保持はこのクラスが所有する。
    /// </summary>
    public void Launch(Vector3 launchVelocity, int incomingSlashId)
    {
        if (tuning == null || stoppedByGameOver || State == MotionState.Falling || State == MotionState.Fallen) return;
        if (!IsFinite(launchVelocity.x) || !IsFinite(launchVelocity.y) || !IsFinite(launchVelocity.z)) return;
        // WeightとGlowMultiplierはSlashResolverがすでに掛けている。ここでは初速を再補正しない。
        velocity = launchVelocity;
        velocity.y = 0f;
        if (velocity.sqrMagnitude > .000001f) lastDirection = velocity.normalized;
        slashId = incomingSlashId;
        glow = Mathf.Clamp(glow + tuning.GlowStep * GameContext.MeiStats.GlowGainMultiplier, 0, tuning.MaxGlow);
        State = MotionState.Flying;
        if (bodyCollider != null) bodyCollider.enabled = false;
        EnemyMotionEvents.RaiseLaunched(this, velocity);
        // 状態を先に整え、通知先から同じ敵を参照されても途中の状態を見せない。
        GameEvents.RaiseGlowChanged(this, GlowRatio);
    }

    void Update()
    {
        if (tuning == null || stoppedByGameOver || TimeController.IsPaused || TimeController.IsHitStopping) return;
        float remaining = Time.deltaTime;
        if (State == MotionState.Teetering)
        {
            teeterRemaining -= remaining;
            if (teeterRemaining <= 0f) Recover();
            return;
        }
        if (State != MotionState.Flying && State != MotionState.Falling) return;
        // 高速時も細かく足場を調べ、フレーム落ちで縁の判定を飛ばしにくくする。
        while (remaining > .000001f && (State == MotionState.Flying || State == MotionState.Falling))
        {
            float step = Mathf.Min(remaining, tuning.SimulationStep, tuning.ProbeStepDistance / Mathf.Max(velocity.magnitude, tuning.StopSpeed));
            Simulate(step);
            remaining -= step;
        }
        EnemyMotionEvents.RaiseMotionUpdated(this, velocity);
    }

    void Simulate(float dt)
    {
        if (State == MotionState.Falling)
        {
            transform.position += velocity * dt + Vector3.down * (.5f * tuning.FallGravity * dt * dt);
            velocity.y -= tuning.FallGravity * dt;
            if (transform.position.y <= fallStartHeight - tuning.FallDepth) FallOff();
            return;
        }
        if (!HasGround(transform.position)) { BeginFall(); return; }
        float speed = velocity.magnitude;
        if (speed <= tuning.StopSpeed) { StopOnGround(); return; }
        var direction = velocity / speed;
        // 一定減速の式で積分する。フレームレートで停止距離が変わりにくい。
        float moveTime = Mathf.Min(dt, speed / tuning.Friction);
        float distance = speed * moveTime - .5f * tuning.Friction * moveTime * moveTime;
        if(ObstacleDistance(direction,distance,out float available))
        {
            transform.position+=direction*available;velocity=Vector3.zero;StopOnGround();return;
        }
        transform.position += direction * distance;
        velocity = direction * Mathf.Max(0f, speed - tuning.Friction * dt);
        if (!HasGround(transform.position)) BeginFall();
        else if (velocity.magnitude <= tuning.StopSpeed) StopOnGround();
    }

    bool HasGround(Vector3 position)
    {
        return TryGround(position, out _);
    }
    bool TryGround(Vector3 position, out RaycastHit hit) => Physics.Raycast(position + Vector3.up * tuning.GroundProbeHeight,
        Vector3.down, out hit, tuning.GroundProbeHeight + tuning.GroundProbeDepth, groundMask, QueryTriggerInteraction.Ignore)
        && Vector3.Dot(hit.normal, Vector3.up) > .5f;

    void StopOnGround()
    {
        velocity = Vector3.zero;
        if (bodyCollider != null) bodyCollider.enabled = colliderWasEnabled;
        if (!HasGround(transform.position + lastDirection * tuning.EdgeProbeDistance))
        {
            State = MotionState.Teetering;
            teeterRemaining = tuning.TeeterDuration;
            GameEvents.RaiseEnemyTeetered(this);
        }
        else Recover();
        // slashIdは停止しても消さず、落下か次のLaunchまで保持する。
    }

    void Recover()
    {
        State = MotionState.Idle;
        velocity = Vector3.zero;
        if (bodyCollider != null) bodyCollider.enabled = colliderWasEnabled;
        EnemyMotionEvents.RaiseRecovered(this);
    }

    void BeginFall()
    {
        State = MotionState.Falling;
        fallStartHeight = transform.position.y;
        if (bodyCollider != null) bodyCollider.enabled = false;
        EnemyMotionEvents.RaiseFalling(this);
    }

    void FallOff()
    {
        // 通知前に終端状態へ進め、購読先から再度呼ばれても落下を重複させない。
        State = MotionState.Fallen;
        GameEvents.RaiseEnemyFell(new EnemyFellInfo(slashId, transform.position));
        Destroy(gameObject);
    }

    void OnGameOver()
    {
        stoppedByGameOver = true;
        if (bodyCollider != null) bodyCollider.enabled = false;
    }

    /// <summary>同じ個体をプール等で再利用するとき、ラウンド開始側から呼ぶ。</summary>
    public void ResetState()
    {
        stoppedByGameOver = false;
        State = MotionState.Idle;
        velocity = Vector3.zero;
        glow = 0f;
        slashId = -1;
        if (bodyCollider != null) bodyCollider.enabled = colliderWasEnabled;
        EnemyMotionEvents.RaiseReset(this);
        GameEvents.RaiseGlowChanged(this, 0f);
    }

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
