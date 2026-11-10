#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Sanjo担当素材をUnityのAPIで設定する。YAMLを直接編集しないための制作ツール。</summary>
public static class EnemyActionAssetSetup
{
    const string ROOT = "Assets/_Project/";
    public const string TEST_SCENE = ROOT + "Scenes/Dev/Sanjo/EnemyActionTest.unity";
    [MenuItem("Hitofuri/Sanjo/Open Enemy Action Test")]
    public static void OpenTest()
    {
        EditorSceneManager.OpenScene(TEST_SCENE);
        EditorApplication.ExecuteMenuItem("Window/General/Game");
    }

    [MenuItem("Hitofuri/Sanjo/Rebuild Enemy Action Assets")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        string[] models = { "ShieldActions", "ShadowActions", "WerewolfActions" };
        string[] prefabs = { "FloatingShieldEnemy", "FloatingShadowEnemy", "FloatingWerewolfEnemy" };
        for (int i = 0; i < models.Length; i++)
        {
            var controller = ImportClips(models[i]);
            CreateEnemy(models[i], prefabs[i], controller);
        }
        var system = new GameObject("GameOverSystem");
        system.AddComponent<GameOverController>();
        PrefabUtility.SaveAsPrefabAsset(system, ROOT + "Prefabs/GameOverSystem.prefab");
        Object.DestroyImmediate(system);
        CreateScene(prefabs);
        InstallGameOverSystem();
        AssetDatabase.SaveAssets();
        foreach (var name in prefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/" + name + ".prefab");
            Require(prefab.GetComponentsInChildren<SkinnedMeshRenderer>().Length == 1, name + " skinned renderer");
            Require(prefab.GetComponentsInChildren<Rigidbody>().Length == 0, name + " no rigidbody");
            Require(prefab.GetComponent<EnemyActionAnimation>() != null, name + " animation driver");
            Require(prefab.GetComponent<CapsuleCollider>().isTrigger, name + " trigger collider");
        }
        ValidateRestoredAssets();
        Debug.Log("ENEMY_ACTION_ASSET_BUILD_PASS");
    }

    static void ValidateRestoredAssets()
    {
        foreach (string name in new[] { "FloatingShieldEnemy", "FloatingShadowEnemy", "FloatingWerewolfEnemy", "FloatingMeadowStage", "ShieldGuardian", "OriginalGuardian", "SwiftWerewolf", "DarkMeadowMap", "GameOverSystem" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/" + name + ".prefab");
            Require(prefab != null, name + " exists");
            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, name + " missing script");
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>())
            {
                Require(renderer.sharedMaterials.All(m => m != null && m.shader != null && !ShaderUtil.ShaderHasError(m.shader)), name + " material/shader");
                if (renderer is SkinnedMeshRenderer skinned) Require(skinned.sharedMesh != null, name + " skinned mesh reference");
                else Require(renderer.GetComponent<MeshFilter>()?.sharedMesh != null, name + " mesh reference");
            }
            foreach (var animator in prefab.GetComponentsInChildren<Animator>())
                Require(animator.runtimeAnimatorController != null, name + " controller reference");
        }
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/FloatingMeadowStage.prefab").GetComponentsInChildren<MeshCollider>().Length == 1, "Stage has one ground collider");
        foreach (string name in new[] { "ShieldActions", "ShadowActions", "WerewolfActions" })
            foreach (string clip in new[] { "Idle", "Defense", "Attack", "Spawn" })
                Require(AssetDatabase.LoadAssetAtPath<AnimationClip>(ROOT + "Animations/" + name + clip + ".anim").length > .9f, name + clip + " clip duration");
        Debug.Log("RESTORED_NINE_PREFABS_REFERENCE_CHECK_PASS");
    }

    static AnimatorController ImportClips(string model)
    {
        string path = ROOT + "Art/" + model + ".fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        Require(importer != null, "Missing FBX " + path);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.optimizeGameObjects = false;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        var defaults = importer.defaultClipAnimations;
        Require(defaults.Length > 0, model + " exported take");
        string take = defaults[0].takeName;
        var names = new[] { "Idle", "Defense", "Attack", "Spawn" };
        int[] first = { 0, 31, 61, 91 }, last = { 30, 60, 90, 120 };
        importer.clipAnimations = names.Select((name, i) => new ModelImporterClipAnimation {
            name = name, takeName = take, firstFrame = first[i], lastFrame = last[i],
            loopTime = i == 0, loopPose = i == 0, lockRootRotation = true,
            lockRootHeightY = true, lockRootPositionXZ = true, keepOriginalOrientation = true,
            keepOriginalPositionY = true, keepOriginalPositionXZ = true
        }).ToArray();
        importer.SaveAndReimport();
        string controllerPath = ROOT + "Animations/" + model + ".controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.parameters = new AnimatorControllerParameter[0];
        controller.AddParameter("Defending", AnimatorControllerParameterType.Bool);
        var machine = controller.layers[0].stateMachine;
        foreach (var child in machine.states) machine.RemoveState(child.state);
        var states = new AnimatorState[4];
        var imported = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();
        for (int i = 0; i < names.Length; i++)
        {
            var clip = Object.Instantiate(imported.Single(c => c.name == names[i]));
            clip.name = model + names[i];
            string clipPath = ROOT + "Animations/" + clip.name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null) { EditorUtility.CopySerialized(clip, existing); Object.DestroyImmediate(clip); clip = existing; }
            else AssetDatabase.CreateAsset(clip, clipPath);
            states[i] = machine.AddState(names[i], new Vector3(i * 240, 40, 0));
            states[i].motion = clip;
            states[i].writeDefaultValues = true;
        }
        machine.defaultState = states[3];
        Transition(states[3], states[0], true);
        Transition(states[0], states[1], false).AddCondition(AnimatorConditionMode.If, 0, "Defending");
        Transition(states[1], states[0], false).AddCondition(AnimatorConditionMode.IfNot, 0, "Defending");
        Transition(states[2], states[1], true).AddCondition(AnimatorConditionMode.If, 0, "Defending");
        Transition(states[2], states[0], true).AddCondition(AnimatorConditionMode.IfNot, 0, "Defending");
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, bool exit)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = exit; transition.exitTime = 1; transition.hasFixedDuration = true;
        transition.duration = .08f;
        return transition;
    }

    static void CreateEnemy(string model, string name, AnimatorController controller)
    {
        var root = new GameObject(name);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Art/" + model + ".fbx"));
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        // FBXの寸法を測り、3体の基準の高さをそろえる。ゲーム本体のTransformは動かさない。
        var renderer = visual.GetComponentInChildren<SkinnedMeshRenderer>();
        Require(renderer != null, model + " skinned mesh");
        float scale = 2.55f / renderer.bounds.size.y;
        visual.transform.localScale *= scale;
        var bounds = renderer.bounds;
        visual.transform.localPosition = new Vector3(-bounds.center.x, .3f - bounds.min.y, -bounds.center.z);
        var materials = renderer.sharedMaterials;
        renderer.sharedMaterials = materials.Select(m => AssetDatabase.LoadAssetAtPath<Material>(ROOT + "Materials/" + name + (m != null && m.name.Contains("Eye") ? "Eyes" : "Body") + ".mat")).ToArray();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.updateWhenOffscreen = true;
        var animator = visual.GetComponent<Animator>();
        if (animator == null) animator = visual.AddComponent<Animator>();
        animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ROOT + "Art/" + model + ".fbx").OfType<Avatar>().First();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.isTrigger = true; capsule.center = new Vector3(0, 1.55f, 0); capsule.height = 2.55f; capsule.radius = .48f;
        var driver = root.AddComponent<EnemyActionAnimation>();
        var serialized = new SerializedObject(driver);
        serialized.FindProperty("animator").objectReferenceValue = animator;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        int layer = LayerMask.NameToLayer("Enemy");
        if (layer >= 0) foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = layer;
        PrefabUtility.SaveAsPrefabAsset(root, ROOT + "Prefabs/" + name + ".prefab");
        Object.DestroyImmediate(root);
    }

    static void CreateScene(string[] prefabs)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var actors = new EnemyActionAnimation[3];
        for (int i = 0; i < prefabs.Length; i++)
        {
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/" + prefabs[i] + ".prefab"));
            actor.transform.position = new Vector3((1 - i) * 3.3f, 0, 0);
            actors[i] = actor.GetComponent<EnemyActionAnimation>();
        }
        var stage = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/FloatingMeadowStage.prefab"));
        stage.transform.position = Vector3.down * .65f;
        // アクションの輪郭が草に埋もれないよう、この確認シーンだけ草の描画を隠す。
        foreach (var renderer in stage.GetComponentsInChildren<Renderer>())
            if (renderer.name.Contains("Grass")) renderer.enabled = false;
        var system = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/GameOverSystem.prefab"));
        var camera = new GameObject("TestCamera").AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.transform.position = new Vector3(5, 5.2f, 12);
        camera.transform.LookAt(new Vector3(0, 1.35f, 0));
        camera.orthographic = true; camera.orthographicSize = 4.7f;
        camera.nearClipPlane = .03f; camera.farClipPlane = 150;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.13f, .14f, .16f);
        var lamp = new GameObject("PreviewLightOnly").AddComponent<Light>();
        lamp.type = LightType.Point; lamp.range = 20; lamp.intensity = 25;
        lamp.transform.position = new Vector3(0, 3, 4);
        var sun = new GameObject("PreviewFillOnly").AddComponent<Light>();sun.type = LightType.Directional;sun.intensity = 1.2f;sun.transform.rotation = Quaternion.Euler(35, 160, 0);
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.3f, .3f, .3f);
        var demo = new GameObject("TestControlsOnly").AddComponent<EnemyActionDemo>();
        var serialized = new SerializedObject(demo);
        var array = serialized.FindProperty("actors");array.arraySize = 3;
        for (int i = 0; i < 3; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = actors[i];
        serialized.FindProperty("gameOver").objectReferenceValue = system.GetComponent<GameOverController>();
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), TEST_SCENE);
    }

    static void InstallGameOverSystem()
    {
        // 本番シーンには終了通知の受け口だけを追加する。テスト用UIや仮HPは持ち込まない。
        const string path = ROOT + "Scenes/Main/InGame.unity";
        EditorSceneManager.OpenScene(path);
        if (Object.FindFirstObjectByType<GameOverController>() == null)
        {
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Prefabs/GameOverSystem.prefab"));
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
        }
        EditorSceneManager.OpenScene(TEST_SCENE);
    }

    static void Require(bool pass, string message)
    {
        if (!pass) throw new Exception(message);
    }
}
#endif
