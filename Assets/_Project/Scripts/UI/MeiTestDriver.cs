using System.Collections.Generic;
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
    [Tooltip("Y キーで周りに置く試しの敵の数と、置く距離(m)")]
    [SerializeField, Min(1)] int targetCount = 10;
    [SerializeField] float targetMinDistance = 1.2f;
    [SerializeField] float targetMaxDistance = 4.5f;
    [SerializeField] bool showHelp = true;

    // テストで流す斬り抜けの番号。髙橋さんの番号とぶつからないよう大きな数から始める
    const int TEST_SLASH_ID_START = 90000;

    readonly List<GameObject> targets = new List<GameObject>();
    int wave;
    int testSlashId = TEST_SLASH_ID_START;

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
        // 0:選択画面を通さずに「震」を刻んだことにする(もう一度押すと外す)
        // 今の大銘は MeiManager の状態から読む。選択画面で刻んだ震とも、ずれないようにするため
        if (k.digit0Key.wasPressedThisFrame && meiManager != null)
        {
            bool hasShin = meiManager.State.Major == MajorMeiId.Shin;
            meiManager.SetMajorForTest(hasShin ? MajorMeiId.None : MajorMeiId.Shin);
        }
        // Y:周りに試しの敵を置き直す
        if (k.yKey.wasPressedThisFrame) PlaceTargets();
        // U:ここで斬り抜けが終わったことにする。震が刻まれていれば、周りの敵が外へ押し出される
        if (k.uKey.wasPressedThisFrame)
        {
            testSlashId++;
            Vector3 here = TestCenter();
            GameEvents.RaiseSlashStarted(new SlashStartedInfo(testSlashId, here, here));
            GameEvents.RaiseSlashFinished(new SlashFinishedInfo(testSlashId, 0));
        }
    }

    /// <summary>震が押し出す中心と同じ所(ShinEffect と同じ決め方)。主人公がいれば主人公、いなければこのオブジェクトの位置</summary>
    Vector3 TestCenter() => GameContext.PlayerTransform != null ? GameContext.PlayerTransform.position : transform.position;

    void PlaceTargets()
    {
        if (meiManager == null || meiManager.Tuning == null) return;
        Vector3 center = TestCenter();
        foreach (GameObject t in targets) if (t != null) Destroy(t);
        targets.Clear();
        int layer = LayerMask.NameToLayer("Enemy");
        for (int i = 0; i < targetCount; i++)
        {
            float angle = 360f * i / targetCount + Random.Range(-10f, 10f);
            float distance = Random.Range(targetMinDistance, targetMaxDistance);
            Vector3 pos = center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
            GameObject t = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            t.name = "MeiTestTarget";
            t.transform.position = pos + Vector3.up;
            t.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
            if (layer >= 0) t.layer = layer;
            t.AddComponent<MeiTestTarget>().Configure(meiManager.Tuning);
            targets.Add(t);
        }
    }

    void OnGUI()
    {
        if (!showHelp) return;
        // 髙橋さんと三條さんが読むのと同じ場所(GameContext.MeiStats)から読む。ここの数字が、ゲームに効く値
        IMeiStats s = GameContext.MeiStats;
        GUI.Box(new Rect(Screen.width - 430, Screen.height - 250, 420, 240),
            "銘のテスト(キーボード)\n" +
            "7: 小銘の選択画面をすぐ開く\n" +
            $"8: {fallsPerPress}体落としたことにする\n" +
            "9: ウェーブをクリアしたことにする\n" +
            $"0: 震を刻む/外す(今 {(meiManager != null && meiManager.State.Major == MajorMeiId.Shin ? "震" : "なし")})\n" +
            "Y: 試しの敵を周りに置く  U: 斬り抜けが終わったことにする\n" +
            $"線の幅 ×{s.LineWidthMultiplier:0.00}  吹き飛ばす力 ×{s.LaunchPowerMultiplier:0.00}\n" +
            $"手ぶらの速さ ×{s.FreeHandSpeedMultiplier:0.00}  自動で斬り戻すまで ×{s.AutoReturnTimeMultiplier:0.00}\n" +
            $"返し {(s.HasReturnSlash ? "あり" : "なし")}  時間 {(TimeController.IsPaused ? "止まっている" : "動いている")}");
    }
}
