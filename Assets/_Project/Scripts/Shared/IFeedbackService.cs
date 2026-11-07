using UnityEngine;

/// <summary>
/// 揺れ、SE、エフェクトの中身。増永さんが Feedback/ で実装し、OnEnable で Feedback.Service に入れる。
/// 窓口(Feedback)と中身を分けたのは、Shared/ を触らずに中身を作れるようにするため。
/// </summary>
public interface IFeedbackService
{
    void Shake(float strength, float duration);
    void PlaySe(SeId id, Vector3 position);
    void SpawnVfx(VfxId id, Vector3 position, Quaternion rotation);
}
