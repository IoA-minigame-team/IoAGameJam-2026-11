using UnityEngine;

/// <summary>
/// 斬り抜けの間だけ、刀の軌跡(Trail Renderer)を出す。
/// 0.2 秒の斬り抜けを目で追えるようにするため。PlayerController の状態を読むだけで、操作には関わらない。
/// 軌跡の太さや色、残る時間は Trail Renderer のインスペクターで決める。
/// </summary>
// PlayerController が状態を変えた同じフレームのうちに切り替えるため、後から動かす
[DefaultExecutionOrder(100)]
public sealed class SwordTrailView : MonoBehaviour
{
    [SerializeField] PlayerController controller;
    [Tooltip("刀に付けた軌跡。切っ先などに置く")]
    [SerializeField] TrailRenderer[] trails;

    bool wasSlashing;

    void Awake()
    {
        if (controller == null) controller = GetComponent<PlayerController>();
        SetEmitting(false);
    }

    void OnDisable()
    {
        SetEmitting(false);
        wasSlashing = false;
    }

    void Update()
    {
        if (controller == null) return;

        bool isSlashing = controller.CurrentState == PlayerController.State.Slashing;
        if (isSlashing == wasSlashing) return;
        wasSlashing = isSlashing;

        if (isSlashing)
        {
            // 刺していた場所から手元へ刀が戻った分の線を残さないよう、出す前に消す
            foreach (TrailRenderer trail in trails)
            {
                if (trail != null) trail.Clear();
            }
        }
        // 止めても、出ていた線は Trail Renderer の Time で消えていく
        SetEmitting(isSlashing);
    }

    void SetEmitting(bool emitting)
    {
        foreach (TrailRenderer trail in trails)
        {
            if (trail != null) trail.emitting = emitting;
        }
    }
}
