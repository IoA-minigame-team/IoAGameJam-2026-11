using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 揺れ、SE、エフェクトの中身(IFeedbackService の実装)。OnEnable で Feedback.Service に入るので、
/// 髙橋さんと三條さんは Feedback.PlaySe(SeId.Slash, pos) のように呼ぶだけで鳴る。
/// どの音とエフェクトを出すかは FeedbackTuning で選ぶので、素材を差し替えてもコードは変わらない。
///
/// 自分でイベントを受けて出すのは、増永が足した音と、連携ガイドで増永が出すことになっているものだけ
/// (落ちる音と光の粒、踏みとどまる音、ウェーブ開始の音、吹き飛ぶ敵の残光)。
/// 敵が白く光るのと縁で揺れるのは、三條さんの EnemyActionAnimation が同じイベントで出しているので、ここでは出さない(二重になるため)。主人公まわりの音とエフェクトは髙橋さんが呼ぶので、ここでは鳴らさない(二重になるため)。
/// </summary>
public sealed class FeedbackService : MonoBehaviour, IFeedbackService
{
    [SerializeField] FeedbackTuning tuning;
    [Tooltip("画面の揺れを出す元。カメラ側に CinemachineImpulseListener が要る(本番のカメラは髙橋さんに付けてもらう)")]
    [SerializeField] CinemachineImpulseSource impulseSource;
    [Tooltip("同時に鳴らせる音の数。足りないときは古い音から止まる")]
    [SerializeField, Min(1)] int voiceCount = 16;

    AudioSource[] voices;
    // それぞれの声を鳴らし始めた実時間。全部が鳴っているときに、いちばん前に鳴らし始めたものを止めるため
    // 鳴らし始めた順番(大きいほど新しい)。時刻だと同じフレームで鳴らした音が同じ値になり、古さを比べられないため連番にする
    long[] voiceStartedOrder;
    long playCount;
    int nextVoice;

    // 一振りの中で何回目の当たりの音か、何回目の縁を越える音か。斬り抜けが始まるたびに 0 に戻し、数に応じて音を高くする
    int hitStep;
    int edgeStep;

    // 当たりの音を少しずつずらして鳴らすための待ち行列(鳴らす実時間と、音の高さ)
    readonly Queue<KeyValuePair<float, float>> hitQueue = new Queue<KeyValuePair<float, float>>();
    float nextHitTime;
    // 最後に当たりの音を実際に鳴らした実時間。止まったコマのあとでも、間をあけて1つずつ鳴らすため
    float lastHitPlayed = float.NegativeInfinity;

    void Awake()
    {
        // 1つの AudioSource で1つの音を鳴らす。PlayOneShot だと、後から pitch を変えたときに鳴っている音まで変わってしまうため
        voices = new AudioSource[voiceCount];
        voiceStartedOrder = new long[voiceCount];
        for (int i = 0; i < voiceCount; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // 見下ろしの固定カメラなので、左右の定位はつけない
            voices[i] = source;
        }
    }

    void OnEnable()
    {
        Feedback.Service = this;
        // 残光の黒い雷を描くマテリアルを渡す(エフェクトを作る前は空なので、そのときは雷を出さない)
        if (tuning != null) BlackBolt.Configure(tuning.BoltCoreMaterial, tuning.BoltRimMaterial);
        GameEvents.SlashStarted += OnSlashStarted;
        GameEvents.EnemyHit += OnEnemyHit;
        GameEvents.EnemyFell += OnEnemyFell;
        GameEvents.EnemyTeetered += OnEnemyTeetered;
        GameEvents.WaveStarted += OnWaveStarted;
    }

    void OnDisable()
    {
        // 別の中身に入れ替わっていたら消さない
        if (ReferenceEquals(Feedback.Service, this)) Feedback.Service = null;
        GameEvents.SlashStarted -= OnSlashStarted;
        GameEvents.EnemyHit -= OnEnemyHit;
        GameEvents.EnemyFell -= OnEnemyFell;
        GameEvents.EnemyTeetered -= OnEnemyTeetered;
        GameEvents.WaveStarted -= OnWaveStarted;
        hitQueue.Clear();
        lastHitPlayed = float.NegativeInfinity;
    }

    void Update()
    {
        // ヒットストップ中(timeScale 0)でも鳴らすので実時間で見る。
        // 1コマで鳴らすのは1つだけにし、前に実際に鳴らした時刻からも間をあける。
        // コマが長く止まって予定の時刻をいくつも過ぎても、同じコマでまとめて鳴って1つの音に潰れないようにするため
        float now = Time.unscaledTime;
        float interval = tuning != null ? tuning.HitSeInterval : 0f;
        if (hitQueue.Count > 0 && hitQueue.Peek().Key <= now && now - lastHitPlayed >= interval)
        {
            Play(SeId.Hit, hitQueue.Dequeue().Value);
            lastHitPlayed = now;
        }
    }

    // ================= IFeedbackService =================

    public void Shake(float strength, float duration)
    {
        if (impulseSource == null) return;
        // 揺れの長さはインパルスの定義に持たせる(Impulse Type が Uniform などのときに効く。Legacy だと効かない)
        if (duration > 0f) impulseSource.ImpulseDefinition.ImpulseDuration = duration;
        impulseSource.GenerateImpulseWithForce(strength);
    }

    public void PlaySe(SeId id, Vector3 position)
    {
        // position は今は使わない(見下ろしの固定カメラで、左右の定位をつけないため)
        switch (id)
        {
            case SeId.Hit:
                QueueHit();
                break;
            case SeId.EdgeCross:
                PlayEdgeCross();
                break;
            default:
                Play(id, 1f);
                break;
        }
    }

    public void SpawnVfx(VfxId id, Vector3 position, Quaternion rotation)
    {
        FeedbackTuning.VfxEntry entry = tuning != null ? tuning.FindVfx(id) : null;
        if (entry == null || entry.Prefab == null) return;
        // 数が少ないうちは生成と破棄で足りる。重くなったらプールにする
        GameObject vfx = Instantiate(entry.Prefab, position, rotation);
        Destroy(vfx, entry.Lifetime);
    }

    /// <summary>音を鳴らす。pitch は 1 が元の高さ。UI など、高さを変えて鳴らしたいとき用</summary>
    public void Play(SeId id, float pitch)
    {
        FeedbackTuning.SeEntry entry = tuning != null ? tuning.FindSe(id) : null;
        if (entry == null || entry.Clips == null || entry.Clips.Length == 0) return;
        AudioClip clip = entry.Clips[Random.Range(0, entry.Clips.Length)];
        if (clip == null) return;

        int index = PickVoice();
        AudioSource source = voices[index];
        voiceStartedOrder[index] = ++playCount;
        source.clip = clip;
        source.volume = entry.Volume;
        source.pitch = pitch * (1f + Random.Range(-entry.PitchJitter, entry.PitchJitter));
        source.Play();
    }

    /// <summary>
    /// 鳴らす AudioSource の番号を選ぶ。まず鳴り終わっているものを探し、全部鳴っているときだけ、いちばん前に鳴らし始めたものを止めて使う。
    /// 順番だけで選ぶと、空きがあるのに長い音(雷など)を途中で切ってしまうため。
    /// 古さは鳴らし始めた連番で比べる(空きを使うと順番と鳴らした順がずれるので、順番では古さが分からない)
    /// </summary>
    int PickVoice()
    {
        for (int i = 0; i < voices.Length; i++)
        {
            int index = (nextVoice + i) % voices.Length;
            if (voices[index].isPlaying) continue;
            nextVoice = (index + 1) % voices.Length;
            return index;
        }
        int oldest = 0;
        for (int i = 1; i < voices.Length; i++)
        {
            if (voiceStartedOrder[i] < voiceStartedOrder[oldest]) oldest = i;
        }
        nextVoice = (oldest + 1) % voices.Length;
        return oldest;
    }

    // ================= 数で高さが変わる音 =================

    /// <summary>
    /// 当たりの音。一振りで何回目かで音階を上る(カン、カン、カン)。最後の段を超えたら、高い2音を交互に鳴らす。
    /// 髙橋さんは当たるたびに PlaySe(SeId.Hit) を呼ぶだけでよく、高さはこちらで決める
    /// </summary>
    void QueueHit()
    {
        int[] steps = tuning != null ? tuning.HitPitchSemitones : null;
        if (steps == null || steps.Length == 0)
        {
            Play(SeId.Hit, 1f);
            return;
        }
        int last = steps.Length - 1;
        int semitone = hitStep <= last ? steps[hitStep] : steps[last] - ((hitStep - last) % 2) * 2;
        hitStep++;

        // 同じフレームに何体も当たると1つの音に潰れるので、前の当たりの音から間をあけて鳴らす
        float playAt = Mathf.Max(Time.unscaledTime, nextHitTime);
        nextHitTime = playAt + tuning.HitSeInterval;
        hitQueue.Enqueue(new KeyValuePair<float, float>(playAt, SemitonesToPitch(semitone)));
    }

    /// <summary>縁を越える音。一振りで落とすほど高くなり、「抜けた」が重なるほど気持ちよくする</summary>
    void PlayEdgeCross()
    {
        float perStep = tuning != null ? tuning.EdgeCrossSemitonePerFall : 0f;
        Play(SeId.EdgeCross, SemitonesToPitch(edgeStep * perStep));
        edgeStep++;
    }

    static float SemitonesToPitch(float semitones) => Mathf.Pow(2f, semitones / 12f);

    // ================= イベントから出すもの =================

    void OnSlashStarted(SlashStartedInfo info)
    {
        // 新しい一振りなので、音の高さを1段目に戻す
        hitStep = 0;
        edgeStep = 0;
    }

    /// <summary>
    /// 吹き飛んだ敵に残光(FlightTrail)を付ける。敵を動かすのは三條さんのコードなので、残光は位置の変化を見て描くだけ。
    /// 付けるのは敵の根っこ(位置を持つ所)。見た目の子は三條さんが傾けているので触らない
    /// </summary>
    void OnEnemyHit(EnemyHitInfo info)
    {
        Transform enemy = info.Enemy != null ? info.Enemy.Transform : null;
        if (enemy == null || tuning == null) return;
        FlightTrail trail = enemy.GetComponent<FlightTrail>();
        if (trail == null) trail = enemy.gameObject.AddComponent<FlightTrail>();
        trail.Begin(tuning);
    }

    // 落ちる音(Fall)は増永が足した ID なので、三條さんは呼ばない。光の粒も連携ガイドで増永が出すことになっている
    void OnEnemyFell(EnemyFellInfo info)
    {
        Play(SeId.Fall, 1f);
        SpawnVfx(VfxId.FallSpark, info.Position, Quaternion.identity);
    }

    void OnEnemyTeetered(ILaunchable enemy) => Play(SeId.Teeter, 1f);

    void OnWaveStarted(int wave) => Play(SeId.WaveStart, 1f);
}
