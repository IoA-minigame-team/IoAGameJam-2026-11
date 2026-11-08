using UnityEngine;

/// <summary>
/// 刀から主人公までの線を地面に描く簡易版。中心線と幅の両縁の3本で、端で当てると逸れることを読ませる。
/// 見た目の作り込みは後でする。太さや色は各 LineRenderer のインスペクターで決める。
/// </summary>
public sealed class SlashLineView : MonoBehaviour
{
    // 線の長さがこれより短いときは、主人公の向きから縁の方向を決める
    const float MIN_LINE_LENGTH = 0.01f;

    [SerializeField] PlayerController controller;
    [SerializeField] LineRenderer centerLine;
    [SerializeField] LineRenderer leftEdge;
    [SerializeField] LineRenderer rightEdge;
    [Tooltip("地面から浮かせる高さ(m)。地面と重なってちらつかないようにする")]
    [SerializeField, Min(0f)] float heightOffset = 0.02f;

    void Awake()
    {
        Setup(centerLine);
        Setup(leftEdge);
        Setup(rightEdge);
        SetVisible(false);
    }

    void LateUpdate()
    {
        if (controller == null) return;

        bool visible = controller.IsLineVisible;
        SetVisible(visible);
        if (!visible) return;

        float y = controller.FootHeight + heightOffset;
        Vector3 start = controller.PlantedPosition;
        Vector3 end = controller.transform.position;
        start.y = y;
        end.y = y;

        Vector3 flat = end - start;
        Vector3 direction = flat.magnitude >= MIN_LINE_LENGTH ? flat.normalized : controller.transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, direction) * (controller.CurrentLineWidth * 0.5f);

        SetPoints(centerLine, start, end);
        SetPoints(leftEdge, start - side, end - side);
        SetPoints(rightEdge, start + side, end + side);
    }

    static void Setup(LineRenderer line)
    {
        if (line == null) return;
        line.useWorldSpace = true;
        line.positionCount = 2;
    }

    void SetVisible(bool visible)
    {
        if (centerLine != null) centerLine.enabled = visible;
        if (leftEdge != null) leftEdge.enabled = visible;
        if (rightEdge != null) rightEdge.enabled = visible;
    }

    static void SetPoints(LineRenderer line, Vector3 a, Vector3 b)
    {
        if (line == null) return;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
    }
}
