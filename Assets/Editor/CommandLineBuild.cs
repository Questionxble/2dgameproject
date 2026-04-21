using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class CommandLineBuild
{
    private const string DefaultLinuxServerExecutableName = "2dgameproject_server";
    private static readonly string DefaultLinuxServerOutputPath = Path.Combine(
        "C:\\Users\\chris\\source\\deployments\\Linux\\LinuxServerBuild_cli",
        DefaultLinuxServerExecutableName);

    [MenuItem("Build/Build Linux Dedicated Server")]
    public static void BuildLinuxDedicatedServerMenu()
    {
        BuildLinuxDedicatedServer();
    }

    public static void BuildLinuxDedicatedServer()
    {
        string outputPath = GetCommandLineArgument("buildOutput");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = DefaultLinuxServerOutputPath;
        }

        string[] enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (enabledScenes.Length == 0)
        {
            throw new BuildFailedException("No enabled scenes were found in Build Settings.");
        }

        string outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new BuildFailedException($"Invalid build output path: {outputPath}");
        }

        Directory.CreateDirectory(outputDirectory);

        StandaloneBuildSubtarget previousSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;

        try
        {
            EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = enabledScenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneLinux64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.StrictMode
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Linux dedicated server build failed with result {summary.result}. " +
                    $"Errors: {summary.totalErrors}, Warnings: {summary.totalWarnings}");
            }

            UnityEngine.Debug.Log(
                $"Linux dedicated server build succeeded: {summary.outputPath} " +
                $"({summary.totalSize} bytes, {summary.totalTime})");
        }
        finally
        {
            EditorUserBuildSettings.standaloneBuildSubtarget = previousSubtarget;
        }
    }

    private static string GetCommandLineArgument(string argumentName)
    {
        string[] args = Environment.GetCommandLineArgs();
        string expectedName = $"-{argumentName}";

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}