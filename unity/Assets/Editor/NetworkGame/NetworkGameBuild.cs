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

        [MenuItem("Pochinki/Network Game/Build/Build dedicated server and WebGL client")]
        public static void BuildAllFromMenu()
        {
            BuildAll();
        }

        public static void BuildAllFromCommandLine()
        {
            BuildAll();
        }

        [MenuItem("Pochinki/Network Game/Build/Build Windows dedicated server")]
        public static void BuildDedicatedServer()
        {
            NetworkGameAssetInstaller.Install(openScene: false);
            string location = ResolveRepositoryPath(ServerRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(location) ?? throw new InvalidOperationException());

            var options = new BuildPlayerOptions
            {
                scenes = new[] { NetworkGameAssetInstaller.ScenePath },
                locationPathName = location,
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.Development,
            };

            EnsureSuccessful(BuildPipeline.BuildPlayer(options), "Windows dedicated server");
            Debug.Log($"[Network Game] Dedicated server built at {location}.");
        }

        [MenuItem("Pochinki/Network Game/Build/Build WebGL client")]
        public static void BuildWebClient()
        {
            NetworkGameAssetInstaller.Install(openScene: false);
            string location = ResolveRepositoryPath(WebClientRelativePath);
            Directory.CreateDirectory(location);

            string previousTemplate = PlayerSettings.WebGL.template;
            try
            {
                PlayerSettings.WebGL.template = "PROJECT:DiscordActivity";
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { NetworkGameAssetInstaller.ScenePath },
                    locationPathName = location,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.Development,
                };

                EnsureSuccessful(BuildPipeline.BuildPlayer(options), "WebGL client");
            }
            finally
            {
                PlayerSettings.WebGL.template = previousTemplate;
            }

            Debug.Log($"[Network Game] WebGL client built at {location}.");
        }

        private static void BuildAll()
        {
            BuildDedicatedServer();
            BuildWebClient();
            Debug.Log("[Network Game] Server and WebGL gameplay builds completed successfully.");
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
