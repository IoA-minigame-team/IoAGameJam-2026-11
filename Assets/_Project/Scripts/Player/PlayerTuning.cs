using UnityEngine;

/// <summary>
/// 主人公の調整値(髙橋)。初期値は仕様書「パラメータ」の表に合わせてある。
/// 銘の効果はここに足さず、使うときに GameContext.MeiStats の倍率を掛ける。
/// </summary>
[CreateAssetMenu(fileName = "PlayerTuning", menuName = "Hitofuri/PlayerTuning")]
public sealed class PlayerTuning : ScriptableObject
{
    // 演出のスロー倍率の下限。0 だと Time.deltaTime で進む斬り抜けと硬直が止まり、
    // 演出は硬直の終わりを待ってからスローを解くので、操作できないまま固まる
    const float MIN_CINEMATIC_SLOW_SCALE = 0.05f;

    [Header("移動")]
    // 仕様に「通常の速度」の数値はないので仮の値
    [SerializeField, Min(0f)] float moveSpeed = 5f;
    [Tooltip("手ぶらの移動速度。通常の速度に掛ける")]
    [SerializeField, Min(0f)] float freeHandSpeedRatio = 1.3f;

    [Header("突き立てと斬り戻し")]
    [Tooltip("突き立てる瞬間の隙(秒)")]
    [SerializeField, Min(0f)] float plantRecovery = 0.1f;
    // 見下ろしのカメラからはまっすぐ立てた刀が点にしか見えないので、少し傾けて刃を見せる
    [Tooltip("突き立てた刀を傾ける角度(度)。刀の頭がカメラから見て奥へ倒れる")]
    [SerializeField, Range(0f, 45f)] float plantTiltAngle = 20f;
    [Tooltip("斬り戻しの所要時間(秒)。瞬間移動と斬り抜けを含み、この間は無敵")]
    [SerializeField, Min(0.01f)] float slashDuration = 0.2f;
    [Tooltip("斬り戻し後の硬直(秒)、1体以上当たったとき")]
    [SerializeField, Min(0f)] float hitRecovery = 0.1f;
    [Tooltip("斬り戻し後の硬直(秒)、空振りのとき")]
    [SerializeField, Min(0f)] float whiffRecovery = 0.6f;
    // 仕様は「斬り抜けた直後」とだけあるので仮の値。切り返しの溜めを見せたくなったら増やす
    [Tooltip("大銘「返し」で、1回目を斬り抜けてから逆向きに斬り抜け始めるまでの間(秒)。この間も無敵")]
    [SerializeField, Min(0f)] float returnSlashDelay = 0f;

    [Header("線と吹き飛ばし")]
    [Tooltip("線の幅(m)")]
    [SerializeField, Min(0f)] float lineWidth = 1.2f;
    [Tooltip("基本の吹き飛ばし力(m/s)")]
    [SerializeField, Min(0f)] float baseLaunchPower = 6f;
    [Tooltip("線1mごとに吹き飛ばし力に足す割合。0.1 で +10%")]
    [SerializeField, Min(0f)] float lengthBonusPerMeter = 0.1f;
    [Tooltip("線の端で当たったときの、斬り抜ける向きからの逸れ(度)")]
    [SerializeField, Range(0f, 90f)] float edgeDeflectAngle = 90f;
    // 仕様は「端ほど弱く」とだけあるので仮の値
    [Tooltip("線の端で当たったときの吹き飛ばし力の割合。中央で 1")]
    [SerializeField, Range(0f, 1f)] float edgePowerRatio = 0.5f;

    [Header("自動の斬り戻し")]
    // パラメータ表では EnemyTuning だが、数えるのは主人公側なのでここに置く(朝会で共有)
    [Tooltip("突き立ててから自動で斬り戻すまでの時間(秒)")]
    [SerializeField, Min(0f)] float autoReturnTime = 3f;
    [Tooltip("刀に群がる灯喰らい1体ごとに縮める秒数")]
    [SerializeField, Min(0f)] float autoReturnReductionPerSwarm = 0.3f;
    [Tooltip("自動で斬り戻すまでの時間の下限(秒)")]
    [SerializeField, Min(0f)] float autoReturnMinTime = 1f;

    [Header("被弾")]
    [Tooltip("被弾の上限。銘の増減は MeiStats.MaxDamageOffset を足す")]
    [SerializeField, Min(1)] int maxDamageCount = 3;
    // 仕様にない値。敵に触れ続けたとき、一度に何回も被弾しないようにする
    [Tooltip("被弾した後の無敵(秒)")]
    [SerializeField, Min(0f)] float damageInvincibleTime = 1f;
    // 仕様にない値。場外へ落ちたら被弾1回として開始位置へ戻す。浅いと段差を降りただけで場外になるので、島の高低差より深くする
    [Tooltip("開始位置からこれだけ下まで落ちたら場外とみなす(m)")]
    [SerializeField, Min(0.1f)] float fallOutDepth = 5f;

    [Header("ヒットストップ")]
    [SerializeField, Min(0f)] float hitStopBase = 0.05f;
    [SerializeField, Min(0f)] float hitStopPerEnemy = 0.01f;
    [SerializeField, Min(0f)] float hitStopMax = 0.25f;
    // ここから下は仕様にない値。初撃だけだとカクつきに見えるので、2体目以降も1体ごとに短く止める
    [Tooltip("2体目の当たりの止まり(秒)")]
    [SerializeField, Min(0f)] float hitStopChain = 0.04f;
    [Tooltip("3体目以降、1体ごとに止まりへ掛ける倍率。群れでリズムが重くなりすぎないよう、だんだん短くする")]
    [SerializeField, Range(0f, 1f)] float hitStopChainDecay = 0.85f;
    [Tooltip("2体目以降の1回の止まりの下限(秒)")]
    [SerializeField, Min(0f)] float hitStopChainMin = 0.02f;
    [Tooltip("1回の斬り抜けで止まる合計の上限(秒)。返しは別に数える")]
    [SerializeField, Min(0f)] float hitStopPassMax = 0.35f;
    [Tooltip("初撃の揺れの強さ。止まった画に当たった印を出すため")]
    [SerializeField, Min(0f)] float hitShakeFirst = 0.25f;
    [Tooltip("2体目以降の揺れの強さ")]
    [SerializeField, Min(0f)] float hitShakeChain = 0.12f;
    [Tooltip("当たりの揺れの長さ(秒)")]
    [SerializeField, Min(0f)] float hitShakeDuration = 0.12f;
    [Tooltip("当たりのエフェクトを敵の位置からどれだけ上に出すか(m)。敵の原点は足元なので、そのままだと地面に埋まる")]
    [SerializeField] float hitVfxHeight = 1f;

    [Header("納刀")]
    // 仕様は「納刀音と小さなヒットストップ」だけで値はないので仮の値
    [Tooltip("当たった一振りの終点から、鍔が鳴る(納刀の音と小さなヒットストップ)までの秒数。振り抜きの保持と納刀の動きに合わせる")]
    [SerializeField, Min(0f)] float sheatheClickDelay = 0.3f;
    [Tooltip("納刀のヒットストップ(秒)。当たりのヒットストップより短くして、締めの小さな間にする")]
    [SerializeField, Min(0f)] float sheatheHitStop = 0.06f;

    [Header("残像")]
    // 仕様は「残像」だけで値はないので仮の値
    [Tooltip("斬り抜け中、前の残像からこの距離(m)進むごとに体の形を残す")]
    [SerializeField, Min(0.05f)] float afterimageSpacing = 0.8f;
    [Tooltip("残像が消えるまでの秒数(scaled time。ヒットストップ中は止まる)")]
    [SerializeField, Min(0.01f)] float afterimageLifetime = 0.25f;
    [Tooltip("同時に出せる残像の数。足りないときは古いものから使い回す")]
    [SerializeField, Range(1, 32)] int afterimageMaxCount = 12;

    [Header("斬り抜け演出")]
    [Tooltip("線上の敵がこの数以上なら定点カメラに切り替える")]
    [SerializeField, Min(1)] int cinematicThreshold = 5;
    [Tooltip("見下ろしへ戻るブレンドの秒数")]
    [SerializeField, Min(0f)] float cinematicBlendBack = 0.3f;
    // 演出中のスロー倍率と演出後の無敵は「テストで決める」なので仮の値
    [Tooltip("演出中の時間の倍率。離した瞬間から見下ろしへ戻り始めるまで")]
    [SerializeField, Range(MIN_CINEMATIC_SLOW_SCALE, 1f)] float cinematicSlowScale = 0.3f;
    [Tooltip("見下ろしへ戻り終えてからの無敵(秒)。演出の始まりから戻り終えるまでも無敵")]
    [SerializeField, Min(0f)] float postCinematicInvincible = 0.5f;
    // 定点カメラの置き場所は仕様で「任せる」なので仮の値。終点と斬り抜ける向きだけから決め、ステージの形には頼らない
    // 寄り具合を保ったまま回り込みだけ変えられるよう、距離と角度で持つ
    [Tooltip("終点の寄りのカメラの、終点からの水平の距離(m)")]
    [SerializeField, Min(0f)] float cinematicCameraDistance = 3.1f;
    [Tooltip("終点の寄りのカメラを置く向き(度)。0 で線の真横(画面の手前側)、90 で真正面(斬り抜ける向きの先)。負なら背中側へ回る")]
    [SerializeField, Range(-90f, 90f)] float cinematicCameraAngle = 45f;
    [Tooltip("定点カメラの高さ(終点からの高さ、m)")]
    [SerializeField] float cinematicCameraHeight = 2.5f;
    [Tooltip("定点カメラが見る位置。線の始点 0 から終点 1 のどこを見るか。駆け込みと止まる瞬間の両方を収める")]
    [SerializeField, Range(0f, 1.5f)] float cinematicLookAtRatio = 0.85f;
    [Tooltip("定点カメラが見る位置の高さ(線からの高さ、m)")]
    [SerializeField] float cinematicLookHeight = 0f;
    // 硬直 0.1 秒だけでは納刀が見えないので、演出のときだけ定点カメラのまま少し待つ
    [Tooltip("硬直が明けてから見下ろしへ戻り始めるまで、定点カメラのまま待つ秒数(実時間)。この間も主人公は動けない")]
    [SerializeField, Min(0f)] float cinematicSheatheHold = 0.4f;

    [Header("斬り抜け演出の駆け込み(肩越しカメラ)")]
    // 駆け込みは肩越しで追い、途中で終点の寄りへ切り替える。値は仕様で「任せる」なので仮の値
    [Tooltip("1回目の斬り抜けが線のこの割合まで進んだら、肩越しから終点の寄りへ切り替える。0 なら肩越しを使わない")]
    [SerializeField, Range(0f, 1f)] float cinematicRunInSwitchRatio = 0.5f;
    [Tooltip("肩越しカメラを主人公の後ろへ離す距離(m)")]
    [SerializeField] float cinematicShoulderBack = 2.5f;
    [Tooltip("肩越しカメラを横へずらす距離(m)。定点カメラと同じ側(画面の手前)へずらす")]
    [SerializeField] float cinematicShoulderSide = 1f;
    [Tooltip("肩越しカメラの高さ(主人公の体の中心からの高さ、m)")]
    [SerializeField] float cinematicShoulderHeight = 1.2f;
    [Tooltip("肩越しカメラが見る位置を、主人公からどれだけ前に置くか(m)")]
    [SerializeField] float cinematicShoulderLookAhead = 4f;
    [Tooltip("肩越しカメラが見る位置の高さ(主人公の体の中心からの高さ、m)")]
    [SerializeField] float cinematicShoulderLookHeight = 0f;

    [Header("見た目")]
    // 見た目の値は仕様にないので仮の値。手触りを見ながら決める
    [Tooltip("アニメーションを切り替えるときのクロスフェード(秒)")]
    [SerializeField, Min(0f)] float animationCrossFade = 0.08f;
    [Tooltip("斬り抜けに入るときのクロスフェード(秒)。瞬間移動に合わせて短くする")]
    [SerializeField, Min(0f)] float slashCrossFade = 0f;
    [Tooltip("走りのブレンドが最大になる速さ(m/s)。手ぶらの速さに合わせる")]
    [SerializeField, Min(0.01f)] float runBlendSpeed = 6.5f;
    [Tooltip("被弾後の無敵の間に点滅する間隔(秒)")]
    [SerializeField, Min(0.01f)] float damageBlinkInterval = 0.08f;
    [Tooltip("突き立てた姿勢を、手ぶらになってからも見た目だけ残す長さ(秒)。動き出したらすぐ切り替える")]
    [SerializeField, Min(0f)] float plantPoseHold = 0.2f;
    [Tooltip("斬り抜けの振り抜いた姿勢を、硬直に入ってから見た目だけ残す長さ(秒)")]
    [SerializeField, Min(0f)] float slashPoseHold = 0.1f;

    public float MoveSpeed => moveSpeed;
    public float FreeHandSpeedRatio => freeHandSpeedRatio;
    public float PlantRecovery => plantRecovery;
    public float PlantTiltAngle => plantTiltAngle;
    public float SlashDuration => slashDuration;
    public float HitRecovery => hitRecovery;
    public float WhiffRecovery => whiffRecovery;
    public float ReturnSlashDelay => returnSlashDelay;
    public float LineWidth => lineWidth;
    public float BaseLaunchPower => baseLaunchPower;
    public float LengthBonusPerMeter => lengthBonusPerMeter;
    public float EdgeDeflectAngle => edgeDeflectAngle;
    public float EdgePowerRatio => edgePowerRatio;
    public float AutoReturnTime => autoReturnTime;
    public float AutoReturnReductionPerSwarm => autoReturnReductionPerSwarm;
    public float AutoReturnMinTime => autoReturnMinTime;
    public int MaxDamageCount => maxDamageCount;
    public float DamageInvincibleTime => damageInvincibleTime;
    public float FallOutDepth => Mathf.Max(0.1f, fallOutDepth);
    public float HitStopBase => hitStopBase;
    public float HitStopPerEnemy => hitStopPerEnemy;
    public float HitStopMax => hitStopMax;
    public float HitStopChain => hitStopChain;
    public float HitStopChainDecay => hitStopChainDecay;
    public float HitStopChainMin => hitStopChainMin;
    public float HitStopPassMax => hitStopPassMax;
    public float HitShakeFirst => hitShakeFirst;
    public float HitShakeChain => hitShakeChain;
    public float HitShakeDuration => hitShakeDuration;
    public float HitVfxHeight => hitVfxHeight;
    public float SheatheClickDelay => sheatheClickDelay;
    public float SheatheHitStop => sheatheHitStop;
    public float AfterimageSpacing => afterimageSpacing;
    public float AfterimageLifetime => afterimageLifetime;
    public int AfterimageMaxCount => afterimageMaxCount;
    public int CinematicThreshold => cinematicThreshold;
    public float CinematicBlendBack => cinematicBlendBack;
    // Range はインスペクターでしか効かず、アセットに 0 が残っていても通るので、読み出しでも下限で切る
    public float CinematicSlowScale => Mathf.Clamp(cinematicSlowScale, MIN_CINEMATIC_SLOW_SCALE, 1f);
    public float PostCinematicInvincible => postCinematicInvincible;
    public float CinematicCameraDistance => cinematicCameraDistance;
    public float CinematicCameraAngle => cinematicCameraAngle;
    public float CinematicCameraHeight => cinematicCameraHeight;
    public float CinematicLookAtRatio => cinematicLookAtRatio;
    public float CinematicLookHeight => cinematicLookHeight;
    public float CinematicSheatheHold => cinematicSheatheHold;
    public float CinematicRunInSwitchRatio => cinematicRunInSwitchRatio;
    public float CinematicShoulderBack => cinematicShoulderBack;
    public float CinematicShoulderSide => cinematicShoulderSide;
    public float CinematicShoulderHeight => cinematicShoulderHeight;
    public float CinematicShoulderLookAhead => cinematicShoulderLookAhead;
    public float CinematicShoulderLookHeight => cinematicShoulderLookHeight;
    public float AnimationCrossFade => animationCrossFade;
    public float SlashCrossFade => slashCrossFade;
    public float RunBlendSpeed => runBlendSpeed;
    public float DamageBlinkInterval => damageBlinkInterval;
    public float PlantPoseHold => plantPoseHold;
    public float SlashPoseHold => slashPoseHold;
}
