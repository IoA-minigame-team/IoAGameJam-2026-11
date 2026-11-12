using UnityEngine;

/// <summary>
/// 大銘「震」を試すための仮の敵(Dev シーン専用、本番では三條さんの Enemy が押し出される)。
/// ILaunchable の口だけ持ち、受け取った初速で滑って、だんだん止まる。重さや盾は無い。
/// </summary>
public sealed class MeiTestTarget : MonoBehaviour, ILaunchable
{
    // 1秒あたりに減る速さの割合。止まるまでの距離を見やすくするため
    const float FRICTION = 3f;
    // これより遅くなったら止まったことにする(m/s)
    const float STOP_SPEED = 0.05f;

    Vector3 velocity;

    public Transform Transform => transform;
    public Vector3 Position => transform.position;
    public float Weight => 1f;
    public float GlowMultiplier => 1f;
    public bool IsShieldFacing(Vector3 slashDir) => false;

    public void Launch(Vector3 launchVelocity, int slashId)
    {
        velocity = launchVelocity;
        velocity.y = 0f;
    }

    void Update()
    {
        if (velocity.sqrMagnitude < STOP_SPEED * STOP_SPEED) return;
        float dt = Time.deltaTime;
        transform.position += velocity * dt;
        velocity *= Mathf.Max(0f, 1f - FRICTION * dt);
    }
}
