using System.Collections.Generic;
using UnityEngine;

/// <summary>斬り抜けで当たる1体分の結果</summary>
public readonly struct SlashTarget
{
    public readonly ILaunchable Enemy;
    /// <summary>線上の位置。始点で 0、終点で 1。主人公がここを通り過ぎた瞬間に吹き飛ばす</summary>
    public readonly float Progress;
    /// <summary>敵に渡す初速(水平だけ)</summary>
    public readonly Vector3 Velocity;
    /// <summary>正面度。線の中心軸で 1、幅の端で 0</summary>
    public readonly float Frontness;

    public SlashTarget(ILaunchable enemy, float progress, Vector3 velocity, float frontness)
    {
        Enemy = enemy;
        Progress = progress;
        Velocity = velocity;
        Frontness = frontness;
    }
}

/// <summary>
/// 斬り戻しの線の判定と、吹き飛ばしの初速の計算。
/// 状態を持たない static にしたのは、式だけを読んで調整できるようにするため。
/// </summary>
public static class SlashResolver
{
    // 一度の斬り抜けで拾うコライダーの上限。群れでも足りる数にしてある
    const int MAX_COLLIDERS = 128;
    // 線の判定の箱の高さ(m)。敵の背丈を覆えればよく、手触りの値ではないのでここに置く
    const float CHECK_HEIGHT = 4f;
    // これより短い線は向きが決まらないので、主人公の向きを使う
    const float MIN_LINE_LENGTH = 0.01f;

    static readonly Collider[] colliderBuffer = new Collider[MAX_COLLIDERS];
    static readonly HashSet<ILaunchable> seen = new HashSet<ILaunchable>();

    /// <summary>
    /// 線(start→end)に当たる敵を集め、通り過ぎる順に並べて results に入れる。
    /// 盾に正面から当たった敵は吹き飛ばさないので results に入れず、その数を shieldBlockedCount で返す。
    /// 戻り値は斬り抜ける向き(水平、正規化済み)
    /// </summary>
    public static Vector3 Resolve(
        Vector3 start,
        Vector3 end,
        Vector3 fallbackDirection,
        PlayerTuning tuning,
        IMeiStats mei,
        LayerMask enemyMask,
        List<SlashTarget> results,
        out int shieldBlockedCount)
    {
        results.Clear();
        shieldBlockedCount = 0;

        Vector3 flat = end - start;
        flat.y = 0f;
        float length = flat.magnitude;
        Vector3 direction = length >= MIN_LINE_LENGTH ? flat / length : Flatten(fallbackDirection);
        Vector3 right = Vector3.Cross(Vector3.up, direction);

        float halfWidth = tuning.LineWidth * mei.LineWidthMultiplier * 0.5f;
        if (halfWidth <= 0f) return direction;

        // 線より少し大きい箱で候補を集め、正確な判定は下で敵の Position を使って行う
        Vector3 center = (start + end) * 0.5f;
        Vector3 halfExtents = new Vector3(halfWidth, CHECK_HEIGHT * 0.5f, length * 0.5f + halfWidth);
        int count = Physics.OverlapBoxNonAlloc(
            center, halfExtents, colliderBuffer, Quaternion.LookRotation(direction), enemyMask, QueryTriggerInteraction.Collide);

        float lengthBonus = 1f + tuning.LengthBonusPerMeter * length;
        float basePower = tuning.BaseLaunchPower * lengthBonus * mei.LaunchPowerMultiplier;

        seen.Clear();
        for (int i = 0; i < count; i++)
        {
            // 斬り戻しの瞬間に1回だけ呼ぶので、GetComponent の重さは気にしなくてよい
            ILaunchable enemy = colliderBuffer[i].GetComponentInParent<ILaunchable>();
            colliderBuffer[i] = null;
            if (enemy == null || !seen.Add(enemy)) continue;

            Vector3 offset = enemy.Position - start;
            offset.y = 0f;
            float along = Vector3.Dot(offset, direction);
            if (along < 0f || along > length) continue;

            float side = Vector3.Dot(offset, right);
            float absSide = Mathf.Abs(side);
            if (absSide > halfWidth) continue;

            if (enemy.IsShieldFacing(direction))
            {
                shieldBlockedCount++;
                continue;
            }

            // 0 が中央、1 が幅の端
            float edge = absSide / halfWidth;

            // 中央ほど斬り抜ける向きへまっすぐ、端ほど敵がいる側へ逸らす
            float angle = tuning.EdgeDeflectAngle * edge * Mathf.Sign(side);
            Vector3 launchDirection = Quaternion.AngleAxis(angle, Vector3.up) * direction;

            // 端ほど弱く。重さと光の蓄積は敵から読み、主人公側では持たない
            float power = basePower
                * Mathf.Lerp(1f, tuning.EdgePowerRatio, edge)
                * enemy.Weight
                * enemy.GlowMultiplier;

            float progress = length >= MIN_LINE_LENGTH ? along / length : 0f;
            results.Add(new SlashTarget(enemy, progress, launchDirection * power, 1f - edge));
        }
        seen.Clear();

        results.Sort((a, b) => a.Progress.CompareTo(b.Progress));
        return direction;
    }

    static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0f ? v.normalized : Vector3.forward;
    }
}
