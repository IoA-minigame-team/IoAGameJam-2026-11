using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 大銘「引力」の効果(仕様書「銘」:刀を刺した瞬間、周りの敵を刀へ引き寄せる)。
/// 刀が刺さったら(SwordPlanted)周りの敵に、刀の手前で止まる初速を ILaunchable.Launch で渡す。
/// 敵は一定の減速(EnemyTuning.Friction)で止まるので、止めたい距離 d に対して初速を √(2 × 減速 × d) にすれば、ちょうどその距離で止まる。
/// 敵のコードを変えずに寄せるための方法。引き寄せ中の敵は吹き飛び中と同じ扱いになり、斬れない(だから寄せる距離に上限を設けている)。
/// </summary>
public sealed class InryokuEffect : MonoBehaviour
{
    const int MAX_COLLIDERS = 64;
    // 刀とほぼ同じ所にいる敵は、寄せる必要がない
    const float MIN_PULL_DISTANCE = 0.05f;
    // 縮む輪と光の線の太さ(m)
    const float RING_WIDTH = 0.12f;
    const float STREAK_WIDTH = 0.06f;
    // 縮む輪の最後の大きさ(m)。刀に重なって消える
    const float RING_END_RADIUS = 0.1f;
    // 吹き飛ばしではないので一振りの番号を持たせない(引き寄せ中に落ちても、一振りの落とした数に入れない)
    const int NO_SLASH_ID = -1;

    [SerializeField] MeiTuning tuning;
    [Tooltip("敵の減速を読むため(読むだけ)。InGame の敵が使っている EnemyTuning を入れる")]
    [SerializeField] EnemyTuning enemyTuning;
    [Tooltip("引き寄せる敵のレイヤー。髙橋さんの斬り抜けと同じ Enemy レイヤーにする")]
    [SerializeField] LayerMask enemyMask;
    [Tooltip("輪と光の線を描くマテリアル。FeedbackTuning の ringMaterial と同じものを入れる。空なら描かない")]
    [SerializeField] Material ringMaterial;

    readonly Collider[] colliderBuffer = new Collider[MAX_COLLIDERS];
    readonly HashSet<ILaunchable> pulled = new HashSet<ILaunchable>();
    MajorMeiId current = MajorMeiId.None;

    void OnEnable()
    {
        GameEvents.MajorMeiChanged += OnMajorMeiChanged;
        GameEvents.SwordPlanted += OnSwordPlanted;
    }

    void OnDisable()
    {
        GameEvents.MajorMeiChanged -= OnMajorMeiChanged;
        GameEvents.SwordPlanted -= OnSwordPlanted;
    }

    void OnMajorMeiChanged(MajorMeiId major) => current = major;

    void OnSwordPlanted(Vector3 swordPosition)
    {
        if (current != MajorMeiId.Inryoku || tuning == null || enemyTuning == null) return;

        float radius = tuning.InryokuRadius;
        Color color = tuning.InryokuColor;
        if (ringMaterial != null)
        {
            MeiPulseRing.SpawnRing(ringMaterial, swordPosition, radius, RING_END_RADIUS, RING_WIDTH, tuning.InryokuRingDuration, color);
        }

        int count = Physics.OverlapSphereNonAlloc(swordPosition, radius, colliderBuffer, enemyMask, QueryTriggerInteraction.Collide);
        float friction = enemyTuning.Friction;

        pulled.Clear();
        for (int i = 0; i < count; i++)
        {
            ILaunchable enemy = colliderBuffer[i].GetComponentInParent<ILaunchable>();
            colliderBuffer[i] = null;
            if (enemy == null || !pulled.Add(enemy)) continue;

            Vector3 toSword = swordPosition - enemy.Position;
            toSword.y = 0f;
            float distance = toSword.magnitude;
            // 止めたい所まで、上限の距離だけ寄せる
            float pullDistance = Mathf.Min(distance - tuning.InryokuStopDistance, tuning.InryokuMaxPullDistance);
            if (pullDistance < MIN_PULL_DISTANCE) continue;

            Vector3 direction = toSword / distance;
            // 寄せる技なので、剛力、重さ、光の蓄積は掛けない。掛けると止まる位置がずれ、刀を通り越してしまう
            float speed = Mathf.Sqrt(2f * friction * pullDistance);
            enemy.Launch(direction * speed, NO_SLASH_ID);

            if (ringMaterial != null)
            {
                MeiPulseRing.SpawnStreak(ringMaterial, enemy.Position, swordPosition, STREAK_WIDTH, tuning.InryokuStreakDuration, color);
            }
        }
        pulled.Clear();
    }
}
