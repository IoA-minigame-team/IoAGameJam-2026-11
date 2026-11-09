/// <summary>
/// ゲームの結果を ResultScene へ渡すための置き場。シーンをまたいで残すため static にしてある。
/// 書くのは ScoreCounter(ゲームが終わったとき)、読むのは ResultScreen。
/// </summary>
public static class GameResult
{
    public static bool HasResult { get; private set; }
    public static bool Cleared { get; private set; }
    public static int Score { get; private set; }
    public static int MaxSwing { get; private set; }
    public static int MaxChain { get; private set; }
    public static int Falls { get; private set; }
    public static int WaveReached { get; private set; }
    public static MajorMeiId Major { get; private set; }
    /// <summary>この結果で自己ベストを更新したか</summary>
    public static bool IsNewBest { get; private set; }

    public static void Set(bool cleared, int score, int maxSwing, int maxChain, int falls, int waveReached, MajorMeiId major, bool isNewBest)
    {
        HasResult = true;
        Cleared = cleared;
        Score = score;
        MaxSwing = maxSwing;
        MaxChain = maxChain;
        Falls = falls;
        WaveReached = waveReached;
        Major = major;
        IsNewBest = isNewBest;
    }
}
