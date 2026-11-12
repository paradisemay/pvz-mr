using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Pvz.Editor
{
    /// <summary>
    /// Applies project-wide settings for the MR prototype (OpenXR Meta features for Android, player settings).
    /// Idempotent; run from the menu or in batchmode:
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod Pvz.Editor.PvzProjectSetup.Apply
    /// </summary>
    public static class PvzProjectSetup
    {
        public const string ProductName = "ПВЗ MR";
        public const string CompanyName = "PVZ MR Team";
        public const string ApplicationId = "com.pvzmr.prototype";

        // Feature ids from com.unity.xr.openxr (Meta Quest support) and com.unity.xr.meta-openxr.
        private static readonly string[] AndroidFeaturesToEnable =
        {
            "com.unity.openxr.feature.metaquest",
            "com.unity.openxr.feature.arfoundation-meta-session",
            "com.unity.openxr.feature.arfoundation-meta-camera",
            "com.unity.openxr.feature.arfoundation-meta-plane",
            "com.unity.openxr.feature.arfoundation-meta-anchor",
            "com.unity.openxr.feature.arfoundation-meta-raycast",
            "com.unity.openxr.feature.meta-display-utilities",
        };

        // Legacy Oculus Quest feature conflicts with Meta Quest support.
        private static readonly string[] AndroidFeaturesToDisable =
        {
            "com.unity.openxr.feature.oculusquest",
        };

        [MenuItem("PVZ/Apply Project Setup")]
        public static void Apply()
        {
            var log = new StringBuilder("[PvzProjectSetup]\n");
            ApplyPlayerSettings(log);
            ApplyOpenXrFeatures(log);
            ApplyRenderPipeline(log);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        private static void ApplyPlayerSettings(StringBuilder log)
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            log.AppendLine($"  player: '{ProductName}' {ApplicationId}, IL2CPP, ARM64, minSdk 32");
        }

        private static void ApplyOpenXrFeatures(StringBuilder log)
        {
            // Creates feature assets for packages added after OpenXR settings were first generated (e.g. Meta OpenXR).
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            foreach (var id in AndroidFeaturesToEnable) SetFeature(id, true, log);
            foreach (var id in AndroidFeaturesToDisable) SetFeature(id, false, log);
        }

        private static void SetFeature(string featureId, bool enabled, StringBuilder log)
        {
            var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, featureId);
            if (feature == null)
            {
                log.AppendLine($"  openxr: feature '{featureId}' not found (package missing?)");
                return;
            }
            if (feature.enabled != enabled)
            {
                feature.enabled = enabled;
                EditorUtility.SetDirty(feature);
            }
            log.AppendLine($"  openxr: {featureId} = {enabled}");
        }

        /// <summary>Passthrough needs a transparent framebuffer: HDR off on the mobile URP asset.</summary>
        private static void ApplyRenderPipeline(StringBuilder log)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets/Settings" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("Mobile")) continue;
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null) continue;
                asset.supportsHDR = false;
                EditorUtility.SetDirty(asset);
                log.AppendLine($"  urp: HDR off in {path}");
            }
        }
    }
}
