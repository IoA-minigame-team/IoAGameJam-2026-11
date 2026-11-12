using UnityEngine;

/// <summary>
/// 刀から主人公までの線を地面に描く簡易版。中心線と幅の両縁の3本で、端で当てると逸れることを読ませる。
/// 地面に凸凹があるので、線を細かく区切って各点を地面の高さに合わせ、地面に潜らないようにする。
/// 見た目の作り込みは後でする。太さや色は各 LineRenderer のインスペクターで決める。
/// </summary>
public sealed class SlashLineView : MonoBehaviour
{
    // 線の長さがこれより短いときは、主人公の向きから縁の方向を決める
    const float MIN_LINE_LENGTH = 0.01f;
    // 1回の Raycast で拾う当たりの上限。主人公のカプセルと地面が重なっても地面を取りこぼさない数
    const int MAX_RAY_HITS = 8;
    // 線は始点と終点の2点が最低限必要
    const int MIN_POINT_COUNT = 2;

    [SerializeField] PlayerController controller;
    [SerializeField] LineRenderer centerLine;
    [SerializeField] LineRenderer leftEdge;
    [SerializeField] LineRenderer rightEdge;
    [Tooltip("地面から浮かせる高さ(m)。地面と重なってちらつかないようにする")]
    [SerializeField, Min(0f)] float heightOffset = 0.02f;

    [Header("地面に沿わせる")]
    [Tooltip("地面として高さを拾うレイヤー。敵のレイヤーは含めない")]
    [SerializeField] LayerMask groundMask = 1; // Default
    [Tooltip("線を区切る間隔(m)。短いほど凸凹に細かく沿うが、Raycast が増える")]
    [SerializeField, Min(0.05f)] float segmentLength = 0.25f;
    [Tooltip("1本の線の点の上限。線が長くなっても負荷を抑える")]
    [SerializeField, Min(MIN_POINT_COUNT)] int maxPointCount = 64;
    [Tooltip("足元からどれだけ上から地面を探すか(m)。足元より高い盛り上がりも拾えるようにする")]
    [SerializeField, Min(0f)] float rayStartHeight = 1f;
    [Tooltip("下向きに地面を探す長さ(m)")]
    [SerializeField, Min(0.1f)] float rayLength = 3f;

    Vector3[] points;
    readonly RaycastHit[] rayHits = new RaycastHit[MAX_RAY_HITS];

    void Awake()
    {
        points = new Vector3[maxPointCount];
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

        float footHeight = controller.FootHeight;
        Vector3 start = controller.PlantedPosition;
        Vector3 end = controller.transform.position;
        start.y = footHeight;
        end.y = footHeight;

        Vector3 flat = end - start;
        Vector3 direction = flat.magnitude >= MIN_LINE_LENGTH ? flat.normalized : controller.transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, direction) * (controller.CurrentLineWidth * 0.5f);

        // 縁だけ段差に掛かることもあるので、3本それぞれの位置で地面を取る
        SetPoints(centerLine, start, end, footHeight);
        SetPoints(leftEdge, start - side, end - side, footHeight);
        SetPoints(rightEdge, start + side, end + side, footHeight);
    }

    static void Setup(LineRenderer line)
    {
        if (line == null) return;
        line.useWorldSpace = true;
        line.positionCount = MIN_POINT_COUNT;
    }

    void SetVisible(bool visible)
    {
        if (centerLine != null) centerLine.enabled = visible;
        if (leftEdge != null) leftEdge.enabled = visible;
        if (rightEdge != null) rightEdge.enabled = visible;
    }

    /// <summary>a から b までを区切り、各点を地面の高さに合わせて線に設定する</summary>
    void SetPoints(LineRenderer line, Vector3 a, Vector3 b, float footHeight)
    {
        if (line == null) return;

        float length = Vector3.Distance(a, b);
        int count = Mathf.Clamp(Mathf.CeilToInt(length / segmentLength) + 1, MIN_POINT_COUNT, points.Length);

        for (int i = 0; i < count; i++)
        {
            Vector3 p = Vector3.Lerp(a, b, i / (float)(count - 1));
            p.y = SampleGroundHeight(p, footHeight) + heightOffset;
            points[i] = p;
        }

        line.positionCount = count;
        line.SetPositions(points);
    }

    /// <summary>真下の地面の高さを返す。見つからなければ(島の外など)足元の高さにする</summary>
    float SampleGroundHeight(Vector3 point, float footHeight)
    {
        Vector3 origin = new Vector3(point.x, footHeight + rayStartHeight, point.z);
        int hitCount = Physics.RaycastNonAlloc(
            origin, Vector3.down, rayHits, rayLength, groundMask, QueryTriggerInteraction.Ignore);

        // 主人公も地面と同じ Default レイヤーなので、自分のカプセルに当たった分は捨てる。
        // RaycastNonAlloc の結果は近い順に並ばないので、一番近い地面を探す
        float nearest = float.MaxValue;
        float groundHeight = footHeight;
        Transform self = controller.transform;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = rayHits[i];
            if (hit.collider.transform.IsChildOf(self)) continue;
            if (hit.distance >= nearest) continue;
            nearest = hit.distance;
            groundHeight = hit.point.y;
        }
        return groundHeight;
    }
}
