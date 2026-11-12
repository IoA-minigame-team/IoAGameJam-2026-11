#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class PlayerIntegrationSceneAudit
{
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-sanjoIntegrationOutput");
        string output=i>=0&&i+1<args.Length?args[i+1]:Path.GetTempPath();Directory.CreateDirectory(output);var report=new StringBuilder();
        foreach(var scene in new[]{PlayerIntegrationAssetSetup.TEST_SCENE,PlayerIntegrationAssetSetup.ROUTE_SCENE,"Assets/_Project/Scenes/Main/InGame.unity"})
        {
            EditorSceneManager.OpenScene(scene);Physics.SyncTransforms();report.AppendLine("SCENE "+scene);
            var player=Object.FindFirstObjectByType<PlayerController>();var flow=Object.FindFirstObjectByType<WaveController>();
            if(player==null||flow==null)throw new Exception("Player or WaveSystem missing");
            if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player.gameObject)!=PlayerIntegrationAssetSetup.PLAYER)throw new Exception("Wrong player prefab");
            var collider=Object.FindFirstObjectByType<MeshCollider>();
            if(Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Length!=1||collider.transform.root.name!="GameJamIsland")throw new Exception("Multiple active floors or incorrect island");
            if(!collider.name.StartsWith("DarkMeadow_Walkable_Ground"))throw new Exception("Wrong floor");
            var fences=Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(c=>c.name.StartsWith("FencePanel")).ToArray();
            if(fences.Length!=12||fences.Any(f=>f.gameObject.layer!=2||f.isTrigger))throw new Exception("Fence collider mismatch");
            var stage=collider.transform.root;
            if(stage.GetComponentsInChildren<MeshRenderer>().Any(r=>r.sharedMaterials.Any(m=>m==null)))throw new Exception("Missing stage material");
            var data=new SerializedObject(player);
            report.AppendLine("PLAYER "+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player.gameObject)+" @ "+player.transform.position.ToString("F3"));
            report.AppendLine("Input: "+data.FindProperty("moveAction").objectReferenceValue+" / "+data.FindProperty("plantAction").objectReferenceValue+" enemyMask="+data.FindProperty("enemyMask").intValue);
            report.AppendLine("Floor bounds: "+collider.bounds+" | 12 non-trigger solid fence panels on layer 2");
            report.AppendLine("Wave plan: "+AssetDatabase.GetAssetPath(flow.Plan)+" totalWaves="+flow.TotalWaves);
            foreach(var point in flow.Spawner.Points)
            {
                report.AppendLine("SPAWN "+point.name+" active="+point.gameObject.activeSelf+" "+point.Areas+" radius="+point.Radius+" @ "+point.transform.position.ToString("F3"));
                if(point.gameObject.activeSelf&&!PlayerIntegrationAssetSetup.Ground(collider,point.transform.position,out _))throw new Exception("Spawn without floor");
            }
            report.AppendLine();
        }
        foreach(var name in new[]{"Shadow","Shield","Werewolf"})
        {
            string path="Assets/_Project/Prefabs/Floating"+name+"Enemy.prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab.GetComponent<EnemyAutoAttack>()==null)throw new Exception("Missing automatic attack: "+name);
            var tuning=prefab.GetComponent<Enemy>().Tuning;
            report.AppendLine(path+" role="+tuning.BehaviourRole+" tuning="+AssetDatabase.GetAssetPath(tuning)+" speed="+tuning.MoveSpeed+" attackRange="+tuning.AttackRange+" windup="+tuning.AttackWindup+" lunge="+tuning.AttackLungeDuration+"*"+tuning.AttackLungeSpeed+" cooldown="+tuning.AttackCooldown);
        }
        File.WriteAllText(Path.Combine(output,"scene-settings.txt"),report.ToString());
        EditorSceneManager.OpenScene(PlayerIntegrationAssetSetup.ROUTE_SCENE);Debug.Log("PLAYER_INTEGRATION_SCENE_AUDIT_PASS");
    }
}
#endif
