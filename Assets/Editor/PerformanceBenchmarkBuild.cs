using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class PerformanceBenchmarkBuild
{
    public static void BuildDevelopmentPlayer()
    {
        BuildPlayer(BuildOptions.Development);
    }

    public static void BuildReleasePlayer()
    {
        BuildPlayer(BuildOptions.None);
    }

    private static void BuildPlayer(BuildOptions options)
    {
        string outputPath = GetArgumentValue("-performance-build-path");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/01-GameScene.unity" },
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = options
        });

        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"Performance build failed: {report.summary.result}");
    }

    private static string GetArgumentValue(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, name);
        return arguments[index + 1];
    }
}
