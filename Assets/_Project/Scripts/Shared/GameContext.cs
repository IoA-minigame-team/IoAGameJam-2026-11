using UnityEngine;

/// <summary>
/// 担当をまたいで「今の状態」を読むための置き場。値を書くのは各項目の持ち主だけで、他の人は読むだけ。
/// イベントだけだと、途中で湧いた敵が刀の状態を知れないので、状態もここに置く。
/// </summary>
public static class GameContext
{
    static IMeiStats meiStats = DefaultMeiStats.Instance;

    /// <summary>
    /// 銘の効果(倍率)。書くのは増永さん。null を入れると銘なしの値に戻るので、読む側は null を気にしなくてよい。
    /// </summary>
    public static IMeiStats MeiStats
    {
        get => meiStats;
        set => meiStats = value ?? DefaultMeiStats.Instance;
    }

    /// <summary>主人公の Transform。書くのは髙橋。敵が主人公を追うときに使う。主人公がいなければ null</summary>
    public static Transform PlayerTransform { get; set; }

    /// <summary>刀が地面に刺さっているか。書くのは髙橋</summary>
    public static bool IsSwordPlanted { get; set; }

    /// <summary>刀の位置。書くのは髙橋。刺さっていない間は主人公とともに動く</summary>
    public static Vector3 SwordPosition { get; set; }

    /// <summary>
    /// 自動で斬り戻すまでの残り秒数。書くのは髙橋。刀が刺さっていない間は 0。
    /// 残り時間の見せ方(増永さん)が、主人公のクラスを参照せずに読めるようにするため
    /// </summary>
    public static float AutoReturnRemaining { get; set; }

    /// <summary>
    /// 自動で斬り戻すまでの今の制限時間(秒)。書くのは髙橋。刀が刺さっていない間は 0。
    /// 群がり数と銘で途中でも変わる。残り時間をゲージの割合で見せるとき、計算式を持たずに済むようにするため
    /// </summary>
    public static float AutoReturnLimit { get; set; }

    /// <summary>
    /// 今の遊び方。書くのは増永さん(タイトル)。シーンをまたいで残るので、結果画面の「もう一度」は同じ遊び方で始まる。
    /// ウェーブ、得点、銘の各担当が、主人公やタイトルのクラスを参照せずに分岐できるようにここに置く
    /// </summary>
    public static GameMode Mode { get; set; }

    // ドメインリロードを切った再生でも、前回の状態が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        meiStats = DefaultMeiStats.Instance;
        PlayerTransform = null;
        IsSwordPlanted = false;
        SwordPosition = Vector3.zero;
        AutoReturnRemaining = 0f;
        AutoReturnLimit = 0f;
        Mode = GameMode.Clear;
    }
}
