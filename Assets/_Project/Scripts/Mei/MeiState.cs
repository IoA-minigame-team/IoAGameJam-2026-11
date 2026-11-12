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

    public bool CanStack(MinorMeiId id) => GetStage(id) < tuning.MaxStage;

    public void AddStage(MinorMeiId id)
    {
        if (!CanStack(id)) return;
        stages[id] = GetStage(id) + 1;
    }

    public void SetMajor(MajorMeiId id) => Major = id;

    /// <summary>
    /// 能力の倍率。1 + (段数 × 1段あたりの量) を全部足し、能力ごとの上限で切る。
    /// 同じ能力を上げる小銘が2つあっても、足し算なので順番で結果が変わらない。
    /// </summary>
    public float GetMultiplier(MeiStat stat)
    {
        float sum = 0f;
        foreach (MeiTuning.MinorMeiDef def in tuning.MinorMeis)
        {
            if (def.Stat == stat) sum += GetStage(def.Id) * def.PerStage;
        }
        return Mathf.Clamp(1f + sum, MIN_MULTIPLIER, tuning.GetCap(stat));
    }

    /// <summary>あと1段重ねたときの倍率。選択画面で「×1.25 から ×1.50 へ」と見せるため</summary>
    public float GetMultiplierIfAdded(MinorMeiId id)
    {
        MeiTuning.MinorMeiDef target = tuning.FindMinor(id);
        if (target == null) return 1f;
        float sum = 0f;
        foreach (MeiTuning.MinorMeiDef def in tuning.MinorMeis)
        {
            if (def.Stat != target.Stat) continue;
            int stage = GetStage(def.Id) + (def.Id == id ? 1 : 0);
            sum += stage * def.PerStage;
        }
        return Mathf.Clamp(1f + sum, MIN_MULTIPLIER, tuning.GetCap(target.Stat));
    }

    // ---- IMeiStats(使う瞬間に毎回読まれるので、キャッシュせずにその場で計算する) ----

    public float LineWidthMultiplier => GetMultiplier(MeiStat.LineWidth);
    public float LaunchPowerMultiplier => GetMultiplier(MeiStat.LaunchPower);
    public float FreeHandSpeedMultiplier => GetMultiplier(MeiStat.FreeHandSpeed);
    public float AutoReturnTimeMultiplier => GetMultiplier(MeiStat.AutoReturnTime);
    public float SlashSpeedMultiplier => GetMultiplier(MeiStat.SlashSpeed);
    public float GlowGainMultiplier => GetMultiplier(MeiStat.GlowGain);
    public float LureRadiusMultiplier => GetMultiplier(MeiStat.LureRadius);
    // 捨て身(被弾の上限 -1)はまだ入れていない
    public int MaxDamageOffset => 0;
    public bool HasReturnSlash => Major == MajorMeiId.Kaeshi;
}
