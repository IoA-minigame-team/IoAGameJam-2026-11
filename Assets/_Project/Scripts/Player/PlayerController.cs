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
    // これより短い線は向きが決まらないので、返しをしない(SlashResolver の判定と同じ値)
    const float MIN_RETURN_LINE_LENGTH = 0.01f;
    const float MIN_MOVE_INPUT = 0.0001f;
    // カメラが真下を向いていて右が決まらないときの判定
    const float MIN_AXIS_LENGTH = 0.0001f;

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

    // 斬り抜け。slashStart と slashEnd は離した瞬間に確定した線(刀の位置 → 離した地点)
    int nextSlashId = 1;
    int currentSlashId;
    Vector3 slashStart;
    Vector3 slashEnd;
    // 今の斬り抜けの両端。返しのときは線を逆にたどるので slashStart、slashEnd と入れ替わる
    Vector3 passStart;
    Vector3 passEnd;
    Vector3 passDirection;
    float slashDuration;
    float slashElapsed;
    readonly List<SlashTarget> slashTargets = new List<SlashTarget>();
    int nextTargetIndex;
    // passHitCount は今の斬り抜けだけ(ヒットストップ用)、slashHitCount は返しを含めた一振りの合計
    int passHitCount;
    int slashHitCount;

    public State CurrentState { get; private set; }

    /// <summary>大銘「返し」の2回目の斬り抜け中か。見た目で斬り抜けのアニメを流し直すため</summary>
    public bool IsReturnSlashing { get; private set; }

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
        GameContext.AutoReturnLimit = 0f;
        // 無効にしていた間の群がり数は古いので捨てる。次の SwarmCountChanged で正しい数になる
        swarmCount = 0;

        GameEvents.SwarmCountChanged += OnSwarmCountChanged;
        GameEvents.GameOver += OnGameOver;
    }

    void OnDisable()
    {
        GameEvents.SwarmCountChanged -= OnSwarmCountChanged;
        GameEvents.GameOver -= OnGameOver;

        if (GameContext.PlayerTransform == transform) GameContext.PlayerTransform = null;
        ClearAutoReturn();
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
                // 刀は手に戻っているので、光は主人公とともに動く
                GameContext.SwordPosition = transform.position;
                break;

            case State.Recovery:
                GameContext.SwordPosition = transform.position;
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
                PlantedSwordRotation());
        }

        GameContext.IsSwordPlanted = true;
        GameContext.SwordPosition = plantedPosition;
        GameEvents.RaiseSwordPlanted(plantedPosition);
        Feedback.PlaySe(SeId.SwordPlant, plantedPosition);
        Feedback.SpawnVfx(VfxId.LightRing, plantedPosition, Quaternion.identity);

        stateTimer = tuning.PlantRecovery;
        CurrentState = State.Planting;
    }

    // 刺さった刀の向きはカメラを基準に決める。見下ろしのカメラから刃の平らな面と長さが見えるようにするため。
    // 主人公の向きに合わせると、向きによっては刃の厚み側しか見えず、細い線になる
    Quaternion PlantedSwordRotation()
    {
        Vector3 cameraRight = Vector3.right;
        if (cameraTransform != null)
        {
            Vector3 flatRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up);
            if (flatRight.sqrMagnitude > MIN_AXIS_LENGTH) cameraRight = flatRight.normalized;
        }
        // 刀の Z(刃幅の向き)を画面の横にそろえると、X(刃の面)がカメラを向く
        Quaternion faceCamera = Quaternion.LookRotation(cameraRight, Vector3.up);
        // 画面の横を軸に正の角度で回すと、刀の頭が奥(カメラから離れる向き)へ倒れて長く見える
        return Quaternion.AngleAxis(tuning.PlantTiltAngle, cameraRight) * faceCamera;
    }

    // 群がるほど早く斬り戻される。群がる数が途中で変わっても、その時点の制限時間と比べる
    float UpdateAutoReturnRemaining()
    {
        float limit = Mathf.Max(
            tuning.AutoReturnMinTime,
            tuning.AutoReturnTime - tuning.AutoReturnReductionPerSwarm * swarmCount);
        limit *= GameContext.MeiStats.AutoReturnTimeMultiplier;

        float remaining = Mathf.Max(0f, limit - freeHandElapsed);
        GameContext.AutoReturnLimit = limit;
        GameContext.AutoReturnRemaining = remaining;
        return remaining;
    }

    // 刀が刺さっていない間は、残り時間も制限時間も 0 にしておく
    static void ClearAutoReturn()
    {
        GameContext.AutoReturnRemaining = 0f;
        GameContext.AutoReturnLimit = 0f;
    }

    // ---- 斬り戻し ----

    void BeginSlash()
    {
        // 一振りごとに新しい番号。同じ斬り抜けで当たった敵には全員この番号を渡す。返しも同じ番号を使う
        currentSlashId = nextSlashId++;

        // 線は離した瞬間に確定する
        slashStart = plantedPosition;
        slashEnd = transform.position;
        slashHitCount = 0;
        IsReturnSlashing = false;

        Vector3 direction = StartSlashPass(slashStart, slashEnd, transform.forward, 0f);

        // 返しでは流さない。受け手(ScoreCounter など)が同じ一振りを2回始めないようにするため
        GameEvents.RaiseSlashStarted(new SlashStartedInfo(currentSlashId, slashStart, slashEnd));

        // 刀の位置へ瞬間移動する。斬り抜けは敵や壁に引っかからないよう、CharacterController を切って直接動かす
        characterController.enabled = false;
        transform.SetPositionAndRotation(slashStart, Quaternion.LookRotation(direction));
        ReturnSwordToHand();

        ClearAutoReturn();
        CurrentState = State.Slashing;
    }

    // 1回分の斬り抜けを始める。返しでもう一度呼ぶ。戻り値は斬り抜ける向き
    Vector3 StartSlashPass(Vector3 start, Vector3 end, Vector3 fallbackDirection, float delay)
    {
        passStart = start;
        passEnd = end;

        // 当たる敵はこの斬り抜けを始める瞬間に集める。返しでは、1回目の後に線へ入ってきた敵も当たる
        passDirection = SlashResolver.Resolve(
            start, end, fallbackDirection, tuning, GameContext.MeiStats, enemyMask,
            slashTargets, out int shieldBlockedCount);
        // 盾に弾かれた音は SeId にまだ無い(増永さんに追加を頼む)。追加されたら 1 以上のときに鳴らす
        LastShieldBlockedCount = shieldBlockedCount;

        Feedback.PlaySe(SeId.Slash, start);
        Feedback.SpawnVfx(VfxId.SlashTrail, start, Quaternion.LookRotation(passDirection));

        slashDuration = tuning.SlashDuration / Mathf.Max(MIN_SPEED_MULTIPLIER, GameContext.MeiStats.SlashSpeedMultiplier);
        // 負から数え始めると、progress が 0 に張り付いたまま始点で待つ
        slashElapsed = -delay;
        nextTargetIndex = 0;
        passHitCount = 0;
        return passDirection;
    }

    void UpdateSlash(float dt)
    {
        slashElapsed += dt;
        float progress = Mathf.Clamp01(slashElapsed / slashDuration);
        transform.position = Vector3.Lerp(passStart, passEnd, progress);

        // 返しの間合いの最中はまだ斬り抜けていない。始点にいる敵(Progress が 0)を待つ間に飛ばさないよう、当てる処理へ進まない
        if (slashElapsed < 0f) return;

        // 通り過ぎた敵から順に吹き飛ばす。斬り抜けに合わせて群れが飛んでいくように見せるため
        while (nextTargetIndex < slashTargets.Count && slashTargets[nextTargetIndex].Progress <= progress)
        {
            ApplyHit(slashTargets[nextTargetIndex]);
            nextTargetIndex++;
        }

        if (progress >= 1f) FinishSlashPass();
    }

    void ApplyHit(SlashTarget target)
    {
        // 斬り抜けの間に消えた敵(先に落ちたなど)は飛ばさない
        if (target.Enemy == null || (target.Enemy is Object unityObject && unityObject == null)) return;

        if (passHitCount == 0)
        {
            // ヒットストップは斬り抜けごとに最初に当たった瞬間に1回だけ。長さはその斬り抜けで当てる数で決める
            float hitStop = Mathf.Min(
                tuning.HitStopBase + tuning.HitStopPerEnemy * slashTargets.Count,
                tuning.HitStopMax);
            TimeController.HitStop(hitStop);
        }

        passHitCount++;
        slashHitCount++;
        Vector3 position = target.Enemy.Position;
        target.Enemy.Launch(target.Velocity, currentSlashId);
        GameEvents.RaiseEnemyHit(new EnemyHitInfo(currentSlashId, target.Enemy, target.Velocity, target.Frontness));
        Feedback.PlaySe(SeId.Hit, position);
        Feedback.SpawnVfx(VfxId.Hit, position, Quaternion.identity);
    }

    void FinishSlashPass()
    {
        transform.position = passEnd;
        slashTargets.Clear();

        if (ShouldStartReturnSlash())
        {
            // 大銘「返し」: 同じ線を逆向きにもう一度斬り抜ける。無敵のまま Slashing を続け、硬直は返しの後だけにする
            IsReturnSlashing = true;
            Vector3 direction = StartSlashPass(slashEnd, slashStart, -passDirection, tuning.ReturnSlashDelay);
            transform.rotation = Quaternion.LookRotation(direction);
            return;
        }

        FinishSlash();
    }

    bool ShouldStartReturnSlash()
    {
        if (IsReturnSlashing) return false;
        if (!GameContext.MeiStats.HasReturnSlash) return false;
        Vector3 flat = slashEnd - slashStart;
        flat.y = 0f;
        return flat.magnitude >= MIN_RETURN_LINE_LENGTH;
    }

    void FinishSlash()
    {
        characterController.enabled = true;
        IsReturnSlashing = false;

        // 返しがあっても一振りは1つなので、ここで1回だけ流す。当てた数は返しを含めた合計
        GameEvents.RaiseSlashFinished(new SlashFinishedInfo(currentSlashId, slashHitCount));

        // 当たれば連鎖、外せば隙。盾に正面から当たっただけなら slashHitCount は 0 なので空振り。
        // 返しでは1回目と返しのどちらかで当たれば当たり
        bool hit = slashHitCount > 0;
        LastSlashHit = hit;
        int newChain = hit ? Chain + 1 : 0;
        if (newChain != Chain)
        {
            Chain = newChain;
            GameEvents.RaiseChainChanged(Chain);
        }

        Feedback.PlaySe(hit ? SeId.Sheathe : SeId.Whiff, transform.position);

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
        // 斬り抜けの途中なら今の斬り抜けの終点(返しなら刀の位置)に置いてから止める。CharacterController を切ったままにしないため
        if (CurrentState == State.Slashing)
        {
            transform.position = passEnd;
            characterController.enabled = true;
            slashTargets.Clear();
        }
        IsReturnSlashing = false;
        ClearAutoReturn();
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
