using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

/// <summary>
/// テスト用(Dev シーン専用、本番シーンには置かない)。キーボードで髙橋さんと三條さんのイベントを「それらしい順番」で流し、
/// 2人の実装が無くても、得点、一振りのカウント、称号、連鎖、ウェーブの表示と、揺れ、SE、エフェクトを試せるようにする。
/// Feedback の呼び方は、髙橋さんの PlayerController(feature/kapi-player-control)と同じ場所、同じ ID にそろえてある。
/// GameEventsDebugger(右クリックで1つずつ流す)の、通しで流す版。
/// </summary>
public sealed class CTestDriver : MonoBehaviour
{
    [Tooltip("1キーの一振りで落とす数")]
    [SerializeField, Min(1)] int fallsPerSwing = 7;
    [Tooltip("当たってから落ちるまでの間隔(秒)")]
    [SerializeField, Min(0f)] float fallInterval = 0.12f;
    [Tooltip("4キーで揺らす強さと秒数")]
    [SerializeField, Min(0f)] float shakeStrength = 0.5f;
    [SerializeField, Min(0f)] float shakeDuration = 0.25f;
    [Tooltip("6キーでエフェクトを出す位置(このオブジェクトからのずれ)")]
    [SerializeField] Vector3 vfxOffset = new Vector3(0f, 0f, 3f);
    [SerializeField] bool showHelp = true;
    [Tooltip("記録(一振り最多など)を画面の隅に出すため。HUD には得点しか出ないので、確認用にここで見る")]
    [SerializeField] ScoreCounter score;

    // 5キー、6キーで順に試す ID の一覧(None は飛ばす)
    static readonly SeId[] ALL_SE = (SeId[])Enum.GetValues(typeof(SeId));
    static readonly VfxId[] ALL_VFX = (VfxId[])Enum.GetValues(typeof(VfxId));

    int slashId = 1000;
    int chain;
    int wave;
    int seIndex;
    int vfxIndex;
    string lastTried = "";

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (k == null) return;

        if (k.digit1Key.wasPressedThisFrame) StartCoroutine(Swing(fallsPerSwing));
        if (k.digit2Key.wasPressedThisFrame) StartCoroutine(Swing(0));
        if (k.digit3Key.wasPressedThisFrame) { wave++; GameEvents.RaiseWaveStarted(wave); }
        if (k.digit0Key.wasPressedThisFrame) StartCoroutine(Swing(20));
        if (k.digit4Key.wasPressedThisFrame) { Feedback.Shake(shakeStrength, shakeDuration); lastTried = "揺れ"; }
        if (k.digit5Key.wasPressedThisFrame) PlayNextSe();
        if (k.digit6Key.wasPressedThisFrame) SpawnNextVfx();
    }

    /// <summary>一振りを流す。count が 0 なら空振り</summary>
    IEnumerator Swing(int count)
    {
        slashId++;
        int id = slashId;
        Vector3 start = transform.position, end = start + Vector3.forward * 8f;

        // 置く(髙橋さんは刺したときに、刺さる音と光の輪を呼ぶ)
        GameEvents.RaiseSwordPlanted(start);
        Feedback.PlaySe(SeId.SwordPlant, start);
        Feedback.SpawnVfx(VfxId.LightRing, start, Quaternion.identity);
        yield return new WaitForSeconds(0.3f);

        // 斬り抜け(斬り抜けの音と光の軌跡)
        GameEvents.RaiseSlashStarted(new SlashStartedInfo(id, start, end));
        Feedback.PlaySe(SeId.Slash, start);
        Feedback.SpawnVfx(VfxId.SlashTrail, start, Quaternion.LookRotation(end - start));

        // 斬り抜けは0.2秒。その間に、線の上に並んだ敵に順に当たったことにする
        for (int i = 0; i < count; i++)
        {
            Vector3 p = Vector3.Lerp(start, end, (i + 1f) / (count + 1f));
            GameEvents.RaiseEnemyHit(new EnemyHitInfo(id, new DummyLaunchable(transform, p), Vector3.forward * 8f, 1f));
            Feedback.PlaySe(SeId.Hit, p);
            Feedback.SpawnVfx(VfxId.Hit, p, Quaternion.identity);
            yield return new WaitForSeconds(0.2f / Mathf.Max(1, count));
        }
        GameEvents.RaiseSlashFinished(new SlashFinishedInfo(id, count));
        // 1体以上当たれば連鎖が続き、空振りなら 0 に戻る(仕様)
        chain = count > 0 ? chain + 1 : 0;
        GameEvents.RaiseChainChanged(chain);
        Feedback.PlaySe(count > 0 ? SeId.Sheathe : SeId.Whiff, end);

        // 吹き飛んだ敵が順に縁を越えて落ちる(縁を越える音は三條さんが呼ぶ想定)
        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(fallInterval);
            Vector3 fallPosition = end + Vector3.right * Random.Range(-2f, 2f);
            Feedback.PlaySe(SeId.EdgeCross, fallPosition);
            GameEvents.RaiseEnemyFell(new EnemyFellInfo(id, fallPosition));
        }
    }

    void PlayNextSe()
    {
        seIndex = NextIndex(seIndex, ALL_SE.Length);
        SeId id = ALL_SE[seIndex];
        Feedback.PlaySe(id, transform.position);
        lastTried = "SE " + id;
    }

    void SpawnNextVfx()
    {
        vfxIndex = NextIndex(vfxIndex, ALL_VFX.Length);
        VfxId id = ALL_VFX[vfxIndex];
        // 軌跡は向きがあるので、カメラから見やすい横向きに出す
        Feedback.SpawnVfx(id, transform.position + vfxOffset, Quaternion.LookRotation(Vector3.right));
        lastTried = "エフェクト " + id;
    }

    // 0 番(None)を飛ばして順に回す
    static int NextIndex(int current, int length) => current + 1 < length ? current + 1 : 1;

    void OnGUI()
    {
        if (!showHelp) return;
        string record = score == null ? "" :
            $"\n得点 {score.Score}  一振り最多 {score.MaxSwing}  最大連鎖 {score.MaxChain}  落とした数 {score.Falls}";
        GUI.Box(new Rect(10, Screen.height - 230, 460, 220),
            "C テスト(キーボード)\n" +
            $"1: {fallsPerSwing}体まとめて落とす一振り\n" +
            "2: 空振り(連鎖が 0 に戻る)\n" +
            "3: ウェーブ開始\n" +
            "0: 20体落とす一振り(一閃、断ち、一刀)\n" +
            "4: 画面を揺らす\n" +
            "5: SE を順に鳴らす\n" +
            "6: エフェクトを順に出す" +
            record +
            (lastTried.Length > 0 ? "\n最後に試したもの: " + lastTried : ""));
    }

    /// <summary>テスト用の仮の敵。当たった位置だけ持つ。Launch されてもログを出すだけ</summary>
    sealed class DummyLaunchable : ILaunchable
    {
        readonly Transform owner;
        readonly Vector3 position;

        public DummyLaunchable(Transform owner, Vector3 position)
        {
            this.owner = owner;
            this.position = position;
        }

        public Transform Transform => owner;
        public Vector3 Position => position;
        public float Weight => 1f;
        public float GlowMultiplier => 1f;
        public bool IsShieldFacing(Vector3 slashDir) => false;
        public void Launch(Vector3 velocity, int id) => Debug.Log($"[CTest] Launch vel={velocity} id={id}");
    }
}
