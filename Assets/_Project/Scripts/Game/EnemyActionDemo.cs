using UnityEngine;

/// <summary>Sanjoの確認シーン専用。実際のHPや敵AIを持たず、共有イベントの接続を試す。</summary>
public sealed class EnemyActionDemo : MonoBehaviour
{
    [SerializeField] EnemyActionAnimation[] actors;
    [SerializeField] GameOverController gameOver;
    int remaining = 3;
    bool defending;

    public void SetDefense(bool value)
    {
        defending = value;
        foreach (var actor in actors) actor.SetDefending(value);
    }

    public void Attack()
    {
        foreach (var actor in actors) actor.PlayAttack();
    }

    public void Spawn()
    {
        defending = false;
        foreach (var actor in actors) actor.PlaySpawn();
    }

    public void SimulateDamage()
    {
        if (gameOver.IsGameOver) return;
        remaining = Mathf.Max(0, remaining - 1);
        GameEvents.RaisePlayerDamaged(remaining);
    }

    public void ResetDemo()
    {
        remaining = 3;
        defending = false;
        gameOver.ResetRound();
        foreach (var actor in actors) actor.ResetPresentation();
    }

    void OnGUI()
    {
        // テスト専用UI。担当Cの本番UIとは接続しない。
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1200f, Screen.height / 700f, 1f));
        var label = new GUIStyle(GUI.skin.label) { fontSize = 20 };
        var button = new GUIStyle(GUI.skin.button) { fontSize = 18 };
        GUI.Label(new Rect(24, 16, 1150, 32), "ENEMY ACTION TEST   |   Shield / Shadow / Werewolf", label);
        GUI.Label(new Rect(24, 49, 1150, 32), gameOver.IsGameOver ? "GAME OVER - event received, animations stopped" : "Animation + shared PlayerDamaged event test", label);
        GUI.enabled = !gameOver.IsGameOver;
        if (GUI.Button(new Rect(24, 595, 200, 44), "Spawn / Pop", button)) Spawn();
        if (GUI.Button(new Rect(236, 595, 220, 44), defending ? "Defense OFF" : "Defense ON", button)) SetDefense(!defending);
        if (GUI.Button(new Rect(468, 595, 200, 44), "Attack", button)) Attack();
        if (GUI.Button(new Rect(680, 595, 275, 44), "Test damage (" + remaining + " left)", button)) SimulateDamage();
        GUI.enabled = true;
        if (GUI.Button(new Rect(967, 595, 210, 44), "Reset test", button)) ResetDemo();
        GUI.Label(new Rect(24, 650, 1150, 32), "Test damage x3 -> GameOver once.  This scene does not implement enemy AI / combat damage.", label);
    }
}
