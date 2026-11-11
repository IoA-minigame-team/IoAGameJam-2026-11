using System;
using UnityEngine;

/// <summary>
/// 三條担当内の姿勢通知。共有GameEventsにない移動状態だけを補う。
/// 共有APIは変更せず、見た目の購読者がEnemyの具象クラスを知らずに動けるようにする。
/// 他担当との命中・光・踏みとどまり・落下の連携には既存のGameEventsを使う。
/// このクラスの通知からLaunchを呼び直さず、姿勢などの受け手自身の処理だけを更新する。
/// </summary>
public static class EnemyMotionEvents
{
    public static event Action<ILaunchable, Vector3> Launched;
    public static event Action<ILaunchable, Vector3> MotionUpdated;
    public static event Action<ILaunchable> Falling;
    public static event Action<ILaunchable> Recovered;
    public static event Action<ILaunchable> Reset;
    public static event Action<ILaunchable, bool> GuardChanged;
    public static event Action<ILaunchable, bool> SwarmMembershipChanged;
    public static event Action<ILaunchable> AttackStarted;
    public static event Action<ILaunchable> AttackEnded;

    public static void RaiseLaunched(ILaunchable enemy, Vector3 velocity) => Invoke(Launched, enemy, velocity);
    public static void RaiseMotionUpdated(ILaunchable enemy, Vector3 velocity) => Invoke(MotionUpdated, enemy, velocity);
    public static void RaiseFalling(ILaunchable enemy) => Invoke(Falling, enemy);
    public static void RaiseRecovered(ILaunchable enemy) => Invoke(Recovered, enemy);
    public static void RaiseReset(ILaunchable enemy) => Invoke(Reset, enemy);
    public static void RaiseGuardChanged(ILaunchable enemy, bool guarding) => InvokeBool(GuardChanged, enemy, guarding);
    public static void RaiseSwarmMembershipChanged(ILaunchable enemy, bool swarming) => InvokeBool(SwarmMembershipChanged, enemy, swarming);
    public static void RaiseAttackStarted(ILaunchable enemy) => Invoke(AttackStarted, enemy);
    public static void RaiseAttackEnded(ILaunchable enemy) => Invoke(AttackEnded, enemy);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        Launched = null; MotionUpdated = null; Falling = null; Recovered = null; Reset = null;
        GuardChanged = null;SwarmMembershipChanged = null;
        AttackStarted = null;AttackEnded = null;
    }
    static void Invoke(Action<ILaunchable> handler, ILaunchable enemy)
    {
        if (handler == null) return;
        foreach (Action<ILaunchable> receiver in handler.GetInvocationList())
        {
            try { receiver(enemy); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
    static void Invoke(Action<ILaunchable, Vector3> handler, ILaunchable enemy, Vector3 velocity)
    {
        if (handler == null) return;
        foreach (Action<ILaunchable, Vector3> receiver in handler.GetInvocationList())
        {
            try { receiver(enemy, velocity); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
    static void InvokeBool(Action<ILaunchable, bool> handler, ILaunchable enemy, bool value)
    {
        if (handler == null) return;
        foreach (Action<ILaunchable, bool> receiver in handler.GetInvocationList())
        {
            try { receiver(enemy, value); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
}
