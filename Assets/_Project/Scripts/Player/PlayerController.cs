using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 主人公の操作。移動、突き立て、手ぶら、斬り戻し、硬直、連鎖を1つの状態の流れで持つ。
/// 線の判定と初速の式は SlashResolver、被弾は PlayerHealth に分けてある。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerController : MonoBehaviour
{
    public enum State
    {
        /// <summary>刀を持っている</summary>
        Armed,
        /// <summary>突き立てた直後の隙</summary>
        Planting,
        /// <summary>手ぶら。離すか時間切れで斬り戻す</summary>
        FreeHand,
        /// <summary>斬り抜け中。無敵</summary>
        Slashing,
        /// <summary>斬り戻し後の硬直</summary>
        Recovery,
        /// <summary>ゲームオーバー後。何も受け付けない</summary>
        Disabled,
    }

    // 0 で割らないための下限
    const float MIN_SPEED_MULTIPLIER = 0.01f;
    const float MIN_MOVE_INPUT = 0.0001f;

    [SerializeField] PlayerTuning tuning;

    [Header("入力")]
    [SerializeField] InputActionReference moveAction;
    [SerializeField] InputActionReference plantAction;

    [Header("参照")]
    [Tooltip("刀の見た目。突き立てると親から外してその場に残す")]
    [SerializeField] Transform sword;
    [Tooltip("移動の基準にするカメラ。空なら Camera.main")]
    [SerializeField] Transform cameraTransform;
    [Tooltip("斬り抜けで当てる敵のレイヤー")]
    [SerializeField] LayerMask enemyMask;

    CharacterController characterController;

    // 刀を手に戻すときの持ち方
    Transform swordHolder;
    Vector3 swordLocalPosition;
    Quaternion swordLocalRotation;

    float stateTimer;
    bool plantBuffered;
    int swarmCount;

    // 突き立て
    Vector3 plantedPosition;
    float freeHandElapsed;

    // 斬り抜け
    int nextSlashId = 1;
    int currentSlashId;
    Vector3 slashStart;
    Vector3 slashEnd;
    float slashDuration;
    float slashElapsed;
    readonly List<SlashTarget> slashTargets = new List<SlashTarget>();
    int nextTargetIndex;
    int slashHitCount;

    public State CurrentState { get; private set; }

    /// <summary>斬り抜け中は無敵(PlayerHealth が読む)</summary>
    public bool IsSlashInvincible => CurrentState == State.Slashing;

    /// <summary>刀が刺さっている位置。線の表示で使う</summary>
    public Vector3 PlantedPosition => plantedPosition;

    /// <summary>刀が地面にあり、線を表示する状態か</summary>
    public bool IsLineVisible => CurrentState == State.Planting || CurrentState == State.FreeHand;

    /// <summary>今の線の幅(銘の倍率込み)</summary>
    public float CurrentLineWidth => tuning.LineWidth * GameContext.MeiStats.LineWidthMultiplier;

    /// <summary>足元の高さ。線を地面に描くときと、刀を地面に刺すときに使う</summary>
    // CharacterController は skinWidth の分だけ地面から浮いて止まるので、その分を引いて地面に合わせる
    public float FootHeight =>
        transform.position.y + characterController.center.y - characterController.height * 0.5f - characterController.skinWidth;

    /// <summary>今の連鎖数</summary>
    public int Chain { get; private set; }

    /// <summary>直前の斬り戻しで1体以上当たったか。硬直の見た目を当たりと空振りで分けるため</summary>
    public bool LastSlashHit { get; private set; }

    /// <summary>直前の斬り戻しで、盾に正面から弾かれた敵の数</summary>
    public int LastShieldBlockedCount { get; private set; }

    void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

        if (sword != null)
        {
            swordHolder = sword.parent;
            swordLocalPosition = sword.localPosition;
            swordLocalRotation = sword.localRotation;
        }
    }

    void OnEnable()
    {
        // Unity のオブジェクトは ?. だと未設定を見分けられないので、!= null で確かめる
        if (moveAction != null) moveAction.action.Enable();
        if (plantAction != null) plantAction.action.Enable();

        GameContext.PlayerTransform = transform;
        GameContext.IsSwordPlanted = false;
        GameContext.SwordPosition = transform.position;
        GameContext.AutoReturnRemaining = 0f;

        GameEvents.SwarmCountChanged += OnSwarmCountChanged;
        GameEvents.GameOver += OnGameOver;
    }

    void OnDisable()
    {
        GameEvents.SwarmCountChanged -= OnSwarmCountChanged;
        GameEvents.GameOver -= OnGameOver;

        if (GameContext.PlayerTransform == transform) GameContext.PlayerTransform = null;
        GameContext.AutoReturnRemaining = 0f;
    }

    void Update()
    {
        if (CurrentState == State.Disabled) return;

        // 銘の選択などで止まっている間は入力を読まない。A ボタンで選んだときに刀を置かないため
        bool acceptInput = !TimeController.IsPaused;
        float dt = Time.deltaTime;

        switch (CurrentState)
        {
            case State.Armed:
                Move(tuning.MoveSpeed);
                GameContext.SwordPosition = transform.position;
                if (acceptInput && (WasPlantPressed() || (plantBuffered && IsPlantHeld())))
                {
                    PlantSword();
                }
                plantBuffered = false;
                break;

            case State.Planting:
                freeHandElapsed += dt;
                UpdateAutoReturnRemaining();
                stateTimer -= dt;
                // 隙の間に離しても、隙が終わってから斬り戻す
                if (stateTimer <= 0f) CurrentState = State.FreeHand;
                break;

            case State.FreeHand:
                Move(tuning.MoveSpeed * tuning.FreeHandSpeedRatio * GameContext.MeiStats.FreeHandSpeedMultiplier);
                freeHandElapsed += dt;
                float remaining = UpdateAutoReturnRemaining();
                // 押下を「今押しているか」で見るので、一時停止中に離しても再開した最初のフレームで拾える
                if ((acceptInput && !IsPlantHeld()) || remaining <= 0f) BeginSlash();
                break;

            case State.Slashing:
                UpdateSlash(dt);
                break;

            case State.Recovery:
                // 硬直の終わり際に押した分を覚えておき、押し続けていれば硬直明けに突き立てる
                if (acceptInput && WasPlantPressed()) plantBuffered = true;
                stateTimer -= dt;
                if (stateTimer <= 0f) CurrentState = State.Armed;
                break;
        }
    }

    // ---- 移動 ----

    void Move(float speed)
    {
        Vector2 input = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        Vector3 direction = ToWorldDirection(input);

        // SimpleMove は重力も掛けてくれる。時間は scaled なのでヒットストップやスローに従う
        characterController.SimpleMove(direction * speed);

        if (direction.sqrMagnitude > MIN_MOVE_INPUT)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    // 画面の上が W になるよう、カメラの向きを地面に投影して基準にする
    Vector3 ToWorldDirection(Vector2 input)
    {
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        if (cameraTransform != null)
        {
            forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        }
        return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
    }

    // ---- 突き立て ----

    void PlantSword()
    {
        plantedPosition = transform.position;
        freeHandElapsed = 0f;

        if (sword != null)
        {
            // 刀のピボットは「地面に接する点」に置いてあるので、足元の高さに置けば刺さって見える
            sword.SetParent(null, true);
            sword.SetPositionAndRotation(
                new Vector3(plantedPosition.x, FootHeight, plantedPosition.z),
                Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        }

        GameContext.IsSwordPlanted = true;
        GameContext.SwordPosition = plantedPosition;
        GameEvents.RaiseSwordPlanted(plantedPosition);
        Feedback.PlaySe(SeId.SwordPlant, plantedPosition);
        Feedback.SpawnVfx(VfxId.LightRing, plantedPosition, Quaternion.identity);

        stateTimer = tuning.PlantRecovery;
        CurrentState = State.Planting;
    }

    // 群がるほど早く斬り戻される。群がる数が途中で変わっても、その時点の制限時間と比べる
    float UpdateAutoReturnRemaining()
    {
        float limit = Mathf.Max(
            tuning.AutoReturnMinTime,
            tuning.AutoReturnTime - tuning.AutoReturnReductionPerSwarm * swarmCount);
        limit *= GameContext.MeiStats.AutoReturnTimeMultiplier;

        float remaining = Mathf.Max(0f, limit - freeHandElapsed);
        GameContext.AutoReturnRemaining = remaining;
        return remaining;
    }

    // ---- 斬り戻し ----

    void BeginSlash()
    {
        // 一振りごとに新しい番号。同じ斬り抜けで当たった敵には全員この番号を渡す
        currentSlashId = nextSlashId++;

        // 線は離した瞬間に確定する
        slashStart = plantedPosition;
        slashEnd = transform.position;

        Vector3 direction = SlashResolver.Resolve(
            slashStart, slashEnd, transform.forward, tuning, GameContext.MeiStats, enemyMask,
            slashTargets, out int shieldBlockedCount);
        // 盾に弾かれた音は SeId にまだ無い(増永さんに追加を頼む)。追加されたら 1 以上のときに鳴らす
        LastShieldBlockedCount = shieldBlockedCount;

        GameEvents.RaiseSlashStarted(new SlashStartedInfo(currentSlashId, slashStart, slashEnd));
        Feedback.PlaySe(SeId.Slash, slashStart);
        Feedback.SpawnVfx(VfxId.SlashTrail, slashStart, Quaternion.LookRotation(direction));

        // 刀の位置へ瞬間移動する。斬り抜けは敵や壁に引っかからないよう、CharacterController を切って直接動かす
        characterController.enabled = false;
        transform.SetPositionAndRotation(slashStart, Quaternion.LookRotation(direction));
        ReturnSwordToHand();

        slashDuration = tuning.SlashDuration / Mathf.Max(MIN_SPEED_MULTIPLIER, GameContext.MeiStats.SlashSpeedMultiplier);
        slashElapsed = 0f;
        nextTargetIndex = 0;
        slashHitCount = 0;

        GameContext.AutoReturnRemaining = 0f;
        CurrentState = State.Slashing;
    }

    void UpdateSlash(float dt)
    {
        slashElapsed += dt;
        float progress = Mathf.Clamp01(slashElapsed / slashDuration);
        transform.position = Vector3.Lerp(slashStart, slashEnd, progress);

        // 通り過ぎた敵から順に吹き飛ばす。斬り抜けに合わせて群れが飛んでいくように見せるため
        while (nextTargetIndex < slashTargets.Count && slashTargets[nextTargetIndex].Progress <= progress)
        {
            ApplyHit(slashTargets[nextTargetIndex]);
            nextTargetIndex++;
        }

        if (progress >= 1f) FinishSlash();
    }

    void ApplyHit(SlashTarget target)
    {
        // 斬り抜けの間に消えた敵(先に落ちたなど)は飛ばさない
        if (target.Enemy == null || (target.Enemy is Object unityObject && unityObject == null)) return;

        if (slashHitCount == 0)
        {
            // ヒットストップは最初に当たった瞬間に1回だけ。長さはこの一振りで当てる数で決める
            float hitStop = Mathf.Min(
                tuning.HitStopBase + tuning.HitStopPerEnemy * slashTargets.Count,
                tuning.HitStopMax);
            TimeController.HitStop(hitStop);
        }

        slashHitCount++;
        Vector3 position = target.Enemy.Position;
        target.Enemy.Launch(target.Velocity, currentSlashId);
        GameEvents.RaiseEnemyHit(new EnemyHitInfo(currentSlashId, target.Enemy, target.Velocity, target.Frontness));
        Feedback.PlaySe(SeId.Hit, position);
        Feedback.SpawnVfx(VfxId.Hit, position, Quaternion.identity);
    }

    void FinishSlash()
    {
        transform.position = slashEnd;
        characterController.enabled = true;
        slashTargets.Clear();

        GameEvents.RaiseSlashFinished(new SlashFinishedInfo(currentSlashId, slashHitCount));

        // 当たれば連鎖、外せば隙。盾に正面から当たっただけなら slashHitCount は 0 なので空振り
        bool hit = slashHitCount > 0;
        LastSlashHit = hit;
        int newChain = hit ? Chain + 1 : 0;
        if (newChain != Chain)
        {
            Chain = newChain;
            GameEvents.RaiseChainChanged(Chain);
        }

        Feedback.PlaySe(hit ? SeId.Sheathe : SeId.Whiff, slashEnd);

        // 大銘「返し」(GameContext.MeiStats.HasReturnSlash)はここで分岐する予定。今回は範囲外

        stateTimer = hit ? tuning.HitRecovery : tuning.WhiffRecovery;
        CurrentState = State.Recovery;
    }

    void ReturnSwordToHand()
    {
        GameContext.IsSwordPlanted = false;
        GameContext.SwordPosition = transform.position;

        if (sword == null) return;
        sword.SetParent(swordHolder, false);
        sword.localPosition = swordLocalPosition;
        sword.localRotation = swordLocalRotation;
    }

    // ---- 入力 ----

    bool WasPlantPressed() => plantAction != null && plantAction.action.WasPressedThisFrame();

    bool IsPlantHeld() => plantAction != null && plantAction.action.IsPressed();

    // ---- イベント ----

    void OnSwarmCountChanged(int count) => swarmCount = Mathf.Max(0, count);

    void OnGameOver()
    {
        // 斬り抜けの途中なら終点に置いてから止める。CharacterController を切ったままにしないため
        if (CurrentState == State.Slashing)
        {
            transform.position = slashEnd;
            characterController.enabled = true;
        }
        GameContext.AutoReturnRemaining = 0f;
        CurrentState = State.Disabled;
    }

#if UNITY_EDITOR
    // 斬り抜けの線をシーンビューで確かめるため
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || tuning == null || !IsLineVisible) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(plantedPosition, transform.position);
    }
#endif
}
