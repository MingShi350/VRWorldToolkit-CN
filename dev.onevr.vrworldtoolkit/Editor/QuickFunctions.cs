#if VRC_SDK_VRCSDK3
#define VRWT_IS_VRC
#endif

#if VRWT_IS_VRC
using VRC.SDKBase;
using VRC.Core;
#endif

using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRWorldToolkit.Editor
{
    public class QuickFunctions : EditorWindow
    {
#if VRWT_IS_VRC
        [MenuItem("VRWorld Toolkit/快捷功能/复制世界 ID", false, 4)]
        public static void CopyWorldID()
        {
            var descriptors = FindObjectsOfType(typeof(VRC_SceneDescriptor)) as VRC_SceneDescriptor[];

            if (descriptors.Length is 1)
            {
                var pipelineManager = descriptors[0].GetComponent<PipelineManager>();

                if (pipelineManager) EditorGUIUtility.systemCopyBuffer = pipelineManager.blueprintId;
            }
        }

        [MenuItem("VRWorld Toolkit/快捷功能/复制世界 ID", true)]
        private static bool CopyWorldIDValidate()
        {
            var descriptors = FindObjectsOfType(typeof(VRC_SceneDescriptor)) as VRC_SceneDescriptor[];

            if (descriptors.Length is 1)
            {
                var pipelineManager = descriptors[0].GetComponent<PipelineManager>();

                if (pipelineManager) return pipelineManager.blueprintId.Length > 0;
            }

            return false;
        }
        
        [MenuItem("VRWorld Toolkit/快捷功能/打开 VRChat Worlds 构建文件夹", false, 5)]
        public static void OpenBuildFolder()
        {
#if UNITY_EDITOR_WIN
            var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var localLowPath = Path.Combine(userProfilePath, "AppData", "LocalLow", "VRChat", "VRChat", "Worlds");

            if (Directory.Exists(localLowPath)) {
                System.Diagnostics.Process.Start("explorer.exe", localLowPath.Replace("/", "\\"));
            }
            else
            {
                EditorUtility.DisplayDialog("找不到文件夹", "VRChat Worlds 构建文件夹不存在。你可能还没有使用 VRChat SDK 进行过构建。", "确定");
            }
#else
            EditorUtility.DisplayDialog("提示", "此功能目前仅支持在 Windows 上的 Unity Editor 中使用。未执行任何操作。", "确定");
#endif
        }

        [MenuItem("VRWorld Toolkit/快捷功能/设置层与碰撞矩阵", false, 6)]
        public static void SetupLayersCollisionMatrix()
        {
            if (!UpdateLayers.AreLayersSetup()) UpdateLayers.SetupEditorLayers();

            if (!UpdateLayers.IsCollisionLayerMatrixSetup()) UpdateLayers.SetupCollisionLayerMatrix();
        }

        [MenuItem("VRWorld Toolkit/快捷功能/设置层与碰撞矩阵", true)]
        private static bool SetupLayersCollisionMatrixValidate()
        {
            return !UpdateLayers.AreLayersSetup() || !UpdateLayers.IsCollisionLayerMatrixSetup();
        }
#endif

        [MenuItem("VRWorld Toolkit/快捷功能/从场景中移除丢失的脚本", false, 7)]
        private static void FindAndRemoveMissingScripts()
        {
            if (EditorUtility.DisplayDialog("移除丢失的脚本", "运行此操作将遍历当前打开场景中的所有游戏对象，并移除所有包含丢失脚本的组件。此操作不可撤销！\n\n确定要继续吗？", "继续", "取消"))
            {
                var overallRemovedCount = 0;
                var allGameObjects = Resources.FindObjectsOfTypeAll(typeof(GameObject));
                var allGameObjectsLength = allGameObjects.Length;
                for (var i = 0; i < allGameObjectsLength; i++)
                {
                    var gameObject = allGameObjects[i] as GameObject;

                    if (gameObject != null && (gameObject.hideFlags != HideFlags.None || EditorUtility.IsPersistent(gameObject.transform.root.gameObject))) continue;

                    if (EditorUtility.DisplayCancelableProgressBar("正在检查丢失的脚本", gameObject.name, (float) i / allGameObjectsLength)) break;

                    var removedCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                    if (removedCount > 0)
                    {
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
                        overallRemovedCount += removedCount;
                    }
                }

                EditorUtility.ClearProgressBar();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                var message = overallRemovedCount > 0 ? $"共移除了 {overallRemovedCount} 个包含丢失脚本的组件。" : "未发现包含丢失脚本的组件。";
                EditorUtility.DisplayDialog("移除丢失的脚本", message, "确定");
            }
        }
    }
}