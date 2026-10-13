using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PacMan.EditorTools
{
    public static class BuildPlayer
    {
        public const string Output = "Builds/Windows/PacMan3D.exe";

        [MenuItem("Pac-Man/Build Windows Player")]
        public static void Windows()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/PacMan3D.unity" },
                locationPathName = Output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log($"Build {report.summary.result}: {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors -> {Output}");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
