using UnityEngine;

/// <summary>
/// 「いつ、何体出すか」と波番号の進行だけを担当する。
/// データはEnemyWavePlan、実際の配置と生存数はEnemySpawnerに任せる。
/// 他担当への開始・完了通知はGameEventsを使う。画面や銘の選択処理を直接呼ばない。
/// </summary>
public sealed class WaveController : MonoBehaviour
{
    // Ready→Spawning→WaitingForEnemies→Intermission→次の波。最後だけCompletedへ進む。
    // GameOverは被弾による終了、Faultedは出現データや配置の問題で進行できない状態。
    public enum WaveState { Ready, Spawning, WaitingForEnemies, Intermission, Completed, GameOver, Faulted }
    [SerializeField] EnemyWavePlan plan;
    [SerializeField] EnemySpawner spawner;
    [SerializeField] GameOverController gameOver;
    [SerializeField] bool startAutomatically = true;
    [SerializeField] bool autoAdvance = true;
    // 添字は「現在の波の出現グループ」。remaining=未出現数、due=次に出す時刻、intervals=果てなくの強化を反映した間隔。
    EnemyWavePlan.Batch[] currentBatches;
    GameObject[] chosenPrefabs;
    int[] remaining;
    float[] due, blockedSince, intervals;
    float elapsed, delay;
    bool startPending;
    int queuedStartWave = 1;
    public WaveState State { get; private set; }
    public int CurrentWaveNumber { get; private set; }
    public int TotalWaves => plan != null && plan.Waves != null ? plan.Waves.Length : 0;
    public int SpawnedThisWave { get; private set; }
    /// <summary>まだ出していない数。UIの残り総数は、これにSpawner.AliveCountを足す。</summary>
    public int PendingCount { get; private set; }
    public string LastError { get; private set; }
    public EnemySpawner Spawner => spawner;
    public EnemyWavePlan Plan => plan;
    public bool AutoAdvance { get => autoAdvance;set => autoAdvance = value; }
    /// <summary>果てなく(エンドレス)か。タイトルで選んだ遊び方をGameContextから読む</summary>
    public bool IsEndless => GameContext.Mode == GameMode.Endless;

    void Awake()
    {
        // 同担当のGameOver部品だけを初期化時に取得する。Sharedや他担当のクラスは書き換えない。
        if (gameOver == null) gameOver = FindFirstObjectByType<GameOverController>();
    }
    void OnEnable()
    {
        GameEvents.GameOver += OnGameOver;
        if (gameOver == null) gameOver = FindFirstObjectByType<GameOverController>();
        // 無効中に終了通知を聞き逃した場合も、出現を再開しない。
        if (gameOver != null && gameOver.IsGameOver) OnGameOver();
    }
    void OnDisable() => GameEvents.GameOver -= OnGameOver;
    void Start() { if (startAutomatically && State == WaveState.Ready) QueueStart(); }

    public bool StartRun()
    {
        if (State != WaveState.Ready || startPending) return false;
        return QueueStart();
    }
    /// <summary>新しいシーンのラウンドを、選んだ波から始める。主人公内部は書き換えない。</summary>
    public bool StartRunAtWave(int number)
    {
        if(State!=WaveState.Ready||startPending||number<1||number>TotalWaves)return false;
        queuedStartWave=number;return QueueStart();
    }
    bool QueueStart()
    {
        if (gameOver != null && gameOver.IsGameOver) { OnGameOver(); return false; }
        if (!ValidatePlan()) return false;
        delay = plan.InitialDelay;startPending = true;return true;
    }
    bool ValidatePlan()
    {
        if (plan == null || TotalWaves == 0 || spawner == null) { Fault("Wave plan or EnemySpawner is missing.");return false; }
        if (IsEndless && plan.EndlessLoopFrom > TotalWaves) { Fault("Endless loop start is beyond the last wave.");return false; }
        if (spawner.Plan != plan) { Fault("WaveController and EnemySpawner must use the same EnemyWavePlan.");return false; }
        foreach (var wave in plan.Waves)
        {
            if (wave == null || wave.Batches == null || wave.Batches.Length == 0) { Fault("Empty wave definition.");return false; }
            foreach (var batch in wave.Batches)
            {
                if (batch == null) { Fault("Wave batch is missing."); return false; }
                var choices = batch.RandomPrefabs;
                if (choices == null || choices.Length == 0) choices = new[] { batch.Prefab };
                foreach (var choice in choices)
                    if (choice == null || choice.GetComponent<Enemy>() == null) { Fault("Wave batch needs an Enemy prefab."); return false; }
            }
        }
        return true;
    }
    void Update()
    {
        if (TimeController.IsPaused || TimeController.IsHitStopping || State == WaveState.GameOver || State == WaveState.Faulted || State == WaveState.Completed) return;
        spawner?.RefreshOwnedEnemies();
        if (spawner != null && spawner.HasUnexpectedRemoval) { Fault(spawner.LastFailure);return; }
        float dt = Time.deltaTime;
        if (startPending)
        {
            delay -= dt;
            if (delay <= 0) { startPending = false;BeginWave(queuedStartWave); }
            return;
        }
        if (State == WaveState.Intermission)
        {
            if (autoAdvance) { delay -= dt;if (delay <= 0) BeginWave(CurrentWaveNumber + 1); }
            return;
        }
        if (State != WaveState.Spawning) return;
        // 時刻は停止中には進めない。WaveStartedを受けたUIが開始演出でPauseしても先に出現しない。
        elapsed += dt;
        var batches = currentBatches;
        int budget = plan.MaxSpawnsPerFrame;
        for (int i = 0; i < batches.Length && budget > 0 && State == WaveState.Spawning; i++)
        {
            while (remaining[i] > 0 && elapsed >= due[i] && budget > 0 && State == WaveState.Spawning)
            {
                if(chosenPrefabs[i]==null)chosenPrefabs[i]=batches[i].PickPrefab();
                if (!spawner.TrySpawn(chosenPrefabs[i], batches[i].Areas, out _))
                {
                    // 塞がれた出現を「出した」と数えない。再試行し、長く失敗した場合は原因付きで止める。
                    if (blockedSince[i] < 0) blockedSince[i] = elapsed;
                    if (elapsed - blockedSince[i] >= plan.BlockedTimeout) { Fault(spawner.LastFailure);return; }
                    due[i] = elapsed + plan.RetryInterval;break;
                }
                chosenPrefabs[i]=null;remaining[i]--;PendingCount--;SpawnedThisWave++;budget--;blockedSince[i] = -1;
                due[i] += intervals[i];
            }
        }
        if (PendingCount == 0 && State == WaveState.Spawning) State = WaveState.WaitingForEnemies;
    }

    void LateUpdate()
    {
        if (State != WaveState.WaitingForEnemies || TimeController.IsPaused || TimeController.IsHitStopping) return;
        spawner.RefreshOwnedEnemies();
        if (spawner.HasUnexpectedRemoval) { Fault(spawner.LastFailure);return; }
        if (spawner.AliveCount != 0) return;
        // EnemyFellの通知チェーンが終わってから完了する。未出現の予定が残る間も完了させない。
        // 果てなくでは最後の波で終わらず、次の波へ進む
        State = !IsEndless && CurrentWaveNumber >= TotalWaves ? WaveState.Completed : WaveState.Intermission;
        delay = plan.InterWaveDelay;
        GameEvents.RaiseWaveCleared(CurrentWaveNumber);
    }
    void BeginWave(int number)
    {
        // 予定数と現在番号を先に確定してから通知する。購読先のUIが最新の残数を読める順序にする。
        CurrentWaveNumber = number;SpawnedThisWave = PendingCount = 0;elapsed = 0;
        var batches = currentBatches = ResolveWave(number, out int lap);
        chosenPrefabs = new GameObject[batches.Length];remaining = new int[batches.Length];due = new float[batches.Length];blockedSince = new float[batches.Length];intervals = new float[batches.Length];
        for (int i = 0; i < batches.Length; i++)
        {
            remaining[i] = ScaledCount(batches[i], lap);PendingCount += remaining[i];due[i] = batches[i].Delay;blockedSince[i] = -1;
            intervals[i] = ScaledInterval(batches[i], lap);
        }
        State = WaveState.Spawning;
        GameEvents.RaiseWaveStarted(number);
    }
    /// <summary>
    /// 波番号から元にする波と周回数を決める。データにある波はそのまま(lap=0)。
    /// それより後は EndlessLoopFrom から最後までを順に繰り返す。既定では第6波=第4波の1周目、第7波=第5波の1周目、第8波=第4波の2周目。
    /// </summary>
    EnemyWavePlan.Batch[] ResolveWave(int number, out int lap)
    {
        lap = 0;
        if (number <= TotalWaves) return plan.Waves[number - 1].Batches;
        int from = Mathf.Min(plan.EndlessLoopFrom, TotalWaves);
        int loopLength = TotalWaves - from + 1;
        int past = number - TotalWaves - 1;
        lap = past / loopLength + 1;
        return plan.Waves[from - 1 + past % loopLength].Batches;
    }
    int ScaledCount(EnemyWavePlan.Batch batch, int lap)
    {
        if (lap == 0) return batch.Count;
        // 元の数が上限より多いときは元の数を下回らせない
        int grown = Mathf.CeilToInt(batch.Count * (1f + plan.EndlessCountGrowth * lap));
        return Mathf.Max(batch.Count, Mathf.Min(plan.EndlessMaxCount, grown));
    }
    float ScaledInterval(EnemyWavePlan.Batch batch, int lap)
    {
        if (lap == 0) return batch.Interval;
        return Mathf.Max(Mathf.Min(batch.Interval, plan.EndlessMinInterval), batch.Interval * Mathf.Pow(plan.EndlessIntervalScale, lap));
    }
    public bool AdvanceNow()
    {
        if (State != WaveState.Intermission || TimeController.IsPaused || TimeController.IsHitStopping) return false;
        BeginWave(CurrentWaveNumber + 1);return true;
    }
    void OnGameOver() { startPending = false;State = WaveState.GameOver; }
    void Fault(string message) { startPending = false;LastError = message;State = WaveState.Faulted;Debug.LogWarning("[WaveController] " + message, this); }

    /// <summary>B担当の進行だけを再開する。HP・スコア・入力は各所有者が別途初期化する。</summary>
    public void RestartRun()
    {
        startPending = false;spawner?.ClearOwnedEnemies();gameOver?.ResetRound();
        queuedStartWave=1;
        CurrentWaveNumber = SpawnedThisWave = PendingCount = 0;LastError = null;State = WaveState.Ready;
        QueueStart();
    }
}
