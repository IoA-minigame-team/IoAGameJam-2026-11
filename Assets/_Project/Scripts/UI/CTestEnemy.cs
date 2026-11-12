using UnityEngine;

/// <summary>
/// テスト用の仮の敵(Dev シーン専用、本番では三條さんの敵を使う)。
/// 三條さんの連携ガイドの「実装の例」と同じ形で ILaunchable を実装し、吹き飛び、落下、踏みとどまり、光の蓄積のイベントを本物と同じように流す。
/// 増永の演出(黒い雷の残光、白く光る、縁で揺れる、光の粒、縁を越える音、スコア)を、敵の完成を待たずに確かめるため。
/// </summary>
public sealed class CTestEnemy : MonoBehaviour, ILaunchable
{
    // 仕様書「パラメータ」に近い仮の値。本物の値は EnemyTuning(三條さん)
    const float FRICTION = 5.5f;
    const float GLOW_STEP = 0.3f;
    const float GLOW_MAX = 1.5f;
    const float FALL_GRAVITY = 18f;

    float islandRadius = 8f;
    Vector3 velocity;
    int slashId = -1;
    float glow;
    bool flying;
    bool falling;
    float fallSpeed;

    public Transform Transform => transform;
    public Vector3 Position => transform.position;
    public float Weight => 1f;
    public float GlowMultiplier => 1f + glow;
    public bool IsShieldFacing(Vector3 slashDir) => false;

    /// <summary>テスト用の敵を作る。見た目の子(カプセル)を付けるのは、縁で揺れる演出が子を揺らすため</summary>
    public static CTestEnemy Create(Vector3 position, float radius)
    {
        GameObject root = new GameObject("CTestEnemy");
        root.transform.position = position;
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
        body.GetComponent<Renderer>().material.color = new Color(0.1f, 0.1f, 0.12f);
        CTestEnemy enemy = root.AddComponent<CTestEnemy>();
        enemy.islandRadius = radius;
        return enemy;
    }

    public void Launch(Vector3 launchVelocity, int id)
    {
        velocity = new Vector3(launchVelocity.x, 0f, launchVelocity.z);
        slashId = id;
        flying = true;
        glow = Mathf.Min(GLOW_MAX, glow + GLOW_STEP * GameContext.MeiStats.GlowGainMultiplier);
        GameEvents.RaiseGlowChanged(this, glow / GLOW_MAX);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (falling)
        {
            // 霧の下へ落ちていく。十分に落ちたら消す
            fallSpeed += FALL_GRAVITY * dt;
            transform.position += (velocity * 0.6f + Vector3.down * fallSpeed) * dt;
            if (transform.position.y < -8f) Destroy(gameObject);
            return;
        }
        if (!flying) return;

        // 物理エンジンは使わず、初速と摩擦で動かす(仕様)
        transform.position += velocity * dt;
        float speed = velocity.magnitude;
        float next = Mathf.Max(0f, speed - FRICTION * dt);
        velocity = speed > 0f ? velocity * (next / speed) : Vector3.zero;

        Vector3 flat = new Vector3(transform.position.x, 0f, transform.position.z);
        if (flat.magnitude > islandRadius)
        {
            falling = true;
            flying = false;
            // 縁を越えた瞬間に「抜けた」音。本物の敵では、三條さんの Enemy が落ち始める所(BeginFall)で同じ1行を呼んでもらう
            Feedback.PlaySe(SeId.EdgeCross, transform.position);
            GameEvents.RaiseEnemyFell(new EnemyFellInfo(slashId, transform.position));
            return;
        }
        if (next <= 0.35f)
        {
            flying = false;
            if (islandRadius - flat.magnitude < 1f) GameEvents.RaiseEnemyTeetered(this);
        }
    }
}
