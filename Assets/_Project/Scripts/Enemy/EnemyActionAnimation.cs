using UnityEngine;

/// <summary>
/// 敵の見た目だけを担当する。Root移動と盾判定はEnemy、攻撃の進行はEnemyAutoAttackに任せる。
/// 接触ダメージは計算せず、復帰の完了だけを敵内部のイベントで知らせる。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyActionAnimation : MonoBehaviour
{
    static readonly int IDLE = Animator.StringToHash("Idle");
    static readonly int DEFENSE = Animator.StringToHash("Defense");
    static readonly int ATTACK = Animator.StringToHash("Attack");
    static readonly int SPAWN = Animator.StringToHash("Spawn");
    static readonly int DEFENDING = Animator.StringToHash("Defending");
    static readonly int GLOW_RATIO = Shader.PropertyToID("_GlowRatio");
    // AnimatorのIdle/Attackと、自前で加える吹き飛び姿勢は別の状態として管理する。
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
    bool CanGuard => launchable is Enemy enemy && enemy.Tuning != null
        && enemy.Tuning.HasShield && enemy.Tuning.BehaviourRole == EnemyBehaviourRole.ShieldBearer;

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
        // 再読み込み・再有効化の後も、実際の敵設定からガード可否を読み直す。
        launchable = GetComponent<ILaunchable>();
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
        // 表示だけを戻してもラウンドの終了は解除しない。Root側が保持する終了状態を読み直す。
        if (launchable is Enemy enemy && enemy.IsStoppedByGameOver)
        {
            spawnPending = false;
            OnGameOver();
        }
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
        // 表示を外したりプールへ戻した場合、完了通知を待ったEnemyを置き去りにしない。
        SetReaction(ReactionMode.None);
        RestorePivot();
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
        // 外部から一括でガード通知が来ても、盾なしの敵には予約を残さない。
        requestedGuard = value && CanGuard;
        if (!IsReacting) SetDefending(requestedGuard);
    }
    void OnAttackStarted(ILaunchable source) { if (Owns(source)) PlayAttack(); }

    void LateUpdate()
    {
        if (spawnPending) { spawnPending = false; PlaySpawn(); }
        if (!CanGuard) requestedGuard = false;
        if (!IsReacting && requestedGuard != IsDefending) SetDefending(requestedGuard);
        if (IsStoppedByGameOver)
        {
            // Animatorの停止だけではコードで加えた関節姿勢が戻るため、最後の見た目も保持する。
            if (leftArm != null) leftArm.localRotation = frozenLeftArm;
            if (rightArm != null) rightArm.localRotation = frozenRightArm;
            if (head != null) head.localRotation = frozenHead;
            return;
        }
        EnforceGuardMotionPolicy();
        if (reaction == ReactionMode.None) return;
        if (reactionTuning == null || reactionPivot == null)
        {
            // 表示用の参照が欠けても、復帰待ちだけが永続する状態にはしない。
            RestorePivot();
            SetReaction(ReactionMode.None);
            return;
        }
        float dt = TimeController.IsPaused || TimeController.IsHitStopping ? 0f : Time.deltaTime;
        reactionTime += dt;
        float blend = 1f - Mathf.Exp(-reactionTuning.ReactionResponse * dt);
        if (reaction == ReactionMode.Recovering)
        {
            reactionPivot.localPosition = Vector3.Lerp(reactionPivot.localPosition, pivotPosition, blend);
            reactionPivot.localRotation = Quaternion.Slerp(reactionPivot.localRotation, pivotRotation, blend);
            if (Vector3.Distance(reactionPivot.localPosition, pivotPosition) < .001f && Quaternion.Angle(reactionPivot.localRotation, pivotRotation) < .1f)
            {
                // 見た目を確定してから待機を解除する。次フレームの攻撃には通常姿勢から入る。
                RestorePivot();
                SetReaction(ReactionMode.None);
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

    void EnforceGuardMotionPolicy()
    {
        if (CanGuard || !CanPlay()) return;
        requestedGuard = false;
        IsDefending = false;
        // 入口の制限だけでは、再読み込み前のDefenseやAnimatorへの直接指示が残り得る。
        animator.SetBool(DEFENDING, false);
        bool defendingNow = animator.GetCurrentAnimatorStateInfo(0).shortNameHash == DEFENSE;
        bool enteringDefense = animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == DEFENSE;
        if (defendingNow || enteringDefense) animator.Play(IDLE, 0, 0f);
        // Attack/Spawn中は状態を変更せず、そのまま再生を続ける。
    }

    public void SetDefending(bool defending)
    {
        // デモや別スクリプトから直接呼ばれた場合も、ガード可能な種類に限定する。
        defending = defending && CanGuard;
        if (!CanPlay() || IsReacting) return;
        requestedGuard = defending;
        // 盾なしへのガード指示を無視するとき、再生中の攻撃までIdleで中断しない。
        if (!defending && !IsDefending)
        {
            animator.SetBool(DEFENDING, false);
            return;
        }
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
        if (reactionTuning == null || reactionPivot == null) return;
        SetReaction(ReactionMode.Launched);
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
        SetReaction(ReactionMode.Teeter); reactionTime = 0;
    }
    public void PlayFall()
    {
        if (!CanPlay()) return;
        SetReaction(ReactionMode.Falling); reactionTime = 0;
    }
    public void EndLaunch()
    {
        if (!CanPlay() || reactionTuning == null || reactionPivot == null)
        {
            RestorePivot();
            SetReaction(ReactionMode.None);
            return;
        }
        // 移動の停止通知から、姿勢を戻す時間へ移る。この時点ではまだ追跡・攻撃を再開させない。
        SetReaction(ReactionMode.Recovering);
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
        SetReaction(ReactionMode.None);
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
    void SetReaction(ReactionMode next)
    {
        bool wasBusy = IsReacting;
        reaction = next;
        // Launched→Recoveringの間は待機を維持し、Noneとの境目だけを通知する。
        if (wasBusy != IsReacting) EnemyMotionEvents.RaisePresentationBusyChanged(launchable, IsReacting);
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
