using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 頭上の目印を出している敵の一覧。書くのは敵の目印(EnemyMarker)、読むのは画面外の敵アイコン。
/// UI が敵のクラスを参照せずに「今見せるべき敵」を回せるように、目印の位置だけをここに置く
/// </summary>
public static class EnemyMarkers
{
    static readonly List<Transform> active = new List<Transform>();

    /// <summary>目印を出している敵の Transform(目印そのもの)。破棄済みが混ざることはない</summary>
    public static IReadOnlyList<Transform> Active => active;

    public static void Register(Transform marker)
    {
        if (marker != null && !active.Contains(marker)) active.Add(marker);
    }

    public static void Unregister(Transform marker)
    {
        active.Remove(marker);
    }

    // ドメインリロードを切った再生でも、前回の敵が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        active.Clear();
    }
}
