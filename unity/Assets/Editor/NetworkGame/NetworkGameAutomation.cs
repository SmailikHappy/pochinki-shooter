using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Pochinki.Networking.Game.Editor
{
    internal static class NetworkGameAutomation
    {
        private const string InstallArgument = "-networkGameInstall";
        private const string BuildArgument = "-networkGameBuildAll";
        private const string ReleaseBuildArgument = "-networkGameBuildRelease";

        private static bool shouldInstall;
        private static bool shouldBuild;
        private static bool shouldBuildRelease;
        private static bool hasRun;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            shouldInstall = arguments.Contains(InstallArgument);
            shouldBuild = arguments.Contains(BuildArgument);
            shouldBuildRelease = arguments.Contains(ReleaseBuildArgument);

            if (shouldInstall || shouldBuild || shouldBuildRelease)
            {
                Debug.Log("[Network Game] Editor automation is waiting for a ready AssetDatabase.");
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
                if (shouldBuildRelease)
                {
                    NetworkGameBuild.BuildReleaseFromCommandLine();
                }
                else if (shouldBuild)
                {
                    NetworkGameBuild.BuildAllFromCommandLine();
                }
                else
                {
                    NetworkGameAssetInstaller.InstallFromCommandLine();
                }

                Debug.Log("[Network Game] Editor automation completed.");
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
