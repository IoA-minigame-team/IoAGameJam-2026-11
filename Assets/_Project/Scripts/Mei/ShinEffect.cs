using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 大銘「震」の効果(仕様書「銘」:斬り抜けて止まった瞬間、自分の周りの敵を外へ押し出す)。増永の担当。
/// 大銘が変わったこと(MajorMeiChanged)を受けて、震が刻まれている間だけ、斬り抜けが終わるたび(SlashFinished)に周りの敵を押し出す。
/// 敵を動かすのは敵自身(三條さん)なので、ここは ILaunchable.Launch で初速を渡すだけ。物理エンジンで力は加えない。
/// slashId は直前の SlashFinished の番号を使うので、押し出して落ちた敵も同じ一振りとして数えられる(連携ガイド)。
/// </summary>
public sealed class ShinEffect : MonoBehaviour
{
    // 一度に押し出せる敵の数の上限(当たり判定の入れ物の大きさ)
    const int MAX_COLLIDERS = 64;
    // 斬り抜けの終点を何本分覚えておくか。古いものから捨てる
    const int MAX_REMEMBERED_SLASHES = 8;
    // 敵が真上にいて向きが決められないときに使う最小の距離(m)
    const float MIN_OFFSET = 0.01f;

    [SerializeField] MeiTuning tuning;
    [Tooltip("押し出す敵のレイヤー。髙橋さんの斬り抜けと同じ Enemy レイヤーにする")]
    [SerializeField] LayerMask enemyMask;

    readonly Collider[] colliderBuffer = new Collider[MAX_COLLIDERS];
    readonly HashSet<ILaunchable> pushed = new HashSet<ILaunchable>();
    // slashId ごとの斬り抜けの終点。SlashFinished には位置が無いので、SlashStarted で覚えておく
    readonly Dictionary<int, Vector3> slashEnds = new Dictionary<int, Vector3>();
    readonly Queue<int> slashOrder = new Queue<int>();
    MajorMeiId current = MajorMeiId.None;

    void OnEnable()
    {
        GameEvents.MajorMeiChanged += OnMajorMeiChanged;
        GameEvents.SlashStarted += OnSlashStarted;
        GameEvents.SlashFinished += OnSlashFinished;
    }

    void OnDisable()
    {
        GameEvents.MajorMeiChanged -= OnMajorMeiChanged;
        GameEvents.SlashStarted -= OnSlashStarted;
        GameEvents.SlashFinished -= OnSlashFinished;
    }

    void OnMajorMeiChanged(MajorMeiId major) => current = major;

    void OnSlashStarted(SlashStartedInfo info)
    {
        slashEnds[info.SlashId] = info.End;
        slashOrder.Enqueue(info.SlashId);
        while (slashOrder.Count > MAX_REMEMBERED_SLASHES) slashEnds.Remove(slashOrder.Dequeue());
    }

    void OnSlashFinished(SlashFinishedInfo info)
    {
        if (current != MajorMeiId.Shin || tuning == null) return;
        Push(CenterOf(info.SlashId), info.SlashId);
    }

    /// <summary>止まった場所。主人公がいれば主人公の位置、いなければ斬り抜けの終点(主人公は終点で止まるため)</summary>
    Vector3 CenterOf(int slashId)
    {
        if (GameContext.PlayerTransform != null) return GameContext.PlayerTransform.position;
        return slashEnds.TryGetValue(slashId, out Vector3 end) ? end : transform.position;
    }

    void Push(Vector3 center, int slashId)
    {
        Feedback.PlaySe(SeId.Shin, center);
        Feedback.Shake(tuning.ShinShakeStrength, tuning.ShinShakeDuration);
        Feedback.SpawnVfx(VfxId.LightRing, center, Quaternion.identity);

        float radius = tuning.ShinRadius;
        int count = Physics.OverlapSphereNonAlloc(center, radius, colliderBuffer, enemyMask, QueryTriggerInteraction.Collide);
        float launchPower = tuning.ShinPower * GameContext.MeiStats.LaunchPowerMultiplier;

        pushed.Clear();
        for (int i = 0; i < count; i++)
        {
            // 1つの敵に当たり判定が複数あっても、押し出すのは1回だけ
            ILaunchable enemy = colliderBuffer[i].GetComponentInParent<ILaunchable>();
            colliderBuffer[i] = null;
            if (enemy == null || !pushed.Add(enemy)) continue;

            Vector3 offset = enemy.Position - center;
            offset.y = 0f;
            float distance = offset.magnitude;
            // 真上にいて向きが決められない敵は、適当な外向きに押し出す
            Vector3 direction = distance > MIN_OFFSET ? offset / distance : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;

            // 盾を中心へ向けている敵は、斬り抜けと同じく正面からは押し出せない(押す向き=刀が進む向きとして渡す)
            if (enemy.IsShieldFacing(direction)) continue;

            // 近いほど強く。重さと光の蓄積は、斬り抜けと同じく敵から読んで掛ける
            float edge = Mathf.Clamp01(distance / radius);
            float power = launchPower
                * Mathf.Lerp(1f, tuning.ShinEdgePowerRatio, edge)
                * enemy.Weight
                * enemy.GlowMultiplier;
            enemy.Launch(direction * power, slashId);
        }
        pushed.Clear();
    }
}
