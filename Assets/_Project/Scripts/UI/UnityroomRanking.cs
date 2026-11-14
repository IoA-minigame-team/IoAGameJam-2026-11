using unityroom.Api;

/// <summary>
/// unityroom のスコアボードへの送信。果てなくの得点だけを載せる(5波の記録は一振り最多で競うので載せない)
/// </summary>
public static class UnityroomRanking
{
    // unityroom で作ったスコアボードの番号。調整値ではなく向こうの設定と対になる番号なので定数にする
    const int ENDLESS_BOARD_NO = 1;

    public static void SendEndless(int score)
    {
        // 0点を載せても意味がないので送らない
        if (score <= 0) return;
        // Prefab の無いシーン(Dev シーンなど)から始めたときは null が返る。そこでゲームを止めないよう送らずに終える
        IUnityroomApiClient client = UnityroomApiClient.Instance;
        if (client == null) return;
        // ローカルの自己ベストを更新したかに関係なく毎回送る。ブラウザの記録が消えても、サーバーの最高値は高いときだけ更新されるため
        client.SendScore(ENDLESS_BOARD_NO, score, ScoreboardWriteMode.HighScoreDesc);
    }
}
