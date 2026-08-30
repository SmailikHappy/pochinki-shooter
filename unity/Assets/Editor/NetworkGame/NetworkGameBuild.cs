using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Pochinki.Networking.Game.Editor
{
    public static class NetworkGameBuild
    {
        private const string ServerRelativePath = "Builds/Game/Server/PochinkiGameServer.exe";
        private const string WebClientRelativePath = "client/public/unity-build";

        private enum BuildFlavor
        {
            Development,
            Release,
        }

        [MenuItem("Pochinki/Network Game/Build/Development/Build dedicated server and WebGL client")]
        public static void BuildAllDevelopmentFromMenu()
        {
            BuildAll(BuildFlavor.Development);
        }

        [MenuItem("Pochinki/Network Game/Build/Development/Build Windows dedicated server")]
        public static void BuildDedicatedServerDevelopmentFromMenu()
        {
            BuildDedicatedServer(BuildFlavor.Development);
        }

        [MenuItem("Pochinki/Network Game/Build/Development/Build WebGL client")]
        public static void BuildWebClientDevelopmentFromMenu()
        {
            BuildWebClient(BuildFlavor.Development);
        }

        [MenuItem("Pochinki/Network Game/Build/Release/Build dedicated server and WebGL client")]
        public static void BuildAllReleaseFromMenu()
        {
            BuildAll(BuildFlavor.Release);
        }

        [MenuItem("Pochinki/Network Game/Build/Release/Build Windows dedicated server")]
        public static void BuildDedicatedServerReleaseFromMenu()
        {
            BuildDedicatedServer(BuildFlavor.Release);
        }

        [MenuItem("Pochinki/Network Game/Build/Release/Build WebGL client")]
        public static void BuildWebClientReleaseFromMenu()
        {
            BuildWebClient(BuildFlavor.Release);
        }

        // Kept as the development entry point for existing editor automation.
        public static void BuildAllFromCommandLine()
        {
            BuildAll(BuildFlavor.Development);
        }

        public static void BuildReleaseFromCommandLine()
        {
            BuildAll(BuildFlavor.Release);
        }

        private static void BuildDedicatedServer(BuildFlavor flavor)
        {
            NetworkGameAssetInstaller.Install(openScene: false);
            string location = ResolveRepositoryPath(ServerRelativePath);
            string outputDirectory = Path.GetDirectoryName(location)
                ?? throw new InvalidOperationException("Dedicated server output directory was not found.");
            RecreateOutputDirectory(outputDirectory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { NetworkGameAssetInstaller.ScenePath },
                locationPathName = location,
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = GetBuildOptions(flavor),
            };

            EnsureSuccessful(BuildPipeline.BuildPlayer(options), "Windows dedicated server");
            Debug.Log($"[Network Game] {flavor} dedicated server built at {location}.");
        }

        private static void BuildWebClient(BuildFlavor flavor)
        {
            NetworkGameAssetInstaller.Install(openScene: false);
            string location = ResolveRepositoryPath(WebClientRelativePath);
            RecreateOutputDirectory(location);

            string previousTemplate = PlayerSettings.WebGL.template;
            try
            {
                PlayerSettings.WebGL.template = "PROJECT:DiscordActivity";
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { NetworkGameAssetInstaller.ScenePath },
                    locationPathName = location,
                    target = BuildTarget.WebGL,
                    options = GetBuildOptions(flavor),
                };

                EnsureSuccessful(BuildPipeline.BuildPlayer(options), "WebGL client");
            }
            finally
            {
                PlayerSettings.WebGL.template = previousTemplate;
            }

            Debug.Log($"[Network Game] {flavor} WebGL client built at {location}.");
        }

        private static void BuildAll(BuildFlavor flavor)
        {
            BuildDedicatedServer(flavor);
            BuildWebClient(flavor);
            Debug.Log($"[Network Game] {flavor} server and WebGL gameplay builds completed successfully.");
        }

        private static BuildOptions GetBuildOptions(BuildFlavor flavor)
        {
            return flavor == BuildFlavor.Development
                ? BuildOptions.Development
                : BuildOptions.None;
        }

        private static void RecreateOutputDirectory(string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, recursive: true);

            Directory.CreateDirectory(fullPath);
        }

        private static void EnsureSuccessful(BuildReport report, string label)
        {
            if (report.summary.result == BuildResult.Succeeded)
            {
                return;
            }

            throw new BuildFailedException(
                $"[Network Game] {label} build failed: {report.summary.result}, " +
                $"{report.summary.totalErrors} errors.");
        }

        private static string ResolveRepositoryPath(string repositoryRelativePath)
        {
            string unityProjectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Unity project root was not found.");
            string repositoryRoot = Directory.GetParent(unityProjectRoot)?.FullName
                ?? throw new InvalidOperationException("Repository root was not found.");
            return Path.GetFullPath(Path.Combine(repositoryRoot, repositoryRelativePath));
        }
    }
}
