using System.Collections.Generic;
using System.IO;
using TowerOfChrome.Core.Fsm;
using TowerOfChrome.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-off build script that programmatically constructs Assets/Scenes/Main.unity: a
/// GameManager plus one child GameObject per GameState, wired into GameManager's screenRoots
/// list so the FSM can toggle them active/inactive. Written as a script (not hand-built in the
/// Editor GUI) so the scene construction is reproducible and reviewable as code.
///
/// Run via: Unity.exe -batchmode -nographics -projectPath &lt;path&gt; -executeMethod SceneBuilder.BuildMainScene -quit
/// </summary>
public static class SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Main.unity";

    public static void BuildMainScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var gmGo = new GameObject("GameManager");
        var gm = gmGo.AddComponent<GameManager>();

        var states = new[]
        {
            GameState.Menu, GameState.ClassSelect, GameState.Explore,
            GameState.Combat, GameState.Inventory, GameState.GameOver,
        };

        var so = new SerializedObject(gm);
        var screenRootsProp = so.FindProperty("screenRoots");
        screenRootsProp.arraySize = states.Length;

        for (var i = 0; i < states.Length; i++)
        {
            var root = new GameObject($"Screen_{states[i]}");
            root.transform.SetParent(gmGo.transform);

            var element = screenRootsProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("State").enumValueIndex = (int)states[i];
            element.FindPropertyRelative("Root").objectReferenceValue = root;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);

        var alreadyInBuildSettings = false;
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (s.path == ScenePath)
                alreadyInBuildSettings = true;
        }
        if (!alreadyInBuildSettings)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
            EditorBuildSettings.scenes = list.ToArray();
        }

        Debug.Log($"[SceneBuilder] {ScenePath} created with GameManager and {states.Length} screen roots.");
    }
}
