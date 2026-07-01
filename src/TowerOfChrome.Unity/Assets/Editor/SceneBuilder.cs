using System.Collections.Generic;
using System.IO;
using TowerOfChrome.Core.Fsm;
using TowerOfChrome.Unity;
using TowerOfChrome.Unity.Screens;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// One-off build script that programmatically constructs Assets/Scenes/Main.unity: a
/// GameManager plus one child GameObject per GameState, wired into GameManager's screenRoots
/// list so the FSM can toggle them active/inactive. Written as a script (not hand-built in the
/// Editor GUI) so the scene construction is reproducible and reviewable as code.
///
/// Screens that have a real UI Toolkit implementation (Menu, ClassSelect so far) get a
/// UIDocument + their View MonoBehaviour attached automatically; the rest stay empty
/// placeholders until their own screen is built.
///
/// Run via: Unity.exe -batchmode -nographics -projectPath &lt;path&gt; -executeMethod SceneBuilder.BuildMainScene -quit
/// </summary>
public static class SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Main.unity";
    private const string PanelSettingsPath = "Assets/UI/DefaultPanelSettings.asset";

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

        var panelSettings = GetOrCreatePanelSettings();

        for (var i = 0; i < states.Length; i++)
        {
            var root = new GameObject($"Screen_{states[i]}");
            root.transform.SetParent(gmGo.transform);
            // Stay inactive in the saved scene so Awake/OnEnable only fire once GameManager.Awake()
            // calls ActivateScreenRoot (after registries are built) -- not at scene-load time, which
            // races GameManager's own Awake against every screen view's OnEnable.
            root.SetActive(false);

            var element = screenRootsProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("State").enumValueIndex = (int)states[i];
            element.FindPropertyRelative("Root").objectReferenceValue = root;

            switch (states[i])
            {
                case GameState.Menu:
                    AddScreenUi<MenuScreenView>(root, "Assets/UI/MenuScreen.uxml", panelSettings, gm);
                    break;
                case GameState.ClassSelect:
                    AddScreenUi<ClassSelectScreenView>(root, "Assets/UI/ClassSelectScreen.uxml", panelSettings, gm);
                    break;
                case GameState.Explore:
                    AddScreenUi<ExploreScreenView>(root, "Assets/UI/ExploreScreen.uxml", panelSettings, gm);
                    break;
            }
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

    private static PanelSettings GetOrCreatePanelSettings()
    {
        var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (existing != null)
            return existing;

        var settings = ScriptableObject.CreateInstance<PanelSettings>();
        Directory.CreateDirectory("Assets/UI");
        AssetDatabase.CreateAsset(settings, PanelSettingsPath);
        AssetDatabase.SaveAssets();
        return settings;
    }

    private static void AddScreenUi<TView>(GameObject root, string uxmlPath, PanelSettings panelSettings, GameManager gm)
        where TView : MonoBehaviour
    {
        var uiDocument = root.AddComponent<UIDocument>();
        uiDocument.panelSettings = panelSettings;
        uiDocument.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);

        var view = root.AddComponent<TView>();

        var viewSo = new SerializedObject(view);
        var gmProp = viewSo.FindProperty("gameManager");
        if (gmProp != null)
            gmProp.objectReferenceValue = gm;
        viewSo.ApplyModifiedPropertiesWithoutUndo();
    }
}
