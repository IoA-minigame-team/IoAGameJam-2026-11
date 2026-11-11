using UnityEngine;

/// <summary>敵の見た目だけを担当する。移動、盾の成立、攻撃ダメージはEnemy側が所有する。</summary>
[DisallowMultipleComponent]
public sealed class EnemyActionAnimation : MonoBehaviour
{
    static readonly int IDLE = Animator.StringToHash("Idle");
    static readonly int DEFENSE = Animator.StringToHash("Defense");
    static readonly int ATTACK = Animator.StringToHash("Attack");
    static readonly int SPAWN = Animator.StringToHash("Spawn");
    static readonly int DEFENDING = Animator.StringToHash("Defending");
    static readonly int GLOW_RATIO = Shader.PropertyToID("_GlowRatio");
    enum ReactionMode { None, Launched, Teeter, Falling, Recovering }

    [SerializeField] Animator animator;
    [SerializeField, Min(0f)] float transitionDuration = .08f;
    [SerializeField] Transform reactionPivot;
    [SerializeField] EnemyTuning reactionTuning;
    Transform leftArm, rightArm, head;
    Quaternion leftArmRest, rightArmRest, headRest, pivotRotation;
    Quaternion frozenLeftArm, frozenRightArm, frozenHead;
    Vector3 pivotPosition, launchVelocity, launchDirection = Vector3.forward;
    Renderer[] renderers;
    MaterialPropertyBlock properties;
    ReactionMode reaction;
    float reactionTime;
    bool spawnPending;
    bool requestedGuard;
    ILaunchable launchable;

    public bool IsDefending { get; private set; }
    public bool IsStoppedByGameOver { get; private set; }
    public bool IsReacting => reaction != ReactionMode.None;

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (reactionPivot == null && animator != null) reactionPivot = animator.transform;
        if (reactionPivot != null) { pivotRotation = reactionPivot.localRotation; pivotPosition = reactionPivot.localPosition; }
        if (animator != null)
        {
            // 関節の検索は初期化時だけ。LateUpdateでは取得済みの参照を使う。
            foreach (var bone in animator.GetComponentsInChildren<Transform>())
            {
                if (bone.name == "L_Arm") { leftArm = bone; leftArmRest = bone.localRotation; }
                if (bone.name == "R_Arm") { rightArm = bone; rightArmRest = bone.localRotation; }
                if (bone.name == "Head") { head = bone; headRest = bone.localRotation; }
            }
        }
        renderers = GetComponentsInChildren<Renderer>();
        properties = new MaterialPropertyBlock();
        launchable = GetComponent<ILaunchable>();
    }

    void OnEnable()
    {
        GameEvents.GameOver += OnGameOver;
        GameEvents.GlowChanged += OnGlowChanged;
        GameEvents.EnemyTeetered += OnTeetered;
        EnemyMotionEvents.Launched += OnLaunched;
        EnemyMotionEvents.MotionUpdated += OnMotionUpdated;
        EnemyMotionEvents.Falling += OnFalling;
        EnemyMotionEvents.Recovered += OnRecovered;
        EnemyMotionEvents.Reset += OnReset;
        EnemyMotionEvents.GuardChanged += OnGuardChanged;
        EnemyMotionEvents.AttackStarted += OnAttackStarted;
        IsStoppedByGameOver = false;
        spawnPending = true;
        if (animator != null) animator.speed = 1f;
    }
    void OnDisable()
    {
        GameEvents.GameOver -= OnGameOver;
        GameEvents.GlowChanged -= OnGlowChanged;
        GameEvents.EnemyTeetered -= OnTeetered;
        EnemyMotionEvents.Launched -= OnLaunched;
        EnemyMotionEvents.MotionUpdated -= OnMotionUpdated;
        EnemyMotionEvents.Falling -= OnFalling;
        EnemyMotionEvents.Recovered -= OnRecovered;
        EnemyMotionEvents.Reset -= OnReset;
        EnemyMotionEvents.GuardChanged -= OnGuardChanged;
        EnemyMotionEvents.AttackStarted -= OnAttackStarted;
    }

    // 受信した対象が自分と一致したときだけ再生する。別の敵の通知では動かない。
    void OnLaunched(ILaunchable source, Vector3 velocity) { if (Owns(source)) BeginLaunch(velocity); }
    void OnMotionUpdated(ILaunchable source, Vector3 velocity) { if (Owns(source)) SetLaunchVelocity(velocity); }
    void OnFalling(ILaunchable source) { if (Owns(source)) PlayFall(); }
    void OnRecovered(ILaunchable source) { if (Owns(source)) EndLaunch(); }
    void OnReset(ILaunchable source) { if (Owns(source)) ResetPresentation(); }
    void OnGlowChanged(ILaunchable source, float ratio) { if (Owns(source)) SetGlowRatio(ratio); }
    void OnTeetered(ILaunchable source) { if (Owns(source)) PlayTeeter(launchDirection); }
    bool Owns(ILaunchable source) => launchable != null && ReferenceEquals(source, launchable);
    void OnGuardChanged(ILaunchable source, bool value)
    {
        if (!Owns(source)) return;
        requestedGuard = value;
        if (!IsReacting) SetDefending(value);
    }
    void OnAttackStarted(ILaunchable source) { if (Owns(source)) PlayAttack(); }

    void LateUpdate()
    {
        if (spawnPending) { spawnPending = false; PlaySpawn(); }
        if (!IsReacting && requestedGuard != IsDefending) SetDefending(requestedGuard);
        if (IsStoppedByGameOver)
        {
            // Animatorの停止だけではコードで加えた関節姿勢が戻るため、最後の見た目も保持する。
            if (leftArm != null) leftArm.localRotation = frozenLeftArm;
            if (rightArm != null) rightArm.localRotation = frozenRightArm;
            if (head != null) head.localRotation = frozenHead;
            return;
        }
        if (reaction == ReactionMode.None || reactionTuning == null || reactionPivot == null) return;
        float dt = TimeController.IsPaused || TimeController.IsHitStopping ? 0f : Time.deltaTime;
        reactionTime += dt;
        float blend = 1f - Mathf.Exp(-reactionTuning.ReactionResponse * dt);
        if (reaction == ReactionMode.Recovering)
        {
            reactionPivot.localPosition = Vector3.Lerp(reactionPivot.localPosition, pivotPosition, blend);
            reactionPivot.localRotation = Quaternion.Slerp(reactionPivot.localRotation, pivotRotation, blend);
            if (Vector3.Distance(reactionPivot.localPosition, pivotPosition) < .001f && Quaternion.Angle(reactionPivot.localRotation, pivotRotation) < .1f)
            {
                RestorePivot(); reaction = ReactionMode.None;
            }
            return;
        }
        Vector3 axis = Vector3.Cross(Vector3.up, launchDirection);
        if (reactionPivot.parent != null) axis = reactionPivot.parent.InverseTransformDirection(axis);
        float angle, hop = 0;
        if (reaction == ReactionMode.Teeter)
            angle = Mathf.Sin(reactionTime * reactionTuning.TeeterFrequency * 2f * Mathf.PI) * reactionTuning.TeeterAngle;
        else if (reaction == ReactionMode.Falling)
            angle = reactionTuning.LaunchLean + reactionTime * reactionTuning.FallSpinSpeed;
        else
        {
            angle = reactionTuning.LaunchLean * Mathf.Clamp(launchVelocity.magnitude / reactionTuning.ReactionReferenceSpeed, .35f, 1f);
            hop = Mathf.Sin(Mathf.PI * Mathf.Clamp01(reactionTime / reactionTuning.LaunchHopDuration)) * reactionTuning.LaunchHopHeight;
        }
        reactionPivot.localRotation = Quaternion.Slerp(reactionPivot.localRotation, Quaternion.AngleAxis(angle, axis) * pivotRotation, blend);
        reactionPivot.localPosition = pivotPosition + Vector3.up * hop;
        // Animatorが通常姿勢を評価した後で腕を広げる。RootとColliderはこの姿勢に巻き込まない。
        float arm = reaction == ReactionMode.Teeter ? -55f : 38f;
        if (leftArm != null) leftArm.localRotation = leftArmRest * Quaternion.Euler(arm, 0, -22f);
        if (rightArm != null) rightArm.localRotation = rightArmRest * Quaternion.Euler(arm, 0, 22f);
        if (head != null) head.localRotation = headRest * Quaternion.Euler(-12f, 0, 0);
    }

    public void SetDefending(bool defending)
    {
        if (!CanPlay() || IsReacting) return;
        requestedGuard = defending;
        IsDefending = defending;
        animator.SetBool(DEFENDING, defending);
        animator.CrossFadeInFixedTime(defending ? DEFENSE : IDLE, transitionDuration);
    }
    public void PlayAttack()
    {
        if (!CanPlay() || IsReacting) return;
        animator.CrossFadeInFixedTime(ATTACK, transitionDuration, 0, 0f);
    }
    public void PlaySpawn()
    {
        if (!CanPlay() || IsReacting) return;
        spawnPending = false;
        requestedGuard = false;
        IsDefending = false;
        animator.SetBool(DEFENDING, false);
        animator.Play(SPAWN, 0, 0f);
    }

    public void BeginLaunch(Vector3 velocity)
    {
        if (!CanPlay()) return;
        // 出現と同じフレームに当たっても、出現処理が吹き飛び姿勢を上書きしない。
        spawnPending = false;
        requestedGuard = false;
        IsDefending = false;
        animator.SetBool(DEFENDING, false);
        animator.Play(IDLE, 0, 0f);
        reaction = ReactionMode.Launched;
        reactionTime = 0;
        SetLaunchVelocity(velocity);
    }
    public void SetLaunchVelocity(Vector3 velocity)
    {
        launchVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        if (launchVelocity.sqrMagnitude > .000001f) launchDirection = launchVelocity.normalized;
    }
    public void PlayTeeter(Vector3 outward)
    {
        if (!CanPlay()) return;
        if (outward.sqrMagnitude > .000001f) launchDirection = outward.normalized;
        reaction = ReactionMode.Teeter; reactionTime = 0;
    }
    public void PlayFall()
    {
        if (!CanPlay()) return;
        reaction = ReactionMode.Falling; reactionTime = 0;
    }
    public void EndLaunch()
    {
        if (!CanPlay()) return;
        reaction = ReactionMode.Recovering;
        animator.CrossFadeInFixedTime(IDLE, transitionDuration);
    }
    public void SetGlowRatio(float ratio)
    {
        if (renderers == null) return;
        foreach (var renderer in renderers)
        {
            // 共有材質を変えず、その個体だけを光らせる。他の演出のプロパティは残す。
            renderer.GetPropertyBlock(properties);
            properties.SetFloat(GLOW_RATIO, Mathf.Clamp01(ratio));
            renderer.SetPropertyBlock(properties);
        }
    }
    public void ResetPresentation()
    {
        IsStoppedByGameOver = false;
        reaction = ReactionMode.None;
        requestedGuard = false;
        RestorePivot();
        if (animator == null) return;
        animator.speed = 1f;
        PlaySpawn();
    }
    void RestorePivot()
    {
        if (reactionPivot != null) { reactionPivot.localPosition = pivotPosition; reactionPivot.localRotation = pivotRotation; }
    }
    bool CanPlay() => !IsStoppedByGameOver && animator != null && animator.isActiveAndEnabled;
    void OnGameOver()
    {
        if (leftArm != null) frozenLeftArm = leftArm.localRotation;
        if (rightArm != null) frozenRightArm = rightArm.localRotation;
        if (head != null) frozenHead = head.localRotation;
        IsStoppedByGameOver = true;
        if (animator != null) animator.speed = 0f;
    }
}
