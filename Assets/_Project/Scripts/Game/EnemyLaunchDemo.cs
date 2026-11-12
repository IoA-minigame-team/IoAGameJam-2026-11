using UnityEngine;

/// <summary>Sanjo確認シーン用。ILaunchableと共有イベントで吹き飛びを試す。追跡AIや本番UIではない。</summary>
public sealed class EnemyLaunchDemo : MonoBehaviour
{
    [SerializeField] GameObject[] enemyPrefabs;
    [SerializeField] Enemy[] actors;
    [SerializeField] GameOverController gameOver;
    [SerializeField] float testLaunchSpeed = 4f;
    [SerializeField] float edgeLaunchSpeed = 8f;
    Vector3[] homePositions;
    Quaternion[] homeRotations;
    int nextSlashId = 100;
    int fellCount, teeterCount, lastFellSlashId = -1;
    string status = "Choose a launch test";
    public Enemy[] Actors => actors;
    public int LastIssuedSlashId { get; private set; } = -1;

    void Awake()
    {
        homePositions = new Vector3[actors.Length]; homeRotations = new Quaternion[actors.Length];
        for (int i = 0; i < actors.Length; i++)
        {
            homePositions[i] = actors[i].transform.position;
            homeRotations[i] = actors[i].transform.rotation;
        }
    }
    void OnEnable()
    {
        GameEvents.EnemyFell += OnFell;
        GameEvents.EnemyTeetered += OnTeetered;
    }
    void OnDisable()
    {
        GameEvents.EnemyFell -= OnFell;
        GameEvents.EnemyTeetered -= OnTeetered;
    }
    void OnFell(EnemyFellInfo info) { fellCount++; lastFellSlashId = info.SlashId; }
    void OnTeetered(ILaunchable enemy) { teeterCount++; }

    public void ResetTest()
    {
        gameOver.ResetRound(); fellCount = 0; teeterCount = 0; lastFellSlashId = -1;
        for (int i = 0; i < actors.Length; i++)
        {
            if (actors[i] == null) actors[i] = Instantiate(enemyPrefabs[i]).GetComponent<Enemy>();
            actors[i].transform.SetPositionAndRotation(homePositions[i], homeRotations[i]);
            actors[i].ResetState();
        }
        Physics.SyncTransforms();
        status = "Reset: Spawn / Pop";
    }
    public void LaunchSoft() { ResetTest(); LaunchAll(testLaunchSpeed); status = "Launch -> friction -> stop"; }
    public void LaunchAgain() { LaunchAll(testLaunchSpeed); status = "Another slash: glow increases"; }
    public void LaunchOverEdge()
    {
        ResetTest();
        foreach (var actor in actors) actor.transform.position = new Vector3(actor.transform.position.x, 0, 4f);
        Physics.SyncTransforms(); LaunchAll(edgeLaunchSpeed); status = "No ground -> fall -> EnemyFell";
    }
    public void ShowTeeter()
    {
        ResetTest();
        foreach (var actor in actors) actor.transform.position = new Vector3(actor.transform.position.x, 0, 4.7f);
        Physics.SyncTransforms(); LaunchAll(.35f); status = "Stop near edge -> EnemyTeetered -> recover";
    }
    void LaunchAll(float speed)
    {
        LastIssuedSlashId = ++nextSlashId;
        foreach (var actor in actors)
        {
            if (actor == null) continue;
            ILaunchable target = actor;
            // テスト呼出し側がプレイヤー側と同じ倍率を掛ける。Enemyは受け取った初速のまま動く。
            target.Launch(Vector3.forward * (speed * target.Weight * target.GlowMultiplier), LastIssuedSlashId);
        }
    }
    public void EndGameForTest() => GameEvents.RaisePlayerDamaged(0);

    void OnGUI()
    {
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1200f, Screen.height / 700f, 1));
        var label = new GUIStyle(GUI.skin.label) { fontSize = 19 };
        var button = new GUIStyle(GUI.skin.button) { fontSize = 17 };
        GUI.Label(new Rect(24, 12, 1150, 30), "ILaunchable TEST   |   Shield / Shadow / Werewolf", label);
        GUI.Label(new Rect(24, 43, 1150, 30), gameOver.IsGameOver ? "GAME OVER: movement + reaction stopped" : status, label);
        GUI.Label(new Rect(24, 74, 1150, 30), "EnemyTeetered: " + teeterCount + "   EnemyFell: " + fellCount + "   Last fall slashId: " + lastFellSlashId, label);
        GUI.enabled = !gameOver.IsGameOver;
        if (GUI.Button(new Rect(24, 565, 210, 42), "Launch (stop)", button)) LaunchSoft();
        if (GUI.Button(new Rect(246, 565, 210, 42), "Hit again / glow", button)) LaunchAgain();
        if (GUI.Button(new Rect(468, 565, 210, 42), "Teeter at edge", button)) ShowTeeter();
        if (GUI.Button(new Rect(690, 565, 210, 42), "Launch over edge", button)) LaunchOverEdge();
        if (GUI.Button(new Rect(912, 565, 264, 42), "GameOver event test", button)) EndGameForTest();
        GUI.enabled = true;
        if (GUI.Button(new Rect(24, 620, 210, 42), "Reset test", button)) ResetTest();
        GUI.Label(new Rect(250, 624, 940, 30), "Initial velocity -> custom friction / gravity.  No Rigidbody forces.", label);
    }
}
