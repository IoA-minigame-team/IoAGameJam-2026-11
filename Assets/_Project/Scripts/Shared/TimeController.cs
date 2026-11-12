using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 時間を止める、遅くする処理の窓口。Time.timeScale を書き換えてよいのはこのクラスだけ。
/// 複数の要求を優先度「停止 > ヒットストップ > スロー」でまとめ、ぶつからないようにする。
/// 経過時間は unscaled time で数えるので、要求した側が時間を戻す処理を書く必要はない。
/// </summary>
public static class TimeController
{
    // ヒットストップは完全に止める。少しだけ動かしたくなったらここを変える
    const float HIT_STOP_SCALE = 0f;
    const float PAUSE_SCALE = 0f;
    const float NORMAL_SCALE = 1f;

    struct SlowRequest
    {
        public float Scale;
        public float Remaining;
    }

    // 止めた本人が解除する形にするため、要求者ごとに持つ。2人が止めたら2人とも解除するまで動かない
    static readonly HashSet<object> pauseOwners = new HashSet<object>();
    static readonly List<SlowRequest> slows = new List<SlowRequest>();
    // 長さが前もって決まらないスロー(演出など)。止めた本人が ReleaseSlow で解除する
    static readonly Dictionary<object, float> slowOwners = new Dictionary<object, float>();
    static readonly List<object> destroyedSlowOwners = new List<object>();
    static float hitStopRemaining;
    static TimeControllerRunner runner;

    /// <summary>誰かが停止を要求しているか</summary>
    public static bool IsPaused => pauseOwners.Count > 0;

    /// <summary>ヒットストップ中か</summary>
    public static bool IsHitStopping => hitStopRemaining > 0f;

    /// <summary>ヒットストップ。duration は実時間の秒数。重なったら長い方を残す</summary>
    public static void HitStop(float duration)
    {
        if (!EnsureRunner()) return;
        hitStopRemaining = Mathf.Max(hitStopRemaining, duration);
        Apply();
    }

    /// <summary>スロー。scale は時間の倍率(0〜1)、duration は実時間の秒数。重なったら最も遅い倍率を使う</summary>
    public static void Slow(float scale, float duration)
    {
        if (!EnsureRunner()) return;
        if (duration <= 0f) return;
        slows.Add(new SlowRequest { Scale = Mathf.Clamp01(scale), Remaining = duration });
        Apply();
    }

    /// <summary>
    /// 解除するまで続くスロー。owner には呼んだ本人(this)を渡し、解除も同じ owner で行う。
    /// 同じ owner で呼び直すと倍率を上書きする。Slow と重なったら最も遅い倍率を使う
    /// </summary>
    public static void RequestSlow(object owner, float scale)
    {
        if (owner == null) return;
        if (!EnsureRunner()) return;
        slowOwners[owner] = Mathf.Clamp01(scale);
        Apply();
    }

    /// <summary>RequestSlow で遅くした本人が解除する</summary>
    public static void ReleaseSlow(object owner)
    {
        if (owner == null) return;
        if (!slowOwners.Remove(owner)) return;
        Apply();
    }

    /// <summary>時間を止める。owner には呼んだ本人(this)を渡し、解除も同じ owner で行う</summary>
    public static void RequestPause(object owner)
    {
        if (owner == null) return;
        if (!EnsureRunner()) return;
        pauseOwners.Add(owner);
        Apply();
    }

    /// <summary>RequestPause で止めた本人が解除する</summary>
    public static void ReleasePause(object owner)
    {
        if (owner == null) return;
        pauseOwners.Remove(owner);
        Apply();
    }

    /// <summary>TimeControllerRunner から毎フレーム呼ばれる</summary>
    internal static void Tick(float unscaledDeltaTime)
    {
        // 解除し忘れたまま破棄されたオブジェクトのせいで、ずっと止まったままにならないようにする
        pauseOwners.RemoveWhere(owner => owner is Object unityObject && unityObject == null);
        RemoveDestroyedSlowOwners();

        // 銘の選択中などで止まっている間は、ヒットストップとスローの残り時間を減らさない
        if (!IsPaused)
        {
            hitStopRemaining = Mathf.Max(0f, hitStopRemaining - unscaledDeltaTime);

            for (int i = slows.Count - 1; i >= 0; i--)
            {
                SlowRequest slow = slows[i];
                slow.Remaining -= unscaledDeltaTime;
                if (slow.Remaining <= 0f) slows.RemoveAt(i);
                else slows[i] = slow;
            }
        }

        Apply();
    }

    static void Apply()
    {
        Time.timeScale = CalculateScale();
    }

    static float CalculateScale()
    {
        if (IsPaused) return PAUSE_SCALE;
        if (IsHitStopping) return HIT_STOP_SCALE;

        float scale = NORMAL_SCALE;
        foreach (SlowRequest slow in slows)
        {
            scale = Mathf.Min(scale, slow.Scale);
        }
        foreach (float ownerScale in slowOwners.Values)
        {
            scale = Mathf.Min(scale, ownerScale);
        }
        return scale;
    }

    // 解除し忘れたまま破棄されたオブジェクトのせいで、ずっと遅いままにならないようにする
    static void RemoveDestroyedSlowOwners()
    {
        if (slowOwners.Count == 0) return;
        foreach (object owner in slowOwners.Keys)
        {
            if (owner is Object unityObject && unityObject == null) destroyedSlowOwners.Add(owner);
        }
        foreach (object owner in destroyedSlowOwners) slowOwners.Remove(owner);
        destroyedSlowOwners.Clear();
    }

    static bool EnsureRunner()
    {
        // エディタの編集中に呼ばれても、シーンにオブジェクトを作らない
        if (!Application.isPlaying) return false;
        if (runner != null) return true;

        GameObject go = new GameObject(nameof(TimeControllerRunner));
        Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<TimeControllerRunner>();
        return true;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 前のシーンのヒットストップやスローを次のシーンに持ち込まない
        hitStopRemaining = 0f;
        slows.Clear();
        slowOwners.Clear();
        Apply();
    }

    // ドメインリロードを切った再生でも、前回の停止や要求が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        pauseOwners.Clear();
        slows.Clear();
        slowOwners.Clear();
        hitStopRemaining = 0f;
        runner = null;
        Time.timeScale = NORMAL_SCALE;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
}
