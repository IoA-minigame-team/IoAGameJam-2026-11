using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面外にいる敵の方向を、画面の縁にアイコンで知らせる。
/// 見下ろしカメラの外から迫る敵に気づけないという試遊の意見から足した。
/// 敵の一覧は EnemyMarkers(頭上の目印が登録する)から読むので、敵のクラスには触らない。
/// アイコンは「明るい丸の下地 + 正立の影のアイコン + 敵の向きへ回す赤い三角」で組む。
/// 黒いシルエットだけだと夜の舞台に溶けるので下地を敷き、影の形が逆さにならないよう回すのは三角だけにする。
/// 全画面に広げた RectTransform に付ける。子は実行時に作る。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class OffscreenEnemyIndicator : MonoBehaviour
{
    // 三角と丸は画像を増やさずに済むよう実行時に描く。そのときの解像度(px)
    const int SHAPE_TEXTURE_SIZE = 64;
    // 下地の丸に対する、影のアイコンの大きさ
    const float ICON_SCALE = 0.78f;
    // 三角の大きさと、中心から三角までの距離(アイコンの大きさに対する割合)
    const float ARROW_SCALE = 0.36f;
    const float ARROW_OFFSET = 0.66f;

    [SerializeField] FeedbackTuning tuning;
    [Tooltip("影のアイコン(enemy_icon_256)")]
    [SerializeField] Sprite enemyIcon;
    [Tooltip("下地の丸の色。黒いアイコンが夜の舞台で読めるように明るくする")]
    [SerializeField] Color backingColor = new Color(0.92f, 0.92f, 0.92f, 0.9f);
    [Tooltip("最初に作っておくアイコンの数。足りなければ増やす")]
    [SerializeField, Min(1)] int initialPoolSize = 8;

    sealed class Indicator
    {
        public RectTransform Root;
        public RectTransform Arrow;
        public Image ArrowImage;
    }

    readonly List<Indicator> pool = new List<Indicator>();
    RectTransform area;
    Sprite circleSprite;
    Sprite arrowSprite;
    Camera cachedCamera;
    bool gameOver;

    void Awake()
    {
        area = (RectTransform)transform;
        if (tuning == null) Debug.LogError("OffscreenEnemyIndicator: FeedbackTuning を設定してください", this);
        circleSprite = CreateShapeSprite(true);
        arrowSprite = CreateShapeSprite(false);
        for (int i = 0; i < initialPoolSize; i++) pool.Add(CreateIndicator(i));
    }

    void OnEnable() => GameEvents.GameOver += OnGameOver;

    void OnDisable() => GameEvents.GameOver -= OnGameOver;

    void OnDestroy()
    {
        if (circleSprite != null) { Destroy(circleSprite.texture); Destroy(circleSprite); }
        if (arrowSprite != null) { Destroy(arrowSprite.texture); Destroy(arrowSprite); }
    }

    void OnGameOver() => gameOver = true;

    // カメラは敵や主人公が動いたあと(Cinemachine は LateUpdate)に位置が決まるので、ここも LateUpdate にする
    void LateUpdate()
    {
        int used = 0;
        if (CanShow(out Camera cam))
        {
            Vector2 size = area.rect.size;
            IReadOnlyList<Transform> markers = EnemyMarkers.Active;
            for (int i = 0; i < markers.Count; i++)
            {
                Transform marker = markers[i];
                if (marker == null) continue;
                if (!TryEdgePoint(cam, marker.position, size, out Vector2 position, out float angle)) continue;
                if (used >= pool.Count) pool.Add(CreateIndicator(used));
                Place(pool[used], position, angle);
                used++;
            }
        }
        for (int i = used; i < pool.Count; i++)
        {
            if (pool[i].Root.gameObject.activeSelf) pool[i].Root.gameObject.SetActive(false);
        }
    }

    bool CanShow(out Camera cam)
    {
        cam = null;
        // 斬り抜け演出中はカメラが肩越しに回るので、縁の方向が普段と食い違う。ポーズ中と終了後も邪魔なので出さない
        if (tuning == null || gameOver || TimeController.IsPaused || GameContext.IsSlashCinematicPlaying) return false;
        // Camera.main は毎回探すと重いので、消えたときだけ取り直す
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled) cachedCamera = Camera.main;
        cam = cachedCamera;
        return cam != null;
    }

    /// <summary>
    /// 画面外なら、画面の中心から敵へ向かう線が余白を引いた枠とぶつかる点を返す。画面内なら false
    /// </summary>
    bool TryEdgePoint(Camera cam, Vector3 world, Vector2 size, out Vector2 position, out float angle)
    {
        position = Vector2.zero;
        angle = 0f;
        Vector3 viewport = cam.WorldToViewportPoint(world);
        bool behind = viewport.z < 0f;
        float inset = tuning.OffscreenInset;
        bool inside = !behind
            && viewport.x >= -inset && viewport.x <= 1f + inset
            && viewport.y >= -inset && viewport.y <= 1f + inset;
        if (inside) return false;

        Vector2 direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
        // カメラの後ろにある点は上下左右が反転して返ってくるので、向きを戻す
        if (behind) direction = -direction;
        if (direction.sqrMagnitude < 0.000001f) direction = Vector2.down;

        // 縦横の比が違う画面でも余白が px で揃うよう、px の座標で枠に当てる
        Vector2 pixel = new Vector2(direction.x * size.x, direction.y * size.y);
        float halfWidth = Mathf.Max(0f, size.x * 0.5f - tuning.OffscreenMargin);
        float halfHeight = Mathf.Max(0f, size.y * 0.5f - tuning.OffscreenMargin);
        float scaleX = Mathf.Abs(pixel.x) > 0.0001f ? halfWidth / Mathf.Abs(pixel.x) : float.MaxValue;
        float scaleY = Mathf.Abs(pixel.y) > 0.0001f ? halfHeight / Mathf.Abs(pixel.y) : float.MaxValue;
        position = size * 0.5f + pixel * Mathf.Min(scaleX, scaleY);
        angle = Mathf.Atan2(pixel.y, pixel.x) * Mathf.Rad2Deg;
        return true;
    }

    void Place(Indicator indicator, Vector2 position, float angle)
    {
        if (!indicator.Root.gameObject.activeSelf) indicator.Root.gameObject.SetActive(true);
        float iconSize = tuning.OffscreenIconSize;
        indicator.Root.sizeDelta = new Vector2(iconSize, iconSize);
        indicator.Root.anchoredPosition = position;
        float radians = angle * Mathf.Deg2Rad;
        indicator.Arrow.anchoredPosition = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * (iconSize * ARROW_OFFSET);
        indicator.Arrow.sizeDelta = Vector2.one * (iconSize * ARROW_SCALE);
        indicator.Arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
        indicator.ArrowImage.color = tuning.OffscreenArrowColor;
    }

    Indicator CreateIndicator(int index)
    {
        var root = new GameObject("EnemyIndicator" + index, typeof(RectTransform));
        var rootRect = (RectTransform)root.transform;
        rootRect.SetParent(area, false);
        // 左下を原点にして px で置く
        rootRect.anchorMin = rootRect.anchorMax = Vector2.zero;
        rootRect.pivot = new Vector2(0.5f, 0.5f);

        CreateImage("Backing", rootRect, circleSprite, backingColor, Vector2.one);
        CreateImage("Icon", rootRect, enemyIcon, Color.white, Vector2.one * ICON_SCALE);
        Image arrow = CreateImage("Arrow", rootRect, arrowSprite, Color.white, Vector2.zero);
        var arrowRect = arrow.rectTransform;
        arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(0.5f, 0.5f);

        root.SetActive(false);
        return new Indicator { Root = rootRect, Arrow = arrowRect, ArrowImage = arrow };
    }

    static Image CreateImage(string name, RectTransform parent, Sprite sprite, Color color, Vector2 relativeSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        // 親の大きさに対する割合で広げる。Arrow はあとで直接大きさを決める
        Vector2 half = relativeSize * 0.5f;
        rect.anchorMin = new Vector2(0.5f, 0.5f) - half;
        rect.anchorMax = new Vector2(0.5f, 0.5f) + half;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        // 操作の邪魔をしないよう、クリックを受けない
        image.raycastTarget = false;
        return image;
    }

    /// <summary>丸(circle が true)か、右を向いた三角を、縁をなめらかにして描く</summary>
    static Sprite CreateShapeSprite(bool circle)
    {
        int n = SHAPE_TEXTURE_SIZE;
        var texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = circle ? "IndicatorCircle" : "IndicatorArrow",
        };
        var pixels = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                // -1〜1 の座標
                float u = (x + 0.5f) / n * 2f - 1f;
                float v = (y + 0.5f) / n * 2f - 1f;
                // 図形の内側で正、外側で負になる距離(px)
                float distance = circle ? (1f - Mathf.Sqrt(u * u + v * v)) * n * 0.5f : TriangleDistance(u, v) * n * 0.5f;
                byte alpha = (byte)(Mathf.Clamp01(distance + 0.5f) * 255f);
                pixels[y * n + x] = new Color32(255, 255, 255, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }

    // 頂点 (1,0)、(-0.8,0.9)、(-0.8,-0.9) の三角。3辺のうち一番近い辺までの距離
    static float TriangleDistance(float u, float v)
    {
        float d1 = EdgeDistance(u, v, new Vector2(1f, 0f), new Vector2(-0.8f, 0.9f));
        float d2 = EdgeDistance(u, v, new Vector2(-0.8f, 0.9f), new Vector2(-0.8f, -0.9f));
        float d3 = EdgeDistance(u, v, new Vector2(-0.8f, -0.9f), new Vector2(1f, 0f));
        return Mathf.Min(d1, Mathf.Min(d2, d3));
    }

    // a→b の辺の左側を内側として、符号付きの距離を返す(頂点は反時計回りに並べる)
    static float EdgeDistance(float u, float v, Vector2 a, Vector2 b)
    {
        Vector2 edge = b - a;
        Vector2 normal = new Vector2(-edge.y, edge.x).normalized;
        return Vector2.Dot(new Vector2(u, v) - a, normal);
    }
}
