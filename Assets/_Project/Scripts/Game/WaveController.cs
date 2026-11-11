using UnityEngine;

/// <summary>ウェーブの予定・生存数・待ち時間を所有し、開始と完了を共有GameEventsで通知する。</summary>
public sealed class WaveController : MonoBehaviour
{
    public enum WaveState { Ready, Spawning, WaitingForEnemies, Intermission, Completed, GameOver, Faulted }
    [SerializeField] EnemyWavePlan plan;
    [SerializeField] EnemySpawner spawner;
    [SerializeField] GameOverController gameOver;
    [SerializeField] bool startAutomatically = true;
    [SerializeField] bool autoAdvance = true;
    int[] remaining;
    float[] due, blockedSince;
    float elapsed, delay;
    bool startPending;
    int queuedStartWave = 1;
    public WaveState State { get; private set; }
    public int CurrentWaveNumber { get; private set; }
    public int TotalWaves => plan != null && plan.Waves != null ? plan.Waves.Length : 0;
    public int SpawnedThisWave { get; private set; }
    public int PendingCount { get; private set; }
    public string LastError { get; private set; }
    public EnemySpawner Spawner => spawner;
    public EnemyWavePlan Plan => plan;
    public bool AutoAdvance { get => autoAdvance;set => autoAdvance = value; }

    void Awake()
    {
        // 同担当のGameOver部品だけを初期化時に取得する。Sharedや他担当のクラスは書き換えない。
        if (gameOver == null) gameOver = FindFirstObjectByType<GameOverController>();
    }
    void OnEnable() => GameEvents.GameOver += OnGameOver;
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
        if (!ValidatePlan()) return false;
        delay = plan.InitialDelay;startPending = true;return true;
    }
    bool ValidatePlan()
    {
        if (plan == null || TotalWaves == 0 || spawner == null) { Fault("Wave plan or EnemySpawner is missing.");return false; }
        foreach (var wave in plan.Waves)
        {
            if (wave == null || wave.Batches == null || wave.Batches.Length == 0) { Fault("Empty wave definition.");return false; }
            foreach (var batch in wave.Batches)
                if (batch == null || batch.Prefab == null || batch.Prefab.GetComponent<Enemy>() == null) { Fault("Wave batch needs an Enemy prefab.");return false; }
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
        elapsed += dt;
        var batches = plan.Waves[CurrentWaveNumber - 1].Batches;
        int budget = plan.MaxSpawnsPerFrame;
        for (int i = 0; i < batches.Length && budget > 0 && State == WaveState.Spawning; i++)
        {
            while (remaining[i] > 0 && elapsed >= due[i] && budget > 0 && State == WaveState.Spawning)
            {
                if (!spawner.TrySpawn(batches[i].Prefab, batches[i].Areas, out _))
                {
                    if (blockedSince[i] < 0) blockedSince[i] = elapsed;
                    if (elapsed - blockedSince[i] >= plan.BlockedTimeout) { Fault(spawner.LastFailure);return; }
                    due[i] = elapsed + plan.RetryInterval;break;
                }
                remaining[i]--;PendingCount--;SpawnedThisWave++;budget--;blockedSince[i] = -1;
                due[i] += batches[i].Interval;
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
        State = CurrentWaveNumber == TotalWaves ? WaveState.Completed : WaveState.Intermission;
        delay = plan.InterWaveDelay;
        GameEvents.RaiseWaveCleared(CurrentWaveNumber);
    }
    void BeginWave(int number)
    {
        CurrentWaveNumber = number;SpawnedThisWave = PendingCount = 0;elapsed = 0;
        var batches = plan.Waves[number - 1].Batches;
        remaining = new int[batches.Length];due = new float[batches.Length];blockedSince = new float[batches.Length];
        for (int i = 0; i < batches.Length; i++) { remaining[i] = batches[i].Count;PendingCount += remaining[i];due[i] = batches[i].Delay;blockedSince[i] = -1; }
        State = WaveState.Spawning;
        GameEvents.RaiseWaveStarted(number);
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
