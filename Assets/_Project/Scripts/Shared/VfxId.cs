/// <summary>
/// エフェクトのID。増永さんが直接項目を足してよい。
/// インスペクターで選んだ値は番号で保存されるので、途中に挟まず末尾に足す。
/// </summary>
public enum VfxId
{
    None,
    /// <summary>刀を置いたときの光の輪</summary>
    LightRing,
    /// <summary>斬り抜けの光の軌跡</summary>
    SlashTrail,
    /// <summary>敵に当たった</summary>
    Hit,
    /// <summary>落ちた敵が光の粒になって消える</summary>
    FallSpark,
}
