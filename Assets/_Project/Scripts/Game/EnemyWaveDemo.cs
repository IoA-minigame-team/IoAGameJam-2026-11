using UnityEngine;

/// <summary>Sanjo用の確認操作。敵の落下を実際に起こして、共有イベントから進行することを試す。</summary>
public sealed class EnemyWaveDemo : MonoBehaviour
{
    [SerializeField] WaveController flow;
    [SerializeField] Camera reviewCamera;
    [SerializeField] Collider stageGround;
    int started, cleared, lastStarted, lastCleared, slashId = 1000;
    bool showPoints = true;
    Vector3 cameraPosition;
    Quaternion cameraRotation;
    float cameraSize;
    public WaveController Flow => flow;
    public int StartedEvents => started;
    public int ClearedEvents => cleared;

    void Awake()
    {
        cameraPosition = reviewCamera.transform.position;cameraRotation = reviewCamera.transform.rotation;cameraSize = reviewCamera.orthographicSize;
    }
    void OnEnable() { GameEvents.WaveStarted += OnStarted;GameEvents.WaveCleared += OnCleared; }
    void OnDisable() { GameEvents.WaveStarted -= OnStarted;GameEvents.WaveCleared -= OnCleared; }
    void OnStarted(int wave) { started++;lastStarted = wave; }
    void OnCleared(int wave) { cleared++;lastCleared = wave; }

    public void ThrowSpawnedEnemies()
    {
        if (flow.State == WaveController.WaveState.GameOver || flow.State == WaveController.WaveState.Completed) return;
        slashId++;
        var bounds = stageGround.bounds;
        foreach (var enemy in flow.Spawner.OwnedEnemies)
        {
            if (enemy == null || enemy.State == Enemy.MotionState.Falling || enemy.State == Enemy.MotionState.Fallen) continue;
            // 確認専用。足場の外へ移してからLaunchし、本物の落下経路を通す。完了通知を直接流さない。
            var direction = Vector3.ProjectOnPlane(enemy.Position - bounds.center, Vector3.up).normalized;
            if (direction.sqrMagnitude < .000001f) direction = Vector3.forward;
            float distance = Mathf.Max(bounds.extents.x, bounds.extents.z) + 4f;
            enemy.transform.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + direction * distance;
            ILaunchable target = enemy;target.Launch(direction * (4f * target.Weight * target.GlowMultiplier), slashId);
        }
        Physics.SyncTransforms();
    }
    public void RestartTest() { started = cleared = lastStarted = lastCleared = 0;flow.RestartRun();Overview(); }
    public void FocusLastSpawn()
    {
        var enemy = flow.Spawner.LastSpawned;
        if (enemy == null) return;
        reviewCamera.transform.position = enemy.Position + new Vector3(4, 4, 7);
        reviewCamera.transform.LookAt(enemy.Position + Vector3.up);reviewCamera.orthographicSize = 4.2f;
    }
    public void Overview()
    {
        reviewCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);reviewCamera.orthographicSize = cameraSize;
    }
    void OnGUI()
    {
        var previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1200f, Screen.height / 700f, 1));
        var label = new GUIStyle(GUI.skin.label) { fontSize = 18 };
        var button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
        GUI.Label(new Rect(22, 10, 1150, 28), "WAVE FLOW TEST  |  " + flow.State + "  |  Wave " + flow.CurrentWaveNumber + " / " + flow.TotalWaves, label);
        GUI.Label(new Rect(22, 39, 1150, 28), "Pending: " + flow.PendingCount + "   Alive: " + flow.Spawner.AliveCount + "   Started: " + started + " (#" + lastStarted + ")   Cleared: " + cleared + " (#" + lastCleared + ")", label);
        GUI.Label(new Rect(22, 68, 1150, 28), "Last spawn: " + flow.Spawner.LastSpawnPoint + "   " + (flow.LastError ?? flow.Spawner.LastFailure ?? ""), label);
        GUI.enabled = flow.State != WaveController.WaveState.GameOver && flow.State != WaveController.WaveState.Completed;
        if (GUI.Button(new Rect(22, 574, 245, 42), "Throw spawned enemies (test)", button)) ThrowSpawnedEnemies();
        if (GUI.Button(new Rect(279, 574, 170, 42), "Next wave", button)) flow.AdvanceNow();
        if (GUI.Button(new Rect(461, 574, 170, 42), "Focus last spawn", button)) FocusLastSpawn();
        if (GUI.Button(new Rect(643, 574, 170, 42), "Overview", button)) Overview();
        if (GUI.Button(new Rect(825, 574, 170, 42), "GameOver test", button)) GameEvents.RaisePlayerDamaged(0);
        GUI.enabled = true;
        if (GUI.Button(new Rect(1007, 574, 170, 42), "Restart test", button)) RestartTest();
        showPoints = GUI.Toggle(new Rect(22, 636, 220, 28), showPoints, "Show spawn point labels");
        GUI.Label(new Rect(254, 632, 930, 28), "Spawn locations are editable Transforms. Test throw uses real EnemyFell, not fake WaveCleared.", label);
        GUI.matrix = previous;
        if (!showPoints) return;
        var pointLabel = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
        foreach (var point in flow.Spawner.Points)
        {
            if (point == null) continue;
            var screen = reviewCamera.WorldToScreenPoint(point.transform.position + Vector3.up * .5f);
            if (screen.z > 0) GUI.Label(new Rect(screen.x - 42, Screen.height - screen.y, 150, 23), point.name, pointLabel);
        }
    }
}
