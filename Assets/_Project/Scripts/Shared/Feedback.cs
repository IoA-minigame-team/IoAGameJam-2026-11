using UnityEngine;

/// <summary>
/// 揺れ、SE、エフェクトを鳴らす窓口。IDを指定するだけで鳴らせる。
/// 中身(Service)がまだ無くても何もせずに返るので、演出の完成を待たずに呼ぶ場所だけ先に書ける。
/// </summary>
public static class Feedback
{
    /// <summary>中身。入れるのは増永さん(OnEnable で入れ、OnDisable で null に戻す)</summary>
    public static IFeedbackService Service { get; set; }

    /// <summary>画面を揺らす。strength は揺れの強さ、duration は秒数</summary>
    public static void Shake(float strength, float duration) => Service?.Shake(strength, duration);

    public static void PlaySe(SeId id, Vector3 position) => Service?.PlaySe(id, position);

    public static void SpawnVfx(VfxId id, Vector3 position, Quaternion rotation) => Service?.SpawnVfx(id, position, rotation);

    // ドメインリロードを切った再生でも、破棄済みの中身が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Service = null;
}
