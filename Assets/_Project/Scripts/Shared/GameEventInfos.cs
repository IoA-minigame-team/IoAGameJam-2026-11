using UnityEngine;

// 値が多いイベントは構造体にまとめる。
// 後から値を足しても、既存の購読側のメソッドのシグネチャが変わらずに済むため。

/// <summary>斬り戻しを始めた(刀を引き抜いた)</summary>
public readonly struct SlashStartedInfo
{
    public readonly int SlashId;
    /// <summary>斬り抜けの始点(刀の位置)</summary>
    public readonly Vector3 Start;
    /// <summary>斬り抜けの終点(ボタンを離した地点)</summary>
    public readonly Vector3 End;

    public SlashStartedInfo(int slashId, Vector3 start, Vector3 end)
    {
        SlashId = slashId;
        Start = start;
        End = end;
    }
}

/// <summary>斬り抜けが敵に当たった</summary>
public readonly struct EnemyHitInfo
{
    public readonly int SlashId;
    public readonly ILaunchable Enemy;
    /// <summary>敵に渡した初速</summary>
    public readonly Vector3 Velocity;
    /// <summary>正面度。線の中心軸に近いほど 1、幅の端ほど 0</summary>
    public readonly float Frontness;

    public EnemyHitInfo(int slashId, ILaunchable enemy, Vector3 velocity, float frontness)
    {
        SlashId = slashId;
        Enemy = enemy;
        Velocity = velocity;
        Frontness = frontness;
    }
}

/// <summary>斬り戻しが終わった(納刀)</summary>
public readonly struct SlashFinishedInfo
{
    public readonly int SlashId;
    /// <summary>当てた数。0 なら空振り(盾に正面から当たっただけの場合も 0)</summary>
    public readonly int HitCount;

    public bool IsWhiff => HitCount == 0;

    public SlashFinishedInfo(int slashId, int hitCount)
    {
        SlashId = slashId;
        HitCount = hitCount;
    }
}

/// <summary>敵が場外に落ちた</summary>
public readonly struct EnemyFellInfo
{
    /// <summary>落とした一振りの番号。吹き飛ばされずに落ちた場合は -1</summary>
    public readonly int SlashId;
    public readonly Vector3 Position;

    public EnemyFellInfo(int slashId, Vector3 position)
    {
        SlashId = slashId;
        Position = position;
    }
}
