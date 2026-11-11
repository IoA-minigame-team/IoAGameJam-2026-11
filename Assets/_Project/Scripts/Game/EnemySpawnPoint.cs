using System;
using UnityEngine;

[Flags]
public enum EnemySpawnArea
{
    Center = 1, North = 2, East = 4, South = 8, West = 16,
    AnyEdge = North | East | South | West,
    Anywhere = Center | AnyEdge
}

/// <summary>Scene上で編集する出現位置。中央と各方角を、ウェーブ設定から選べるようにする。</summary>
public sealed class EnemySpawnPoint : MonoBehaviour
{
    [SerializeField] EnemySpawnArea areas = EnemySpawnArea.AnyEdge;
    [SerializeField, Min(0f)] float radius = 1.5f;
    public EnemySpawnArea Areas => areas;
    public float Radius => Mathf.Max(0, radius);

    void OnDrawGizmos()
    {
        Gizmos.color = (areas & EnemySpawnArea.Center) != 0 ? new Color(.5f, .85f, 1f) : Color.white;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * .15f, .25f);
        Gizmos.DrawRay(transform.position, transform.forward * 1.2f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * .5f, name + " [" + areas + "]");
#endif
    }
    void OnDrawGizmosSelected() { Gizmos.color = Color.gray;Gizmos.DrawWireSphere(transform.position, Radius); }
}
