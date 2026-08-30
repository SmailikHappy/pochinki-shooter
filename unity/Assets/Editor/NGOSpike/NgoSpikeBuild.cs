using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Pochinki.Networking.Spike.Editor
{
    public static class NgoSpikeBuild
    {
        private const string ServerRelativePath = "Builds/NGOSpike/Server/PochinkiNgoSpikeServer.exe";
        private const string WebClientRelativePath = "client/public/ngo-spike";

        [MenuItem("Pochinki/NGO Spike/Build/Build dedicated server and WebGL client")]
        public static void BuildAllFromMenu()
        {
            BuildAll();
        }

        public static void BuildAllFromCommandLine()
        {
            BuildAll();
        }

        [MenuItem("Pochinki/NGO Spike/Build/Build Windows dedicated server")]
        public static void BuildDedicatedServer()
        {
            NgoSpikeAssetGenerator.Generate(false);
            string location = ResolveRepositoryPath(ServerRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(location) ?? throw new InvalidOperationException());

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { NgoSpikeAssetGenerator.ScenePath },
                locationPathName = location,
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.Development,
            };

            EnsureSuccessful(BuildPipeline.BuildPlayer(options), "Windows dedicated server");
            Debug.Log($"[NGO Spike] Dedicated server built at {location}.");
        }

        [MenuItem("Pochinki/NGO Spike/Build/Build WebGL client")]
        public static void BuildWebClient()
        {
            NgoSpikeAssetGenerator.Generate(false);
            string location = ResolveRepositoryPath(WebClientRelativePath);
            Directory.CreateDirectory(location);

            string previousTemplate = PlayerSettings.WebGL.template;
            try
            {
                PlayerSettings.WebGL.template = "PROJECT:DiscordActivity";
                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = new[] { NgoSpikeAssetGenerator.ScenePath },
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

            Debug.Log($"[NGO Spike] WebGL client built at {location}.");
        }

        private static void BuildAll()
        {
            BuildDedicatedServer();
            BuildWebClient();
            Debug.Log("[NGO Spike] Server and WebGL client builds completed successfully.");
        }

        private static void EnsureSuccessful(BuildReport report, string label)
        {
            if (report.summary.result == BuildResult.Succeeded)
            {
                return;
            }

            throw new BuildFailedException(
                $"[NGO Spike] {label} build failed: {report.summary.result}, " +
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
