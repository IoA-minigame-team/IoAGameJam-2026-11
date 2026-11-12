using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 斬り抜けの間、体の形を一定の距離ごとに写して金の残像として短く残す(演出「斬り抜ける: 残像」)。
/// 0.2 秒で走り抜ける動きを、通った道筋ごと目に残すため。PlayerController の状態を読むだけで、操作には関わらない。
/// 刀は軌跡(SwordTrailView)があるので写さない。
/// </summary>
// PlayerController が動かした後の位置で写すため、後から動かす
[DefaultExecutionOrder(100)]
public sealed class SlashAfterimage : MonoBehaviour
{
    // URP の Unlit と Lit の色のプロパティ
    static readonly int baseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] PlayerTuning tuning;
    [SerializeField] PlayerController controller;
    [Tooltip("写す体。刀は含めない")]
    [SerializeField] SkinnedMeshRenderer body;
    [Tooltip("残像のマテリアル。加算の半透明にして、_BaseColor を薄めて消していく")]
    [SerializeField] Material afterimageMaterial;

    sealed class Afterimage
    {
        public GameObject GameObject;
        public MeshRenderer Renderer;
        public Mesh Mesh;
        public float Age;
    }

    Afterimage[] pool;
    int nextIndex;
    Transform poolRoot;
    MaterialPropertyBlock properties;
    Color baseColor = Color.white;

    bool wasSlashing;
    bool wasReturnSlashing;
    Vector3 lastSpawnPosition;

    void Awake()
    {
        if (controller == null) controller = GetComponent<PlayerController>();
        if (tuning == null || body == null || afterimageMaterial == null) return;

        properties = new MaterialPropertyBlock();
        if (afterimageMaterial.HasProperty(baseColorId)) baseColor = afterimageMaterial.GetColor(baseColorId);

        // 主人公の子にすると一緒に動いてしまうので、シーンの直下に置く。生成と破棄を斬り抜けごとに繰り返さないよう、先に作って使い回す
        poolRoot = new GameObject(name + "_Afterimages").transform;
        pool = new Afterimage[tuning.AfterimageMaxCount];
        for (int i = 0; i < pool.Length; i++)
        {
            GameObject go = new GameObject("Afterimage");
            go.transform.SetParent(poolRoot, false);
            Mesh mesh = new Mesh { name = "Afterimage" };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = afterimageMaterial;
            // 光の形なので影は落とさない
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            go.SetActive(false);
            pool[i] = new Afterimage { GameObject = go, Renderer = meshRenderer, Mesh = mesh };
        }
    }

    void OnDisable()
    {
        HideAll();
        wasSlashing = false;
        wasReturnSlashing = false;
    }

    void OnDestroy()
    {
        if (pool != null)
        {
            foreach (Afterimage image in pool) Destroy(image.Mesh);
        }
        if (poolRoot != null) Destroy(poolRoot.gameObject);
    }

    void Update()
    {
        if (pool == null || controller == null) return;

        bool isSlashing = controller.CurrentState == PlayerController.State.Slashing;
        bool isReturnSlashing = controller.IsReturnSlashing;
        Vector3 position = transform.position;

        if (isSlashing)
        {
            // 斬り抜けの始めと返しの始めは、刀の位置へ瞬間移動した直後なので、その場で1つ写して数え直す
            bool started = !wasSlashing || (isReturnSlashing && !wasReturnSlashing);
            if (started || (position - lastSpawnPosition).sqrMagnitude >= tuning.AfterimageSpacing * tuning.AfterimageSpacing)
            {
                Spawn();
                lastSpawnPosition = position;
            }
        }
        wasSlashing = isSlashing;
        wasReturnSlashing = isReturnSlashing;

        Fade(Time.deltaTime);
    }

    void Spawn()
    {
        Afterimage image = pool[nextIndex];
        nextIndex = (nextIndex + 1) % pool.Length;

        // 体の Transform の拡大も含めて焼くので、写した側の拡大は 1 のままにする
        body.BakeMesh(image.Mesh, true);
        Transform source = body.transform;
        image.GameObject.transform.SetPositionAndRotation(source.position, source.rotation);
        image.Age = 0f;
        image.GameObject.SetActive(true);
        ApplyColor(image, 1f);
    }

    // scaled time で数えるので、ヒットストップ中は残像も止まったまま残る
    void Fade(float dt)
    {
        float lifetime = tuning.AfterimageLifetime;
        foreach (Afterimage image in pool)
        {
            if (!image.GameObject.activeSelf) continue;
            image.Age += dt;
            if (image.Age >= lifetime)
            {
                image.GameObject.SetActive(false);
                continue;
            }
            ApplyColor(image, 1f - image.Age / lifetime);
        }
    }

    // 加算で描くので、色ごと暗くすると消えていく。アルファも合わせて下げ、半透明の設定でも同じように消えるようにする
    void ApplyColor(Afterimage image, float strength)
    {
        Color color = baseColor * strength;
        color.a = baseColor.a * strength;
        properties.SetColor(baseColorId, color);
        image.Renderer.SetPropertyBlock(properties);
    }

    void HideAll()
    {
        if (pool == null) return;
        foreach (Afterimage image in pool)
        {
            if (image.GameObject != null) image.GameObject.SetActive(false);
        }
    }
}
