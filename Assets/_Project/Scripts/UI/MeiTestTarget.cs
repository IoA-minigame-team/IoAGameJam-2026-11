using UnityEngine;

/// <summary>
/// 大銘「震」を試すための仮の敵(Dev シーン専用、本番では三條さんの Enemy が押し出される)。
/// ILaunchable の口だけ持ち、受け取った初速で滑って、だんだん止まる。重さや盾は無い。
/// </summary>
public sealed class MeiTestTarget : MonoBehaviour, ILaunchable
{
    // 摩擦と止まる速さは MeiTuning の「テスト用の敵」の欄で直す
    float friction;
    float stopSpeed;
    Vector3 velocity;

    public void Configure(MeiTuning tuning)
    {
        friction = tuning.TestTargetFriction;
        stopSpeed = tuning.TestTargetStopSpeed;
    }

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
        if (velocity.sqrMagnitude <= stopSpeed * stopSpeed) return;
        float dt = Time.deltaTime;
        transform.position += velocity * dt;
        velocity *= Mathf.Max(0f, 1f - friction * dt);
    }
}
