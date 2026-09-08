using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class MurdokuWindowsBuild
{
    private const string MenuPath = "Tools/Murdoku/Build Windows Test Package";

    [MenuItem(MenuPath)]
    public static void BuildWindowsTestPackage()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[Murdoku Build] 请先退出播放模式。\n");
            return;
        }

        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        string version = string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion)
            ? "unversioned"
            : PlayerSettings.bundleVersion;
        string outputDirectory = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            "Builds",
            $"Murdoku-{version}-Windows-x64"));
        Directory.CreateDirectory(outputDirectory);

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(outputDirectory, "Murdoku.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });

        BuildSummary summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[Murdoku Build] BUILD_SUCCEEDED path={outputDirectory} size={summary.totalSize}");
            return;
        }

        Debug.LogError($"[Murdoku Build] BUILD_FAILED result={summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings}");
    }
}
