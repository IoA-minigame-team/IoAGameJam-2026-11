using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 見下ろしのカメラ(CinemachineCamera)に主人公を結びつける。Follow に主人公を入れ、主人公の移動の基準をこのカメラにする。
/// カメラ一式を Prefab(GameCameraRig)にまとめ、シーンに置くだけで動くようにするため。主人公の Prefab にシーンのカメラを持たせない向きは SlashCinematic と同じ。
/// 移動の基準を Main Camera ではなくこのカメラにするのは、斬り抜け演出のカメラやブレンドの間もスティックの向きと画面の向きをそろえるため。
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public sealed class PlayerCameraBinder : MonoBehaviour
{
    [Tooltip("空なら GameContext.PlayerTransform から探す")]
    [SerializeField] PlayerController player;

    CinemachineCamera playerCamera;
    bool isBound;

    void Awake()
    {
        playerCamera = GetComponent<CinemachineCamera>();
    }

    void OnEnable()
    {
        Bind();
    }

    // 主人公の OnEnable がこちらより後だと、OnEnable の時点では GameContext.PlayerTransform が空なので、ここでもう一度探す
    void Start()
    {
        // 揺れを実時間で動かす。ヒットストップで止まっている間も揺れて、止まった画に当たった手応えが出るようにするため。
        // Cinemachine はシーンを読み込んだ後(Awake より後)に Impulse の設定を初期化するので、Start で入れる
        CinemachineImpulseManager.Instance.IgnoreTimeScale = true;

        Bind();
        if (!isBound)
        {
            Debug.LogWarning("PlayerCameraBinder: 主人公が見つかりません。Player に割り当てるか、主人公をシーンに置いてください", this);
        }
    }

    void OnDisable()
    {
        isBound = false;
    }

    void Bind()
    {
        if (isBound) return;
        if (player == null && GameContext.PlayerTransform != null)
        {
            player = GameContext.PlayerTransform.GetComponent<PlayerController>();
        }
        if (player == null) return;

        // シーンで別の追従先を入れてあれば、それを優先する
        if (playerCamera.Follow == null) playerCamera.Follow = player.transform;
        player.SetMoveCamera(transform);
        isBound = true;
    }
}
