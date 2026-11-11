#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class PlayerIntegrationInventory
{
    const string ROOT="Assets/_Project/";
    public static void Build()
    {
        EditorSceneManager.OpenScene(ROOT+"Scenes/Dev/Kapi/PlayerTest.unity");
        var player=UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if(player==null)throw new Exception("Source PlayerController missing");
        var copy=UnityEngine.Object.Instantiate(player.gameObject);copy.name="PlayerIntegrationActor";
        PrefabUtility.SaveAsPrefabAsset(copy,ROOT+"Prefabs/PlayerIntegrationActor.prefab");
        var cc=player.GetComponent<CharacterController>();
        var source=new SerializedObject(player);
        string text="Source scene: PlayerTest\nPlayer root: "+player.name+"\nComponents: "+string.Join(",",player.GetComponents<Component>().Select(c=>c.GetType().FullName))+"\nCC radius="+cc.radius+" height="+cc.height+" centre="+cc.center;
        foreach(var name in new[]{"tuning","moveAction","plantAction","sword","cameraTransform"})text+="\n"+name+"="+source.FindProperty(name).objectReferenceValue;
        var stage=AssetDatabase.LoadAssetAtPath<GameObject>(ROOT+"Prefabs/FloatingMeadowStage.prefab");
        var ground=stage.GetComponentInChildren<MeshCollider>();text+="\nIsland local mesh bounds="+ground.sharedMesh.bounds;
        File.WriteAllText("C:/Users/sanjo/Documents/Codex/2026-11-08/new-chat/work/player-integration/inventory.txt",text);
        AssetDatabase.SaveAssets();Debug.Log("PLAYER_INTEGRATION_INVENTORY_PASS");
    }
}
#endif
