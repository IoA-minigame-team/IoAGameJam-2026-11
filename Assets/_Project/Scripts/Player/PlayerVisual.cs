using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 主人公の見た目。PlayerController の状態を読んで、アニメーション、アクセントカラー、被弾中の点滅を切り替える。
/// 操作のコードに見た目を混ぜないよう、読むだけにしてある。
/// </summary>
// PlayerController が状態を変えた同じフレームのうちに切り替えるため、後から動かす
[DefaultExecutionOrder(100)]
public sealed class PlayerVisual : MonoBehaviour
{
    // Animator のステート名。Animator Controller 側もこの名前で作る
    const string STATE_LOCOMOTION = "Locomotion";
    const string STATE_PLANT = "Plant";
    const string STATE_FREE_LOCOMOTION = "FreeLocomotion";
    const string STATE_SLASH = "SlashDash";
    const string STATE_SHEATHE = "Sheathe";
    const string STATE_WHIFF = "Whiff";
    const string STATE_DOWN = "Down";
    const string PARAM_SPEED = "Speed";
    const int BASE_LAYER = 0;
    // これより速ければ動き出したとみなし、残している姿勢をやめる(m/s)
    const float MIN_MOVING_SPEED = 0.1f;

    static readonly int locomotionHash = Animator.StringToHash(STATE_LOCOMOTION);
    static readonly int plantHash = Animator.StringToHash(STATE_PLANT);
    static readonly int freeLocomotionHash = Animator.StringToHash(STATE_FREE_LOCOMOTION);
    static readonly int slashHash = Animator.StringToHash(STATE_SLASH);
    static readonly int sheatheHash = Animator.StringToHash(STATE_SHEATHE);
    static readonly int whiffHash = Animator.StringToHash(STATE_WHIFF);
    static readonly int downHash = Animator.StringToHash(STATE_DOWN);
    static readonly int speedHash = Animator.StringToHash(PARAM_SPEED);

    [SerializeField] PlayerTuning tuning;
    [SerializeField] PlayerController controller;
    [SerializeField] PlayerHealth health;
    [Tooltip("体の Animator。仮モデルの間は空でよい")]
    [SerializeField] Animator animator;

    [Header("アクセントカラー")]
    [Tooltip("体のうち、このマテリアルを使っているスロットを手ぶらの間だけ差し替える")]
    [SerializeField] Material accentMaterial;
    [Tooltip("手ぶらの間に使う白黒のマテリアル")]
    [SerializeField] Material accentOffMaterial;

    [Header("体の Renderer")]
    [Tooltip("体の見た目の親。この下の Renderer をアクセントと点滅の対象にする")]
    [SerializeField] Transform modelRoot;
    [Tooltip("刀の根。刀は手ぶらの間も光ったままにするので、この下の Renderer は対象から外す")]
    [SerializeField] Transform swordRoot;

    readonly List<Renderer> bodyRenderers = new List<Renderer>();
    // アクセントが点いているときと消えているときのマテリアルの並び。毎回配列を作らないよう先に用意する
    readonly List<Material[]> accentOnMaterials = new List<Material[]>();
    readonly List<Material[]> accentOffMaterials = new List<Material[]>();
    readonly List<Renderer> accentRenderers = new List<Renderer>();

    CharacterController characterController;
    bool hasAnimator;
    bool hasSpeedParam;
    PlayerController.State lastState;
    bool wasReturnSlashing;
    bool isFirstFrame = true;
    bool accentOn = true;
    bool bodyVisible = true;
    float blinkElapsed;
    float horizontalSpeed;

    // 決めの姿勢を残している間に来た切り替えは、残し終わってから行う
    float poseHoldRemaining;
    // 当たった一振りの後、納刀の鍔が鳴るまで納刀の動きを残しているか
    bool isHoldingSheathe;
    bool hasPendingCrossFade;
    int pendingStateHash;
    float pendingDuration;

    void Awake()
    {
        if (controller == null) controller = GetComponent<PlayerController>();
        if (health == null) health = GetComponent<PlayerHealth>();
        if (controller != null) characterController = controller.GetComponent<CharacterController>();

        hasAnimator = animator != null && animator.runtimeAnimatorController != null;
        if (hasAnimator)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == speedHash && parameter.type == AnimatorControllerParameterType.Float) hasSpeedParam = true;
            }
        }

        CollectRenderers();
    }

    void OnDisable()
    {
        // 無効になったときに白黒や非表示のまま残らないよう戻す
        SetAccent(true);
        SetBodyVisible(true);
    }

    void Update()
    {
        if (controller == null) return;

        PlayerController.State state = controller.CurrentState;
        if (isFirstFrame || state != lastState)
        {
            OnStateChanged(state);
            lastState = state;
            isFirstFrame = false;
        }

        // 大銘「返し」は状態が Slashing のまま2回目に入るので、ここで見て斬り抜けの動きを流し直す
        bool isReturnSlashing = controller.IsReturnSlashing;
        if (isReturnSlashing && !wasReturnSlashing) RestartSlash();
        wasReturnSlashing = isReturnSlashing;

        UpdateSpeed(state);
        UpdatePoseHold(state);
        UpdateBlink();
    }

    void OnStateChanged(PlayerController.State state)
    {
        // 置いた瞬間に白黒になり、引き抜いた(斬り抜けに入った)瞬間に色が戻る
        SetAccent(state != PlayerController.State.Planting && state != PlayerController.State.FreeHand);

        if (!hasAnimator) return;

        // 突き立て、斬り抜け、倒れるは操作に直結するので、残している姿勢があってもすぐ切り替える
        switch (state)
        {
            case PlayerController.State.Armed:
                // 硬直は納刀の動きより短いので、そのままだと納刀が見えずに走りへ戻る。止まっている間は鍔が鳴るまで納刀を見せる
                if (lastState == PlayerController.State.Recovery && controller.IsSheathePending)
                {
                    // 振り抜いた形をまだ残していたら、ここで納刀へ移る
                    if (hasPendingCrossFade && pendingStateHash == sheatheHash) CrossFadeNow(sheatheHash, tuning.AnimationCrossFade);
                    isHoldingSheathe = true;
                }
                RequestCrossFade(locomotionHash, tuning.AnimationCrossFade);
                break;
            case PlayerController.State.Planting: CrossFadeNow(plantHash, tuning.AnimationCrossFade); break;
            case PlayerController.State.FreeHand:
                // 突き立てた瞬間が一瞬で消えないよう、刺した姿勢を少し残してから手ぶらの待機へ移る
                StartPoseHold(tuning.PlantPoseHold);
                RequestCrossFade(freeLocomotionHash, tuning.AnimationCrossFade);
                break;
            case PlayerController.State.Slashing: CrossFadeNow(slashHash, tuning.SlashCrossFade); break;
            case PlayerController.State.Recovery:
                // 振り抜いた形を見せてから、納刀か空振りへ移る
                StartPoseHold(tuning.SlashPoseHold);
                RequestCrossFade(controller.LastSlashHit ? sheatheHash : whiffHash, tuning.AnimationCrossFade);
                break;
            case PlayerController.State.Disabled: CrossFadeNow(downHash, tuning.AnimationCrossFade); break;
        }
    }

    void RestartSlash()
    {
        if (!hasAnimator || !animator.HasState(BASE_LAYER, slashHash)) return;
        // 体を逆向きにしてから同じクリップを最初から流す。同じステートへの CrossFade では流し直さないことがあるので Play を使う
        poseHoldRemaining = 0f;
        isHoldingSheathe = false;
        hasPendingCrossFade = false;
        animator.Play(slashHash, BASE_LAYER, 0f);
    }

    // ---- 決めの姿勢を残す ----

    void StartPoseHold(float duration)
    {
        poseHoldRemaining = duration;
    }

    bool IsHoldingPose => poseHoldRemaining > 0f || (isHoldingSheathe && controller.IsSheathePending);

    void RequestCrossFade(int stateHash, float duration)
    {
        if (!IsHoldingPose)
        {
            CrossFade(stateHash, duration);
            return;
        }

        // 後から来た切り替えで上書きする。残し終わったときに、その時点の状態に合う動きへ移るため
        hasPendingCrossFade = true;
        pendingStateHash = stateHash;
        pendingDuration = duration;
    }

    void CrossFadeNow(int stateHash, float duration)
    {
        poseHoldRemaining = 0f;
        isHoldingSheathe = false;
        hasPendingCrossFade = false;
        CrossFade(stateHash, duration);
    }

    void UpdatePoseHold(PlayerController.State state)
    {
        if (!hasPendingCrossFade) return;

        // scaled time で数えるので、ヒットストップ中は姿勢も止まったまま残る
        poseHoldRemaining -= Time.deltaTime;

        // 動き出したら、足が滑って見えないよう残すのをやめる
        bool canMove = state == PlayerController.State.Armed || state == PlayerController.State.FreeHand;
        bool startedMoving = canMove && horizontalSpeed > MIN_MOVING_SPEED;
        if (IsHoldingPose && !startedMoving) return;

        poseHoldRemaining = 0f;
        isHoldingSheathe = false;
        hasPendingCrossFade = false;
        CrossFade(pendingStateHash, pendingDuration);
    }

    void CrossFade(int stateHash, float duration)
    {
        // ステートがまだ無い(クリップを作っている途中)ときは、警告を出さずに今の動きのままにする
        if (!animator.HasState(BASE_LAYER, stateHash)) return;
        animator.CrossFadeInFixedTime(stateHash, duration, BASE_LAYER);
    }

    void UpdateSpeed(PlayerController.State state)
    {
        // 移動しているのは Armed と FreeHand だけ。斬り抜け中は CharacterController を切っているので速さは読まない
        horizontalSpeed = 0f;
        bool moving = state == PlayerController.State.Armed || state == PlayerController.State.FreeHand;
        if (moving && characterController != null && characterController.enabled)
        {
            Vector3 velocity = characterController.velocity;
            velocity.y = 0f;
            horizontalSpeed = velocity.magnitude;
        }

        if (!hasSpeedParam) return;
        animator.SetFloat(speedHash, Mathf.Clamp01(horizontalSpeed / tuning.RunBlendSpeed));
    }

    void UpdateBlink()
    {
        if (health == null || !health.IsDamageInvincible)
        {
            blinkElapsed = 0f;
            SetBodyVisible(true);
            return;
        }

        // scaled time で数えるので、ヒットストップ中は点滅も止まる
        blinkElapsed += Time.deltaTime;
        int phase = Mathf.FloorToInt(blinkElapsed / tuning.DamageBlinkInterval);
        SetBodyVisible(phase % 2 == 0);
    }

    // ---- Renderer ----

    void CollectRenderers()
    {
        Transform root = modelRoot != null ? modelRoot : transform;
        foreach (Renderer target in root.GetComponentsInChildren<Renderer>(true))
        {
            // 線の LineRenderer と刀は体ではない
            if (target is LineRenderer) continue;
            if (swordRoot != null && target.transform.IsChildOf(swordRoot)) continue;

            bodyRenderers.Add(target);

            if (accentMaterial == null || accentOffMaterial == null) continue;
            Material[] onMaterials = target.sharedMaterials;
            Material[] offMaterials = (Material[])onMaterials.Clone();
            bool usesAccent = false;
            for (int i = 0; i < offMaterials.Length; i++)
            {
                if (offMaterials[i] != accentMaterial) continue;
                offMaterials[i] = accentOffMaterial;
                usesAccent = true;
            }
            if (!usesAccent) continue;

            accentRenderers.Add(target);
            accentOnMaterials.Add(onMaterials);
            accentOffMaterials.Add(offMaterials);
        }
    }

    void SetAccent(bool on)
    {
        if (accentOn == on) return;
        accentOn = on;
        for (int i = 0; i < accentRenderers.Count; i++)
        {
            if (accentRenderers[i] == null) continue;
            // sharedMaterials の差し替えなので、マテリアルの複製は作らない
            accentRenderers[i].sharedMaterials = on ? accentOnMaterials[i] : accentOffMaterials[i];
        }
    }

    void SetBodyVisible(bool visible)
    {
        if (bodyVisible == visible) return;
        bodyVisible = visible;
        foreach (Renderer target in bodyRenderers)
        {
            if (target != null) target.enabled = visible;
        }
    }
}
