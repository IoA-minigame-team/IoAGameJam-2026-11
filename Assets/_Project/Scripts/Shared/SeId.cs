/// <summary>
/// 効果音のID。増永さんが直接項目を足してよい。
/// インスペクターで選んだ値は番号で保存されるので、途中に挟まず末尾に足す。
/// </summary>
public enum SeId
{
    None,
    /// <summary>刀が刺さる</summary>
    SwordPlant,
    /// <summary>斬り抜け</summary>
    Slash,
    /// <summary>敵に当たった</summary>
    Hit,
    /// <summary>納刀</summary>
    Sheathe,
    /// <summary>空振り(鈍い音)</summary>
    Whiff,
    /// <summary>主人公の被弾</summary>
    PlayerDamaged,
    /// <summary>敵が縁を越えた(「抜けた」と分かる音)</summary>
    EdgeCross,
}
