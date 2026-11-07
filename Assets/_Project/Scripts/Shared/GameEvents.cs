using System;
using UnityEngine;

/// <summary>
/// 担当をまたぐ「起きたこと」の知らせ。他の人のクラスを参照せずに伝えるため static にしてある。
/// 受け取る側は OnEnable で登録し、OnDisable で必ず解除する。発行は Raise から始まるメソッドで行う。
/// </summary>
public static class GameEvents
{
    // ---- 髙橋が流す ----

    /// <summary>刀を突き立てた。値は刀の位置</summary>
    public static event Action<Vector3> SwordPlanted;
    /// <summary>斬り戻しを始めた(刀を引き抜いた)</summary>
    public static event Action<SlashStartedInfo> SlashStarted;
    /// <summary>斬り抜けが敵に当たった</summary>
    public static event Action<EnemyHitInfo> EnemyHit;
    /// <summary>斬り戻しが終わった。当てた数が 0 なら空振り</summary>
    public static event Action<SlashFinishedInfo> SlashFinished;
    /// <summary>連鎖数が変わった。空振りで 0 に戻る</summary>
    public static event Action<int> ChainChanged;
    /// <summary>主人公が被弾した。値は残りの被弾回数</summary>
    public static event Action<int> PlayerDamaged;

    // ---- 三條さんが流す ----

    /// <summary>敵の光の蓄積が変わった。値は敵と、上限に対する蓄積の割合(0〜1)</summary>
    public static event Action<ILaunchable, float> GlowChanged;
    /// <summary>敵が縁で踏みとどまった</summary>
    public static event Action<ILaunchable> EnemyTeetered;
    /// <summary>敵が場外に落ちた</summary>
    public static event Action<EnemyFellInfo> EnemyFell;
    /// <summary>刀に群がっている灯喰らいの数が変わった</summary>
    public static event Action<int> SwarmCountChanged;
    /// <summary>ウェーブが始まった。値はウェーブ番号</summary>
    public static event Action<int> WaveStarted;
    /// <summary>ウェーブをクリアした。値はウェーブ番号</summary>
    public static event Action<int> WaveCleared;
    /// <summary>ゲームオーバー</summary>
    public static event Action GameOver;

    // ---- 増永さんが流す ----

    /// <summary>大銘が変わった。値は新しい大銘</summary>
    public static event Action<MajorMeiId> MajorMeiChanged;

    public static void RaiseSwordPlanted(Vector3 position) => Invoke(SwordPlanted, position);
    public static void RaiseSlashStarted(SlashStartedInfo info) => Invoke(SlashStarted, info);
    public static void RaiseEnemyHit(EnemyHitInfo info) => Invoke(EnemyHit, info);
    public static void RaiseSlashFinished(SlashFinishedInfo info) => Invoke(SlashFinished, info);
    public static void RaiseChainChanged(int chain) => Invoke(ChainChanged, chain);
    public static void RaisePlayerDamaged(int remaining) => Invoke(PlayerDamaged, remaining);

    public static void RaiseGlowChanged(ILaunchable enemy, float ratio) => Invoke(GlowChanged, enemy, ratio);
    public static void RaiseEnemyTeetered(ILaunchable enemy) => Invoke(EnemyTeetered, enemy);
    public static void RaiseEnemyFell(EnemyFellInfo info) => Invoke(EnemyFell, info);
    public static void RaiseSwarmCountChanged(int count) => Invoke(SwarmCountChanged, count);
    public static void RaiseWaveStarted(int wave) => Invoke(WaveStarted, wave);
    public static void RaiseWaveCleared(int wave) => Invoke(WaveCleared, wave);
    public static void RaiseGameOver() => Invoke(GameOver);

    public static void RaiseMajorMeiChanged(MajorMeiId mei) => Invoke(MajorMeiChanged, mei);

    // ドメインリロードを切った再生でも、前回の購読が残って破棄済みのオブジェクトが呼ばれないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        SwordPlanted = null;
        SlashStarted = null;
        EnemyHit = null;
        SlashFinished = null;
        ChainChanged = null;
        PlayerDamaged = null;
        GlowChanged = null;
        EnemyTeetered = null;
        EnemyFell = null;
        SwarmCountChanged = null;
        WaveStarted = null;
        WaveCleared = null;
        GameOver = null;
        MajorMeiChanged = null;
    }

    // 購読者ごとに例外を受け止める。誰か1人のハンドラが例外を投げても、残りの人の処理を止めないため
    static void Invoke(Action handler)
    {
        if (handler == null) return;
        foreach (Action h in handler.GetInvocationList())
        {
            try { h(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    static void Invoke<T>(Action<T> handler, T arg)
    {
        if (handler == null) return;
        foreach (Action<T> h in handler.GetInvocationList())
        {
            try { h(arg); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    static void Invoke<T1, T2>(Action<T1, T2> handler, T1 arg1, T2 arg2)
    {
        if (handler == null) return;
        foreach (Action<T1, T2> h in handler.GetInvocationList())
        {
            try { h(arg1, arg2); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
