/// <summary>
/// 小銘の種類。インスペクターで選んだ値は番号で保存されるので、途中に挟まず末尾に足す。
/// 必達は大断・剛力・疾風・長夜の4つ。残りは「できれば」で、MeiTuning で有効にしたときだけ候補に出る。
/// </summary>
public enum MinorMeiId
{
    None,
    /// <summary>大断：線の幅が広がる</summary>
    Daidan,
    /// <summary>剛力：吹き飛ばす力が上がる</summary>
    Goriki,
    /// <summary>疾風：手ぶらで走る速さが上がる</summary>
    Shippu,
    /// <summary>長夜：自動で斬り戻すまでの時間が延びる</summary>
    Choya,
    /// <summary>鋭光：光の蓄積が増える(できれば)</summary>
    Eiko,
    /// <summary>迅刃：斬り抜ける速さが上がる(できれば)</summary>
    Jinba,
    /// <summary>囮：灯喰らいの引き寄せ範囲が広がる(できれば)</summary>
    Otori,
    /// <summary>重刀(代償つき)：吹き飛ばす力が大きく上がり、手ぶらで走る速さが下がる</summary>
    Juto,
    /// <summary>細身(代償つき)：吹き飛ばす力が上がり、線の幅が狭まる</summary>
    Hosomi,
    /// <summary>捨て身(代償つき)：吹き飛ばす力が上がり、被弾できる回数が1減る</summary>
    Sutemi,
}
