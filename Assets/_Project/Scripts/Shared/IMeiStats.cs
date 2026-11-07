/// <summary>
/// 銘の効果をまとめた値。基本値は各自の Tuning に置き、ここの倍率を掛けて使う。
/// 増永さんが小銘の合計を上限で切った値を返すので、読む側は自分で銘の計算をしない。
/// 使う瞬間に毎回読む(銘はプレイ中に変わるので、キャッシュしない)。
/// </summary>
public interface IMeiStats
{
    /// <summary>線の幅の倍率(大断、細身)</summary>
    float LineWidthMultiplier { get; }
    /// <summary>吹き飛ばし力の倍率(剛力、重刀、細身、捨て身)</summary>
    float LaunchPowerMultiplier { get; }
    /// <summary>手ぶらの移動速度の倍率(疾風、重刀)</summary>
    float FreeHandSpeedMultiplier { get; }
    /// <summary>自動で斬り戻すまでの時間の倍率(長夜)</summary>
    float AutoReturnTimeMultiplier { get; }
    /// <summary>斬り抜ける速さの倍率(迅刃)</summary>
    float SlashSpeedMultiplier { get; }
    /// <summary>光の蓄積量の倍率(鋭光)</summary>
    float GlowGainMultiplier { get; }
    /// <summary>灯喰らいの引き寄せ範囲の倍率(囮)</summary>
    float LureRadiusMultiplier { get; }
    /// <summary>被弾の上限に足す数(捨て身で -1)。基本の上限は PlayerTuning</summary>
    int MaxDamageOffset { get; }
    /// <summary>大銘「返し」が刻まれているか</summary>
    bool HasReturnSlash { get; }
}
