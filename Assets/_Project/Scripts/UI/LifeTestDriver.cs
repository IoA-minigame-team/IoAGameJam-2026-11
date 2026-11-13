using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 被弾の残り回数の UI を試すため(Dev シーン専用、本番シーンには置かない)。
/// K で被弾したことにし、L で元の回数に戻す。本物と同じ PlayerDamaged を流すだけで、UI には直接触らない。
/// 0 まで減らすと、三條さんの GameOverController があるシーンではゲームオーバーになるので、テスト用シーンで使う。
/// </summary>
public sealed class LifeTestDriver : MonoBehaviour
{
    [SerializeField] FeedbackTuning tuning;
    [SerializeField] bool showHelp = true;

    int remaining = -1;

    int Full => tuning != null ? tuning.LifeSlots : 3;

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (k == null) return;
        if (remaining < 0) remaining = Full;

        if (k.kKey.wasPressedThisFrame && remaining > 0)
        {
            remaining--;
            GameEvents.RaisePlayerDamaged(remaining);
        }
        if (k.lKey.wasPressedThisFrame)
        {
            remaining = Full;
            GameEvents.RaisePlayerDamaged(remaining);
        }
    }

    void OnGUI()
    {
        if (!showHelp) return;
        GUI.Box(new Rect(10, Screen.height - 90, 320, 80),
            "被弾の残り回数のテスト\nK: 被弾したことにする\nL: 元の回数に戻す\n" + $"残り {Mathf.Max(0, remaining)}");
    }
}
