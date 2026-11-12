using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 銘の抽選から効果の適用までを通しで持つ本体(仕様書「銘」)。
/// ・EnemyFell を数えて、決まった数ごとに小銘の候補を出す
/// ・WaveCleared で大銘の候補を出す(最後のウェーブの後は出さない。MeiTuning でオンにしたときだけ)
/// ・選ばれた銘を MeiState に刻み、大銘が変わったら GameEvents.RaiseMajorMeiChanged で知らせる
/// 画面の表示は MeiSelectWindow に任せ、ここは「何を出すか」と「選ばれたらどうするか」だけを持つ。
/// </summary>
public sealed class MeiManager : MonoBehaviour
{
    [SerializeField] MeiTuning tuning;
    [SerializeField] FeedbackTuning feedbackTuning;
    [SerializeField] MeiSelectWindow window;
    [Tooltip("銘を刻むたびに、今の倍率を Console に出す。確認用")]
    [SerializeField] bool logChanges = false;

    MeiState state;
    int fallsSinceChoice;
    // 小銘と大銘が同時に溜まったときに、順番に1つずつ出すための待ち行列
    readonly Queue<MeiChoiceKind> pending = new Queue<MeiChoiceKind>();
    Coroutine openRoutine;

    public MeiState State => state;
    public MeiTuning Tuning => tuning;

    /// <summary>銘が刻まれた(HUDの銘の一覧を更新するため)</summary>
    public event Action StateChanged;

    void Awake()
    {
        state = new MeiState(tuning);
    }

    void OnEnable()
    {
        GameContext.MeiStats = state;
        GameEvents.EnemyFell += OnEnemyFell;
        GameEvents.WaveCleared += OnWaveCleared;
        // 待っている間に無効にされていたら、残っている要求をここで出し直す(そのままだと次の要求が来るまで開かない)
        if (pending.Count > 0 && openRoutine == null) openRoutine = StartCoroutine(OpenWhenReady());
    }

    void OnDisable()
    {
        GameEvents.EnemyFell -= OnEnemyFell;
        GameEvents.WaveCleared -= OnWaveCleared;
        // 無効にされたら待っている処理を止める。止めた後も次の Request でまた開けるよう印を消しておく
        StopAllCoroutines();
        openRoutine = null;
        // 自分が入れた値だけ戻す。null を入れると銘なしの値(すべて 1)に戻る
        if (ReferenceEquals(GameContext.MeiStats, state)) GameContext.MeiStats = null;
    }

    void OnEnemyFell(EnemyFellInfo info)
    {
        fallsSinceChoice++;
        if (fallsSinceChoice < tuning.FallsPerMinorChoice) return;
        fallsSinceChoice -= tuning.FallsPerMinorChoice;
        Request(MeiChoiceKind.Minor);
    }

    void OnWaveCleared(int wave)
    {
        if (!tuning.OfferMajorMei) return;
        // 最後のウェーブの後はリザルトへ進むので、大銘は出さない
        if (feedbackTuning != null && wave >= feedbackTuning.TotalWaves) return;
        Request(MeiChoiceKind.Major);
    }

    /// <summary>選択画面を出すよう頼む。テスト用のボタンからも呼べるよう public にしてある</summary>
    public void Request(MeiChoiceKind kind)
    {
        pending.Enqueue(kind);
        if (openRoutine == null) openRoutine = StartCoroutine(OpenWhenReady());
    }

    IEnumerator OpenWhenReady()
    {
        // 群れが落ちきるのを見せてから止める。止まっている間も数えられるよう実時間で待つ
        yield return new WaitForSecondsRealtime(tuning.OpenDelay);
        while (window.IsOpen) yield return null;

        while (pending.Count > 0)
        {
            MeiChoiceKind kind = pending.Dequeue();
            List<MeiCandidate> candidates = kind == MeiChoiceKind.Minor ? DrawMinor() : DrawMajor();
            // 小銘がすべて三段に達したなど、出せる候補が無ければ飛ばす
            if (candidates.Count == 0) continue;
            string eyebrow = kind == MeiChoiceKind.Minor ? $"{KanjiNumber.Of(tuning.FallsPerMinorChoice)}体を落とした" : "波を退けた";
            window.Open(kind, eyebrow, candidates, OnChosen);
            while (window.IsOpen) yield return null;
        }
        openRoutine = null;
    }

    List<MeiCandidate> DrawMinor()
    {
        List<MeiTuning.MinorMeiDef> pool = new List<MeiTuning.MinorMeiDef>();
        foreach (MeiTuning.MinorMeiDef def in tuning.MinorMeis)
        {
            if (def.Enabled && state.CanStack(def.Id)) pool.Add(def);
        }

        List<MeiCandidate> result = new List<MeiCandidate>();
        foreach (MeiTuning.MinorMeiDef def in WeightedPick(pool, d => d.Weight, tuning.CandidateCount))
        {
            int stage = state.GetStage(def.Id);
            // 同梱のフォントに矢印が無いので「から」「へ」で書く
            string detail = $"{StatLabel(def.Stat)} ×{state.GetMultiplier(def.Stat):0.00} から ×{state.GetMultiplierIfAdded(def.Id):0.00} へ";
            result.Add(new MeiCandidate(MeiChoiceKind.Minor, def.Id, MajorMeiId.None, false,
                def.DisplayName, def.Description, detail, stage, tuning.MaxStage));
        }
        return result;
    }

    List<MeiCandidate> DrawMajor()
    {
        List<MeiTuning.MajorMeiDef> pool = new List<MeiTuning.MajorMeiDef>();
        foreach (MeiTuning.MajorMeiDef def in tuning.MajorMeis)
        {
            if (def.Enabled && def.Id != state.Major) pool.Add(def);
        }

        List<MeiCandidate> result = new List<MeiCandidate>();
        foreach (MeiTuning.MajorMeiDef def in WeightedPick(pool, d => d.Weight, tuning.CandidateCount))
        {
            result.Add(new MeiCandidate(MeiChoiceKind.Major, MinorMeiId.None, def.Id, false,
                def.DisplayName, def.Description, "刀の光：" + def.GlowDescription, 0, 0));
        }

        // 仕様:「今の大銘を残す」も選べる
        if (state.Major != MajorMeiId.None)
        {
            MeiTuning.MajorMeiDef current = tuning.FindMajor(state.Major);
            string name = current != null ? current.DisplayName : state.Major.ToString();
            result.Add(new MeiCandidate(MeiChoiceKind.Major, MinorMeiId.None, state.Major, true,
                name, "今の大銘を残す", "", 0, 0));
        }
        return result;
    }

    void OnChosen(MeiCandidate chosen)
    {
        if (chosen.Kind == MeiChoiceKind.Minor)
        {
            state.AddStage(chosen.Minor);
        }
        else if (!chosen.IsKeep && chosen.Major != state.Major)
        {
            state.SetMajor(chosen.Major);
            GameEvents.RaiseMajorMeiChanged(chosen.Major);
        }
        StateChanged?.Invoke();
        if (logChanges)
        {
            Debug.Log($"[銘] {chosen.DisplayName} を刻んだ / 線の幅×{state.LineWidthMultiplier:0.00} 吹き飛ばす力×{state.LaunchPowerMultiplier:0.00} " +
                $"手ぶらの速さ×{state.FreeHandSpeedMultiplier:0.00} 自動で斬り戻すまで×{state.AutoReturnTimeMultiplier:0.00} 大銘 {state.Major}");
        }
    }

    /// <summary>重み付きで、重複なしに count 個まで選ぶ</summary>
    static List<T> WeightedPick<T>(List<T> pool, Func<T, float> weightOf, int count)
    {
        List<T> rest = new List<T>(pool);
        List<T> picked = new List<T>();
        while (picked.Count < count && rest.Count > 0)
        {
            float total = 0f;
            foreach (T item in rest) total += Mathf.Max(0f, weightOf(item));
            if (total <= 0f) break;

            float r = UnityEngine.Random.value * total;
            int index = rest.Count - 1;
            for (int i = 0; i < rest.Count; i++)
            {
                r -= Mathf.Max(0f, weightOf(rest[i]));
                if (r <= 0f) { index = i; break; }
            }
            picked.Add(rest[index]);
            rest.RemoveAt(index);
        }
        return picked;
    }

    static string StatLabel(MeiStat stat)
    {
        switch (stat)
        {
            case MeiStat.LineWidth: return "線の幅";
            case MeiStat.LaunchPower: return "吹き飛ばす力";
            case MeiStat.FreeHandSpeed: return "手ぶらの速さ";
            case MeiStat.AutoReturnTime: return "自動で斬り戻すまで";
            case MeiStat.SlashSpeed: return "斬り抜ける速さ";
            case MeiStat.GlowGain: return "光の蓄積";
            case MeiStat.LureRadius: return "引き寄せ範囲";
            default: return stat.ToString();
        }
    }
}
