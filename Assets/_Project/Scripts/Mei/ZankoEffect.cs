using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 大銘「残光」の効果(仕様書「銘」:斬り抜けた線がしばらく残り、触れた敵を押し出す)。
/// 斬り抜けが終わったら(SlashFinished)その線に黒い雷を残し、残っている間に触れた敵を線と直角の向きへ弾き出す。
/// 敵を動かすのは敵自身なので、ここは ILaunchable.Launch で初速を渡すだけ。slashId はその線の番号を使い、落ちた敵を同じ一振りとして数えてもらう。
/// </summary>
public sealed class ZankoEffect : MonoBehaviour
{
    const int MAX_COLLIDERS = 64;
    const int MAX_REMEMBERED_SLASHES = 8;
    // 雷を調べる箱の高さ(m)。斬り抜けの判定(SlashResolver)と同じく、背の高さの違う敵を取りこぼさない程度にとる
    const float CHECK_HEIGHT = 4f;
    // これより短い線は向きが決められないので雷を残さない(主人公の足元で刀を抜いたときなど)
    const float MIN_LINE_LENGTH = 0.3f;
    // 残っている雷を描き直す間隔(秒)。BlackBolt は寿命とともに薄くなるので、重ねて出し続けて最後だけ消えていくように見せる
    const float BOLT_REFRESH_INTERVAL = 0.2f;
    // 敵を弾いた所に出す小さな雷の長さ(m)と寿命(秒)
    const float SPARK_LENGTH = 1.2f;
    const float SPARK_LIFETIME = 0.25f;

    sealed class Line
    {
        public int SlashId;
        public Vector3 Start;
        public Vector3 End;
        public float Remaining;
        public float CheckTimer;
        public float BoltTimer;
        public readonly HashSet<ILaunchable> Launched = new HashSet<ILaunchable>();
    }

    [SerializeField] MeiTuning tuning;
    [Tooltip("弾き出す敵のレイヤー。髙橋さんの斬り抜けと同じ Enemy レイヤーにする")]
    [SerializeField] LayerMask enemyMask;

    readonly Collider[] colliderBuffer = new Collider[MAX_COLLIDERS];
    readonly List<Line> lines = new List<Line>();
    // slashId ごとの線の始点と終点。SlashFinished には位置が無いので、SlashStarted で覚えておく
    readonly Dictionary<int, (Vector3 start, Vector3 end)> slashLines = new Dictionary<int, (Vector3, Vector3)>();
    readonly Queue<int> slashOrder = new Queue<int>();
    MajorMeiId current = MajorMeiId.None;

    void OnEnable()
    {
        GameEvents.MajorMeiChanged += OnMajorMeiChanged;
        GameEvents.SlashStarted += OnSlashStarted;
        GameEvents.SlashFinished += OnSlashFinished;
        GameEvents.GameOver += OnGameOver;
    }

    void OnDisable()
    {
        GameEvents.MajorMeiChanged -= OnMajorMeiChanged;
        GameEvents.SlashStarted -= OnSlashStarted;
        GameEvents.SlashFinished -= OnSlashFinished;
        GameEvents.GameOver -= OnGameOver;
        lines.Clear();
    }

    void OnMajorMeiChanged(MajorMeiId major)
    {
        current = major;
        // 別の大銘に替えたら、残っている雷の判定も止める(見た目の雷は寿命で消える)
        if (major != MajorMeiId.Zanko) lines.Clear();
    }

    void OnGameOver() => lines.Clear();

    void OnSlashStarted(SlashStartedInfo info)
    {
        slashLines[info.SlashId] = (info.Start, info.End);
        slashOrder.Enqueue(info.SlashId);
        while (slashOrder.Count > MAX_REMEMBERED_SLASHES) slashLines.Remove(slashOrder.Dequeue());
    }

    void OnSlashFinished(SlashFinishedInfo info)
    {
        if (current != MajorMeiId.Zanko || tuning == null) return;
        if (!slashLines.TryGetValue(info.SlashId, out var points)) return;

        Vector3 flat = points.end - points.start;
        flat.y = 0f;
        if (flat.magnitude < MIN_LINE_LENGTH) return;

        Line line = new Line
        {
            SlashId = info.SlashId,
            Start = points.start,
            End = points.end,
            Remaining = tuning.ZankoDuration,
        };
        lines.Add(line);
        SpawnBolt(line);
        Feedback.PlaySe(SeId.Zap, (line.Start + line.End) * 0.5f);
        // 斬った直後に線の上にいる敵も、すぐ弾けるようにする
        CheckLine(line);
    }

    void Update()
    {
        if (lines.Count == 0 || tuning == null) return;
        // 敵と同じく、ポーズ中とヒットストップ中は雷も止める
        if (TimeController.IsPaused || TimeController.IsHitStopping) return;

        float dt = Time.deltaTime;
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            Line line = lines[i];
            line.Remaining -= dt;
            if (line.Remaining <= 0f)
            {
                lines.RemoveAt(i);
                continue;
            }

            line.BoltTimer -= dt;
            if (line.BoltTimer <= 0f) SpawnBolt(line);

            line.CheckTimer -= dt;
            if (line.CheckTimer <= 0f) CheckLine(line);
        }
    }

    /// <summary>線に沿って黒い雷を出す。残り時間が少ないほど短い寿命で出し、最後は薄れて消えるようにする</summary>
    void SpawnBolt(Line line)
    {
        line.BoltTimer = BOLT_REFRESH_INTERVAL;
        Vector3 lift = Vector3.up * tuning.ZankoBoltHeight;
        float life = Mathf.Min(line.Remaining, BOLT_REFRESH_INTERVAL * 2f);
        BlackBolt.Spawn(line.Start + lift, line.End + lift, tuning.ZankoBoltWidth, life, tuning.ZankoBoltJitter);
    }

    void CheckLine(Line line)
    {
        line.CheckTimer = tuning.ZankoCheckInterval;

        Vector3 flat = line.End - line.Start;
        flat.y = 0f;
        float length = flat.magnitude;
        Vector3 along = flat / length;
        Vector3 right = Vector3.Cross(Vector3.up, along);
        float radius = tuning.ZankoRadius;

        Vector3 center = (line.Start + line.End) * 0.5f;
        Vector3 halfExtents = new Vector3(radius, CHECK_HEIGHT * 0.5f, length * 0.5f + radius);
        int count = Physics.OverlapBoxNonAlloc(
            center, halfExtents, colliderBuffer, Quaternion.LookRotation(along), enemyMask, QueryTriggerInteraction.Collide);

        float launchPower = tuning.ZankoPower * GameContext.MeiStats.LaunchPowerMultiplier;
        for (int i = 0; i < count; i++)
        {
            ILaunchable enemy = colliderBuffer[i].GetComponentInParent<ILaunchable>();
            colliderBuffer[i] = null;
            // 1本の雷で同じ敵を弾くのは1回だけ。何度も弾くと線の上で敵が震えて見える
            if (enemy == null || line.Launched.Contains(enemy)) continue;

            // 箱は大まかな候補集めなので、線からの距離で正確に判定する
            Vector3 offset = enemy.Position - line.Start;
            offset.y = 0f;
            float t = Mathf.Clamp(Vector3.Dot(offset, along), 0f, length);
            Vector3 fromLine = offset - along * t;
            if (fromLine.magnitude > radius) continue;

            // 線のどちら側にいるかで、外へ弾く向きを決める
            Vector3 direction = Vector3.Dot(fromLine, right) >= 0f ? right : -right;
            if (enemy.IsShieldFacing(direction)) continue;

            line.Launched.Add(enemy);
            // 重さと光の蓄積は、斬り抜けと同じく敵から読んで掛ける
            Vector3 velocity = direction * (launchPower * enemy.Weight * enemy.GlowMultiplier);
            enemy.Launch(velocity, line.SlashId);
            // 当たったことも流し、飛ぶ敵の黒い雷の軌跡と玉突きを起こす
            GameEvents.RaiseEnemyHit(new EnemyHitInfo(line.SlashId, enemy, velocity, 1f));

            Vector3 spark = enemy.Position + Vector3.up * tuning.ZankoBoltHeight;
            BlackBolt.Spawn(spark, spark + direction * SPARK_LENGTH, tuning.ZankoBoltWidth, SPARK_LIFETIME, tuning.ZankoBoltJitter);
            Feedback.PlaySe(SeId.Zap, enemy.Position);
        }
    }
}
