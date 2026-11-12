using System.Collections.Generic;
using UnityEngine;

/// <summary>実プレイヤー確認シーンの観測だけを行う。PlayerDamagedやEnemyFellを代わりに発行しない。</summary>
public sealed class PlayerIntegrationHUD : MonoBehaviour
{
    [SerializeField] WaveController flow;
    readonly Queue<string> recent=new Queue<string>();
    int damageMessages,hits,falls,started,cleared,lastRemaining=-1,attackCount;
    public WaveController Flow=>flow;
    void OnEnable()
    {
        GameEvents.SwordPlanted+=OnSword;GameEvents.SlashStarted+=OnSlash;GameEvents.EnemyHit+=OnHit;GameEvents.PlayerDamaged+=OnDamage;
        GameEvents.EnemyFell+=OnFell;GameEvents.WaveStarted+=OnStarted;GameEvents.WaveCleared+=OnCleared;GameEvents.GameOver+=OnGameOver;
        EnemyMotionEvents.AttackStarted+=OnAttack;
    }
    void OnDisable()
    {
        GameEvents.SwordPlanted-=OnSword;GameEvents.SlashStarted-=OnSlash;GameEvents.EnemyHit-=OnHit;GameEvents.PlayerDamaged-=OnDamage;
        GameEvents.EnemyFell-=OnFell;GameEvents.WaveStarted-=OnStarted;GameEvents.WaveCleared-=OnCleared;GameEvents.GameOver-=OnGameOver;
        EnemyMotionEvents.AttackStarted-=OnAttack;
    }
    void Log(string message){recent.Enqueue(message);while(recent.Count>7)recent.Dequeue();}
    void OnSword(Vector3 position)=>Log("SwordPlanted "+position.ToString("F1"));
    void OnSlash(SlashStartedInfo info)=>Log("SlashStarted id="+info.SlashId);
    void OnHit(EnemyHitInfo info){hits++;Log("EnemyHit id="+info.SlashId);}
    void OnDamage(int remaining){damageMessages++;lastRemaining=remaining;Log("PlayerDamaged remaining="+remaining);}
    void OnFell(EnemyFellInfo info){falls++;Log("EnemyFell id="+info.SlashId);}
    void OnStarted(int wave){started++;Log("WaveStarted "+wave);}
    void OnCleared(int wave){cleared++;Log("WaveCleared "+wave);}
    void OnAttack(ILaunchable enemy){attackCount++;Log("Enemy attack motion");}
    void OnGameOver()=>Log("GameOver");
    void OnGUI()
    {
        GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1200f,Screen.height/700f,1));
        var text=new GUIStyle(GUI.skin.label){fontSize=18};
        GUI.Label(new Rect(20,10,1160,30),"REAL PLAYER INTEGRATION  |  WASD / left stick: move   |   hold LMB / gamepad South: plant",text);
        GUI.Label(new Rect(20,40,1160,30),"Release to slash back. Wave "+flow.CurrentWaveNumber+"/"+flow.TotalWaves+" "+flow.State+" pending="+flow.PendingCount+" alive="+flow.Spawner.AliveCount,text);
        GUI.Label(new Rect(20,70,1160,30),"Attack motions="+attackCount+"  Damage events="+damageMessages+"  Remaining="+lastRemaining+"  Hits="+hits+"  Falls="+falls+"  Wave events="+started+"/"+cleared,text);
        int row=0;foreach(string entry in recent)GUI.Label(new Rect(20,470+row++*25,900,25),entry,text);
        GUI.Label(new Rect(20,665,1160,25),"Real PlayerController + PlayerHealth. Stop and Play again to reset all player / enemy state.",text);
    }
}

