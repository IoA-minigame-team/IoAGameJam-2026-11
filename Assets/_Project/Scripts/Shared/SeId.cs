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
    // ---- 増永が追加 ----
    /// <summary>落ちた敵が霧の中で消える</summary>
    Fall,
    /// <summary>敵が縁で踏みとどまった</summary>
    Teeter,
    /// <summary>雷鳴(タイトルで刀に雷が落ちる、リザルト)</summary>
    Thunder,
    /// <summary>雷のバチバチ(大銘「残光」)</summary>
    Zap,
    /// <summary>銘の選択画面が開いた</summary>
    MeiOpen,
    /// <summary>銘を選んだ</summary>
    MeiPick,
    /// <summary>称号が出た(一閃、断ち、一刀)</summary>
    Callout,
    /// <summary>UIの選択が動いた</summary>
    UiMove,
    /// <summary>ウェーブが始まった</summary>
    WaveStart,
    /// <summary>大銘「震」の衝撃</summary>
    Shin,
}
