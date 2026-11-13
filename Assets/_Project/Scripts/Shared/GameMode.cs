/// <summary>
/// 遊び方の種類。タイトルで選び、InGame と結果画面が読む。
/// Clear は全5波を退けたら終わる。Endless は第五波の後も波を繰り返し、倒れるまで得点を競う。
/// </summary>
public enum GameMode
{
    Clear,
    Endless,
}
