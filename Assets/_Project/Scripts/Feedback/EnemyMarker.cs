using UnityEngine;

/// <summary>
/// 敵の頭上に赤い逆円錐を浮かべる目印。敵Prefabの子に置く。
/// 墨色の敵が夜の舞台に溶けて見つけにくいという試遊の意見から、影の見た目は変えずに頭上だけで位置を知らせる。
/// 画面外の敵アイコンが読めるよう、表示している間は EnemyMarkers に自分を登録する。
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class EnemyMarker : MonoBehaviour
{
    static readonly int BASE_COLOR = Shader.PropertyToID("_BaseColor");

    [SerializeField] FeedbackTuning tuning;

    ILaunchable owner;
    Transform root;
    MeshRenderer meshRenderer;
    Mesh mesh;
    bool falling;
    float bobTime;

    void Awake()
    {
        owner = GetComponentInParent<ILaunchable>();
        // 敵の見た目は吹き飛びや縁の揺れで傾くので、傾かない敵の根元を基準に位置を決める
        root = owner != null ? owner.Transform : transform.parent;
        meshRenderer = GetComponent<MeshRenderer>();
        if (tuning == null) Debug.LogError("EnemyMarker: FeedbackTuning を設定してください", this);
        BuildMesh();
        ApplyColor();
        // 何体も同時に揺れると揃って見えるので、出だしをずらす
        bobTime = Random.value * (tuning != null ? tuning.MarkerBobPeriod : 1f);
    }

    void OnEnable()
    {
        EnemyMotionEvents.Falling += OnFalling;
        EnemyMotionEvents.Reset += OnReset;
        if (!falling) Show(true);
    }

    void OnDisable()
    {
        EnemyMotionEvents.Falling -= OnFalling;
        EnemyMotionEvents.Reset -= OnReset;
        EnemyMarkers.Unregister(transform);
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    void OnFalling(ILaunchable enemy)
    {
        // 縁を越えたらもう狙う相手ではないので、目印も画面外アイコンも消す
        if (!ReferenceEquals(enemy, owner)) return;
        falling = true;
        Show(false);
    }

    void OnReset(ILaunchable enemy)
    {
        if (!ReferenceEquals(enemy, owner)) return;
        falling = false;
        Show(true);
    }

    void Show(bool visible)
    {
        meshRenderer.enabled = visible;
        if (visible) EnemyMarkers.Register(transform);
        else EnemyMarkers.Unregister(transform);
    }

    // 敵が Update で動いたあとに追いかけるため LateUpdate にする
    void LateUpdate()
    {
        if (tuning == null || root == null) return;
        // ヒットストップ中は敵と一緒に止まって見えるよう、通常の時間で揺らす
        bobTime += Time.deltaTime;
        float bob = Mathf.Sin(bobTime * Mathf.PI * 2f / tuning.MarkerBobPeriod) * tuning.MarkerBobAmplitude;
        transform.SetPositionAndRotation(root.position + Vector3.up * (tuning.MarkerHeight + bob), Quaternion.identity);
    }

    void ApplyColor()
    {
        if (tuning == null) return;
        var block = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(block);
        block.SetColor(BASE_COLOR, tuning.MarkerColor);
        meshRenderer.SetPropertyBlock(block);
    }

    /// <summary>
    /// 原点を先端にした逆円錐を作る。ProBuilder を入れずに済むよう、コードで面を張る。
    /// 側面は辺ごとに頂点を分けて、角ばった陰影にする(Unlit でも輪郭が読みやすい)
    /// </summary>
    void BuildMesh()
    {
        int segments = tuning != null ? tuning.MarkerSegments : 12;
        float radius = tuning != null ? tuning.MarkerRadius : 0.18f;
        float length = tuning != null ? tuning.MarkerLength : 0.38f;

        var vertices = new Vector3[segments * 3 + segments + 1];
        var triangles = new int[segments * 3 * 2];
        Vector3 tip = Vector3.zero;
        Vector3 capCenter = Vector3.up * length;
        int v = 0, t = 0;

        // 側面
        for (int i = 0; i < segments; i++)
        {
            Vector3 a = RimPoint(i, segments, radius, length);
            Vector3 b = RimPoint(i + 1, segments, radius, length);
            vertices[v] = tip;
            vertices[v + 1] = b;
            vertices[v + 2] = a;
            triangles[t++] = v;
            triangles[t++] = v + 1;
            triangles[t++] = v + 2;
            v += 3;
        }

        // 上のふた(斜め上のカメラから見えるのはこちら)
        int center = v;
        vertices[v++] = capCenter;
        int rimStart = v;
        for (int i = 0; i < segments; i++) vertices[v++] = RimPoint(i, segments, radius, length);
        for (int i = 0; i < segments; i++)
        {
            triangles[t++] = center;
            triangles[t++] = rimStart + i;
            triangles[t++] = rimStart + (i + 1) % segments;
        }

        mesh = new Mesh { name = "EnemyMarkerCone" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    static Vector3 RimPoint(int index, int segments, float radius, float height)
    {
        float angle = index * Mathf.PI * 2f / segments;
        return new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
    }
}
