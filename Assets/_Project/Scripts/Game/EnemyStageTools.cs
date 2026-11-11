#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>既存のEnemyWavePlanとシーンの出現点を日本語で管理する。調整値を別の場所へ複製しない。</summary>
public sealed class EnemyStageTools : EditorWindow
{
    EnemyWavePlan plan;WaveController flow;SerializedObject planData;
    Vector2 scroll;int tab,wave,sceneHandle;bool general;
    readonly List<string> messages=new List<string>();
    [MenuItem("Hitofuri/Sanjo/ステージ・出現管理")]
    public static void Open(){var window=GetWindow<EnemyStageTools>();window.titleContent=new GUIContent("ステージ・出現管理");window.minSize=new Vector2(720,560);window.RefreshBindings();}
    void OnEnable(){EditorSceneManager.sceneOpened+=OnScene;EditorApplication.playModeStateChanged+=OnMode;RefreshBindings();}
    void OnDisable(){EditorSceneManager.sceneOpened-=OnScene;EditorApplication.playModeStateChanged-=OnMode;}
    void OnScene(Scene scene,OpenSceneMode mode)=>RefreshBindings();
    void OnMode(PlayModeStateChange state)=>RefreshBindings();
    void OnInspectorUpdate(){if(sceneHandle!=SceneManager.GetActiveScene().handle)RefreshBindings();Repaint();}
    void RefreshBindings()
    {
        sceneHandle=SceneManager.GetActiveScene().handle;flow=Object.FindFirstObjectByType<WaveController>();
        if(flow!=null)plan=flow.Plan;planData=plan!=null?new SerializedObject(plan):null;wave=0;messages.Clear();
    }
    void OnGUI()
    {
        EditorGUILayout.LabelField("ステージ・出現管理",EditorStyles.boldLabel);
        EditorGUILayout.LabelField("シーン",SceneManager.GetActiveScene().name);
        using(new EditorGUILayout.HorizontalScope())
        {
            using(new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if(GUILayout.Button("最新主人公を観察"))LatestPlayerHandoffSetup.OpenObservation();
                if(GUILayout.Button("1体の通し確認"))LatestPlayerHandoffSetup.OpenRoute();
            }
            if(GUILayout.Button("再読込"))RefreshBindings();
        }
        tab=GUILayout.Toolbar(tab,new[]{"ウェーブ設定","出現ポイント","接続・引渡し確認"});scroll=EditorGUILayout.BeginScrollView(scroll);
        if(tab==0)DrawWaves();else if(tab==1)DrawPoints();else DrawConnections();
        EditorGUILayout.EndScrollView();
    }
    void DrawWaves()
    {
        var selected=(EnemyWavePlan)EditorGUILayout.ObjectField("編集する出現計画",plan,typeof(EnemyWavePlan),false);
        if(selected!=plan){plan=selected;planData=plan!=null?new SerializedObject(plan):null;wave=0;}
        if(plan==null){EditorGUILayout.HelpBox("WaveControllerのあるシーンを開くか、EnemyWavePlanを選んでください。",MessageType.Info);return;}
        EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(plan),EditorStyles.miniLabel);
        if(flow!=null&&flow.Plan!=plan)
        {
            EditorGUILayout.HelpBox("選択した計画は、まだこのシーンでは使われていません。",MessageType.Warning);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlaying||!LatestPlayerHandoffSetup.OwnsScene(SceneManager.GetActiveScene().path)))
                if(GUILayout.Button("この計画を現在のシーンに設定"))
                {
                    SetPlan(flow,plan);if(flow.Spawner!=null)SetPlan(flow.Spawner,plan);DirtyScene();
                }
        }
        EditorGUILayout.HelpBox("種類・数・開始の遅れ・出現間隔・区域を編集します。Undo対応。保存ボタンでアセットを保存します。",MessageType.Info);
        using(new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            planData.UpdateIfRequiredOrScript();var waves=planData.FindProperty("waves");
            if(waves.arraySize==0){if(GUILayout.Button("最初の波を追加")){waves.arraySize=1;InitializeWave(waves.GetArrayElementAtIndex(0));ApplyPlan();}return;}
            wave=Mathf.Clamp(wave,0,waves.arraySize-1);wave=EditorGUILayout.Popup("編集する波",wave,Enumerable.Range(1,waves.arraySize).Select(i=>"第"+i+"波").ToArray());
            var item=waves.GetArrayElementAtIndex(wave);EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(item.FindPropertyRelative("label"),new GUIContent("この波の説明"));var batches=item.FindPropertyRelative("batches");int total=0;
            int remove=-1;
            for(int i=0;i<batches.arraySize;i++)
            {
                var batch=batches.GetArrayElementAtIndex(i);total+=Mathf.Max(1,batch.FindPropertyRelative("count").intValue);
                using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("出現グループ "+(i+1),EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(batch.FindPropertyRelative("prefab"),new GUIContent("敵のPrefab"));
                    var count=batch.FindPropertyRelative("count");count.intValue=Mathf.Max(1,EditorGUILayout.IntField("出す数",count.intValue));
                    var delay=batch.FindPropertyRelative("delay");delay.floatValue=Mathf.Max(0,EditorGUILayout.FloatField("開始を遅らせる秒数",delay.floatValue));
                    var interval=batch.FindPropertyRelative("interval");interval.floatValue=Mathf.Max(.01f,EditorGUILayout.FloatField("1体ごとの間隔（秒）",interval.floatValue));
                    var areas=batch.FindPropertyRelative("areas");areas.intValue=(int)(EnemySpawnArea)EditorGUILayout.EnumFlagsField("使う出現区域",(EnemySpawnArea)areas.intValue);
                    if(GUILayout.Button("このグループを削除"))remove=i;
                }
            }
            if(EditorGUI.EndChangeCheck())ApplyPlan();EditorGUILayout.LabelField("この波の合計",total+" 体");
            if(remove>=0){batches.DeleteArrayElementAtIndex(remove);ApplyPlan();GUIUtility.ExitGUI();}
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("出現グループを追加")){int i=batches.arraySize;batches.InsertArrayElementAtIndex(i);InitializeBatch(batches.GetArrayElementAtIndex(i));ApplyPlan();}
                if(GUILayout.Button("次の波を追加")){int i=waves.arraySize;waves.InsertArrayElementAtIndex(i);InitializeWave(waves.GetArrayElementAtIndex(i));wave=i;ApplyPlan();}
                if(GUILayout.Button("この波を削除")){waves.DeleteArrayElementAtIndex(wave);ApplyPlan();GUIUtility.ExitGUI();}
            }
            general=EditorGUILayout.Foldout(general,"待ち時間と安全距離",true);
            if(general)
            {
                EditorGUI.BeginChangeCheck();string[] fields={"initialDelay","interWaveDelay","maxSpawnsPerFrame","minimumEnemyDistance","minimumPlayerDistance","groundMargin","retryInterval","blockedTimeout"};
                string[] labels={"最初の開始待ち（秒）","波の間の待ち（秒）","同じ更新で出せる上限","敵同士の最低距離","主人公からの最低距離","床の端からの余白","出現再試行の間隔","出現できないときの制限時間"};
                for(int i=0;i<fields.Length;i++)EditorGUILayout.PropertyField(planData.FindProperty(fields[i]),new GUIContent(labels[i]));
                if(EditorGUI.EndChangeCheck())ApplyPlan();
            }
            if(GUILayout.Button("出現計画を保存")){ApplyPlan();AssetDatabase.SaveAssetIfDirty(plan);}
        }
        if(EditorApplication.isPlaying&&flow!=null)EditorGUILayout.LabelField("実行中",flow.State+"　波 "+flow.CurrentWaveNumber+"　予定 "+flow.PendingCount+"　島上 "+flow.Spawner.AliveCount);
    }
    void ApplyPlan(){planData.ApplyModifiedProperties();EditorUtility.SetDirty(plan);}
    static void SetPlan(Object target,EnemyWavePlan value){var data=new SerializedObject(target);data.FindProperty("plan").objectReferenceValue=value;data.ApplyModifiedProperties();}
    static void InitializeWave(SerializedProperty wave){wave.FindPropertyRelative("label").stringValue="新しい波";var batches=wave.FindPropertyRelative("batches");batches.arraySize=1;InitializeBatch(batches.GetArrayElementAtIndex(0));}
    static void InitializeBatch(SerializedProperty batch)
    {
        batch.FindPropertyRelative("prefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/FloatingShadowEnemy.prefab");
        batch.FindPropertyRelative("count").intValue=1;batch.FindPropertyRelative("areas").intValue=(int)EnemySpawnArea.AnyEdge;batch.FindPropertyRelative("delay").floatValue=0;batch.FindPropertyRelative("interval").floatValue=.7f;
    }
    void DrawPoints()
    {
        if(flow==null||flow.Spawner==null){EditorGUILayout.HelpBox("現在のシーンにWaveControllerとEnemySpawnerがありません。",MessageType.Warning);return;}
        bool editable=!EditorApplication.isPlaying&&LatestPlayerHandoffSetup.OwnsScene(SceneManager.GetActiveScene().path);
        EditorGUILayout.HelpBox("数や時間は出現計画、座標や有効・無効はこのシーンに保存します。チェックを外した点からは出ません。",MessageType.Info);
        using(new EditorGUI.DisabledScope(!editable))
        {
            if(GUILayout.Button("出現ポイントを追加"))
            {
                var node=new GameObject("SpawnPoint_"+(flow.Spawner.Points.Length+1));Undo.RegisterCreatedObjectUndo(node,"出現点を追加");
                Undo.SetTransformParent(node.transform,flow.Spawner.transform,"出現点の親を設定");node.transform.localPosition=Vector3.zero;
                var point=Undo.AddComponent<EnemySpawnPoint>(node);var data=new SerializedObject(flow.Spawner);var points=data.FindProperty("points");
                int index=points.arraySize;points.InsertArrayElementAtIndex(index);points.GetArrayElementAtIndex(index).objectReferenceValue=point;data.ApplyModifiedProperties();Selection.activeGameObject=node;DirtyScene();
            }
            foreach(var point in flow.Spawner.Points)
            {
                if(point==null)continue;
                using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using(new EditorGUILayout.HorizontalScope()){EditorGUILayout.ObjectField(point,typeof(EnemySpawnPoint),true);if(GUILayout.Button("選択",GUILayout.Width(60))){Selection.activeGameObject=point.gameObject;if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.FrameSelected();}}
                    bool active=EditorGUILayout.Toggle("この点を使う",point.gameObject.activeSelf);if(active!=point.gameObject.activeSelf){Undo.RecordObject(point.gameObject,"出現点の有効変更");point.gameObject.SetActive(active);DirtyScene();}
                    var data=new SerializedObject(point);EditorGUI.BeginChangeCheck();EditorGUILayout.PropertyField(data.FindProperty("areas"),new GUIContent("区域"));EditorGUILayout.PropertyField(data.FindProperty("radius"),new GUIContent("周辺に出す半径"));if(EditorGUI.EndChangeCheck()){data.ApplyModifiedProperties();DirtyScene();}
                    Vector3 position=EditorGUILayout.Vector3Field("座標",point.transform.position);if(position!=point.transform.position){Undo.RecordObject(point.transform,"出現点を移動");point.transform.position=position;DirtyScene();}
                    if(GUILayout.Button("真下の床へ高さを合わせる"))
                    {
                        Physics.SyncTransforms();if(Physics.Raycast(point.transform.position+Vector3.up*10,Vector3.down,out var hit,50,1,QueryTriggerInteraction.Ignore)){Undo.RecordObject(point.transform,"出現点を床へ");point.transform.position=hit.point;DirtyScene();}
                    }
                }
            }
            if(GUILayout.Button("出現ポイントのシーンを保存"))EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
        if(!editable)EditorGUILayout.HelpBox("Play中や他担当のシーンでは読み取り専用です。",MessageType.Info);
    }
    void DirtyScene()=>EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    void DrawConnections()
    {
        if(GUILayout.Button("現在の接続と出現データを点検")){messages.Clear();messages.AddRange(Validate(flow));}
        foreach(var message in messages)EditorGUILayout.HelpBox(message,message.StartsWith("NG")?MessageType.Error:message.StartsWith("要確認")?MessageType.Warning:MessageType.Info);
        if(EditorApplication.isPlaying)
        {
            var stats=GameContext.MeiStats;EditorGUILayout.LabelField("銘の供給元",stats.GetType().Name);
            EditorGUILayout.LabelField("威力倍率",stats.LaunchPowerMultiplier.ToString("F3"));EditorGUILayout.LabelField("光蓄積倍率",stats.GlowGainMultiplier.ToString("F3"));EditorGUILayout.LabelField("刀の誘引倍率",stats.LureRadiusMultiplier.ToString("F3"));
            if(stats is DefaultMeiStats)EditorGUILayout.HelpBox("銘は既定の仮値です。本番の銘実装が登録された状態とは区別してください。",MessageType.Warning);
            EditorGUILayout.LabelField("演出サービス",Feedback.Service!=null?Feedback.Service.GetType().Name:"未登録");
        }
        using(new EditorGUI.DisabledScope(EditorApplication.isPlaying||!LatestPlayerHandoffSetup.OwnsScene(SceneManager.GetActiveScene().path)))
            if(GUILayout.Button("このシーンを最新Player.prefabへ接続")){LatestPlayerHandoffSetup.ConnectCurrentScene();RefreshBindings();}
    }
    public static List<string> Validate(WaveController controller)
    {
        var result=new List<string>();var players=Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        result.Add(players.Length==1?"OK 主人公は1体":"NG 主人公の数が "+players.Length);
        if(players.Length==1&&!EditorApplication.isPlaying)result.Add(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(players[0].gameObject)==LatestPlayerHandoffSetup.PLAYER?"OK 最新Player.prefabを直接参照":"要確認 主人公が旧Prefabまたは別Prefabです");
        if(controller==null||controller.Plan==null||controller.Spawner==null){result.Add("NG ウェーブ計画か出現管理が未設定");return result;}
        Physics.SyncTransforms();int good=0;
        foreach(var point in controller.Spawner.Points)if(point!=null&&point.gameObject.activeInHierarchy){if(Physics.Raycast(point.transform.position+Vector3.up*3,Vector3.down,6,1,QueryTriggerInteraction.Ignore))good++;else result.Add("NG 足場がない出現点: "+point.name);}
        result.Add((good>0?"OK":"NG")+" 有効な床上の出現点 "+good);
        var spawnData=new SerializedObject(controller.Spawner);
        if(spawnData.FindProperty("plan").objectReferenceValue!=controller.Plan)result.Add("NG 進行と出現で異なる出現計画が設定されています");
        foreach(var wave in controller.Plan.Waves)foreach(var batch in wave.Batches)
        {
            if(batch.Prefab==null||batch.Prefab.GetComponent<Enemy>()==null)result.Add("NG Enemyを持たない出現Prefab");
            else if(batch.Prefab.GetComponent<Enemy>().Tuning==null)result.Add("NG EnemyTuningが未設定: "+batch.Prefab.name);
            else if(!controller.Spawner.Points.Any(p=>p!=null&&p.gameObject.activeInHierarchy&&(p.Areas&batch.Areas)!=0))result.Add("NG 区域に対応する出現点がありません: "+batch.Prefab.name);
        }
        if(EditorApplication.isPlaying){result.Add(players.Length==1&&GameContext.PlayerTransform==players[0].transform?"OK 主人公の共通参照が登録済み":"NG 主人公の共通参照が不一致");result.Add("銘実装: "+GameContext.MeiStats.GetType().Name);}
        result.Add("波数 "+controller.TotalWaves+"。保存先: "+AssetDatabase.GetAssetPath(controller.Plan));return result;
    }
}
#endif
