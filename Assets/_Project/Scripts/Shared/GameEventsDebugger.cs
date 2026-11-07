using UnityEngine;

/// <summary>
/// テスト用。インスペクターの右クリックメニューから各イベントを仮の値で流し、流れたイベントをログに出す。
/// 他の担当の実装が無くても、自分の受け取り側を試せるようにするため。本番シーンには置かない。
/// </summary>
public sealed class GameEventsDebugger : MonoBehaviour
{
    [Header("流す値")]
    [SerializeField] int slashId = 1;
    [SerializeField] int hitCount = 3;
    [SerializeField] int chain = 1;
    [SerializeField] int remainingDamage = 2;
    [SerializeField, Range(0f, 1f)] float glowRatio = 0.5f;
    [SerializeField] int swarmCount = 3;
    [SerializeField] int wave = 1;
    [SerializeField] MajorMeiId majorMei = MajorMeiId.Kaeshi;
    [SerializeField] Vector3 slashEndOffset = new Vector3(5f, 0f, 0f);
    [SerializeField] float hitSpeed = 6f;

    [Header("時間制御の確認")]
    [SerializeField] float hitStopDuration = 0.5f;
    [SerializeField, Range(0f, 1f)] float slowScale = 0.3f;
    [SerializeField] float slowDuration = 2f;

    [Header("ログ")]
    [SerializeField] bool logEvents = true;

    DummyLaunchable dummyEnemy;

    void Awake()
    {
        dummyEnemy = new DummyLaunchable(transform);
    }

    void OnEnable()
    {
        GameEvents.SwordPlanted += OnSwordPlanted;
        GameEvents.SlashStarted += OnSlashStarted;
        GameEvents.EnemyHit += OnEnemyHit;
        GameEvents.SlashFinished += OnSlashFinished;
        GameEvents.ChainChanged += OnChainChanged;
        GameEvents.PlayerDamaged += OnPlayerDamaged;
        GameEvents.GlowChanged += OnGlowChanged;
        GameEvents.EnemyTeetered += OnEnemyTeetered;
        GameEvents.EnemyFell += OnEnemyFell;
        GameEvents.SwarmCountChanged += OnSwarmCountChanged;
        GameEvents.WaveStarted += OnWaveStarted;
        GameEvents.WaveCleared += OnWaveCleared;
        GameEvents.GameOver += OnGameOver;
        GameEvents.MajorMeiChanged += OnMajorMeiChanged;
    }

    void OnDisable()
    {
        GameEvents.SwordPlanted -= OnSwordPlanted;
        GameEvents.SlashStarted -= OnSlashStarted;
        GameEvents.EnemyHit -= OnEnemyHit;
        GameEvents.SlashFinished -= OnSlashFinished;
        GameEvents.ChainChanged -= OnChainChanged;
        GameEvents.PlayerDamaged -= OnPlayerDamaged;
        GameEvents.GlowChanged -= OnGlowChanged;
        GameEvents.EnemyTeetered -= OnEnemyTeetered;
        GameEvents.EnemyFell -= OnEnemyFell;
        GameEvents.SwarmCountChanged -= OnSwarmCountChanged;
        GameEvents.WaveStarted -= OnWaveStarted;
        GameEvents.WaveCleared -= OnWaveCleared;
        GameEvents.GameOver -= OnGameOver;
        GameEvents.MajorMeiChanged -= OnMajorMeiChanged;

        // 止めたまま無効化されても、時間が止まり続けないようにする
        TimeController.ReleasePause(this);
    }

    // ---- 流す ----

    [ContextMenu("髙橋/SwordPlanted")]
    void RaiseSwordPlanted() => GameEvents.RaiseSwordPlanted(transform.position);

    [ContextMenu("髙橋/SlashStarted")]
    void RaiseSlashStarted() =>
        GameEvents.RaiseSlashStarted(new SlashStartedInfo(slashId, transform.position, transform.position + slashEndOffset));

    [ContextMenu("髙橋/EnemyHit")]
    void RaiseEnemyHit() =>
        GameEvents.RaiseEnemyHit(new EnemyHitInfo(slashId, dummyEnemy, slashEndOffset.normalized * hitSpeed, 1f));

    [ContextMenu("髙橋/SlashFinished")]
    void RaiseSlashFinished() => GameEvents.RaiseSlashFinished(new SlashFinishedInfo(slashId, hitCount));

    [ContextMenu("髙橋/ChainChanged")]
    void RaiseChainChanged() => GameEvents.RaiseChainChanged(chain);

    [ContextMenu("髙橋/PlayerDamaged")]
    void RaisePlayerDamaged() => GameEvents.RaisePlayerDamaged(remainingDamage);

    [ContextMenu("三條/GlowChanged")]
    void RaiseGlowChanged() => GameEvents.RaiseGlowChanged(dummyEnemy, glowRatio);

    [ContextMenu("三條/EnemyTeetered")]
    void RaiseEnemyTeetered() => GameEvents.RaiseEnemyTeetered(dummyEnemy);

    [ContextMenu("三條/EnemyFell")]
    void RaiseEnemyFell() => GameEvents.RaiseEnemyFell(new EnemyFellInfo(slashId, transform.position));

    [ContextMenu("三條/SwarmCountChanged")]
    void RaiseSwarmCountChanged() => GameEvents.RaiseSwarmCountChanged(swarmCount);

    [ContextMenu("三條/WaveStarted")]
    void RaiseWaveStarted() => GameEvents.RaiseWaveStarted(wave);

    [ContextMenu("三條/WaveCleared")]
    void RaiseWaveCleared() => GameEvents.RaiseWaveCleared(wave);

    [ContextMenu("三條/GameOver")]
    void RaiseGameOver() => GameEvents.RaiseGameOver();

    [ContextMenu("増永/MajorMeiChanged")]
    void RaiseMajorMeiChanged() => GameEvents.RaiseMajorMeiChanged(majorMei);

    [ContextMenu("時間/HitStop")]
    void TestHitStop() => TimeController.HitStop(hitStopDuration);

    [ContextMenu("時間/Slow")]
    void TestSlow() => TimeController.Slow(slowScale, slowDuration);

    [ContextMenu("時間/RequestPause")]
    void TestRequestPause() => TimeController.RequestPause(this);

    [ContextMenu("時間/ReleasePause")]
    void TestReleasePause() => TimeController.ReleasePause(this);

    // ---- 受け取ってログに出す ----

    void OnSwordPlanted(Vector3 position) => Log($"SwordPlanted pos={position}");
    void OnSlashStarted(SlashStartedInfo info) => Log($"SlashStarted id={info.SlashId} start={info.Start} end={info.End}");
    void OnEnemyHit(EnemyHitInfo info) => Log($"EnemyHit id={info.SlashId} vel={info.Velocity} front={info.Frontness:F2}");
    void OnSlashFinished(SlashFinishedInfo info) => Log($"SlashFinished id={info.SlashId} hits={info.HitCount}");
    void OnChainChanged(int value) => Log($"ChainChanged {value}");
    void OnPlayerDamaged(int remaining) => Log($"PlayerDamaged remaining={remaining}");
    void OnGlowChanged(ILaunchable enemy, float ratio) => Log($"GlowChanged {enemy.Transform.name} ratio={ratio:F2}");
    void OnEnemyTeetered(ILaunchable enemy) => Log($"EnemyTeetered {enemy.Transform.name}");
    void OnEnemyFell(EnemyFellInfo info) => Log($"EnemyFell id={info.SlashId} pos={info.Position}");
    void OnSwarmCountChanged(int count) => Log($"SwarmCountChanged {count}");
    void OnWaveStarted(int value) => Log($"WaveStarted {value}");
    void OnWaveCleared(int value) => Log($"WaveCleared {value}");
    void OnGameOver() => Log("GameOver");
    void OnMajorMeiChanged(MajorMeiId mei) => Log($"MajorMeiChanged {mei}");

    void Log(string message)
    {
        if (logEvents) Debug.Log($"[GameEvents] {message}", this);
    }

    /// <summary>敵のイベントを試すための仮の敵。Launch されたらログを出すだけ</summary>
    sealed class DummyLaunchable : ILaunchable
    {
        readonly Transform owner;

        public DummyLaunchable(Transform owner) => this.owner = owner;

        public Transform Transform => owner;
        public Vector3 Position => owner.position;
        public float Weight => 1f;
        public float GlowMultiplier => 1f;
        public bool IsShieldFacing(Vector3 slashDir) => false;

        public void Launch(Vector3 velocity, int slashId) =>
            Debug.Log($"[GameEvents] DummyLaunchable.Launch vel={velocity} id={slashId}");
    }
}
