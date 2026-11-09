using UnityEngine;

/// <summary>
/// 主人公の調整値(髙橋)。初期値は仕様書「パラメータ」の表に合わせてある。
/// 銘の効果はここに足さず、使うときに GameContext.MeiStats の倍率を掛ける。
/// </summary>
[CreateAssetMenu(fileName = "PlayerTuning", menuName = "Hitofuri/PlayerTuning")]
public sealed class PlayerTuning : ScriptableObject
{
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

    [Header("ヒットストップ")]
    [SerializeField, Min(0f)] float hitStopBase = 0.05f;
    [SerializeField, Min(0f)] float hitStopPerEnemy = 0.01f;
    [SerializeField, Min(0f)] float hitStopMax = 0.25f;

    [Header("斬り抜け演出")]
    [Tooltip("線上の敵がこの数以上なら定点カメラに切り替える")]
    [SerializeField, Min(1)] int cinematicThreshold = 5;
    [Tooltip("見下ろしへ戻るブレンドの秒数")]
    [SerializeField, Min(0f)] float cinematicBlendBack = 0.3f;
    // 演出中のスロー倍率と演出後の無敵は「テストで決める」なので仮の値
    [SerializeField, Range(0f, 1f)] float cinematicSlowScale = 0.3f;
    [SerializeField, Min(0f)] float postCinematicInvincible = 0.5f;

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

    public float MoveSpeed => moveSpeed;
    public float FreeHandSpeedRatio => freeHandSpeedRatio;
    public float PlantRecovery => plantRecovery;
    public float PlantTiltAngle => plantTiltAngle;
    public float SlashDuration => slashDuration;
    public float HitRecovery => hitRecovery;
    public float WhiffRecovery => whiffRecovery;
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
    public float HitStopBase => hitStopBase;
    public float HitStopPerEnemy => hitStopPerEnemy;
    public float HitStopMax => hitStopMax;
    public int CinematicThreshold => cinematicThreshold;
    public float CinematicBlendBack => cinematicBlendBack;
    public float CinematicSlowScale => cinematicSlowScale;
    public float PostCinematicInvincible => postCinematicInvincible;
    public float AnimationCrossFade => animationCrossFade;
    public float SlashCrossFade => slashCrossFade;
    public float RunBlendSpeed => runBlendSpeed;
    public float DamageBlinkInterval => damageBlinkInterval;
}
