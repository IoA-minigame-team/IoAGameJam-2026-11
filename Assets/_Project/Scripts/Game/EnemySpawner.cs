using System.Collections.Generic;
using UnityEngine;

/// <summary>自分が出現させた敵と、安全な位置選びだけを所有する。ウェーブ番号の進行は持たない。</summary>
public sealed class EnemySpawner : MonoBehaviour
{
    [SerializeField] EnemyWavePlan plan;
    [SerializeField] EnemySpawnPoint[] points;
    [SerializeField] Transform spawnedRoot;
    [SerializeField] LayerMask groundMask = 1;
    [Tooltip("柵などの障害物。床の判定と分けて、候補が柵に重ならないようにする")]
    [SerializeField] LayerMask movementObstacleMask = 4;
    readonly List<Enemy> owned = new List<Enemy>();
    int cursor;
    public IReadOnlyList<Enemy> OwnedEnemies => owned;
    public EnemyWavePlan Plan => plan;
    public EnemySpawnPoint[] Points => points;
    public int AliveCount => owned.Count;
    public int TotalSpawned { get; private set; }
    public int ConfirmedFalls { get; private set; }
    public bool HasUnexpectedRemoval { get; private set; }
    public string LastFailure { get; private set; }
    public Enemy LastSpawned { get; private set; }
    public string LastSpawnPoint { get; private set; }

    void OnEnable() => GameEvents.EnemyFell += OnEnemyFell;
    void OnDisable() => GameEvents.EnemyFell -= OnEnemyFell;
    void OnEnemyFell(EnemyFellInfo info) => RefreshOwnedEnemies();

    public void RefreshOwnedEnemies()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
        {
            var enemy = owned[i];
            // EnemyFellInfoに発行元の参照がないため、登録済みの個体の終端状態を照合する。
            // 破棄後でもStateはC#の保持値。別の敵の通知や偽の通知で残数を減らさない。
            if (!ReferenceEquals(enemy, null) && enemy.State == Enemy.MotionState.Fallen)
            {
                owned.RemoveAt(i);ConfirmedFalls++;
            }
            else if (enemy == null)
            {
                owned.RemoveAt(i);HasUnexpectedRemoval = true;
                LastFailure = "Owned enemy disappeared without EnemyFell. Check external Destroy / pooling.";
            }
        }
    }

    /// <summary>
    /// 先に決めた出現位置。予兆を出してから湧かせるために、位置選びと生成を分ける。
    /// 予約中の位置は他の予約や出現の候補から外す。
    /// </summary>
    public sealed class SpawnReservation
    {
        public GameObject Prefab { get; }
        public Vector3 Position { get; }
        public string PointName { get; }
        internal readonly CapsuleCollider Body;
        internal readonly Quaternion Rotation;
        internal SpawnReservation(GameObject prefab, CapsuleCollider body, Vector3 position, Quaternion rotation, string pointName)
        {
            Prefab = prefab;Body = body;Position = position;Rotation = rotation;PointName = pointName;
        }
    }
    readonly List<SpawnReservation> reservations = new List<SpawnReservation>();
    public int ReservationCount => reservations.Count;

    /// <summary>位置選びと生成を同じフレームで行う。予兆を出さない呼び出し側(確認コードなど)のための従来の口。</summary>
    public bool TrySpawn(GameObject prefab, EnemySpawnArea areas, out Enemy enemy)
    {
        enemy = null;
        return TryReserve(prefab, areas, out var reservation) && TrySpawnReserved(reservation, out enemy);
    }

    /// <summary>
    /// 出現位置だけを決めて押さえる。成功したら、予兆を出したあとで TrySpawnReserved を呼ぶ。
    /// </summary>
    public bool TryReserve(GameObject prefab, EnemySpawnArea areas, out SpawnReservation reservation)
    {
        reservation = null;
        if (plan == null || points == null || points.Length == 0) { LastFailure = "Wave plan or spawn points are missing.";return false; }
        if (prefab == null || prefab.GetComponent<Enemy>() == null) { LastFailure = "Spawn prefab needs Enemy (ILaunchable).";return false; }
        var prefabEnemy = prefab.GetComponent<Enemy>();
        var body = prefabEnemy.BodyCollider != null ? prefabEnemy.BodyCollider : prefab.GetComponent<CapsuleCollider>();
        if (body == null) { LastFailure = "Spawn prefab needs CapsuleCollider."; return false; }
        bool foundArea = false;
        for (int p = 0; p < points.Length; p++)
        {
            int index = (cursor + p) % points.Length;var point = points[index];
            if (point == null || !point.isActiveAndEnabled || (point.Areas & areas) == 0) continue;
            foundArea = true;
            for (int attempt = 0; attempt < plan.CandidateAttempts; attempt++)
            {
                float radius = attempt == 0 ? 0 : point.Radius * Mathf.Sqrt(attempt / (float)Mathf.Max(1, plan.CandidateAttempts - 1));
                float angle = attempt * 2.39996323f;
                var candidate = point.transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                if (!TryGround(candidate, out var position) || !HasMargin(position)) continue;
                Vector3 facing = point.transform.forward;
                var player = GameContext.PlayerTransform;
                if (player != null) facing = player.position - position;
                facing.y = 0;
                if (facing.sqrMagnitude < .000001f) facing = Vector3.forward;
                Quaternion rotation = Quaternion.LookRotation(facing);
                // 姿勢を決めてから検査する。中心がずれたColliderでも、実際に出す向きと判定を一致させる。
                if (!HasClearance(position, rotation, prefab.transform, body, null)) continue;
                reservation = new SpawnReservation(prefab, body, position, rotation, point.name);
                reservations.Add(reservation);
                cursor = (index + 1) % points.Length;LastFailure = null;
                return true;
            }
        }
        LastFailure = foundArea ? "Spawn points blocked: check ground, player distance and enemy spacing." : "No enabled spawn point matches the requested area.";
        return false;
    }

    /// <summary>
    /// 予約した位置に敵を出す。予兆の間に主人公や他の敵が入り込んだら出さずに false を返す。
    /// 成否にかかわらず予約は外れる(塞がれたら呼び出し側が予約し直す)。
    /// </summary>
    public bool TrySpawnReserved(SpawnReservation reservation, out Enemy enemy)
    {
        enemy = null;
        if (reservation == null || !reservations.Remove(reservation)) { LastFailure = "Spawn reservation is missing or already used.";return false; }
        var prefab = reservation.Prefab;var position = reservation.Position;
        if (prefab == null) { LastFailure = "Reserved prefab is missing.";return false; }
        // 向きは湧く瞬間の主人公へ向け直す。予兆の間に主人公が動いても背を向けて湧かない
        Quaternion rotation = reservation.Rotation;
        var player = GameContext.PlayerTransform;
        if (player != null)
        {
            Vector3 facing = player.position - position;facing.y = 0;
            if (facing.sqrMagnitude >= .000001f) rotation = Quaternion.LookRotation(facing);
        }
        if (!HasClearance(position, rotation, prefab.transform, reservation.Body, reservation))
        {
            LastFailure = "Reserved spawn position was blocked before the enemy appeared.";return false;
        }
        var instance = Instantiate(prefab, position, rotation, spawnedRoot);
        enemy = instance.GetComponent<Enemy>();
        owned.Add(enemy);LastSpawned = enemy;TotalSpawned++;LastSpawnPoint = reservation.PointName;
        LastFailure = null;
        return true;
    }

    /// <summary>出さずに予約を外す。</summary>
    public void CancelReservation(SpawnReservation reservation) => reservations.Remove(reservation);

    bool TryGround(Vector3 position, out Vector3 grounded)
    {
        grounded = position;
        if (!Physics.Raycast(position + Vector3.up * plan.ProbeHeight, Vector3.down, out var hit,
            plan.ProbeHeight + plan.ProbeDepth, groundMask, QueryTriggerInteraction.Ignore) || Vector3.Dot(hit.normal, Vector3.up) <= .5f) return false;
        grounded = hit.point;return true;
    }
    bool HasMargin(Vector3 position)
    {
        float margin = plan.GroundMargin;
        return TryGround(position + Vector3.right * margin, out _) && TryGround(position + Vector3.left * margin, out _)
            && TryGround(position + Vector3.forward * margin, out _) && TryGround(position + Vector3.back * margin, out _);
    }
    bool HasClearance(Vector3 position, Quaternion rotation, Transform prefabRoot, CapsuleCollider body, SpawnReservation self)
    {
        if (Physics.CheckSphere(position + Vector3.up, plan.GroundMargin, movementObstacleMask, QueryTriggerInteraction.Ignore)) return false;
        // 足元付近の余白だけでなく、頭や肩を含むカプセル全体が柵に重ならないことを確認する。
        EnemyCapsuleGeometry.GetAtSpawn(body, prefabRoot, position, rotation, spawnedRoot, out var bottom, out var top, out float radius);
        if (Physics.CheckCapsule(bottom, top, radius, movementObstacleMask, QueryTriggerInteraction.Ignore)) return false;
        var player = GameContext.PlayerTransform;
        if (player != null && FlatDistance(position, player.position) < plan.MinimumPlayerDistance) return false;
        foreach (var existing in owned)
            if (existing != null && FlatDistance(position, existing.Position) < plan.MinimumEnemyDistance) return false;
        // 予兆を出している場所にも、まだいない敵がいるものとして扱う
        foreach (var other in reservations)
            if (other != self && FlatDistance(position, other.Position) < plan.MinimumEnemyDistance) return false;
        return true;
    }
    static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0;return Vector3.Distance(a, b); }

    public void ClearOwnedEnemies()
    {
        // リセットは撃破ではない。スコアやWaveClearedのためのEnemyFellを捏造しない。
        foreach (var enemy in owned) if (enemy != null) Destroy(enemy.gameObject);
        owned.Clear();reservations.Clear();cursor = TotalSpawned = ConfirmedFalls = 0;HasUnexpectedRemoval = false;
        LastSpawned = null;LastSpawnPoint = null;LastFailure = null;
    }
}
