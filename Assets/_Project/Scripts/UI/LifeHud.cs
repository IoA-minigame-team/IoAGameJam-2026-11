using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 被弾の残り回数を、左上に金の菱形の印で並べて見せる(髙橋さんの了承済み。仕様の「体力ゲージは出さない」の代わりに、ゲージではなく小さな印にした)。
/// 残り回数は髙橋さんの PlayerHealth が流す PlayerDamaged(残り) だけから読む。PlayerHealth には直接触らない。
/// 最初の PlayerDamaged が届くまでは FeedbackTuning の lifeSlots の数だけ並べておく。
/// 動きは実時間で回す。被弾の瞬間はヒットストップで時間が止まることがあるため。
/// </summary>
public sealed class LifeHud : MonoBehaviour
{
    // 割れた印が広がる大きさ(元の何倍か)
    const float BURST_SCALE = 2.2f;
    // 残りが1つのとき、脈打つ大きさの幅
    const float LAST_PULSE_SCALE = 0.12f;
    // 列が揺れる時間(秒)
    const float SHAKE_TIME = 0.3f;

    static readonly Color GOLD = new Color(0.914f, 0.71f, 0.29f, 1f);
    static readonly Color GOLD_HI = new Color(1f, 0.886f, 0.627f, 1f);
    static readonly Color EMPTY = new Color(1f, 1f, 1f, 0.16f);
    static readonly Color DANGER = new Color(0.93f, 0.33f, 0.24f, 1f);

    [SerializeField] FeedbackTuning tuning;
    [Tooltip("印を並べる場所。子は実行時に作る")]
    [SerializeField] RectTransform row;

    sealed class Slot
    {
        public RectTransform Root;
        public Image Frame;
        public Image Fill;
        public Image Burst;
        public bool Filled;
        public float BurstTime = -1f;
    }

    readonly List<Slot> slots = new List<Slot>();
    int remaining;
    float shakeTime = -1f;
    Vector2 rowHome;

    void Awake()
    {
        if (row == null) row = (RectTransform)transform;
        rowHome = row.anchoredPosition;
        int count = tuning != null ? tuning.LifeSlots : 3;
        SetCount(count, count);
    }

    void OnEnable() => GameEvents.PlayerDamaged += OnPlayerDamaged;

    void OnDisable() => GameEvents.PlayerDamaged -= OnPlayerDamaged;

    void OnPlayerDamaged(int left)
    {
        left = Mathf.Max(0, left);
        // 上限が分からないので、今ある印より多く届いたら印を足す(最初の通知や、銘で上限が増えたとき)
        int total = Mathf.Max(slots.Count, left);
        bool lost = left < remaining;
        SetCount(total, left);
        if (lost) shakeTime = 0f;
    }

    /// <summary>印を total 個に合わせ、先頭から left 個を埋める。減った印は割れて消える</summary>
    void SetCount(int total, int left)
    {
        while (slots.Count < total) slots.Add(CreateSlot(slots.Count));
        for (int i = 0; i < slots.Count; i++)
        {
            Slot s = slots[i];
            s.Root.gameObject.SetActive(i < total);
            bool filled = i < left;
            if (s.Filled && !filled) s.BurstTime = 0f;
            s.Filled = filled;
            s.Fill.enabled = filled;
        }
        remaining = left;
    }

    Slot CreateSlot(int index)
    {
        float size = tuning != null ? tuning.LifeIconSize : 30f;
        float spacing = tuning != null ? tuning.LifeIconSpacing : 18f;
        Slot s = new Slot();
        s.Root = NewRect("Life" + index, row);
        s.Root.anchorMin = s.Root.anchorMax = new Vector2(0f, 0.5f);
        s.Root.pivot = new Vector2(0.5f, 0.5f);
        s.Root.sizeDelta = new Vector2(size, size);
        s.Root.anchoredPosition = new Vector2(size * 0.5f + index * (size + spacing), 0f);
        // 菱形にするため、四角を45度回す(画像を用意しなくて済む)
        s.Root.localRotation = Quaternion.Euler(0f, 0f, 45f);
        s.Frame = NewImage("Frame", s.Root, EMPTY, 1f);
        s.Fill = NewImage("Fill", s.Root, GOLD, 0.72f);
        s.Burst = NewImage("Burst", s.Root, GOLD_HI, 0.72f);
        s.Burst.enabled = false;
        s.Filled = true;
        return s;
    }

    void Update()
    {
        if (tuning == null) return;
        float dt = Time.unscaledDeltaTime;
        bool last = remaining == 1;
        float pulse = last ? Mathf.Sin(Time.unscaledTime * tuning.LifeLastPulse * Mathf.PI * 2f) * 0.5f + 0.5f : 0f;

        foreach (Slot s in slots)
        {
            // 残り1つは赤みを帯びて脈打ち、あと1回で終わると分かるようにする
            if (s.Filled)
            {
                s.Fill.color = last ? Color.Lerp(GOLD, DANGER, 0.35f + 0.4f * pulse) : GOLD;
                s.Fill.rectTransform.localScale = Vector3.one * (1f + (last ? LAST_PULSE_SCALE * pulse : 0f));
            }
            UpdateBurst(s, dt);
        }
        UpdateShake(dt);
    }

    /// <summary>割れた印:白く光って広がりながら消える</summary>
    void UpdateBurst(Slot s, float dt)
    {
        if (s.BurstTime < 0f) return;
        s.BurstTime += dt;
        float k = Mathf.Clamp01(s.BurstTime / tuning.LifeLoseTime);
        s.Burst.enabled = k < 1f;
        s.Burst.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, BURST_SCALE, 1f - (1f - k) * (1f - k));
        Color c = Color.Lerp(Color.white, DANGER, k);
        c.a = 1f - k;
        s.Burst.color = c;
        if (k >= 1f) s.BurstTime = -1f;
    }

    void UpdateShake(float dt)
    {
        if (shakeTime < 0f) return;
        shakeTime += dt;
        float k = shakeTime / SHAKE_TIME;
        if (k >= 1f)
        {
            shakeTime = -1f;
            row.anchoredPosition = rowHome;
            return;
        }
        float amount = tuning.LifeShake * (1f - k);
        row.anchoredPosition = rowHome + new Vector2(Mathf.Sin(shakeTime * 70f) * amount, 0f);
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image NewImage(string name, Transform parent, Color color, float ratio)
    {
        RectTransform rt = NewRect(name, parent);
        rt.anchorMin = new Vector2(0.5f - ratio * 0.5f, 0.5f - ratio * 0.5f);
        rt.anchorMax = new Vector2(0.5f + ratio * 0.5f, 0.5f + ratio * 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}
