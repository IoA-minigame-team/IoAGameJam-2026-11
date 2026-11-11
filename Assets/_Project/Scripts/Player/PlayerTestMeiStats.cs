using UnityEngine;

/// <summary>
/// テスト用の銘の値。増永さんの銘の実装ができるまで、銘が効いたときの主人公の動きを確かめるために使う。本番では使わない。
/// 有効な間だけ GameContext.MeiStats に入る。MeiStats は使う瞬間に毎回読まれるので、プレイ中に値を変えればすぐ効く。
/// </summary>
public sealed class PlayerTestMeiStats : MonoBehaviour, IMeiStats
{
    [Header("倍率(銘なしは 1)")]
    [SerializeField, Min(0.01f)] float lineWidthMultiplier = 1f;
    [SerializeField, Min(0.01f)] float launchPowerMultiplier = 1f;
    [SerializeField, Min(0.01f)] float freeHandSpeedMultiplier = 1f;
    [SerializeField, Min(0.01f)] float autoReturnTimeMultiplier = 1f;
    [SerializeField, Min(0.01f)] float slashSpeedMultiplier = 1f;
    [SerializeField, Min(0.01f)] float glowGainMultiplier = 1f;
    [SerializeField, Min(0.01f)] float lureRadiusMultiplier = 1f;

    [Header("増減と大銘")]
    [Tooltip("被弾の上限に足す数。捨て身は -1")]
    [SerializeField] int maxDamageOffset;
    [Tooltip("大銘「返し」")]
    [SerializeField] bool hasReturnSlash;

    public float LineWidthMultiplier => lineWidthMultiplier;
    public float LaunchPowerMultiplier => launchPowerMultiplier;
    public float FreeHandSpeedMultiplier => freeHandSpeedMultiplier;
    public float AutoReturnTimeMultiplier => autoReturnTimeMultiplier;
    public float SlashSpeedMultiplier => slashSpeedMultiplier;
    public float GlowGainMultiplier => glowGainMultiplier;
    public float LureRadiusMultiplier => lureRadiusMultiplier;
    public int MaxDamageOffset => maxDamageOffset;
    public bool HasReturnSlash => hasReturnSlash;

    void OnEnable() => GameContext.MeiStats = this;

    void OnDisable()
    {
        // 他の誰かが入れ替えていたら、それは消さない
        if (ReferenceEquals(GameContext.MeiStats, this)) GameContext.MeiStats = null;
    }
}
