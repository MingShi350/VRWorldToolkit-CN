#if VRC_SDK_VRCSDK3
using VRC.SDKBase.Editor.BuildPipeline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace VRWorldToolkit.Editor
{
    public class DisableOnBuildCallback : IVRCSDKBuildRequestedCallback, IProcessSceneWithReport
    {
        public int callbackOrder => 1;

        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            DisableOnBuildManager.ToggleObjectsUsingTag("DisableOnBuild", false, false);
            DisableOnBuildManager.ToggleObjectsUsingTag("EnableOnBuild", true, false);

            return true;
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            DisableOnBuildManager.ToggleObjectsUsingTag("DisableOnBuild", false, false);
            DisableOnBuildManager.ToggleObjectsUsingTag("EnableOnBuild", true, false);
        }
    }

    public class DisableOnBuildManager : UnityEditor.Editor
    {
        // Disable On Build
        [MenuItem("VRWorld Toolkit/构建时功能/构建时禁用/设置", false, 13)]
        private static void DisableOnBuildSetup()
        {
            if (EditorUtility.DisplayDialog("设置构建时禁用", "此设置将添加一个新标签 DisableOnBuild。将此标签分配给游戏对象后，该对象将在构建前被禁用。", "设置", "取消"))
            {
                Helper.AddTag("DisableOnBuild");
            }
        }

        [MenuItem("VRWorld Toolkit/构建时功能/构建时禁用/设置", true)]
        private static bool DisableOnBuildSetupValidate()
        {
            return !Helper.TagExists("DisableOnBuild");
        }

        [MenuItem("VRWorld Toolkit/构建时功能/构建时禁用/禁用对象", false, 24)]
        private static void DisableDisableObjectsLoop()
        {
            ToggleObjectsUsingTag("DisableOnBuild", false, true);
        }

        [MenuItem("VRWorld Toolkit/构建时功能/构建时禁用/禁用对象", true)]
        private static bool DisableDisableObjectsValidate()
        {
            return Helper.TagExists("DisableOnBuild");
        }

        [MenuItem("VRWorld Toolkit/构建时功能/构建时禁用/启用对象", false, 25)]
        private static void EnableDisableObjectsLoop()
        {
            ToggleObjectsUsingTag("DisableOnBuild", true, true);
        }

        [MenuItem("VRWorld Toolkit/构建时功能/构建时禁用/启用对象", true)]
        private static bool EnableObjectsLoopValidate()
        {
            return Helper.TagExists("DisableOnBuild");
        }

        // Enable On Build
        [MenuItem("VRWorld Toolkit/构建时功能/构建时启用/设置", false, 13)]
        private static void EnableOnBuildSetup()
        {
            if (EditorUtility.DisplayDialog("设置构建时启用", "此设置将添加一个新标签 EnableOnBuild。将此标签分配给游戏对象后，该对象将在构建前被启用。", "设置", "取消"))
            {
                Helper.AddTag("EnableOnBuild");
            }
        }

        [MenuItem("VRWorld Toolkit/构建时功能/构建时启用/设置", true)]
        private static bool EnableOnBuildSetupValidate()
        {
            return !Helper.TagExists("EnableOnBuild");
        }

        [MenuItem("VRWorld Toolkit/构建时功能/构建时启用/禁用对象", false, 24)]
        private static void DisableEnableObjectsLoop()
        {
            ToggleObjectsUsingTag("EnableOnBuild", false, true);
        }

        [MenuItem("VRWorld Toolkit/构建时功能/构建时启用/禁用对象", true)]
        private static bool DisableEnableObjectsValidate()
        {
            return Helper.TagExists("EnableOnBuild");
        }

        [MenuItem("VRWorld Toolkit/构建时功能/构建时启用/启用对象", false, 25)]
        private static void EnableEnableObjectsLoop()
        {
            ToggleObjectsUsingTag("EnableOnBuild", true, true);
        }

        [MenuItem("VRWorld Toolkit/构建时功能/构建时启用/启用对象", true)]
        private static bool EnableEnableObjectsLoopValidate()
        {
            return Helper.TagExists("EnableOnBuild");
        }

        public static void ToggleObjectsUsingTag(string tag, bool active, bool markSceneDirty)
        {
            if (!Helper.TagExists(tag)) return;

            var toggledGameObjectCount = 0;
            var allGameObjects = Resources.FindObjectsOfTypeAll(typeof(GameObject));
            var allGameObjectsLength = allGameObjects.Length;
            for (var i = 0; i < allGameObjectsLength; i++)
            {
                var gameObject = allGameObjects[i] as GameObject;

                if (gameObject.hideFlags != HideFlags.None || EditorUtility.IsPersistent(gameObject.transform.root.gameObject)) continue;

                if (gameObject.CompareTag(tag))
                {
                    gameObject.SetActive(active);
                    toggledGameObjectCount++;
                }
            }

            var state = active ? "启用" : "禁用";
            var plural = toggledGameObjectCount > 1 ? "" : "";
            Debug.Log($"已将场景中 {toggledGameObjectCount} 个带有标签 {tag} 的游戏对象设置为{state}");
            if (markSceneDirty) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
#endif