using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Murdoku.Editor
{
    /// <summary>
    /// Windows 构建完成后，把项目 Saves/Puzzles 中的关卡放入安装包的 StreamingAssets。
    /// PuzzleSaveManager 会在构建版首次启动时将它们复制到玩家的可写存档目录。
    /// </summary>
    public sealed class BundledPuzzleBuildProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows &&
                report.summary.platform != BuildTarget.StandaloneWindows64)
            {
                Debug.LogWarning(
                    "BundledPuzzleBuildProcessor currently supports Windows builds only. " +
                    "No bundled puzzles were copied for " + report.summary.platform + ".");
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            string sourceDirectory = string.IsNullOrEmpty(projectRoot)
                ? null
                : Path.Combine(projectRoot, "Saves", "Puzzles");
            if (string.IsNullOrEmpty(sourceDirectory) || !Directory.Exists(sourceDirectory))
            {
                Debug.LogWarning("BundledPuzzleBuildProcessor: Saves/Puzzles was not found; the build has no bundled puzzles.");
                return;
            }

            string executablePath = report.summary.outputPath;
            string buildDirectory = Path.GetDirectoryName(executablePath);
            string executableName = Path.GetFileNameWithoutExtension(executablePath);
            if (string.IsNullOrEmpty(buildDirectory) || string.IsNullOrEmpty(executableName))
            {
                throw new BuildFailedException("Could not resolve the Windows build data directory.");
            }

            string destinationDirectory = Path.Combine(
                buildDirectory,
                executableName + "_Data",
                "StreamingAssets",
                "Puzzles");
            Directory.CreateDirectory(destinationDirectory);

            int copiedCount = 0;
            foreach (string sourcePath in Directory.GetFiles(sourceDirectory, "*.json"))
            {
                string destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(sourcePath));
                File.Copy(sourcePath, destinationPath, true);
                copiedCount++;
            }

            Debug.Log($"Bundled {copiedCount} puzzle(s) into {destinationDirectory}.");
        }
    }
}
