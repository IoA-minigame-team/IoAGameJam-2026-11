using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 今刻まれている銘(小銘の段数と大銘)と、そこから出す最終の倍率。
/// IMeiStats を実装して GameContext.MeiStats に入れるので、髙橋さんと三條さんはここの値を読むだけで銘の効果を使える。
/// MonoBehaviour にしないのは、シーンに置かなくても計算だけ確かめられるようにするため。
/// </summary>
public sealed class MeiState : IMeiStats
{
    // 倍率が 0 以下になると、掛けた値が消えて動かなくなる。代償つきの銘を足したときの保険
    const float MIN_MULTIPLIER = 0.1f;

    readonly MeiTuning tuning;
    readonly Dictionary<MinorMeiId, int> stages = new Dictionary<MinorMeiId, int>();

    public MajorMeiId Major { get; private set; } = MajorMeiId.None;

    public MeiState(MeiTuning tuning)
    {
        this.tuning = tuning;
    }

    public int GetStage(MinorMeiId id) => stages.TryGetValue(id, out int stage) ? stage : 0;

    /// <summary>代償つきの小銘は1回まで、ふつうの小銘は MaxStage 段まで(仕様)</summary>
    public bool CanStack(MinorMeiId id) => GetStage(id) < MaxStageOf(id);

    public int MaxStageOf(MinorMeiId id) => tuning.FindCost(id) != null ? 1 : tuning.MaxStage;

    public bool IsCost(MinorMeiId id) => tuning.FindCost(id) != null;

    public void AddStage(MinorMeiId id)
    {
        if (!CanStack(id)) return;
        stages[id] = GetStage(id) + 1;
    }

    public void SetMajor(MajorMeiId id) => Major = id;

    /// <summary>
    /// 能力の倍率。1 + (段数 × 1段あたりの量) を全部足し、能力ごとの上限で切る。
    /// 代償つきの小銘は、上がる量も下がる量も同じ足し算に入れる。足し算なので、刻んだ順番で結果が変わらない。
    /// </summary>
    public float GetMultiplier(MeiStat stat) => Clamp(stat, Sum(stat, MinorMeiId.None));

    /// <summary>id をあと1段重ねたときの、その小銘が上げる能力の倍率。選択画面で「×1.25 から ×1.50 へ」と見せるため</summary>
    public float GetMultiplierIfAdded(MinorMeiId id)
    {
        MeiTuning.MinorMeiDef target = tuning.FindMinor(id);
        if (target == null) return 1f;
        return GetMultiplierIfAdded(id, target.Stat);
    }

    /// <summary>id を刻んだときの stat の倍率。代償つきの小銘で、上がる能力と下がる能力の両方を見せるため</summary>
    public float GetMultiplierIfAdded(MinorMeiId id, MeiStat stat) => Clamp(stat, Sum(stat, id));

    /// <summary>id を刻んだときの、被弾できる回数の増減</summary>
    public int DamageOffsetIfAdded(MinorMeiId id) => SumDamageOffset(id);

    float Clamp(MeiStat stat, float sum) => Mathf.Clamp(1f + sum, MIN_MULTIPLIER, tuning.GetCap(stat));

    /// <summary>stat に足す量の合計。extra に小銘を渡すと、それをあと1段刻んだときの値になる</summary>
    float Sum(MeiStat stat, MinorMeiId extra)
    {
        float sum = 0f;
        foreach (MeiTuning.MinorMeiDef def in tuning.MinorMeis)
        {
            if (def.Stat == stat) sum += StageWith(def.Id, extra) * def.PerStage;
        }
        if (tuning.CostMeis != null)
        {
            foreach (MeiTuning.CostMeiDef def in tuning.CostMeis)
            {
                int stage = StageWith(def.Id, extra);
                if (stage <= 0) continue;
                if (def.BoostStat == stat) sum += def.BoostAmount;
                if (def.CostStat == stat) sum += def.CostAmount;
            }
        }
        return sum;
    }

    int SumDamageOffset(MinorMeiId extra)
    {
        int offset = 0;
        if (tuning.CostMeis == null) return 0;
        foreach (MeiTuning.CostMeiDef def in tuning.CostMeis)
        {
            if (StageWith(def.Id, extra) > 0) offset += def.DamageOffset;
        }
        return offset;
    }

    int StageWith(MinorMeiId id, MinorMeiId extra) => GetStage(id) + (id == extra && extra != MinorMeiId.None ? 1 : 0);

    // ---- IMeiStats(使う瞬間に毎回読まれるので、キャッシュせずにその場で計算する) ----

    public float LineWidthMultiplier => GetMultiplier(MeiStat.LineWidth);
    public float LaunchPowerMultiplier => GetMultiplier(MeiStat.LaunchPower);
    public float FreeHandSpeedMultiplier => GetMultiplier(MeiStat.FreeHandSpeed);
    public float AutoReturnTimeMultiplier => GetMultiplier(MeiStat.AutoReturnTime);
    public float SlashSpeedMultiplier => GetMultiplier(MeiStat.SlashSpeed);
    public float GlowGainMultiplier => GetMultiplier(MeiStat.GlowGain);
    public float LureRadiusMultiplier => GetMultiplier(MeiStat.LureRadius);
    // 捨て身で -1。下がりすぎて 0 回にならないよう、最低1回残すのは髙橋さんの PlayerHealth がしている
    public int MaxDamageOffset => SumDamageOffset(MinorMeiId.None);
    public bool HasReturnSlash => Major == MajorMeiId.Kaeshi;
}
