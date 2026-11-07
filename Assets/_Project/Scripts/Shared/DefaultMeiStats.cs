/// <summary>
/// 銘が何もないときの値。増永さんの実装が入るまで GameContext.MeiStats に入っているので、
/// 髙橋と三條さんは待たずに倍率を掛ける形で書ける。
/// </summary>
public sealed class DefaultMeiStats : IMeiStats
{
    public static readonly DefaultMeiStats Instance = new DefaultMeiStats();

    public float LineWidthMultiplier => 1f;
    public float LaunchPowerMultiplier => 1f;
    public float FreeHandSpeedMultiplier => 1f;
    public float AutoReturnTimeMultiplier => 1f;
    public float SlashSpeedMultiplier => 1f;
    public float GlowGainMultiplier => 1f;
    public float LureRadiusMultiplier => 1f;
    public int MaxDamageOffset => 0;
    public bool HasReturnSlash => false;
}
