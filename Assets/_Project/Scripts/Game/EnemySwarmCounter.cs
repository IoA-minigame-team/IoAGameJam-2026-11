using System.Collections.Generic;
using UnityEngine;

/// <summary>刀の周囲に到着した灯喰らいだけを集計し、共有SwarmCountChangedで他担当へ渡す。</summary>
public sealed class EnemySwarmCounter : MonoBehaviour
{
    readonly HashSet<ILaunchable> members = new HashSet<ILaunchable>();
    int published = -1;
    public int Count => members.Count;
    void OnEnable()
    {
        EnemyMotionEvents.SwarmMembershipChanged += OnMembership;
        GameEvents.SlashStarted += OnSlashStarted;GameEvents.GameOver += Clear;
        members.Clear();
        // 無効化から戻った場合も、その間の所属通知を推測せず現在の個体から復元する。
        foreach (var pursuit in FindObjectsByType<EnemyPursuit>(FindObjectsSortMode.None))
            if (pursuit.IsSwarming) members.Add(pursuit.GetComponent<ILaunchable>());
        published = -1;Publish();
    }
    void OnDisable()
    {
        EnemyMotionEvents.SwarmMembershipChanged -= OnMembership;
        GameEvents.SlashStarted -= OnSlashStarted;GameEvents.GameOver -= Clear;
        Clear();
    }
    void OnMembership(ILaunchable source, bool value)
    {
        if (source == null) return;
        if (value) members.Add(source);else members.Remove(source);
        Publish();
    }
    void OnSlashStarted(SlashStartedInfo info) => Clear();
    void Clear() { members.Clear();Publish(); }
    void Publish()
    {
        if (published == Count) return;
        published = Count;GameEvents.RaiseSwarmCountChanged(Count);
    }
}
