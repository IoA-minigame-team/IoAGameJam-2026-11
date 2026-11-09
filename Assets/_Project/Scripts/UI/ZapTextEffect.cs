using TMPro;
using UnityEngine;

/// <summary>
/// 文字の後ろに「黒い雷の影」をつける。TextMeshPro の下敷き(Underlay)を黒にし、金の光(Glow)で縁取って、
/// 下敷きの位置と太さを数フレームごとにランダムに揺らしてバチバチさせる。
/// 文字ごとにマテリアルの複製を作るので、大きな見出しだけに付ける(小さな文字に全部付けると重くなる)。
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public sealed class ZapTextEffect : MonoBehaviour
{
    static readonly int UNDERLAY_COLOR = Shader.PropertyToID("_UnderlayColor");
    static readonly int UNDERLAY_OFFSET_X = Shader.PropertyToID("_UnderlayOffsetX");
    static readonly int UNDERLAY_OFFSET_Y = Shader.PropertyToID("_UnderlayOffsetY");
    static readonly int UNDERLAY_DILATE = Shader.PropertyToID("_UnderlayDilate");
    static readonly int UNDERLAY_SOFTNESS = Shader.PropertyToID("_UnderlaySoftness");
    static readonly int GLOW_COLOR = Shader.PropertyToID("_GlowColor");
    static readonly int GLOW_OUTER = Shader.PropertyToID("_GlowOuter");
    static readonly int GLOW_POWER = Shader.PropertyToID("_GlowPower");

    [SerializeField] Color shadowColor = new Color(0.02f, 0.02f, 0.03f, 0.95f);
    [SerializeField] Color glowColor = new Color(0.91f, 0.71f, 0.29f, 0.6f);
    [Tooltip("影のずれ(右下)。TextMeshPro の値なので -1〜1 くらい")]
    [SerializeField] Vector2 offset = new Vector2(0.6f, -0.6f);
    [Tooltip("揺らす幅")]
    [SerializeField] float jitter = 0.35f;
    [Tooltip("揺らす間隔(秒)。短いほど激しくバチバチする")]
    [SerializeField] Vector2 interval = new Vector2(0.06f, 0.14f);
    [SerializeField] bool animate = true;

    Material material;
    float timer;

    void Start()
    {
        TMP_Text text = GetComponent<TMP_Text>();
        // fontMaterial は呼んだ時点でこの文字専用の複製になる。他の文字に影響を出さないため
        material = text.fontMaterial;
        material.EnableKeyword("UNDERLAY_ON");
        material.EnableKeyword("GLOW_ON");
        material.SetColor(UNDERLAY_COLOR, shadowColor);
        material.SetFloat(UNDERLAY_SOFTNESS, 0.05f);
        material.SetColor(GLOW_COLOR, glowColor);
        material.SetFloat(GLOW_OUTER, 0.25f);
        material.SetFloat(GLOW_POWER, 0.6f);
        Crackle();
    }

    void Update()
    {
        if (!animate || material == null) return;
        // UIなので、時間が止まっている間も動くよう実時間で数える
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = Random.Range(interval.x, interval.y);
        Crackle();
    }

    void Crackle()
    {
        if (material == null) return;
        material.SetFloat(UNDERLAY_OFFSET_X, offset.x + Random.Range(-jitter, jitter));
        material.SetFloat(UNDERLAY_OFFSET_Y, offset.y + Random.Range(-jitter, jitter));
        material.SetFloat(UNDERLAY_DILATE, Random.Range(0.05f, 0.35f));
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
