using UnityEngine;

/// <summary>
/// TimeController の残り時間を毎フレーム進めるためだけの部品。
/// TimeController が初めて呼ばれたときに自動で作られるので、シーンに置かなくてよい。
/// </summary>
// 他のスクリプトより先に動かし、そのフレームの timeScale を早めに確定させる
[DefaultExecutionOrder(-1000)]
public sealed class TimeControllerRunner : MonoBehaviour
{
    void Update()
    {
        TimeController.Tick(Time.unscaledDeltaTime);
    }
}
