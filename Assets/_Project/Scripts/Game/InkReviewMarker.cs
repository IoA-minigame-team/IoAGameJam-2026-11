using UnityEngine;

/// <summary>確認用の名前札。ゲーム中の敵Prefab自体には付けない。</summary>
public sealed class InkReviewMarker : MonoBehaviour
{
    Transform cameraTransform;
    void Awake(){if(Camera.main!=null)cameraTransform=Camera.main.transform;}
    void LateUpdate(){if(cameraTransform==null)return;transform.position=transform.parent.position+Vector3.up*3.5f;transform.rotation=cameraTransform.rotation;}
}
