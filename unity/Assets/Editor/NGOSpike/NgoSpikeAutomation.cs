using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Pochinki.Networking.Spike.Editor
{
    /// <summary>
    /// Runs NGO spike automation in a regular Unity Editor process. Unity 6000.5
    /// can require a separate entitlement for -batchmode, while the normal Editor
    /// uses the user's already activated seat.
    /// </summary>
    internal static class NgoSpikeAutomation
    {
        private const string GenerateArgument = "-ngoSpikeGenerate";
        private const string BuildArgument = "-ngoSpikeBuildAll";

        private static bool shouldGenerate;
        private static bool shouldBuild;
        private static bool hasRun;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            shouldGenerate = arguments.Contains(GenerateArgument);
            shouldBuild = arguments.Contains(BuildArgument);

            if (shouldGenerate || shouldBuild)
            {
                Debug.Log("[NGO Spike] Editor automation is waiting for a ready AssetDatabase.");
                EditorApplication.update += RunWhenEditorIsReady;
            }
        }

        private static void RunWhenEditorIsReady()
        {
            if (hasRun || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            hasRun = true;
            EditorApplication.update -= RunWhenEditorIsReady;

            try
            {
                if (shouldBuild)
                {
                    NgoSpikeBuild.BuildAllFromCommandLine();
                }
                else
                {
                    NgoSpikeAssetGenerator.Generate(false);
                }

                Debug.Log("[NGO Spike] Editor automation completed.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
