using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 刀の光のテスト用(Dev シーン専用、本番シーンには置かない)。
/// 主人公と敵が無くても、刀を置く、群がる、斬り戻す、を流して、光の輪、揺らぎ、残り時間の輪を確かめるため。
/// </summary>
public sealed class SwordLightTestDriver : MonoBehaviour
{
    [Tooltip("G キーで増やす群がりの数の上限。超えたら 0 に戻る")]
    [SerializeField, Min(1)] int maxSwarm = 6;
    [SerializeField] bool showHelp = true;

    bool planted;
    int swarm;
    int slashId = 5000;

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (k == null) return;

        // P:置く。もう一度押すと斬り戻す(置いた所から自分の所まで)
        if (k.pKey.wasPressedThisFrame)
        {
            if (!planted)
            {
                planted = true;
                GameEvents.RaiseSwordPlanted(transform.position);
                Feedback.PlaySe(SeId.SwordPlant, transform.position);
                Feedback.SpawnVfx(VfxId.LightRing, transform.position, Quaternion.identity);
            }
            else
            {
                planted = false;
                slashId++;
                GameEvents.RaiseSlashStarted(new SlashStartedInfo(slashId, transform.position, transform.position + Vector3.forward * 4f));
                SetSwarm(0);
            }
        }
        // G:刀に群がる灯喰らいを1体増やす(上限を超えたら 0 に戻る)
        if (k.gKey.wasPressedThisFrame) SetSwarm(swarm >= maxSwarm ? 0 : swarm + 1);
    }

    void SetSwarm(int count)
    {
        swarm = count;
        GameEvents.RaiseSwarmCountChanged(swarm);
    }

    void OnGUI()
    {
        if (!showHelp) return;
        GUI.Box(new Rect(Screen.width - 330, 10, 320, 90),
            "刀の光のテスト(キーボード)\n" +
            "P: 刀を置く / もう一度で斬り戻す\n" +
            "G: 群がりを1体増やす\n" +
            $"刀 {(planted ? "置いている" : "持っている")}  群がり {swarm}体");
    }
}
