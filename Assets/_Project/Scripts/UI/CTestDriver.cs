using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// テスト用(Dev シーン専用、本番シーンには置かない)。キーボードで髙橋さんと三條さんのイベントを「それらしい順番」で流し、
/// 2人の実装が無くても、得点、一振りのカウント、称号、連鎖、ウェーブの表示を試せるようにする。
/// GameEventsDebugger(右クリックで1つずつ流す)の、通しで流す版。
/// </summary>
public sealed class CTestDriver : MonoBehaviour
{
    [Tooltip("1キーの一振りで落とす数")]
    [SerializeField, Min(1)] int fallsPerSwing = 7;
    [Tooltip("当たってから落ちるまでの間隔(秒)")]
    [SerializeField, Min(0f)] float fallInterval = 0.12f;
    [SerializeField] bool showHelp = true;
    [Tooltip("記録(一振り最多など)を画面の隅に出すため。HUD には得点しか出ないので、確認用にここで見る")]
    [SerializeField] ScoreCounter score;

    int slashId = 1000;
    int chain;
    int wave;
    DummyLaunchable dummy;

    void Awake() => dummy = new DummyLaunchable(transform);

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (k == null) return;

        if (k.digit1Key.wasPressedThisFrame) StartCoroutine(Swing(fallsPerSwing));
        if (k.digit2Key.wasPressedThisFrame) StartCoroutine(Swing(0));
        if (k.digit3Key.wasPressedThisFrame) { wave++; GameEvents.RaiseWaveStarted(wave); }
        if (k.digit0Key.wasPressedThisFrame) StartCoroutine(Swing(20));
    }

    /// <summary>一振りを流す。count が 0 なら空振り</summary>
    IEnumerator Swing(int count)
    {
        slashId++;
        int id = slashId;
        Vector3 start = transform.position, end = start + Vector3.forward * 8f;

        GameEvents.RaiseSwordPlanted(start);
        yield return new WaitForSeconds(0.3f);
        GameEvents.RaiseSlashStarted(new SlashStartedInfo(id, start, end));

        // 斬り抜けは0.2秒。その間に当たったことにする
        for (int i = 0; i < count; i++)
        {
            GameEvents.RaiseEnemyHit(new EnemyHitInfo(id, dummy, Vector3.forward * 8f, 1f));
            yield return new WaitForSeconds(0.2f / Mathf.Max(1, count));
        }
        GameEvents.RaiseSlashFinished(new SlashFinishedInfo(id, count));
        // 1体以上当たれば連鎖が続き、空振りなら 0 に戻る(仕様)
        chain = count > 0 ? chain + 1 : 0;
        GameEvents.RaiseChainChanged(chain);

        // 吹き飛んだ敵が順に縁を越えて落ちる
        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(fallInterval);
            GameEvents.RaiseEnemyFell(new EnemyFellInfo(id, end + Vector3.right * Random.Range(-2f, 2f)));
        }
    }

    void OnGUI()
    {
        if (!showHelp) return;
        string record = score == null ? "" :
            $"\n得点 {score.Score}  一振り最多 {score.MaxSwing}  最大連鎖 {score.MaxChain}  落とした数 {score.Falls}";
        GUI.Box(new Rect(10, Screen.height - 150, 420, 140),
            "C テスト(キーボード)\n" +
            $"1: {fallsPerSwing}体まとめて落とす一振り\n" +
            "2: 空振り(連鎖が 0 に戻る)\n" +
            "3: ウェーブ開始\n" +
            "0: 20体落とす一振り(一閃、断ち、一刀)" + record);
    }

    /// <summary>テスト用の仮の敵。Launch されてもログを出すだけ</summary>
    sealed class DummyLaunchable : ILaunchable
    {
        readonly Transform owner;
        public DummyLaunchable(Transform owner) => this.owner = owner;
        public Transform Transform => owner;
        public Vector3 Position => owner.position;
        public float Weight => 1f;
        public float GlowMultiplier => 1f;
        public bool IsShieldFacing(Vector3 slashDir) => false;
        public void Launch(Vector3 velocity, int id) => Debug.Log($"[CTest] Launch vel={velocity} id={id}");
    }
}
