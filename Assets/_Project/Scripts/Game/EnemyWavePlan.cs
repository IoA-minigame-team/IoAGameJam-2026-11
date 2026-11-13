using System;
using UnityEngine;

/// <summary>B担当の出現データ。Prefab、数、開始時刻、間隔、出現区域をデータ側へ分ける。</summary>
[CreateAssetMenu(fileName = "EnemyWavePlan", menuName = "Hitofuri/EnemyWavePlan")]
public sealed class EnemyWavePlan : ScriptableObject
{
    [Serializable]
    public sealed class Batch
    {
        [SerializeField] GameObject prefab;
        [SerializeField, Min(1)] int count = 1;
        [SerializeField] EnemySpawnArea areas = EnemySpawnArea.AnyEdge;
        [SerializeField, Min(0f)] float delay;
        [SerializeField, Min(.01f)] float interval = .7f;
        [Tooltip("空なら上のPrefabを使用。複数登録すると1体ごとに等確率で選ぶ")]
        [SerializeField] GameObject[] randomPrefabs;
        public GameObject[] RandomPrefabs => randomPrefabs;
        public GameObject PickPrefab() => randomPrefabs != null && randomPrefabs.Length > 0 ? randomPrefabs[UnityEngine.Random.Range(0, randomPrefabs.Length)] : prefab;
        public GameObject Prefab => prefab;
        public int Count => Mathf.Max(1, count);
        public EnemySpawnArea Areas => areas;
        public float Delay => Mathf.Max(0, delay);
        public float Interval => Mathf.Max(.01f, interval);
    }
    [Serializable]
    public sealed class Wave
    {
        [SerializeField] string label;
        [SerializeField] Batch[] batches;
        public string Label => label;
        public Batch[] Batches => batches;
    }
    [SerializeField] Wave[] waves;
    [Header("進行。値は調整用の初期値")]
    [SerializeField, Min(0f)] float initialDelay = .6f;
    [SerializeField, Min(0f)] float interWaveDelay = 2f;
    [SerializeField, Min(1)] int maxSpawnsPerFrame = 2;
    [Header("安全な出現位置")]
    [SerializeField, Min(0f)] float minimumEnemyDistance = 1.6f;
    [SerializeField, Min(0f)] float minimumPlayerDistance = 3f;
    [SerializeField, Min(0f)] float groundMargin = .6f;
    [SerializeField, Min(.01f)] float probeHeight = 3f;
    [SerializeField, Min(.01f)] float probeDepth = 6f;
    [SerializeField, Min(1)] int candidateAttempts = 13;
    [SerializeField, Min(.01f)] float retryInterval = .25f;
    [SerializeField, Min(.1f)] float blockedTimeout = 10f;
    [Header("果てなく(エンドレス)。最後の波の後、この波から最後までを繰り返す")]
    [Tooltip("繰り返しの始まりの波(1始まり)。4なら第4、5波を交互に出す")]
    [SerializeField, Min(1)] int endlessLoopFrom = 4;
    [Tooltip("1周ごとに出現数へ足す割合。0.25で1周目は1.25倍、2周目は1.5倍")]
    [SerializeField, Min(0f)] float endlessCountGrowth = .25f;
    [Tooltip("1周ごとに出現間隔へ掛ける倍率。小さいほど速く湧く")]
    [SerializeField, Range(.1f, 1f)] float endlessIntervalScale = .9f;
    [SerializeField, Min(.01f)] float endlessMinInterval = .25f;
    [Tooltip("1つの出現グループの上限。混みすぎて出現位置が塞がれ続けると進行が止まるのと、WebGLの負荷を抑えるため")]
    [SerializeField, Min(1)] int endlessMaxCount = 8;
    public Wave[] Waves => waves;
    public float InitialDelay => Mathf.Max(0, initialDelay);
    public float InterWaveDelay => Mathf.Max(0, interWaveDelay);
    public int MaxSpawnsPerFrame => Mathf.Max(1, maxSpawnsPerFrame);
    public float MinimumEnemyDistance => Mathf.Max(0, minimumEnemyDistance);
    public float MinimumPlayerDistance => Mathf.Max(0, minimumPlayerDistance);
    public float GroundMargin => Mathf.Max(0, groundMargin);
    public float ProbeHeight => Mathf.Max(.01f, probeHeight);
    public float ProbeDepth => Mathf.Max(.01f, probeDepth);
    public int CandidateAttempts => Mathf.Max(1, candidateAttempts);
    public float RetryInterval => Mathf.Max(.01f, retryInterval);
    public float BlockedTimeout => Mathf.Max(.1f, blockedTimeout);
    public int EndlessLoopFrom => Mathf.Max(1, endlessLoopFrom);
    public float EndlessCountGrowth => Mathf.Max(0, endlessCountGrowth);
    public float EndlessIntervalScale => Mathf.Clamp(endlessIntervalScale, .1f, 1f);
    public float EndlessMinInterval => Mathf.Max(.01f, endlessMinInterval);
    public int EndlessMaxCount => Mathf.Max(1, endlessMaxCount);
}
