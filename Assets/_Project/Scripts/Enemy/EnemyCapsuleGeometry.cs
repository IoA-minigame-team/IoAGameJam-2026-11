using UnityEngine;

/// <summary>移動時と出現前の検査で、同じCollider定義から同じ形を作る。シーンを変更しない計算だけを担当する。</summary>
public static class EnemyCapsuleGeometry
{
    public static void GetWorld(CapsuleCollider body, out Vector3 bottom, out Vector3 top, out float radius)
    {
        GetShape(body, body.transform.localToWorldMatrix, body.transform.rotation, out bottom, out top, out radius);
    }

    public static void GetAtSpawn(CapsuleCollider body, Transform prefabRoot, Vector3 position, Quaternion rotation, Transform parent,
        out Vector3 bottom, out Vector3 top, out float radius)
    {
        // Instantiate(prefab, position, rotation, parent)と同じ親・Rootローカルスケールで出現後の形を予測する。
        Matrix4x4 rootMatrix = parent == null
            ? Matrix4x4.TRS(position, rotation, prefabRoot.localScale)
            : parent.localToWorldMatrix * Matrix4x4.TRS(parent.InverseTransformPoint(position), Quaternion.Inverse(parent.rotation) * rotation, prefabRoot.localScale);
        Matrix4x4 relativeBody = prefabRoot.worldToLocalMatrix * body.transform.localToWorldMatrix;
        Quaternion bodyRotation = rotation * Quaternion.Inverse(prefabRoot.rotation) * body.transform.rotation;
        GetShape(body, rootMatrix * relativeBody, bodyRotation, out bottom, out top, out radius);
    }

    static void GetShape(CapsuleCollider body, Matrix4x4 matrix, Quaternion rotation,
        out Vector3 bottom, out Vector3 top, out float radius)
    {
        // 無効なColliderのboundsは空になるため、吹き飛び中にも読める定義値から作る。
        Vector3 scale = matrix.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Vector3 axis;
        float heightScale, radiusScale;
        switch (body.direction)
        {
            case 0: axis = Vector3.right; heightScale = scale.x; radiusScale = Mathf.Max(scale.y, scale.z); break;
            case 2: axis = Vector3.forward; heightScale = scale.z; radiusScale = Mathf.Max(scale.x, scale.y); break;
            default: axis = Vector3.up; heightScale = scale.y; radiusScale = Mathf.Max(scale.x, scale.z); break;
        }
        radius = body.radius * radiusScale;
        float halfSegment = Mathf.Max(0, body.height * heightScale * .5f - radius);
        Vector3 center = matrix.MultiplyPoint3x4(body.center);
        Vector3 offset = rotation * axis * halfSegment;
        bottom = center - offset;
        top = center + offset;
    }
}
