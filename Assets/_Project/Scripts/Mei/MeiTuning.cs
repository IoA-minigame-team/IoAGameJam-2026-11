using System;
using UnityEngine;

/// <summary>
/// 銘の調整値(増永)。小銘の上がり幅、能力ごとの上限、抽選の重み、大銘の名前と説明をまとめる。
/// 初期値は仕様書「銘」「パラメータ」に合わせてある。段階ごとの上がり幅は仕様で増永に任されているので、ここで決める(遊んでみて直す)。
/// </summary>
[CreateAssetMenu(fileName = "MeiTuning", menuName = "Hitofuri/MeiTuning")]
public sealed class MeiTuning : ScriptableObject
{
    [Serializable]
    public sealed class MinorMeiDef
    {
        [SerializeField] MinorMeiId id;
        [SerializeField] string displayName;
        [SerializeField] string description;
        [SerializeField] MeiStat stat;
        [Tooltip("1段階ごとに倍率へ足す量。0.25 で +25%。重ねがけは足し算(仕様)")]
        [SerializeField] float perStage = 0.25f;
        [Tooltip("抽選の重み。大きいほど出やすい")]
        [SerializeField, Min(0f)] float weight = 1f;
        [Tooltip("オフにすると候補に出ない。「できれば」の銘はオフで置いておく")]
        [SerializeField] bool enabled = true;

        public MinorMeiId Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public MeiStat Stat => stat;
        public float PerStage => perStage;
        public float Weight => weight;
        public bool Enabled => enabled;

        public MinorMeiDef(MinorMeiId id, string displayName, string description, MeiStat stat, float perStage, bool enabled)
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
            this.stat = stat;
            this.perStage = perStage;
            this.enabled = enabled;
        }
    }

    /// <summary>
    /// 代償つきの小銘(仕様:各1回まで)。上がる能力と、代わりに下がる能力(または被弾できる回数)を持つ。
    /// ふつうの小銘とは別の配列にしてあるのは、もう作ってある MeiTuning.asset にも初期値のまま入るようにするため
    /// (配列ごと足すと、アセットに無い欄はコードの初期値になる)
    /// </summary>
    [Serializable]
    public sealed class CostMeiDef
    {
        [SerializeField] MinorMeiId id;
        [SerializeField] string displayName;
        [SerializeField] string description;
        [Tooltip("上がる能力と、倍率へ足す量")]
        [SerializeField] MeiStat boostStat;
        [SerializeField] float boostAmount = 0.4f;
        [Tooltip("代わりに下がる能力と、倍率へ足す量(マイナス)。0 なら能力は下がらない")]
        [SerializeField] MeiStat costStat;
        [SerializeField] float costAmount = 0f;
        [Tooltip("被弾できる回数の増減(捨て身で -1)。髙橋さんの PlayerHealth が MeiStats.MaxDamageOffset として読む")]
        [SerializeField] int damageOffset = 0;
        [Tooltip("抽選の重み。ふつうの小銘(1)より少し出にくくしてある")]
        [SerializeField, Min(0f)] float weight = 0.6f;
        [SerializeField] bool enabled = true;

        public MinorMeiId Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public MeiStat BoostStat => boostStat;
        public float BoostAmount => boostAmount;
        public MeiStat CostStat => costStat;
        public float CostAmount => costAmount;
        public int DamageOffset => damageOffset;
        public float Weight => weight;
        public bool Enabled => enabled;

        public CostMeiDef(MinorMeiId id, string displayName, string description, MeiStat boostStat, float boostAmount,
            MeiStat costStat, float costAmount, int damageOffset)
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
            this.boostStat = boostStat;
            this.boostAmount = boostAmount;
            this.costStat = costStat;
            this.costAmount = costAmount;
            this.damageOffset = damageOffset;
        }
    }

    [Serializable]
    public sealed class MajorMeiDef
    {
        [SerializeField] MajorMeiId id;
        [SerializeField] string displayName;
        [SerializeField] string description;
        [Tooltip("刀の光り方の説明。刻んだ大銘は刀の光り方で見分ける(仕様)")]
        [SerializeField] string glowDescription;
        [SerializeField, Min(0f)] float weight = 1f;
        [SerializeField] bool enabled = true;

        public MajorMeiId Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public string GlowDescription => glowDescription;
        public float Weight => weight;
        public bool Enabled => enabled;

        public MajorMeiDef(MajorMeiId id, string displayName, string description, string glowDescription, bool enabled)
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
            this.glowDescription = glowDescription;
            this.enabled = enabled;
        }
    }

    [Serializable]
    public sealed class StatCap
    {
        [SerializeField] MeiStat stat;
        [SerializeField, Min(0.1f)] float max = 2f;

        public MeiStat Stat => stat;
        public float Max => max;

        public StatCap(MeiStat stat, float max)
        {
            this.stat = stat;
            this.max = max;
        }
    }

    // 選択画面(MeiSceneBuilder)が作るカードの数。これより多く抽選すると、選べない候補が出てしまう
    public const int MAX_CANDIDATES = 3;

    [Header("小銘")]
    [Tooltip("場外に落とした数がこの数に達するたびに、小銘の候補が出る")]
    [SerializeField, Min(1)] int fallsPerMinorChoice = 15;
    [Tooltip("同じ小銘を重ねられる最大の段数")]
    [SerializeField, Min(1)] int maxStage = 3;
    [Tooltip("一度に並べる候補の数。選択画面のカードは3枚なので、3 より多くはできない")]
    [SerializeField, Range(1, MAX_CANDIDATES)] int candidateCount = 3;
    [SerializeField]
    MinorMeiDef[] minorMeis =
    {
        // 7種とも、効果を読む側(髙橋さん、三條さん)ができているので候補に出す。三段まで重ねると、大断 1.30倍、剛力 1.60倍、疾風 1.30倍、長夜 1.45倍になる
        new MinorMeiDef(MinorMeiId.Daidan, "大断", "線の幅が広がる", MeiStat.LineWidth, 0.1f, true),
        new MinorMeiDef(MinorMeiId.Goriki, "剛力", "吹き飛ばす力が上がる", MeiStat.LaunchPower, 0.2f, true),
        new MinorMeiDef(MinorMeiId.Shippu, "疾風", "手ぶらで走る速さが上がる", MeiStat.FreeHandSpeed, 0.1f, true),
        new MinorMeiDef(MinorMeiId.Choya, "長夜", "自動で斬り戻すまでの時間が延びる", MeiStat.AutoReturnTime, 0.15f, true),
        new MinorMeiDef(MinorMeiId.Eiko, "鋭光", "光の蓄積が増える", MeiStat.GlowGain, 0.25f, true),
        new MinorMeiDef(MinorMeiId.Jinba, "迅刃", "斬り抜ける速さが上がる", MeiStat.SlashSpeed, 0.2f, true),
        new MinorMeiDef(MinorMeiId.Otori, "囮", "灯喰らいの引き寄せ範囲が広がる", MeiStat.LureRadius, 0.2f, true),
    };

    [Tooltip("代償つきの小銘(できれば)。各1回まで刻める。下がる能力は、ふつうの小銘で上げた分と足し算になる")]
    [SerializeField]
    CostMeiDef[] costMeis =
    {
        new CostMeiDef(MinorMeiId.Juto, "重刀", "吹き飛ばす力が大きく上がり、手ぶらで走る速さが下がる", MeiStat.LaunchPower, 0.5f, MeiStat.FreeHandSpeed, -0.2f, 0),
        new CostMeiDef(MinorMeiId.Hosomi, "細身", "吹き飛ばす力が上がり、線の幅が狭まる", MeiStat.LaunchPower, 0.3f, MeiStat.LineWidth, -0.2f, 0),
        new CostMeiDef(MinorMeiId.Sutemi, "捨て身", "吹き飛ばす力が上がり、被弾できる回数が1減る", MeiStat.LaunchPower, 0.4f, MeiStat.LaunchPower, 0f, -1),
    };

    [Tooltip("能力ごとの全体の上限(倍率)。ここに無い能力の上限は defaultCap")]
    [SerializeField]
    StatCap[] caps =
    {
        new StatCap(MeiStat.LineWidth, 2f),
        new StatCap(MeiStat.LaunchPower, 2.5f),
    };
    [SerializeField, Min(1f)] float defaultCap = 2f;

    [Header("大銘")]
    [Tooltip("ウェーブをクリアしたときに大銘の候補を出すか")]
    [SerializeField] bool offerMajorMei = true;
    [Tooltip("返しは PlayerController、震は ShinEffect、残光は ZankoEffect、引力は InryokuEffect が効果を出す")]
    [SerializeField]
    MajorMeiDef[] majorMeis =
    {
        new MajorMeiDef(MajorMeiId.Kaeshi, "返し", "斬り抜けた直後、同じ線を逆向きにもう一度斬り抜ける", "ふたつの光が逆向きに回る", true),
        new MajorMeiDef(MajorMeiId.Shin, "震", "斬り抜けて止まった瞬間、周りの敵を外へ押し出す", "脈打つ波紋が広がる", true),
        new MajorMeiDef(MajorMeiId.Zanko, "残光", "斬り抜けた線に雷がしばらく残り、触れた敵を弾き出す", "小さな黒い稲妻が走る", true),
        new MajorMeiDef(MajorMeiId.Inryoku, "引力", "刀を刺した瞬間、周りの敵を刀へ引き寄せる", "光の粒が刀へ吸いこまれる", true),
    };

    [Header("大銘「震」")]
    [Tooltip("斬り抜けて止まった所から、この半径(m)の中の敵を外へ押し出す")]
    [SerializeField, Min(0.1f)] float shinRadius = 4f;
    [Tooltip("押し出す速さ(m/s)。仕様書の基本の吹き飛ばし力(6m/s)より少し強くして、島の外まで届きやすくする")]
    [SerializeField, Min(0f)] float shinPower = 8f;
    [Tooltip("半径の端にいる敵の押し出す速さの割合。近いほど強く、遠いほど弱くする")]
    [SerializeField, Range(0f, 1f)] float shinEdgePowerRatio = 0.5f;
    [Tooltip("押し出した瞬間の画面の揺れの強さと秒数")]
    [SerializeField, Min(0f)] float shinShakeStrength = 0.6f;
    [SerializeField, Min(0f)] float shinShakeDuration = 0.25f;

    [Tooltip("震の輪が半径いっぱいまで広がる秒数。短いほど衝撃らしく見える")]
    [SerializeField, Min(0.01f)] float shinRingDuration = 0.35f;
    [Tooltip("震の輪の色。刀を刺したときの金の輪と見分けられるよう、白寄りにしてある")]
    [SerializeField] Color shinRingColor = new Color(1f, 0.95f, 0.8f, 1f);

    [Header("大銘「残光」")]
    [Tooltip("斬り抜けた線に雷が残る秒数")]
    [SerializeField, Min(0.1f)] float zankoDuration = 1.5f;
    [Tooltip("雷に触れたとみなす、線からの距離(m)")]
    [SerializeField, Min(0.05f)] float zankoRadius = 0.5f;
    [Tooltip("触れた敵を線と直角に弾き出す速さ(m/s)。剛力の倍率、敵の重さと光の蓄積も掛ける")]
    [SerializeField, Min(0f)] float zankoPower = 7f;
    [Tooltip("雷に触れた敵を調べる間隔(秒)。毎フレーム調べるほどの精度は要らないので間引く")]
    [SerializeField, Min(0.01f)] float zankoCheckInterval = 0.05f;
    [Tooltip("残っている雷の見た目の太さ(m)")]
    [SerializeField, Min(0.01f)] float zankoBoltWidth = 0.08f;
    [Tooltip("雷のギザギザの強さ(m)")]
    [SerializeField, Min(0f)] float zankoBoltJitter = 0.25f;
    [Tooltip("雷を地面から浮かせる高さ(m)。地面に埋もれて見えなくならないようにする")]
    [SerializeField, Min(0f)] float zankoBoltHeight = 0.3f;

    [Header("大銘「引力」")]
    [Tooltip("刀を刺した所から、この半径(m)の中の敵を引き寄せる")]
    [SerializeField, Min(0.1f)] float inryokuRadius = 5f;
    [Tooltip("刀からこの距離(m)の所で止める。刀に重ねると斬り抜けの線から外れるので、少し手前にする")]
    [SerializeField, Min(0f)] float inryokuStopDistance = 1.2f;
    [Tooltip("1体を引き寄せる最大の距離(m)。引き寄せ中の敵は斬れないので、長くしすぎない")]
    [SerializeField, Min(0f)] float inryokuMaxPullDistance = 3f;
    [Tooltip("引力の輪が刀まで縮む秒数")]
    [SerializeField, Min(0.01f)] float inryokuRingDuration = 0.4f;
    [Tooltip("引き寄せる敵から刀へ走る光の線が見えている秒数")]
    [SerializeField, Min(0.01f)] float inryokuStreakDuration = 0.3f;
    [Tooltip("引力の輪と光の線の色。吸いこまれる感じを出すため、少し青白くしてある")]
    [SerializeField] Color inryokuColor = new Color(0.75f, 0.85f, 1f, 1f);

    [Header("テスト用の敵(Dev シーンで震を試すときだけ使う)")]
    [Tooltip("試しの敵が押し出されたあと、1秒あたりに減る速さの割合。大きいほど早く止まる")]
    [SerializeField, Min(0f)] float testTargetFriction = 3f;
    [Tooltip("試しの敵がこれより遅くなったら止まったことにする速さ(m/s)")]
    [SerializeField, Min(0f)] float testTargetStopSpeed = 0.05f;

    [Header("画面")]
    [Tooltip("最後に落ちた敵から選択画面を開くまでの実時間(秒)。群れが落ちきるのを見せてから止めるため")]
    [SerializeField, Min(0f)] float openDelay = 0.6f;

    public int FallsPerMinorChoice => fallsPerMinorChoice;
    public int MaxStage => maxStage;
    public int CandidateCount => Mathf.Clamp(candidateCount, 1, MAX_CANDIDATES);
    public MinorMeiDef[] MinorMeis => minorMeis;
    public CostMeiDef[] CostMeis => costMeis;
    public MajorMeiDef[] MajorMeis => majorMeis;
    public bool OfferMajorMei => offerMajorMei;
    public float OpenDelay => openDelay;
    public float ShinRadius => shinRadius;
    public float ShinPower => shinPower;
    public float ShinEdgePowerRatio => shinEdgePowerRatio;
    public float ShinShakeStrength => shinShakeStrength;
    public float ShinShakeDuration => shinShakeDuration;
    public float ShinRingDuration => shinRingDuration;
    public Color ShinRingColor => shinRingColor;
    public float ZankoDuration => zankoDuration;
    public float ZankoRadius => zankoRadius;
    public float ZankoPower => zankoPower;
    public float ZankoCheckInterval => zankoCheckInterval;
    public float ZankoBoltWidth => zankoBoltWidth;
    public float ZankoBoltJitter => zankoBoltJitter;
    public float ZankoBoltHeight => zankoBoltHeight;
    public float InryokuRadius => inryokuRadius;
    public float InryokuStopDistance => inryokuStopDistance;
    public float InryokuMaxPullDistance => inryokuMaxPullDistance;
    public float InryokuRingDuration => inryokuRingDuration;
    public float InryokuStreakDuration => inryokuStreakDuration;
    public Color InryokuColor => inryokuColor;
    public float TestTargetFriction => testTargetFriction;
    public float TestTargetStopSpeed => testTargetStopSpeed;

    public float GetCap(MeiStat stat)
    {
        foreach (StatCap cap in caps)
        {
            if (cap.Stat == stat) return cap.Max;
        }
        return defaultCap;
    }

    public CostMeiDef FindCost(MinorMeiId id)
    {
        if (costMeis == null) return null;
        foreach (CostMeiDef def in costMeis)
        {
            if (def.Id == id) return def;
        }
        return null;
    }

    public MinorMeiDef FindMinor(MinorMeiId id)
    {
        foreach (MinorMeiDef def in minorMeis)
        {
            if (def.Id == id) return def;
        }
        return null;
    }

    public MajorMeiDef FindMajor(MajorMeiId id)
    {
        foreach (MajorMeiDef def in majorMeis)
        {
            if (def.Id == id) return def;
        }
        return null;
    }
}
