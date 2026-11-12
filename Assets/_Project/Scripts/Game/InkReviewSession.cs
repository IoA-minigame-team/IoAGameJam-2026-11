using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>三種確認とゲーム側の再挑戦を担当する。タイトルの移動先は画面担当へ要求だけ渡す。</summary>
[DefaultExecutionOrder(-400)]
public sealed class InkReviewSession : MonoBehaviour
{
    [SerializeField] WaveController flow;
    [SerializeField] bool threeEnemyReview;
    [SerializeField] GameObject playerRoot;
    [SerializeField] Behaviour contactDamageReceiver;
    [SerializeField] MeshCollider ground;
    static bool requestedGuard;
    static int requestedWave=1;
    int startingWave=1;
    public WaveController Flow=>flow;
    public bool IsReview=>threeEnemyReview;
    public bool GuardPractice { get; private set; }
    public bool DamageEnabled=>contactDamageReceiver!=null&&contactDamageReceiver.enabled;
    public int StartingWave=>startingWave;
    void Awake()
    {
        GuardPractice=requestedGuard&&threeEnemyReview;requestedGuard=false;
        startingWave=Mathf.Clamp(requestedWave,1,flow.TotalWaves);requestedWave=1;
        if(threeEnemyReview&&contactDamageReceiver!=null)contactDamageReceiver.enabled=false;
        if(GuardPractice)
        {
            PlacePlayer(new Vector3(-6,0,0));
            Vector3[] positions={new Vector3(2,0,0),new Vector3(-1,0,0),new Vector3(0,0,-3.8f)};
            for(int i=0;i<3;i++)Place(flow.Spawner.Points[i].transform,positions[i]);
        }
    }
    void Start()=>flow.StartRunAtWave(startingWave);
    public void ToggleDamage(){if(threeEnemyReview&&contactDamageReceiver!=null)contactDamageReceiver.enabled=!contactDamageReceiver.enabled;}
    public void GuardLayout()=>ReloadReview(true);
    public void ResetLayout()=>ReloadReview(false);
    public void ReplayAll()=>ReloadCurrent(false,1);
    public void RetryWave()=>ReloadCurrent(GuardPractice,Mathf.Max(1,flow.CurrentWaveNumber));
    public void RequestTitle()=>GameScreenEvents.RaiseTitleRequested();
    void ReloadReview(bool guard)
    {
        if(!threeEnemyReview)return;ReloadCurrent(guard,1);
    }
    void ReloadCurrent(bool guard,int wave)
    {
        requestedGuard=guard;requestedWave=wave;
        // 再挑戦は主人公ごと作り直す。タイトル画面の構築やロードは担当者のTitleRequested受信側が所有する。
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(SceneManager.GetActiveScene().path,new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
#endif
    }
    void Place(Transform target,Vector3 position)
    {
        Physics.SyncTransforms();if(ground.Raycast(new Ray(position+Vector3.up*30,Vector3.down),out var hit,70))position=hit.point;
        target.position=position;
    }
    void PlacePlayer(Vector3 position)
    {
        var controller=playerRoot.GetComponent<CharacterController>();bool enabled=controller!=null&&controller.enabled;if(controller!=null)controller.enabled=false;
        Place(playerRoot.transform,position);if(controller!=null){playerRoot.transform.position+=Vector3.up*(controller.height*.5f-controller.center.y+controller.skinWidth);controller.enabled=enabled;}
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRequests(){requestedGuard=false;requestedWave=1;}
}
