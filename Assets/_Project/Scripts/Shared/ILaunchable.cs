using UnityEngine;

/// <summary>
/// 吹き飛ばせるもの。主人公側は敵の種類を知らずに、この口だけで吹き飛ばす。
/// 実装するのは三條さん、呼ぶのは髙橋と増永さん(大銘「震」「残光」)。
/// </summary>
public interface ILaunchable
{
    /// <summary>見た目の演出(白く光る、縁で揺れる)で敵の本体に触るために出す</summary>
    Transform Transform { get; }

    Vector3 Position { get; }

    /// <summary>吹き飛ばし力に掛ける倍率。盾持ちは重いので 1 より小さい</summary>
    float Weight { get; }

    /// <summary>光の蓄積による吹き飛びやすさの倍率。蓄積なしで 1</summary>
    float GlowMultiplier { get; }

    /// <summary>斬り抜ける向き slashDir に対して盾が正面を向いているか。盾持ち以外は常に false</summary>
    bool IsShieldFacing(Vector3 slashDir);

    /// <summary>初速 velocity で吹き飛びを始める。slashId は落ちるまで持たせて EnemyFell に入れる</summary>
    void Launch(Vector3 velocity, int slashId);
}
