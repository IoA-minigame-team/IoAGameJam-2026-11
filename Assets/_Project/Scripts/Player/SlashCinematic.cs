using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 斬り抜け演出のカメラ。線上の敵が多い一振りだけ、見下ろしから演出用のカメラへハードカットする。
/// 流れ: 離した瞬間にスロー → 肩越しで駆け込みを追う → 途中で終点の寄り(定点)へカット →
/// 硬直 → 納刀を見せる間 → 見下ろしへブレンド → 短い無敵。
/// 大銘「返し」があるときは、1回目は肩越しのまま終点まで追い、返しを肩越し → 返しの終点の寄りで撮り直す。
/// 終点の寄りのカメラと同じ GameObject に付け、シーンに置く(主人公の Prefab にシーンの参照を持たせないため)。
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public sealed class SlashCinematic : MonoBehaviour
{
    enum Phase
    {
        /// <summary>演出していない</summary>
        Idle,
        /// <summary>肩越しカメラで駆け込みを追う</summary>
        RunIn,
        /// <summary>終点の寄りのカメラで、斬り抜けと硬直が終わるのを待つ</summary>
        Slashing,
        /// <summary>硬直が明けた後、寄りのカメラのまま納刀を見せる</summary>
        SheatheHold,
        /// <summary>見下ろしへブレンドで戻している</summary>
        BlendBack,
    }

    // これより短い線は向きが決まらないので、主人公の向きを使う
    const float MIN_DIRECTION_LENGTH = 0.0001f;

    [SerializeField] PlayerTuning tuning;
    [Tooltip("演出を出す主人公")]
    [SerializeField] PlayerController player;
    [Tooltip("ブレンドの切り替えに使う。空なら Camera.main の CinemachineBrain")]
    [SerializeField] CinemachineBrain brain;
    [Tooltip("駆け込みを追う肩越しカメラ。空なら最初から終点の寄りで撮る")]
    [SerializeField] CinemachineCamera runInCamera;

    // この GameObject のカメラ。終点の寄りを撮る
    CinemachineCamera closeCamera;
    PlayerHealth playerHealth;
    CinemachineBlendDefinition originalBlend;
    bool hasOriginalBlend;
    Phase phase;
    float phaseTimer;
    // 一振りの間は同じ向きと横を使う。2台が線の同じ側から撮り、走る向きが画面で逆にならないようにするため
    Vector3 slashDirection;
    Vector3 slashSide;
    // 一振りの線(1回目の始点と終点)。返しは逆にたどるので、撮り直すときに入れ替えて使う
    Vector3 lineStart;
    Vector3 lineEnd;
    // 返しの斬り抜けを撮っているか
    bool shootingReturn;

    /// <summary>演出の最中か(見下ろしへ戻るブレンドを含む)</summary>
    public bool IsPlaying => phase != Phase.Idle;

    void Awake()
    {
        closeCamera = GetComponent<CinemachineCamera>();
        // 普段は切っておき、演出のときだけ有効にして見下ろしのカメラより優先させる
        closeCamera.enabled = false;
        if (runInCamera != null) runInCamera.enabled = false;

        if (brain == null && Camera.main != null) brain = Camera.main.GetComponent<CinemachineBrain>();
        if (player != null) playerHealth = player.GetComponent<PlayerHealth>();

        // スロー中もカメラを動かす決まりなので、Brain が timeScale に従っているとブレンドが遅れる
        if (brain != null && !brain.IgnoreTimeScale)
        {
            Debug.LogWarning("SlashCinematic: CinemachineBrain の Ignore Time Scale を有効にしてください。スロー中にカメラのブレンドが遅れます", brain);
        }
    }

    void OnEnable()
    {
        if (player != null) player.SlashBegan += OnSlashBegan;
        GameEvents.GameOver += OnGameOver;
    }

    void OnDisable()
    {
        if (player != null) player.SlashBegan -= OnSlashBegan;
        GameEvents.GameOver -= OnGameOver;
        StopImmediately();
    }

    void Update()
    {
        if (phase == Phase.Idle) return;
        // 銘の選択などで止まっている間は演出も進めない
        if (TimeController.IsPaused) return;

        // スロー中も決めた秒数で進めるため、実時間で数える
        float dt = Time.unscaledDeltaTime;

        switch (phase)
        {
            case Phase.RunIn:
                if (ShouldStartReturnShots())
                {
                    StartReturnShots();
                }
                // 線の途中まで来たら寄りへ。返しが控えている1回目は寄りを飛ばし、肩越しのまま終点まで追う
                else if (player.CurrentState != PlayerController.State.Slashing
                    || (player.SlashPassProgress >= tuning.CinematicRunInSwitchRatio && !player.HasPendingReturnSlash))
                {
                    CutToClose();
                }
                break;

            case Phase.Slashing:
                // 肩越しを使わない設定でも、返しでは寄りを返しの終点側へ置き直す
                if (ShouldStartReturnShots())
                {
                    StartReturnShots();
                }
                // 硬直は返しの後だけなので、硬直明けまで待てばよい
                else if (player.IsRecoveryFinished)
                {
                    phase = Phase.SheatheHold;
                    phaseTimer = tuning.CinematicSheatheHold;
                }
                break;

            case Phase.SheatheHold:
                phaseTimer -= dt;
                if (phaseTimer <= 0f) BeginBlendBack();
                break;

            case Phase.BlendBack:
                phaseTimer -= dt;
                if (phaseTimer <= 0f) Finish();
                break;
        }
    }

    // 主人公は Update で動くので、LateUpdate で追う。Brain(実行順 100 の LateUpdate)より先に動くので同じフレームに間に合う
    void LateUpdate()
    {
        if (phase == Phase.RunIn) PlaceShoulderCamera(false);
    }

    // 発動は1回目の斬り抜けを始めた瞬間の数だけで決める。返しでは SlashBegan が流れないので判定し直さない
    void OnSlashBegan(Vector3 start, Vector3 end, int targetCount)
    {
        if (tuning == null || player == null) return;
        // 演出中は主人公を止めているので来ないはずだが、来ても重ねて始めない
        if (phase != Phase.Idle) return;
        if (targetCount < tuning.CinematicThreshold) return;

        Begin(start, end);
    }

    void Begin(Vector3 start, Vector3 end)
    {
        TimeController.RequestSlow(this, tuning.CinematicSlowScale);
        player.HoldControl(this);
        // 演出中の被弾は起こさない(気持ちよさの方針「見せ場でも理不尽にしない」)
        if (playerHealth != null) playerHealth.RequestInvincible(this);

        lineStart = start;
        lineEnd = end;
        shootingReturn = false;
        StartPassShots(start, end);
    }

    bool ShouldStartReturnShots() => player.IsReturnSlashing && !shootingReturn;

    // 返しは線を逆にたどるので、向きを逆にして刀を刺した位置(返しの終点)側から撮り直す
    void StartReturnShots()
    {
        shootingReturn = true;
        StartPassShots(lineEnd, lineStart);
    }

    // 1回分の斬り抜けを撮り始める。肩越しが使えれば肩越しから、使えなければ終点の寄りから
    void StartPassShots(Vector3 start, Vector3 end)
    {
        // 横は逆向きでも画面の手前側を選ぶので、返しも1回目と同じ側から撮る(画面の上で逆向きに走って見える)
        DecideSlashAxes(start, end);
        PlaceCloseCamera(start, end);

        CinemachineBlendDefinition cut = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        if (runInCamera != null && tuning.CinematicRunInSwitchRatio > 0f)
        {
            PlaceShoulderCamera(true);
            SwitchCamera(runInCamera, cut);
            phase = Phase.RunIn;
        }
        else
        {
            SwitchCamera(closeCamera, cut);
            phase = Phase.Slashing;
        }
    }

    // 既定のブレンドは Begin で Cut にしたままなので、ハードカットになる
    void CutToClose()
    {
        SwitchCamera(closeCamera, new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f));
        phase = Phase.Slashing;
    }

    void BeginBlendBack()
    {
        TimeController.ReleaseSlow(this);
        SwitchCamera(null, new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, tuning.CinematicBlendBack));
        // ブレンドが始まったら動けるようにする。移動は見下ろしのカメラを基準にしているので向きはずれない
        player.ReleaseControl(this);

        phase = Phase.BlendBack;
        phaseTimer = tuning.CinematicBlendBack;
    }

    void Finish()
    {
        RestoreBlend();
        if (playerHealth != null)
        {
            // 戻り終えた直後の被弾も起こさない。無敵を切らさないよう、解除より先に足す
            playerHealth.GrantInvincible(tuning.PostCinematicInvincible);
            playerHealth.ReleaseInvincible(this);
        }
        phase = Phase.Idle;
    }

    // ゲームオーバーで止まった主人公を演出のカメラで映し続けないよう、すぐ見下ろしへ戻し始める
    void OnGameOver()
    {
        if (phase == Phase.Idle || phase == Phase.BlendBack) return;
        BeginBlendBack();
    }

    // 無効にされたときは Update が来ないので、その場で全部戻す
    void StopImmediately()
    {
        if (phase == Phase.Idle) return;

        TimeController.ReleaseSlow(this);
        if (closeCamera != null) closeCamera.enabled = false;
        if (runInCamera != null) runInCamera.enabled = false;
        RestoreBlend();
        if (player != null) player.ReleaseControl(this);
        if (playerHealth != null) playerHealth.ReleaseInvincible(this);
        phase = Phase.Idle;
    }

    // 置き場所は崖ではなく、斬り抜ける向きと終点から決める。ステージの形に頼らないため
    void DecideSlashAxes(Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;
        direction.y = 0f;
        if (direction.sqrMagnitude < MIN_DIRECTION_LENGTH)
        {
            direction = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
            if (direction.sqrMagnitude < MIN_DIRECTION_LENGTH) direction = Vector3.forward;
        }
        slashDirection = direction.normalized;

        // 線の横のうち、見下ろしのカメラ側(画面の手前)を選ぶ。切り替わっても左右が大きく入れ替わらないようにするため
        Vector3 side = Vector3.Cross(Vector3.up, slashDirection);
        Vector3 viewForward = brain != null ? Vector3.ProjectOnPlane(brain.transform.forward, Vector3.up) : Vector3.forward;
        if (Vector3.Dot(side, viewForward) > 0f) side = -side;
        slashSide = side;
    }

    void PlaceCloseCamera(Vector3 start, Vector3 end)
    {
        // 線の真横(画面の手前側)から、斬り抜ける向きの先へ角度の分だけ回り込む
        float angle = tuning.CinematicCameraAngle * Mathf.Deg2Rad;
        Vector3 offset = (slashSide * Mathf.Cos(angle) + slashDirection * Mathf.Sin(angle)) * tuning.CinematicCameraDistance;
        Vector3 position = end + offset + Vector3.up * tuning.CinematicCameraHeight;
        Vector3 lookAt = Vector3.LerpUnclamped(start, end, tuning.CinematicLookAtRatio)
            + Vector3.up * tuning.CinematicLookHeight;
        PlaceCamera(closeCamera, position, lookAt, true);
    }

    // 主人公の斜め後ろ上に付いて、斬り抜ける向きの先を見る。force は前の位置から補間させないとき
    void PlaceShoulderCamera(bool force)
    {
        Vector3 hero = player.transform.position;
        Vector3 position = hero
            - slashDirection * tuning.CinematicShoulderBack
            + slashSide * tuning.CinematicShoulderSide
            + Vector3.up * tuning.CinematicShoulderHeight;
        Vector3 lookAt = hero
            + slashDirection * tuning.CinematicShoulderLookAhead
            + Vector3.up * tuning.CinematicShoulderLookHeight;
        PlaceCamera(runInCamera, position, lookAt, force);
    }

    void PlaceCamera(CinemachineCamera target, Vector3 position, Vector3 lookAt, bool force)
    {
        Vector3 lookDirection = lookAt - position;
        Quaternion rotation = lookDirection.sqrMagnitude > MIN_DIRECTION_LENGTH
            ? Quaternion.LookRotation(lookDirection, Vector3.up)
            : Quaternion.LookRotation(slashDirection, Vector3.up);

        target.transform.SetPositionAndRotation(position, rotation);
        // 前回の演出の位置から補間しないよう、カメラの状態も合わせる
        if (force) target.ForceCameraPosition(position, rotation);
    }

    // 有効にするカメラを1台だけにする(null なら全部切って見下ろしへ戻す)。
    // Brain は有効なカメラが変わった次の更新でブレンドを選ぶので、切り替える前に既定のブレンドを差し替えておく
    void SwitchCamera(CinemachineCamera active, CinemachineBlendDefinition blend)
    {
        if (brain != null)
        {
            if (!hasOriginalBlend)
            {
                originalBlend = brain.DefaultBlend;
                hasOriginalBlend = true;
            }
            brain.DefaultBlend = blend;
        }
        closeCamera.enabled = active == closeCamera;
        if (runInCamera != null) runInCamera.enabled = active == runInCamera;
    }

    void RestoreBlend()
    {
        if (brain != null && hasOriginalBlend) brain.DefaultBlend = originalBlend;
        hasOriginalBlend = false;
    }
}
