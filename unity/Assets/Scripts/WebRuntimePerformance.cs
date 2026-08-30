using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Pochinki
{
    internal static class WebRuntimePerformance
    {
        private const int WebTargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Apply()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Web quality currently has vSync disabled, but setting it explicitly keeps
            // the frame cap effective if the quality asset changes later.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = WebTargetFrameRate;

#if !DEVELOPMENT_BUILD
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            mainCamera.allowHDR = false;

            UniversalAdditionalCameraData cameraData =
                mainCamera.GetUniversalAdditionalCameraData();
            cameraData.renderShadows = false;
            cameraData.renderPostProcessing = false;
            cameraData.allowHDROutput = false;
#endif
#endif
        }

        internal static void OptimizeMassRenderers(GameObject root)
        {
#if UNITY_WEBGL && !UNITY_EDITOR && !DEVELOPMENT_BUILD
            bool shouldOptimize = true;
#else
            bool shouldOptimize = false;
#endif
            if (!shouldOptimize || root == null)
                return;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }
        }
    }
}
