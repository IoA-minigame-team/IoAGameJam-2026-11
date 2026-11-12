using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// ゲーム中の画面の左下に、刻んだ銘を小さく出す(例:「返し 大銘　大断二段」)。
/// 銘の仕組み(MeiManager)と画面をつなぐ部品。得点などの GameHud とは別にして、銘のタスクだけで直せるようにした。
/// </summary>
public sealed class MeiHud : MonoBehaviour
{
    [SerializeField] MeiManager meiManager;
    [SerializeField] TMP_Text listText;

    void OnEnable()
    {
        if (meiManager != null) meiManager.StateChanged += Refresh;
    }

    void OnDisable()
    {
        if (meiManager != null) meiManager.StateChanged -= Refresh;
    }

    void Start() => Refresh();

    void Refresh()
    {
        if (listText == null || meiManager == null || meiManager.State == null) return;
        MeiState state = meiManager.State;
        MeiTuning tuning = meiManager.Tuning;
        StringBuilder sb = new StringBuilder();

        if (state.Major != MajorMeiId.None)
        {
            MeiTuning.MajorMeiDef major = tuning.FindMajor(state.Major);
            sb.Append("<color=#FFE2A0>").Append(major != null ? major.DisplayName : state.Major.ToString()).Append("</color> 大銘　");
        }
        foreach (MeiTuning.MinorMeiDef def in tuning.MinorMeis)
        {
            int stage = state.GetStage(def.Id);
            if (stage <= 0) continue;
            // 同梱のフォントに■□が無いので、段数は漢数字で出す
            sb.Append(def.DisplayName).Append(KanjiNumber.Of(stage)).Append("段　");
        }
        listText.text = sb.ToString();
    }
}
