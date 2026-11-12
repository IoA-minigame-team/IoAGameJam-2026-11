using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玉突き。吹き飛んだ敵が止まっている敵に当たったら、その敵も吹き飛ばす。
/// 落ちた敵を同じ一振りとして数えてもらうため、当てた敵の slashId をそのまま渡す。
/// 敵の速度は三條さんの Enemy が持っていて ILaunchable からは読めないので、位置の変化から測る。
/// 当てた側は減速させない(減速させるには Launch を呼び直すしかなく、光の蓄積まで増えてしまうため)
/// </summary>
public sealed class TamatsukiResolver : MonoBehaviour
{
    // 一度の掃引で拾うコライダーの上限
    const int MAX_COLLIDERS = 32;
    // Collider から敵を引く表がこれより大きくなったら作り直す。消えた敵の Collider が残り続けないようにするため
    const int MAX_CACHE_SIZE = 256;
    // これより短い移動は掃引せず、その場の重なりだけを調べる
    const float MIN_SWEEP_DISTANCE = 0.0001f;

    /// <summary>玉突きのために追っている、飛んでいる敵1体</summary>
    sealed class Flyer
    {
        public ILaunchable Enemy;
        public int SlashId;
        public Vector3 PreviousPosition;
        public Vector3 Velocity;
        public float Elapsed;
        public bool Measured;
        // 盾に弾かれた相手。重なっている間、毎フレーム弾かれた音が鳴らないよう覚えておく
        public readonly HashSet<ILaunchable> Blocked = new HashSet<ILaunchable>();
    }

    [SerializeField] PlayerTuning tuning;
    [Tooltip("止まっている敵を探すレイヤー。PlayerController の enemyMask と同じにする")]
    [SerializeField] LayerMask enemyMask;

    readonly List<Flyer> flyers = new List<Flyer>();
    readonly Collider[] colliderBuffer = new Collider[MAX_COLLIDERS];
    readonly RaycastHit[] hitBuffer = new RaycastHit[MAX_COLLIDERS];
    readonly Dictionary<Collider, ILaunchable> launchableCache = new Dictionary<Collider, ILaunchable>();
    readonly HashSet<ILaunchable> launchedThisFrame = new HashSet<ILaunchable>();

    void OnEnable()
    {
        GameEvents.EnemyHit += OnEnemyHit;
    }

    void OnDisable()
    {
        GameEvents.EnemyHit -= OnEnemyHit;
        flyers.Clear();
        launchableCache.Clear();
    }

    // 斬り抜けの当たりも玉突きの当たりもここに来る。玉突きで飛んだ敵もまた玉突きを起こせる
    void OnEnemyHit(EnemyHitInfo info)
    {
        if (IsGone(info.Enemy)) return;

        Flyer flyer = FindFlyer(info.Enemy);
        if (flyer == null)
        {
            flyer = new Flyer { Enemy = info.Enemy };
            flyers.Add(flyer);
        }

        // 同じ敵がもう一度飛ばされたときは、新しい一振りとして追い直す
        flyer.SlashId = info.SlashId;
        flyer.PreviousPosition = info.Enemy.Position;
        flyer.Velocity = Flatten(info.Velocity);
        flyer.Elapsed = 0f;
        flyer.Measured = false;
        flyer.Blocked.Clear();
    }

    // 敵は Update で動くので、動き終えた後の位置で測る
    void LateUpdate()
    {
        if (tuning == null || !tuning.TamatsukiEnabled)
        {
            flyers.Clear();
            return;
        }

        // 止まっている間は敵も動かない。位置の差が 0 になり、止まったと誤って追うのをやめないようにする
        float dt = Time.deltaTime;
        if (dt <= 0f || TimeController.IsPaused || TimeController.IsHitStopping) return;

        launchedThisFrame.Clear();

        // 後ろから回す。玉突きで飛んだ敵は末尾に足されるので、このフレームでは調べない(次のフレームから)
        for (int i = flyers.Count - 1; i >= 0; i--)
        {
            if (i >= flyers.Count) continue;
            Flyer flyer = flyers[i];

            if (IsGone(flyer.Enemy))
            {
                flyers.RemoveAt(i);
                continue;
            }

            Vector3 position = flyer.Enemy.Position;

            // 飛ばした直後は、敵の Update と順番によってまだ動いていないことがある。最初のフレームは渡した初速を使う
            if (flyer.Measured) flyer.Velocity = Flatten(position - flyer.PreviousPosition) / dt;
            else flyer.Measured = true;

            flyer.Elapsed += dt;
            if (flyer.Elapsed > tuning.TamatsukiMaxTrackTime || flyer.Velocity.magnitude < tuning.TamatsukiMinSpeed)
            {
                flyers.RemoveAt(i);
                continue;
            }

            CheckHits(flyer, flyer.PreviousPosition, position);
            flyer.PreviousPosition = position;
        }
    }

    /// <summary>前フレームの位置から今の位置まで、敵の体の形を掃引して当たった敵を飛ばす</summary>
    void CheckHits(Flyer flyer, Vector3 from, Vector3 to)
    {
        float radius = tuning.TamatsukiRadius;
        Vector3 bottom = Vector3.up * radius;
        Vector3 top = Vector3.up * Mathf.Max(radius, tuning.TamatsukiBodyHeight - radius);

        Vector3 move = to - from;
        float distance = move.magnitude;

        // 速い敵は1フレームで体1つ分以上進むので、重なりだけだとすり抜ける。掃引で間の敵も拾う
        if (distance < MIN_SWEEP_DISTANCE)
        {
            int count = Physics.OverlapCapsuleNonAlloc(
                to + bottom, to + top, radius, colliderBuffer, enemyMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                TryLaunch(flyer, to, colliderBuffer[i], true);
                colliderBuffer[i] = null;
            }
        }
        else
        {
            Vector3 moveDirection = move / distance;
            int count = Physics.CapsuleCastNonAlloc(
                from + bottom, from + top, radius, moveDirection, hitBuffer, distance, enemyMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hitBuffer[i];
                hitBuffer[i] = default;
                // 掃引の始めから重なっていた相手は、距離 0 で接触点が原点になる(Unity の掃引の仕様)
                bool overlappedAtStart = hit.distance <= 0f && hit.point == Vector3.zero;
                // 向きは終点ではなく接触した瞬間の位置で決める。終点で見ると、1フレームで追い越した相手が後ろにいることになるため
                Vector3 contactPosition = from + moveDirection * hit.distance;
                TryLaunch(flyer, contactPosition, hit.collider, overlappedAtStart);
            }
        }
    }

    /// <summary>
    /// flyerPosition は当てた敵の、接触した瞬間の位置。
    /// overlappedAtStart は調べ始めた時点ですでに重なっていた相手か(掃引で近づいて当たった相手ではない)
    /// </summary>
    void TryLaunch(Flyer flyer, Vector3 flyerPosition, Collider collider, bool overlappedAtStart)
    {
        ILaunchable target = GetLaunchable(collider);
        if (target == null || ReferenceEquals(target, flyer.Enemy) || IsGone(target)) return;
        // 同じフレームで2体から当てられても1回だけ飛ばす
        if (launchedThisFrame.Contains(target) || flyer.Blocked.Contains(target)) return;
        // 飛んでいる者同士は当てない。三條さんの Enemy は飛んでいる間 Collider を切るが、切らない敵もいるため
        if (FindFlyer(target) != null) return;

        float flyerSpeed = flyer.Velocity.magnitude;
        if (flyerSpeed <= 0f) return;
        Vector3 flyerDirection = flyer.Velocity / flyerSpeed;

        Vector3 toTarget = Flatten(target.Position - flyerPosition);
        Vector3 centerDirection = toTarget.sqrMagnitude > 0.000001f ? toTarget.normalized : flyerDirection;
        if (Vector3.Dot(centerDirection, flyerDirection) <= 0f)
        {
            // 初めから重なっていて後ろにいる相手は、もう追い越した相手なので飛ばさない
            if (overlappedAtStart) return;
            // 掃引で当たったのに中心が横か後ろにある(体の縁がかすった)ときは、後ろへ飛ばさないよう進む向きで押す
            centerDirection = flyerDirection;
        }

        Vector3 direction = Vector3.Slerp(flyerDirection, centerDirection, tuning.TamatsukiAimBlend);
        direction.y = 0f;
        direction.Normalize();

        Vector3 hitPosition = target.Position;

        if (target.IsShieldFacing(direction))
        {
            flyer.Blocked.Add(target);
            Feedback.PlaySe(SeId.ShieldBlock, hitPosition);
            return;
        }

        // 銘の吹き飛ばし力は当てた敵の速さにもう入っているので掛け直さない。重さと光の蓄積は当てられた敵から読む
        float power = flyerSpeed * tuning.TamatsukiTransferRatio * target.Weight * target.GlowMultiplier;
        Vector3 velocity = direction * power;

        launchedThisFrame.Add(target);
        // 契約どおり Launch の後に EnemyHit を流す。EnemyHit を受けた自分がこの敵を追い始めるので、玉突きは何段でも続く
        target.Launch(velocity, flyer.SlashId);
        GameEvents.RaiseEnemyHit(new EnemyHitInfo(flyer.SlashId, target, velocity, 1f));
        Feedback.PlaySe(SeId.Hit, hitPosition);
        // 敵の原点は足元なので、体の高さに出して地面に埋まらないようにする
        Feedback.SpawnVfx(VfxId.Hit, hitPosition + Vector3.up * tuning.HitVfxHeight, Quaternion.identity);
    }

    ILaunchable GetLaunchable(Collider collider)
    {
        if (collider == null) return null;
        if (launchableCache.TryGetValue(collider, out ILaunchable cached)) return cached;

        if (launchableCache.Count >= MAX_CACHE_SIZE) launchableCache.Clear();
        // 初めて見た Collider にだけ呼ぶ。毎フレーム GetComponent しないため
        ILaunchable launchable = collider.GetComponentInParent<ILaunchable>();
        launchableCache[collider] = launchable;
        return launchable;
    }

    Flyer FindFlyer(ILaunchable enemy)
    {
        for (int i = 0; i < flyers.Count; i++)
        {
            if (ReferenceEquals(flyers[i].Enemy, enemy)) return flyers[i];
        }
        return null;
    }

    // 落ちた敵は Destroy されるので、Unity の null 判定で確かめる
    static bool IsGone(ILaunchable enemy) =>
        enemy == null || (enemy is Object unityObject && unityObject == null);

    static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
