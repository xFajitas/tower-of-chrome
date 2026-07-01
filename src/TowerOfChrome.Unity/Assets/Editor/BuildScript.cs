using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Windows standalone build entry point. Run via:
/// Unity.exe -batchmode -nographics -projectPath &lt;path&gt; -executeMethod BuildScript.BuildWindows -quit
/// Uses whatever scenes SceneBuilder has already registered in EditorBuildSettings.
/// </summary>
public static class BuildScript
{
    private const string OutputPath = "Builds/Windows/TowerOfChrome.exe";

    public static void BuildWindows()
    {
        var scenes = new List<string>();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
                scenes.Add(scene.path);
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"[BuildScript] result={summary.result} size={summary.totalSize} errors={summary.totalErrors} warnings={summary.totalWarnings} time={summary.totalTime}");

        if (summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
