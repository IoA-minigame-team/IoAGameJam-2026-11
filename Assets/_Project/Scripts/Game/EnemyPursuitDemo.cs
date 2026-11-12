using UnityEngine;

/// <summary>三條のDevシーンだけで共有プレイヤー・刀の代役を置く。実際のPlayerスクリプトは変更しない。</summary>
public sealed class EnemyPursuitDemo : MonoBehaviour
{
    [SerializeField] GameObject[] prefabs;
    [SerializeField] Transform testPlayer;
    [SerializeField] GameObject testSword;
    [SerializeField] WaveController flow;
    [SerializeField] EnemySwarmCounter counter;
    [SerializeField] GameOverController gameOver;
    [SerializeField] Collider stageGround;
    [SerializeField] Camera reviewCamera;
    Enemy[] roster;
    Transform previousPlayer;
    bool previousSword;
    Vector3 previousSwordPosition;
    int route, slashId = 2000;
    public Enemy[] Roster => roster;
    public WaveController Flow => flow;
    public Transform TestPlayer => testPlayer;
    public int SwarmCount => counter.Count;
    public bool WaveMode { get; private set; }

    void OnEnable()
    {
        previousPlayer = GameContext.PlayerTransform;previousSword = GameContext.IsSwordPlanted;previousSwordPosition = GameContext.SwordPosition;
        GameContext.PlayerTransform = testPlayer;GameContext.IsSwordPlanted = false;
    }
    void OnDisable()
    {
        // 他の本物のPlayerが登録されていたら、その所有値を上書きしない。
        if (GameContext.PlayerTransform != testPlayer) return;
        GameContext.PlayerTransform = previousPlayer;GameContext.IsSwordPlanted = previousSword;GameContext.SwordPosition = previousSwordPosition;
    }
    void Start() => ResetRoster();
    public void ResetRoster()
    {
        flow.enabled = false;flow.Spawner.ClearOwnedEnemies();gameOver.ResetRound();
        DestroyRoster();PullSword();MovePlayer(new Vector3(0, 0, 5));WaveMode = false;
        roster = new Enemy[prefabs.Length];
        reviewCamera.orthographicSize = 10.2f;
        for (int i = 0; i < roster.Length; i++)
        {
            var instance = Instantiate(prefabs[i], new Vector3((1 - i) * 4, 0, -3), Quaternion.identity);
            roster[i] = instance.GetComponent<Enemy>();
        }
    }
    void DestroyRoster() { if (roster != null) foreach (var enemy in roster) if (enemy != null) Destroy(enemy.gameObject);roster = null; }
    public void MovePlayer(Vector3 position) { testPlayer.position = position;if (!GameContext.IsSwordPlanted) GameContext.SwordPosition = position; }
    public void PlantSword(Vector3 position)
    {
        testSword.transform.position = position;testSword.SetActive(true);
        GameContext.IsSwordPlanted = true;GameContext.SwordPosition = position;GameEvents.RaiseSwordPlanted(position);
    }
    public void PullSword()
    {
        // 本物のPlayerと同じ順番で通知する。共有のtrueが残る間のSlashStartedでも追跡先を戻せるか確認する。
        GameEvents.RaiseSlashStarted(new SlashStartedInfo(++slashId, GameContext.SwordPosition, testPlayer.position));
        GameContext.IsSwordPlanted = false;GameContext.SwordPosition = testPlayer.position;testSword.SetActive(false);
    }
    public void LaunchRoster()
    {
        if (roster == null) return;
        foreach (var enemy in roster) if (enemy != null) enemy.Launch(Vector3.back * (6 * enemy.Weight * enemy.GlowMultiplier), ++slashId);
    }
    public void StartWaves()
    {
        DestroyRoster();gameOver.ResetRound();WaveMode = true;flow.enabled = true;flow.RestartRun();
        reviewCamera.orthographicSize = 24;
    }
    public void ThrowWaveEnemies()
    {
        var bounds = stageGround.bounds;
        foreach (var enemy in flow.Spawner.OwnedEnemies)
        {
            if (enemy == null || enemy.State == Enemy.MotionState.Falling || enemy.State == Enemy.MotionState.Fallen) continue;
            var direction = Vector3.ProjectOnPlane(enemy.Position - bounds.center, Vector3.up).normalized;
            if (direction.sqrMagnitude < .0001f) direction = Vector3.forward;
            enemy.transform.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + direction * (Mathf.Max(bounds.extents.x, bounds.extents.z) + 4);
            enemy.Launch(direction * (4 * enemy.Weight * enemy.GlowMultiplier), ++slashId);
        }
        Physics.SyncTransforms();
    }
    public void EndGame() => GameEvents.RaisePlayerDamaged(0);
    void OnGUI()
    {
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1200f, Screen.height / 700f, 1));
        var text = new GUIStyle(GUI.skin.label) { fontSize = 18 };var button = new GUIStyle(GUI.skin.button) { fontSize = 17 };
        GUI.Label(new Rect(22, 10, 1150, 30), "ENEMY TARGET TEST  |  cyan = test player, white = sword  |  Swarm: " + SwarmCount, text);
        GUI.Label(new Rect(22, 42, 1150, 30), gameOver.IsGameOver ? "GAME OVER: movement stopped" : WaveMode ? "Wave " + flow.CurrentWaveNumber + "/5  " + flow.State + "  pending=" + flow.PendingCount + " alive=" + flow.Spawner.AliveCount : "Shield + Shadow -> Player   /   Werewolf (LampEater) -> sword in range, otherwise Player", text);
        if (roster != null) for (int i = 0; i < roster.Length; i++) if (roster[i] != null)
        {
            var pursuit = roster[i].GetComponent<EnemyPursuit>();
            GUI.Label(new Rect(22, 76 + i * 27, 1150, 28), pursuit.Role + " : " + pursuit.Target + "  speed=" + roster[i].Tuning.MoveSpeed + (pursuit.IsSwarming ? "  [SWARMING]" : ""), text);
        }
        GUI.enabled = !gameOver.IsGameOver;
        if (GUI.Button(new Rect(22, 540, 185, 40), "Move test player", button))
        {
            Vector3[] positions = { new Vector3(-6,0,5),new Vector3(6,0,5),new Vector3(0,0,-5),new Vector3(0,0,5) };MovePlayer(positions[route++ % positions.Length]);
        }
        if (GUI.Button(new Rect(219, 540, 175, 40), "Sword -X", button)) PlantSword(new Vector3(-6,0,0));
        if (GUI.Button(new Rect(406, 540, 175, 40), "Sword +X", button)) PlantSword(new Vector3(6,0,0));
        if (GUI.Button(new Rect(593, 540, 175, 40), "Pull sword", button)) PullSword();
        if (GUI.Button(new Rect(780, 540, 180, 40), "Launch roster", button)) LaunchRoster();
        if (GUI.Button(new Rect(972, 540, 205, 40), "GameOver test", button)) EndGame();
        GUI.enabled = true;
        if (GUI.Button(new Rect(22, 598, 185, 40), "Reset 3 characters", button)) ResetRoster();
        if (GUI.Button(new Rect(219, 598, 175, 40), "Start 5 waves", button)) StartWaves();
        if (GUI.Button(new Rect(406, 598, 230, 40), "Throw wave enemies (test)", button)) ThrowWaveEnemies();
        GUI.Label(new Rect(22, 654, 1150, 30), "Shared GameContext + GameEvents. Test controls do not edit Player / Shared code.", text);
    }
}
