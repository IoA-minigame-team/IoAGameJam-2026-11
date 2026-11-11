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
}
