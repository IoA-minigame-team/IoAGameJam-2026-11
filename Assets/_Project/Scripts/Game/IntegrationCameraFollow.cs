using UnityEngine;

/// <summary>共有の主人公位置を読むだけの確認用カメラ。主人公の操作や状態は変更しない。</summary>
public sealed class IntegrationCameraFollow : MonoBehaviour
{
    [SerializeField] Vector3 offset = new Vector3(0,18,12);
    void LateUpdate()
    {
        var player=GameContext.PlayerTransform;
        if(player==null)return;
        transform.position=player.position+offset;
        transform.rotation=Quaternion.LookRotation(-offset,Vector3.up);
    }
}
