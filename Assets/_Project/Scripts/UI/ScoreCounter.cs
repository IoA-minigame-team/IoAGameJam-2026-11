using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 得点と記録を数える(仕様書「スコアと記録」)。
/// ・得点は敵が落ちた時点で足す。一振りの得点は落とした数の2乗に比例する
/// ・同じ slashId で、斬り戻しから一定時間内に落ちた敵を「一振り」として数える
/// ・連鎖倍率は斬り戻しを始めた瞬間の連鎖数で決める(落ちるまでに連鎖が変わっても、その一振りの倍率は変えない)
/// ・ゲームオーバーか最後のウェーブのクリアで結果を GameResult に入れ、ResultScene へ移る
/// </summary>
public sealed class ScoreCounter : MonoBehaviour
{
    [SerializeField] FeedbackTuning tuning;
    [Tooltip("ゲームが終わったら ResultScene へ移るか。ResultScene ができるまではオフにしておく")]
    [SerializeField] bool loadResultScene = false;

    sealed class Swing
    {
        public float StartTime;
        public float Multiplier;
        public int Count;
    }

    readonly Dictionary<int, Swing> swings = new Dictionary<int, Swing>();
    int chain;
    int currentWave;
    // 今刻まれている大銘。リザルトに出すため。MeiManager を直接見ずにイベントで知る
    MajorMeiId currentMajor = MajorMeiId.None;
    bool finished;

    public int Score { get; private set; }
    public int MaxSwing { get; private set; }
    public int MaxChain { get; private set; }
    public int Falls { get; private set; }
    public int Chain => chain;
    public float ChainMultiplier => Mathf.Min(tuning.ChainMultiplierMax, 1f + tuning.ChainStep * chain);

    /// <summary>得点が変わった。値は新しい合計</summary>
    public event Action<int> ScoreChanged;
    /// <summary>一振りで落とした数が増えた(一振りのカウント表示と称号のため)</summary>
    public event Action<SwingProgress> SwingProgressed;
    /// <summary>連鎖が変わった。値は連鎖数と倍率</summary>
    public event Action<int, float> ChainUpdated;

    void OnEnable()
    {
        GameEvents.SlashStarted += OnSlashStarted;
        GameEvents.ChainChanged += OnChainChanged;
        GameEvents.EnemyFell += OnEnemyFell;
        GameEvents.WaveStarted += OnWaveStarted;
        GameEvents.WaveCleared += OnWaveCleared;
        GameEvents.GameOver += OnGameOver;
        GameEvents.MajorMeiChanged += OnMajorMeiChanged;
    }

    void OnDisable()
    {
        GameEvents.SlashStarted -= OnSlashStarted;
        GameEvents.ChainChanged -= OnChainChanged;
        GameEvents.EnemyFell -= OnEnemyFell;
        GameEvents.WaveStarted -= OnWaveStarted;
        GameEvents.WaveCleared -= OnWaveCleared;
        GameEvents.GameOver -= OnGameOver;
        GameEvents.MajorMeiChanged -= OnMajorMeiChanged;
    }

    void OnSlashStarted(SlashStartedInfo info)
    {
        // 時間は game time で数える。銘の選択中に止まっている間は一振りの3秒も進まない
        swings[info.SlashId] = new Swing { StartTime = Time.time, Multiplier = ChainMultiplier, Count = 0 };
        // 古い一振りを残し続けないよう、時間を過ぎたものは捨てる
        RemoveExpiredSwings();
    }

    void OnChainChanged(int value)
    {
        chain = value;
        MaxChain = Mathf.Max(MaxChain, chain);
        ChainUpdated?.Invoke(chain, ChainMultiplier);
    }

    void OnEnemyFell(EnemyFellInfo info)
    {
        if (finished) return;

        int count = 1;
        float multiplier = ChainMultiplier;
        if (info.SlashId >= 0 && swings.TryGetValue(info.SlashId, out Swing swing) && Time.time - swing.StartTime <= tuning.SwingWindow)
        {
            swing.Count++;
            count = swing.Count;
            multiplier = swing.Multiplier;
        }

        // n体目で足す点 = 10 × (n² − (n−1)²)。一振りの合計がちょうど 10 × n² になる
        int added = Mathf.RoundToInt(tuning.PointsPerFall * (count * count - (count - 1) * (count - 1)) * multiplier);
        Score += added;
        Falls++;
        MaxSwing = Mathf.Max(MaxSwing, count);

        int swingTotal = Mathf.RoundToInt(tuning.PointsPerFall * count * count * multiplier);
        ScoreChanged?.Invoke(Score);
        SwingProgressed?.Invoke(new SwingProgress(count, swingTotal, added, info.Position, tuning.TitleReachedAt(count)));
    }

    void OnWaveStarted(int wave) => currentWave = wave;

    void OnWaveCleared(int wave)
    {
        // 果てなくでは最後の波を退けても終わらない。倒れるまで続ける
        if (GameContext.Mode != GameMode.Endless && wave >= tuning.TotalWaves) Finish(true);
    }

    void OnGameOver() => Finish(false);

    void OnMajorMeiChanged(MajorMeiId mei) => currentMajor = mei;

    void Finish(bool cleared)
    {
        if (finished) return;
        finished = true;

        // 果てなくは得点で競う別の記録に入れる。銘を重ねた一振り最多で、5波の記録が埋まらないようにするため
        bool isNewBest = GameContext.Mode == GameMode.Endless
            ? RecordStore.SubmitEndlessIfBest(Score, currentWave)
            : RecordStore.SubmitIfBest(MaxSwing, Score);
        GameResult.Set(cleared, Score, MaxSwing, MaxChain, Falls, currentWave, currentMajor, isNewBest);

        if (loadResultScene) StartCoroutine(LoadResultAfterDelay());
    }

    IEnumerator LoadResultAfterDelay()
    {
        // 「斬られた」などを見せてから移る。時間が止まっていても進むよう実時間で待つ
        yield return new WaitForSecondsRealtime(tuning.ResultDelay);
        SceneManager.LoadScene(tuning.ResultSceneName);
    }

    void RemoveExpiredSwings()
    {
        List<int> expired = null;
        foreach (KeyValuePair<int, Swing> pair in swings)
        {
            if (Time.time - pair.Value.StartTime > tuning.SwingWindow * 2f)
            {
                if (expired == null) expired = new List<int>();
                expired.Add(pair.Key);
            }
        }
        if (expired == null) return;
        foreach (int id in expired) swings.Remove(id);
    }
}

/// <summary>一振りのカウントが進んだときの中身</summary>
public readonly struct SwingProgress
{
    /// <summary>この一振りで落とした数</summary>
    public readonly int Count;
    /// <summary>この一振りの合計点</summary>
    public readonly int SwingPoints;
    /// <summary>今落ちた1体で足した点</summary>
    public readonly int Added;
    public readonly UnityEngine.Vector3 Position;
    /// <summary>ちょうど届いた称号。届いていなければ null</summary>
    public readonly string TitleReached;

    public SwingProgress(int count, int swingPoints, int added, UnityEngine.Vector3 position, string titleReached)
    {
        Count = count;
        SwingPoints = swingPoints;
        Added = added;
        Position = position;
        TitleReached = titleReached;
    }
}
