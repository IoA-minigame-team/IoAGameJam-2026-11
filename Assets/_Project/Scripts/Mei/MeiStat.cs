/// <summary>
/// 小銘が上げる能力の種類。IMeiStats の倍率と1対1で対応する。
/// 小銘ごとに「どの能力を上げるか」を MeiTuning で選べるようにするために分けた。
/// </summary>
public enum MeiStat
{
    /// <summary>線の幅(大断)</summary>
    LineWidth,
    /// <summary>吹き飛ばし力(剛力)</summary>
    LaunchPower,
    /// <summary>手ぶらの移動速度(疾風)</summary>
    FreeHandSpeed,
    /// <summary>自動で斬り戻すまでの時間(長夜)</summary>
    AutoReturnTime,
    /// <summary>斬り抜ける速さ(迅刃)</summary>
    SlashSpeed,
    /// <summary>光の蓄積量(鋭光)</summary>
    GlowGain,
    /// <summary>灯喰らいの引き寄せ範囲(囮)</summary>
    LureRadius,
}
