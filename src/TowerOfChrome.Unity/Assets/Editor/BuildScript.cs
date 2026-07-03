using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Standalone build entry points. Run via:
/// Unity.exe -batchmode -nographics -projectPath &lt;path&gt; -executeMethod BuildScript.BuildWindows -quit
/// (swap BuildWindows for BuildAndroid to build the APK instead).
/// Uses whatever scenes SceneBuilder has already registered in EditorBuildSettings.
/// </summary>
public static class BuildScript
{
    private const string OutputPath = "Builds/Windows/TowerOfChrome.exe";
    private const string AndroidOutputPath = "Builds/Android/TowerOfChrome.apk";
    private const string AndroidApplicationIdentifier = "com.fajitas.towerofchrome";

    public static void BuildWindows()
    {
        var options = new BuildPlayerOptions
        {
            scenes = EnabledScenes(),
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        Run(options);
    }

    /// <summary>
    /// Android needs IL2CPP for the ARM64-only target architecture already set in Player
    /// Settings (Mono can't produce ARM64 on Android), plus an application identifier — both
    /// set here explicitly rather than relying on ProjectSettings.asset, which currently has
    /// no per-platform value recorded for either.
    /// </summary>
    public static void BuildAndroid()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, AndroidApplicationIdentifier);

        var options = new BuildPlayerOptions
        {
            scenes = EnabledScenes(),
            locationPathName = AndroidOutputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        };

        Run(options);
    }

    private static string[] EnabledScenes()
    {
        var scenes = new List<string>();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
                scenes.Add(scene.path);
        }
        return scenes.ToArray();
    }

    private static void Run(BuildPlayerOptions options)
    {
        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"[BuildScript] result={summary.result} size={summary.totalSize} errors={summary.totalErrors} warnings={summary.totalWarnings} time={summary.totalTime}");

        if (summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
