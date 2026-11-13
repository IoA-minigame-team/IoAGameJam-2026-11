using System;
using UnityEngine;

/// <summary>
/// 演出、SE、スコア、画面の調整値(増永)。初期値は仕様書「スコアと記録」「パラメータ」に合わせてある。
/// SE と エフェクトの中身(どの音を鳴らすか)もここで選ぶので、音を差し替えてもコードは変わらない。
/// </summary>
[CreateAssetMenu(fileName = "FeedbackTuning", menuName = "Hitofuri/FeedbackTuning")]
public sealed class FeedbackTuning : ScriptableObject
{
    [Serializable]
    public sealed class SeEntry
    {
        [SerializeField] SeId id;
        [Tooltip("複数入れると毎回ランダムに1つ鳴らす")]
        [SerializeField] AudioClip[] clips;
        [SerializeField, Range(0f, 1f)] float volume = 0.8f;
        [Tooltip("音の高さを毎回少しずらす幅。0.05 で ±5%。同じ音の連打が機械的に聞こえないようにする")]
        [SerializeField, Range(0f, 0.3f)] float pitchJitter = 0.03f;

        public SeId Id => id;
        public AudioClip[] Clips => clips;
        public float Volume => volume;
        public float PitchJitter => pitchJitter;
    }

    [Serializable]
    public sealed class VfxEntry
    {
        [SerializeField] VfxId id;
        [SerializeField] GameObject prefab;
        [Tooltip("出してから消すまでの秒数")]
        [SerializeField, Min(0.1f)] float lifetime = 2f;

        public VfxId Id => id;
        public GameObject Prefab => prefab;
        public float Lifetime => lifetime;
    }

    [Serializable]
    public sealed class SwingTitle
    {
        [Tooltip("一振りでこの数以上落とすともらえる")]
        [SerializeField, Min(1)] int minCount = 1;
        [SerializeField] string title = "素振り";

        public int MinCount => minCount;
        public string Title => title;

        public SwingTitle(int minCount, string title)
        {
            this.minCount = minCount;
            this.title = title;
        }
    }

    [Header("スコア")]
    [Tooltip("一振りの得点 = この値 × 落とした数 × 落とした数")]
    [SerializeField, Min(0)] int pointsPerFall = 10;
    [Tooltip("斬り戻しからこの秒数以内に落ちた敵を、同じ一振りとして数える")]
    [SerializeField, Min(0f)] float swingWindow = 3f;
    [Tooltip("1連鎖ごとに得点の倍率へ足す量")]
    [SerializeField, Min(0f)] float chainStep = 0.1f;
    [SerializeField, Min(1f)] float chainMultiplierMax = 2f;

    [Header("称号(少ない順に並べる)")]
    [SerializeField]
    SwingTitle[] titles =
    {
        new SwingTitle(1, "素振り"),
        new SwingTitle(5, "一閃"),
        new SwingTitle(10, "断ち"),
        new SwingTitle(20, "一刀"),
    };

    [Header("ウェーブ")]
    [SerializeField, Min(1)] int totalWaves = 5;
    [Tooltip("ウェーブが始まったときに出す一言。1番目が第一波")]
    [SerializeField]
    string[] waveHints =
    {
        "影だけ。突き立て、斬り戻し、場外を覚える",
        "灯喰らいは、置いた刀の光に群がる",
        "盾持ちは正面から飛ばない。背後に刺す",
        "全種が混ざる",
        "最後の波。重ねた銘で、一振りに大量に",
    };

    [Header("SE")]
    [SerializeField] SeEntry[] seEntries;
    [Tooltip("一振りで何体目に当たったかで、当たりの音(Hit)の高さを上げる半音の数。カン、カン、カンと音階を上る")]
    [SerializeField] int[] hitPitchSemitones = { 0, 2, 4, 7, 9, 12, 14, 16 };
    [Tooltip("当たりの音どうしの最小の間隔(秒)。斬り抜けは0.2秒で終わるので、同時に鳴ると1つの音に潰れてしまう")]
    [SerializeField, Min(0f)] float hitSeInterval = 0.055f;
    [Tooltip("縁を越える音を、一振りで落とすほど上げる半音の数(1体ごと)")]
    [SerializeField, Min(0f)] float edgeCrossSemitonePerFall = 1f;

    [Header("エフェクト")]
    [SerializeField] VfxEntry[] vfxEntries;
    [Tooltip("黒い雷の芯(黒、半透明で重ねる)")]
    [SerializeField] Material boltCoreMaterial;
    [Tooltip("黒い雷の縁取り(金、加算)")]
    [SerializeField] Material boltRimMaterial;
    [Tooltip("刀の光の輪(金、加算)")]
    [SerializeField] Material ringMaterial;

    [Header("敵側の演出")]
    [Tooltip("光の蓄積で近づける色")]
    [SerializeField] Color glowColor = Color.white;
    [Tooltip("蓄積が最大のとき、どれだけ glowColor に近づけるか(0〜1)")]
    [SerializeField, Range(0f, 1f)] float glowStrength = 0.6f;
    [Tooltip("この速さ(m/s)より速く飛んでいる間、後ろに黒い雷を出す")]
    [SerializeField, Min(0f)] float trailMinSpeed = 2.2f;
    [Tooltip("この速さで雷が一番長く太くなる")]
    [SerializeField, Min(0.1f)] float trailFullSpeed = 12f;
    [SerializeField, Min(0.01f)] float trailBoltInterval = 0.04f;
    [Tooltip("雷を出す高さ(敵の足元からの高さ)")]
    [SerializeField] float trailHeight = 0.5f;
    [Tooltip("縁で揺れる時間、角度、速さ")]
    [SerializeField, Min(0.1f)] float teeterDuration = 0.9f;
    [SerializeField] float teeterAngle = 30f;
    [SerializeField] float teeterFrequency = 20f;
    [Tooltip("島の中心。縁で揺れる向き(外向き)を決めるのに使う。仕様に無いので仮に原点")]
    [SerializeField] Vector3 islandCenter = Vector3.zero;

    [Header("刀の光")]
    [SerializeField, Min(0f)] float swordLightIntensity = 3f;
    [Tooltip("群がっていないときの光の揺らぎ")]
    [SerializeField, Min(0f)] float flickerBase = 0.06f;
    [Tooltip("灯喰らい1体ごとに足す揺らぎ")]
    [SerializeField, Min(0f)] float flickerPerSwarm = 0.13f;
    [SerializeField, Min(0f)] float flickerMax = 0.75f;
    [Tooltip("刀を持って走っている間の光の強さ(置いたときを 1 とした割合)。置いた刀のほうが目立つようにする")]
    [SerializeField, Range(0f, 1f)] float carriedLightRatio = 0.35f;
    [Tooltip("光を置く高さ(m)。刀の柄のあたり")]
    [SerializeField, Min(0f)] float swordLightHeight = 0.8f;
    [Tooltip("光が届く距離(m)")]
    [SerializeField, Min(0.1f)] float swordLightRange = 8f;
    [Tooltip("残り時間の輪の、置いた直後の大きさ(m)。残りが減るほど刀へ縮む")]
    [SerializeField, Min(0.1f)] float timerRingRadius = 1.6f;
    [Tooltip("残りがこの秒数を切ったら、残り時間の輪を点滅させる")]
    [SerializeField, Min(0f)] float timerUrgentSeconds = 1f;
    [Tooltip("群がりで光の輪の大きさも揺らす割合。揺らぎ 1 のとき、半径がこの割合だけ増減する")]
    [SerializeField, Range(0f, 0.3f)] float ringWobble = 0.06f;
    // 下の引き寄せ範囲は三條さんの EnemyTuning.LureRadius と同じ値にする(他担当のクラスを直接読まないため、ここに写している)
    // 自動で斬り戻すまでの3つは、GameContext.AutoReturnLimit が 0 のとき(主人公がいないテスト用のシーン)だけ使う
    [Tooltip("灯喰らいの引き寄せ範囲(m)。光の輪の大きさに使う。三條さんの EnemyTuning と同じ値にする")]
    [SerializeField, Min(0f)] float lureRadius = 10f;
    [Tooltip("仮:自動で斬り戻すまでの時間(秒)")]
    [SerializeField, Min(0f)] float autoReturnBase = 3f;
    [Tooltip("仮:灯喰らい1体ごとに縮む秒数")]
    [SerializeField, Min(0f)] float autoReturnPerSwarm = 0.3f;
    [Tooltip("仮:自動で斬り戻すまでの時間の下限(秒)")]
    [SerializeField, Min(0f)] float autoReturnMin = 1f;

    [Header("画面")]
    [Tooltip("得点の数字が追いつく速さ。大きいほど速い")]
    [SerializeField, Min(0.1f)] float scoreCountSpeed = 10f;
    [Tooltip("一振りのカウントを消すまでの秒数(最後に落ちてから)")]
    [SerializeField, Min(0f)] float swingHideDelay = 1.5f;
    [Tooltip("称号を出しておく秒数")]
    [SerializeField, Min(0.1f)] float calloutDuration = 1.5f;
    [Tooltip("ウェーブの帯を出しておく秒数")]
    [SerializeField, Min(0.1f)] float bannerDuration = 1.9f;
    [Tooltip("ゲームの終わりからリザルトへ移るまでの実時間(秒)")]
    [SerializeField, Min(0f)] float resultDelay = 1.4f;

    [Header("大量撃破の色反転")]
    [Tooltip("一振りでこの数を落とした瞬間に、画面の色を一度反転する(断ちの 10 体に合わせる)")]
    [SerializeField, Min(1)] int invertAt = 10;
    [Tooltip("一振りでこの数を落とした瞬間は、二度反転する(一刀の 20 体に合わせる)")]
    [SerializeField, Min(1)] int invertBigAt = 20;
    [Tooltip("反転したまま止めておく実時間(秒)と、元に戻るまでの実時間(秒)")]
    [SerializeField, Min(0f)] float invertHold = 0.06f;
    [SerializeField, Min(0.01f)] float invertFade = 0.22f;
    [Tooltip("二度反転するときの、1回目と2回目の間(秒)")]
    [SerializeField, Min(0f)] float invertGap = 0.05f;
    [Tooltip("反転の強さ。1 で完全に反転する")]
    [SerializeField, Range(0f, 1f)] float invertStrength = 1f;

    [Header("自己ベストの演出")]
    [Tooltip("ゲーム中に一振り最多の自己ベストを超えたとき、知らせを出しておく実時間(秒)")]
    [SerializeField, Min(0.1f)] float bestBannerTime = 2.2f;
    [Tooltip("リザルトで、一振り最多を数え上げてから「自己ベスト更新」の印を押すまでの実時間(秒)")]
    [SerializeField, Min(0f)] float bestStampDelay = 0.95f;
    [Tooltip("印を押す動きの実時間(秒)と、押す前の大きさ(倍)")]
    [SerializeField, Min(0.01f)] float bestStampTime = 0.16f;
    [SerializeField, Min(1f)] float bestStampScale = 2.6f;

    [Header("銘の選択画面")]
    [Tooltip("選ばれているカードを持ち上げる高さ(px)")]
    [SerializeField, Min(0f)] float meiCardLiftHeight = 10f;
    [Tooltip("カードが持ち上がる、下がる速さ(px/秒、実時間)")]
    [SerializeField, Min(0.1f)] float meiCardLiftSpeed = 80f;

    [Header("シーン名(Build Settings に入れておく)")]
    [SerializeField] string titleSceneName = "TitleScene";
    [SerializeField] string gameSceneName = "GameScene";
    [SerializeField] string resultSceneName = "ResultScene";

    public int PointsPerFall => pointsPerFall;
    public float SwingWindow => swingWindow;
    public float ChainStep => chainStep;
    public float ChainMultiplierMax => chainMultiplierMax;
    public int TotalWaves => totalWaves;
    public int[] HitPitchSemitones => hitPitchSemitones;
    public float HitSeInterval => hitSeInterval;
    public float EdgeCrossSemitonePerFall => edgeCrossSemitonePerFall;
    public float ScoreCountSpeed => scoreCountSpeed;
    public float SwingHideDelay => swingHideDelay;
    public float CalloutDuration => calloutDuration;
    public float BannerDuration => bannerDuration;
    public float ResultDelay => resultDelay;
    public float MeiCardLiftHeight => meiCardLiftHeight;
    public float MeiCardLiftSpeed => meiCardLiftSpeed;
    public Material BoltCoreMaterial => boltCoreMaterial;
    public Material BoltRimMaterial => boltRimMaterial;
    public Material RingMaterial => ringMaterial;
    public Color GlowColor => glowColor;
    public float GlowStrength => glowStrength;
    public float TrailMinSpeed => trailMinSpeed;
    public float TrailFullSpeed => trailFullSpeed;
    public float TrailBoltInterval => trailBoltInterval;
    public float TrailHeight => trailHeight;
    public float TeeterDuration => teeterDuration;
    public float TeeterAngle => teeterAngle;
    public float TeeterFrequency => teeterFrequency;
    public Vector3 IslandCenter => islandCenter;
    public float SwordLightIntensity => swordLightIntensity;
    public float FlickerBase => flickerBase;
    public float FlickerPerSwarm => flickerPerSwarm;
    public float FlickerMax => flickerMax;
    public float CarriedLightRatio => carriedLightRatio;
    public float SwordLightHeight => swordLightHeight;
    public float SwordLightRange => swordLightRange;
    public float TimerRingRadius => timerRingRadius;
    public float TimerUrgentSeconds => timerUrgentSeconds;
    public float RingWobble => ringWobble;
    public int InvertAt => invertAt;
    public int InvertBigAt => invertBigAt;
    public float InvertHold => invertHold;
    public float InvertFade => invertFade;
    public float InvertGap => invertGap;
    public float InvertStrength => invertStrength;
    public float BestBannerTime => bestBannerTime;
    public float BestStampDelay => bestStampDelay;
    public float BestStampTime => bestStampTime;
    public float BestStampScale => bestStampScale;
    public float LureRadius => lureRadius;
    public float AutoReturnBase => autoReturnBase;
    public float AutoReturnPerSwarm => autoReturnPerSwarm;
    public float AutoReturnMin => autoReturnMin;
    public string TitleSceneName => titleSceneName;
    public string GameSceneName => gameSceneName;
    public string ResultSceneName => resultSceneName;

    public SeEntry FindSe(SeId id)
    {
        if (seEntries == null) return null;
        foreach (SeEntry entry in seEntries)
        {
            if (entry.Id == id) return entry;
        }
        return null;
    }

    public VfxEntry FindVfx(VfxId id)
    {
        if (vfxEntries == null) return null;
        foreach (VfxEntry entry in vfxEntries)
        {
            if (entry.Id == id) return entry;
        }
        return null;
    }

    /// <summary>一振りで落とした数に応じた称号</summary>
    public string TitleFor(int count)
    {
        string result = titles.Length > 0 ? titles[0].Title : "";
        foreach (SwingTitle t in titles)
        {
            if (count >= t.MinCount) result = t.Title;
        }
        return result;
    }

    /// <summary>ちょうどこの数で新しい称号に届いたか。届いたら称号を返し、届いていなければ null</summary>
    public string TitleReachedAt(int count)
    {
        foreach (SwingTitle t in titles)
        {
            // いちばん下の称号(素振り)は、出しても嬉しくないので知らせない
            if (t.MinCount > 1 && t.MinCount == count) return t.Title;
        }
        return null;
    }

    /// <summary>ウェーブの一言。wave は 1 から数える</summary>
    public string WaveHint(int wave)
    {
        int i = wave - 1;
        return i >= 0 && i < waveHints.Length ? waveHints[i] : "";
    }
}
