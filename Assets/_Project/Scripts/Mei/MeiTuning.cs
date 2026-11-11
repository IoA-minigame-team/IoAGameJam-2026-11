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

    [Header("小銘")]
    [Tooltip("場外に落とした数がこの数に達するたびに、小銘の候補が出る")]
    [SerializeField, Min(1)] int fallsPerMinorChoice = 15;
    [Tooltip("同じ小銘を重ねられる最大の段数")]
    [SerializeField, Min(1)] int maxStage = 3;
    [Tooltip("一度に並べる候補の数")]
    [SerializeField, Min(1)] int candidateCount = 3;
    [SerializeField]
    MinorMeiDef[] minorMeis =
    {
        // 必達の4種。三段まで重ねると、大断 1.30倍、剛力 1.60倍、疾風 1.30倍、長夜 1.45倍になる
        new MinorMeiDef(MinorMeiId.Daidan, "大断", "線の幅が広がる", MeiStat.LineWidth, 0.1f, true),
        new MinorMeiDef(MinorMeiId.Goriki, "剛力", "吹き飛ばす力が上がる", MeiStat.LaunchPower, 0.2f, true),
        new MinorMeiDef(MinorMeiId.Shippu, "疾風", "手ぶらで走る速さが上がる", MeiStat.FreeHandSpeed, 0.1f, true),
        new MinorMeiDef(MinorMeiId.Choya, "長夜", "自動で斬り戻すまでの時間が延びる", MeiStat.AutoReturnTime, 0.15f, true),
        new MinorMeiDef(MinorMeiId.Eiko, "鋭光", "光の蓄積が増える", MeiStat.GlowGain, 0.25f, false),
        new MinorMeiDef(MinorMeiId.Jinba, "迅刃", "斬り抜ける速さが上がる", MeiStat.SlashSpeed, 0.2f, false),
        new MinorMeiDef(MinorMeiId.Otori, "囮", "灯喰らいの引き寄せ範囲が広がる", MeiStat.LureRadius, 0.2f, false),
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
    [Tooltip("ウェーブをクリアしたときに大銘の候補を出すか。大銘の効果のタスクが終わるまではオフにしておく")]
    [SerializeField] bool offerMajorMei = false;
    [Tooltip("必達は「返し」と増永の1種。残りは enabled をオフにすると候補から外れる")]
    [SerializeField]
    MajorMeiDef[] majorMeis =
    {
        new MajorMeiDef(MajorMeiId.Kaeshi, "返し", "斬り抜けた直後、同じ線を逆向きにもう一度斬り抜ける", "ふたつの光が逆向きに回る", true),
        new MajorMeiDef(MajorMeiId.Shin, "震", "斬り抜けて止まった瞬間、周りの敵を外へ押し出す", "脈打つ波紋が広がる", true),
        new MajorMeiDef(MajorMeiId.Zanko, "残光", "斬り抜けた線に雷がしばらく残り、触れた敵を弾き出す", "小さな黒い稲妻が走る", true),
        new MajorMeiDef(MajorMeiId.Inryoku, "引力", "刀を刺した瞬間、周りの敵を刀へ引き寄せる", "光の粒が刀へ吸いこまれる", true),
    };

    [Header("画面")]
    [Tooltip("最後に落ちた敵から選択画面を開くまでの実時間(秒)。群れが落ちきるのを見せてから止めるため")]
    [SerializeField, Min(0f)] float openDelay = 0.6f;

    public int FallsPerMinorChoice => fallsPerMinorChoice;
    public int MaxStage => maxStage;
    public int CandidateCount => candidateCount;
    public MinorMeiDef[] MinorMeis => minorMeis;
    public MajorMeiDef[] MajorMeis => majorMeis;
    public bool OfferMajorMei => offerMajorMei;
    public float OpenDelay => openDelay;

    public float GetCap(MeiStat stat)
    {
        foreach (StatCap cap in caps)
        {
            if (cap.Stat == stat) return cap.Max;
        }
        return defaultCap;
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
