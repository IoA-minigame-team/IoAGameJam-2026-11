using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 銘のテスト用(Dev シーン専用、本番シーンには置かない)。キーボードで選択画面を開き、今の倍率を画面の隅に出す。
/// 得点のテスト(CTestDriver)とは別のファイルにして、銘のタスクだけで直せるようにした。
/// </summary>
public sealed class MeiTestDriver : MonoBehaviour
{
    [SerializeField] MeiManager meiManager;
    [Tooltip("8キーで一度に落としたことにする数。小銘の候補が出る数(MeiTuning)に合わせる")]
    [SerializeField, Min(1)] int fallsPerPress = 15;
    [SerializeField] bool showHelp = true;

    int wave;

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (k == null) return;

        // 7:数えずに、小銘の選択画面をすぐ開く
        if (k.digit7Key.wasPressedThisFrame && meiManager != null) meiManager.Request(MeiChoiceKind.Minor);
        // 8:敵が落ちたことにする。決まった数に達すると、本番と同じ流れで選択画面が開く
        if (k.digit8Key.wasPressedThisFrame)
        {
            for (int i = 0; i < fallsPerPress; i++) GameEvents.RaiseEnemyFell(new EnemyFellInfo(-1, transform.position));
        }
        // 9:ウェーブをクリアしたことにする(大銘の候補。MeiTuning の Offer Major Mei がオンのときだけ出る)
        if (k.digit9Key.wasPressedThisFrame)
        {
            wave++;
            GameEvents.RaiseWaveCleared(wave);
        }
    }

    void OnGUI()
    {
        if (!showHelp) return;
        // 髙橋さんと三條さんが読むのと同じ場所(GameContext.MeiStats)から読む。ここの数字が、ゲームに効く値
        IMeiStats s = GameContext.MeiStats;
        GUI.Box(new Rect(Screen.width - 430, Screen.height - 190, 420, 180),
            "銘のテスト(キーボード)\n" +
            "7: 小銘の選択画面をすぐ開く\n" +
            $"8: {fallsPerPress}体落としたことにする\n" +
            "9: ウェーブをクリアしたことにする\n" +
            $"線の幅 ×{s.LineWidthMultiplier:0.00}  吹き飛ばす力 ×{s.LaunchPowerMultiplier:0.00}\n" +
            $"手ぶらの速さ ×{s.FreeHandSpeedMultiplier:0.00}  自動で斬り戻すまで ×{s.AutoReturnTimeMultiplier:0.00}\n" +
            $"返し {(s.HasReturnSlash ? "あり" : "なし")}  時間 {(TimeController.IsPaused ? "止まっている" : "動いている")}");
    }
}
