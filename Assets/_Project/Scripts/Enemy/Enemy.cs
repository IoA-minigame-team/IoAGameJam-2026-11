using UnityEngine;

/// <summary>
/// ILaunchableの受け口と吹き飛び軌道を所有する。追跡AIや攻撃判定はここへ混ぜない。
/// 見た目へはイベントだけを送り、具体的なアニメーションクラスを参照しない。
/// </summary>
[DisallowMultipleComponent, RequireComponent(typeof(CapsuleCollider))]
public sealed class Enemy : MonoBehaviour, ILaunchable
{
    // Idleは「軌道が止まった」状態。見た目が復帰中なら、まだCanPursueはfalseになる。
    public enum MotionState { Idle, Flying, Teetering, Falling, Fallen }
    [SerializeField] EnemyTuning tuning;
    [Tooltip("追跡・吹き飛びの障害物判定にも使う当たり判定。半径の値をコード側へ複製しない")]
    [SerializeField] CapsuleCollider bodyCollider;
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
    bool presentationBusy;
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
    public CapsuleCollider BodyCollider => bodyCollider;
    /// <summary>追跡と攻撃に共通の再開条件。移動の停止だけでなく、見た目の復帰完了も待つ。</summary>
    public bool CanPursue => isActiveAndEnabled && tuning != null && State == MotionState.Idle && !stoppedByGameOver && !presentationBusy
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
    /// <summary>実Colliderと同じ形を移動先へ掃引し、接触まで進める距離を返す。</summary>
    bool ObstacleDistance(Vector3 direction, float distance, out float available)
    {
        available = distance;
        if (bodyCollider == null) return false;
        GetWorldCapsule(out var bottom, out var top, out float radius);
        // CapsuleCastは開始時点ですでに重なる柵を拾わない。先に同じ形の重なりを調べ、移動量を0にする。
        if (Physics.CheckCapsule(bottom, top, radius, movementObstacleMask, QueryTriggerInteraction.Ignore))
        {
            available = 0;
            return true;
        }
        if (!Physics.CapsuleCast(bottom, top, radius, direction, out var hit, distance, movementObstacleMask, QueryTriggerInteraction.Ignore)) return false;
        // CapsuleCastは形全体を掃引済みなので、半径をもう一度引かない。
        available = Mathf.Max(0, hit.distance);
        return true;
    }

    void GetWorldCapsule(out Vector3 bottom, out Vector3 top, out float radius)
    {
        EnemyCapsuleGeometry.GetWorld(bodyCollider, out bottom, out top, out radius);
    }

    void Awake()
    {
        if (bodyCollider == null) bodyCollider = GetComponent<CapsuleCollider>();
        colliderWasEnabled = bodyCollider != null && bodyCollider.enabled;
        if (tuning == null) Debug.LogError("EnemyTuningを設定してください。吹き飛び処理を開始できません。", this);
    }

    void OnEnable()
    {
        GameEvents.GameOver += OnGameOver;
        EnemyMotionEvents.PresentationBusyChanged += OnPresentationBusyChanged;
    }
    void OnDisable()
    {
        GameEvents.GameOver -= OnGameOver;
        EnemyMotionEvents.PresentationBusyChanged -= OnPresentationBusyChanged;
        presentationBusy = false;
    }
    void OnPresentationBusyChanged(ILaunchable source, bool busy)
    {
        // Animatorや表示クラスを直接参照せず、自分の見た目から届いた完了通知だけを使う。
        if (ReferenceEquals(source, this)) presentationBusy = busy;
    }

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
        if (!TryGround(transform.position, out var currentGround)) { BeginFall(); return; }
        // 島は平面とは限らない。摩擦の計算は水平のまま、足元の高さだけを地面へ合わせる。
        transform.position = new Vector3(transform.position.x, currentGround.point.y, transform.position.z);
        float speed = velocity.magnitude;
        if (speed <= tuning.StopSpeed) { StopOnGround(); return; }
        var direction = velocity / speed;
        // 一定減速の式で積分する。フレームレートで停止距離が変わりにくい。
        float moveTime = Mathf.Min(dt, speed / tuning.Friction);
        float distance = speed * moveTime - .5f * tuning.Friction * moveTime * moveTime;
        Vector3 destination = transform.position + direction * distance;
        bool hasDestinationGround = TryGround(destination, out var destinationGround);
        if (hasDestinationGround) destination.y = destinationGround.point.y;
        Vector3 displacement = destination - transform.position;
        float travel = displacement.magnitude;
        Vector3 travelDirection = travel > .000001f ? displacement / travel : direction;
        // 高さが変わる場所でも、実際に動く3D方向へColliderを掃引して柵との接触を調べる。
        if(ObstacleDistance(travelDirection,travel,out float available))
        {
            transform.position+=travelDirection*available;velocity=Vector3.zero;StopOnGround();return;
        }
        transform.position = destination;
        velocity = direction * Mathf.Max(0f, speed - tuning.Friction * dt);
        if (!hasDestinationGround) BeginFall();
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
        // 軌道が止まった後は、縁なら踏みとどまり、それ以外なら見た目の復帰へ進む。
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
        // ここは移動の終了。Recoveredを受けた見た目が戻り切るまでCanPursueは開かない。
        State = MotionState.Idle;
        velocity = Vector3.zero;
        if (bodyCollider != null) bodyCollider.enabled = colliderWasEnabled;
        EnemyMotionEvents.RaiseRecovered(this);
    }

    void BeginFall()
    {
        // 地上の摩擦とは切り離し、足場を失った高さを基準に落下を計算する。
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
        presentationBusy = false;
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
