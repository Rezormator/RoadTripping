using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zenject;

public static class PlayerSceneSetup
{
    private const string UndoName = "Setup Player Scene";

    [MenuItem("Tools/RoadTripping/Setup Player Scene")]
    public static void Setup()
    {
        var log = new List<string>();

        PlayerView view = Object.FindAnyObjectByType<PlayerView>(FindObjectsInactive.Include);
        if (view == null)
        {
            Debug.LogError("[Setup] PlayerView not found in the open scene. Add it to the player object first.");
            return;
        }

        RemoveOldBootstrap(log);
        SetupCameraEffects(view, log);
        SetupSceneContext(log);

        var scene = view.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path))
        {
            EditorSceneManager.SaveScene(scene);
            log.Add($"Scene saved: {scene.path}");
        }

        Debug.Log("[Setup] Done:\n- " + string.Join("\n- ", log));
    }

    private static void RemoveOldBootstrap(List<string> log)
    {
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (go.name != "GameBootstrap")
                continue;

            bool onlyMissing = go.GetComponents<Component>().All(c => c == null || c is Transform);
            if (!onlyMissing)
                continue;

            Undo.DestroyObjectImmediate(go);
            log.Add("Removed old GameBootstrap object");
        }
    }

    private static void SetupCameraEffects(PlayerView view, List<string> log)
    {
        var viewSo = new SerializedObject(view);
        var cameraRoot = viewSo.FindProperty("cameraRoot").objectReferenceValue as Transform;
        if (cameraRoot == null)
        {
            log.Add("WARNING: Camera Root is not assigned on PlayerView, camera effects skipped");
            return;
        }

        CameraEffectsView effects = cameraRoot.GetComponentInChildren<CameraEffectsView>(true);
        if (effects == null)
        {
            var go = new GameObject("CameraEffects");
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            go.transform.SetParent(cameraRoot, false);
            effects = Undo.AddComponent<CameraEffectsView>(go);
            log.Add("Created CameraEffects under CameraRoot");
        }

        Camera camera = cameraRoot.GetComponentInChildren<Camera>(true);
        if (camera == null)
            camera = Camera.main;

        if (camera != null)
        {
            if (camera.transform.parent != effects.transform)
            {
                Undo.SetTransformParent(camera.transform, effects.transform, UndoName);
                log.Add($"Moved {camera.name} into CameraEffects");
            }

            Undo.RecordObject(camera.transform, UndoName);
            camera.transform.localPosition = Vector3.zero;
            camera.transform.localRotation = Quaternion.identity;

            if (camera.nearClipPlane > 0.05f)
            {
                Undo.RecordObject(camera, UndoName);
                camera.nearClipPlane = 0.03f;
                log.Add("Camera Near Clip Plane set to 0.03");
            }
        }
        else
        {
            log.Add("WARNING: no camera found to put under CameraEffects");
        }

        viewSo.FindProperty("cameraEffects").objectReferenceValue = effects;
        viewSo.ApplyModifiedProperties();
        log.Add("PlayerView.Camera Effects assigned");
    }

    private static void SetupSceneContext(List<string> log)
    {
        SceneContext context = Object.FindAnyObjectByType<SceneContext>(FindObjectsInactive.Include);
        if (context == null)
        {
            var go = new GameObject("SceneContext");
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            context = Undo.AddComponent<SceneContext>(go);
            log.Add("Created SceneContext");
        }

        GameObject host = context.gameObject;

        PlayerInstaller playerInstaller = Object.FindAnyObjectByType<PlayerInstaller>(FindObjectsInactive.Include);
        if (playerInstaller == null)
        {
            playerInstaller = Undo.AddComponent<PlayerInstaller>(host);
            log.Add("Added PlayerInstaller");
        }

        GameInstaller gameInstaller = Object.FindAnyObjectByType<GameInstaller>(FindObjectsInactive.Include);
        if (gameInstaller == null)
        {
            gameInstaller = Undo.AddComponent<GameInstaller>(host);
            log.Add("Added GameInstaller");
        }

        var installerSo = new SerializedObject(playerInstaller);
        AssignAsset<PlayerMovementConfig>(installerSo, "movementConfig", log);
        AssignAsset<CameraBobConfig>(installerSo, "cameraBobConfig", log);
        installerSo.ApplyModifiedProperties();

        var contextSo = new SerializedObject(context);
        SerializedProperty list = contextSo.FindProperty("_monoInstallers");
        AddToList(list, playerInstaller);
        AddToList(list, gameInstaller);
        contextSo.ApplyModifiedProperties();
        log.Add("SceneContext Mono Installers: PlayerInstaller, GameInstaller");
    }

    private static void AssignAsset<T>(SerializedObject so, string property, List<string> log) where T : Object
    {
        SerializedProperty prop = so.FindProperty(property);
        if (prop.objectReferenceValue != null)
            return;

        string guid = AssetDatabase.FindAssets($"t:{typeof(T).Name}").FirstOrDefault();
        if (guid == null)
        {
            log.Add($"WARNING: no {typeof(T).Name} asset found");
            return;
        }

        prop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        log.Add($"PlayerInstaller.{property} assigned");
    }

    private static void AddToList(SerializedProperty list, Object item)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == item)
                return;
        }

        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
    }
}
