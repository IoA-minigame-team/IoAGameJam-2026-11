using UnityEngine;

/// <summary>
/// 被弾の残り回数だけを受け取り、ゲームオーバーを一度だけ通知する。
/// HP・入力停止・リザルト画面は各担当が所有するため、このクラスでは変更しない。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameOverController : MonoBehaviour
{
    public bool IsGameOver { get; private set; }

    void OnEnable()
    {
        GameEvents.PlayerDamaged += OnPlayerDamaged;
        GameEvents.GameOver += OnGameOver;
    }

    void OnDisable()
    {
        GameEvents.PlayerDamaged -= OnPlayerDamaged;
        GameEvents.GameOver -= OnGameOver;
    }

    void OnPlayerDamaged(int remaining)
    {
        if (remaining <= 0) TryEndGame();
    }

    void OnGameOver()
    {
        // 他から終了通知が来た場合も、後続の被弾で同じ通知を重ねない。
        IsGameOver = true;
    }

    public bool TryEndGame()
    {
        if (IsGameOver) return false;
        // 通知先がさらに被弾通知を流しても、再入による二重通知を起こさない。
        IsGameOver = true;
        GameEvents.RaiseGameOver();
        return true;
    }

    /// <summary>同じシーンで次のラウンドを始めるときに呼ぶ。HP等の初期化は各所有者が行う。</summary>
    public void ResetRound()
    {
        IsGameOver = false;
    }
}
