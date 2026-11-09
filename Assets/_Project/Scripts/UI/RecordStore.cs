using UnityEngine;

/// <summary>
/// 自己ベスト(一振り最多と得点)の保存。PlayerPrefs は WebGL でもブラウザに保存される。
/// unityroom に上げたら、一度遊んでページを読み直し、記録が残っているかを確かめる。
/// </summary>
public static class RecordStore
{
    const string KEY_MAX_SWING = "hitofuri.best.maxSwing";
    const string KEY_SCORE = "hitofuri.best.score";

    public static int BestMaxSwing => PlayerPrefs.GetInt(KEY_MAX_SWING, 0);
    public static int BestScore => PlayerPrefs.GetInt(KEY_SCORE, 0);
    public static bool HasRecord => PlayerPrefs.HasKey(KEY_MAX_SWING);

    /// <summary>
    /// 結果を記録と比べ、上回っていれば保存して true を返す。
    /// 主要な記録は一振り最多(仕様)なので、まず一振り最多で比べ、同じなら得点で比べる。
    /// </summary>
    public static bool SubmitIfBest(int maxSwing, int score)
    {
        bool better = !HasRecord || maxSwing > BestMaxSwing || (maxSwing == BestMaxSwing && score > BestScore);
        if (!better) return false;
        PlayerPrefs.SetInt(KEY_MAX_SWING, maxSwing);
        PlayerPrefs.SetInt(KEY_SCORE, score);
        PlayerPrefs.Save();
        return true;
    }
}
