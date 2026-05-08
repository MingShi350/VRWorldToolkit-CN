
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_POST_PROCESSING_STACK_V2
using UnityEngine.Rendering.PostProcessing;
#endif
#if VRC_SDK_VRCSDK3
using VRC.SDKBase;
#endif

namespace VRWorldToolkit.Editor
{
    public class PostProcessingTools : MonoBehaviour
    {
#if VRC_SDK_VRCSDK3
        [MenuItem("VRWorld Toolkit/后期处理/设置后期处理", false, 1)]
        private static void PostProcessingSetup()
        {
#if UNITY_POST_PROCESSING_STACK_V2
            var sceneDescriptors = FindObjectsOfType(typeof(VRC_SceneDescriptor)) as VRC_SceneDescriptor[];
            var avatarDescriptors = FindObjectsOfType(typeof(VRC_AvatarDescriptor)) as VRC_AvatarDescriptor[];

            if (UpdateLayers.AreLayersSetup() || EditorUtility.DisplayDialog("缺少层设置！", "你尚未在 VRCSDK Builder 选项卡中设置项目层。\r\n\r\n选择「继续」立即设置。", "继续", "取消"))
            {
                UpdateLayers.SetupEditorLayers();

                if (sceneDescriptors.Length == 0)
                {
                    if (avatarDescriptors.Length > 0)
                    {
                        SetupBasicPostProcessing();
                    }
                    else if (EditorUtility.DisplayDialog("缺少场景描述符！",
                        "未找到场景描述符或化身描述符。场景描述符必须存在且包含引用摄像机，后期处理才能在游戏中显示。\r\n\r\n你可以通过添加 SDK 附带的 VRCWorld 预制体来添加场景描述符。\r\n\r\n选择「取消」返回并添加场景描述符让设置自动配置引用摄像机，或选择「继续」忽略此警告。",
                        "继续",
                        "取消"))
                    {
                        SetupBasicPostProcessing();
                    }
                }
                else if (sceneDescriptors.Length > 1)
                {
                    EditorUtility.DisplayDialog("存在多个场景描述符！", "发现多个场景描述符，请移除未使用的描述符后重新运行设置。", "确定");
                }
                else
                {
                    SetupWorldPostProcessing(sceneDescriptors);
                }
            }
#endif
        }

        [MenuItem("VRWorld Toolkit/后期处理/设置后期处理", true)]
        private static bool PostProcessingSetupValidation()
        {
            return !(Helper.BuildPlatform() is RuntimePlatform.Android);
        }
#endif

        [MenuItem("VRWorld Toolkit/后期处理/后期处理指南", false, 2)]
        private static void PostProcessingGuide()
        {
            Application.OpenURL("https://gitlab.com/s-ilent/SCSS/-/wikis/Other/Post-Processing");
        }

        private static void SetupBasicPostProcessing()
        {
            GameObject camera = null;

            if (Camera.main != null)
            {
                camera = Camera.main.gameObject;
            }
            else
            {
                if (EditorUtility.DisplayDialog("没有主摄像机！", "当前场景中未找到主摄像机。需要主摄像机来创建后期处理体积（Post Processing Volume）。\r\n\r\n选择「继续」创建一个新的。", "继续", "取消"))
                {
                    camera = Helper.CreateMainCamera();
                }
            }

            if (camera != null)
            {
                SetupPostProcessingGenerics(camera);
            }
        }

#if VRC_SDK_VRCSDK3
        private static void SetupWorldPostProcessing(VRC_SceneDescriptor[] descriptors)
        {
            if (EditorUtility.DisplayDialog("设置后期处理？", "这将设置你场景的引用摄像机，并使用附带的示例后期处理配置文件创建一个新的全局体积。", "确定", "取消"))
            {
                var referenceCamera = descriptors.Length > 0 && descriptors[0].ReferenceCamera;

                GameObject camera = null;

                if (!referenceCamera && Camera.main is null)
                {
                    if (EditorUtility.DisplayDialog("没有主摄像机！", "当前场景中未找到主摄像机。需要主摄像机来创建后期处理体积（Post Processing Volume）。\r\n\r\n选择「继续」创建一个新的。", "继续", "取消"))
                    {
                        camera = Helper.CreateMainCamera();

                        descriptors[0].ReferenceCamera = camera;
                    }
                }
                else if (referenceCamera)
                {
                    camera = descriptors[0].ReferenceCamera;
                }
                else if (Camera.main != null)
                {
                    camera = Camera.main.gameObject;
                }

                if (camera != null)
                {
                    descriptors[0].ReferenceCamera = camera;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(descriptors[0]);

                    SetupPostProcessingGenerics(camera);
                }
            }
        }
#endif

        public static void SetupPostProcessingGenerics(GameObject camera)
        {
#if UNITY_POST_PROCESSING_STACK_V2
            //Use PostProcessing layer if it exists otherwise use Water
            var layer = LayerMask.NameToLayer("PostProcessing") > -1 ? "PostProcessing" : "Water";

            //Make sure the Post Process Layer exists and set it up
            if (!camera.GetComponent<PostProcessLayer>())
                camera.AddComponent(typeof(PostProcessLayer));
            var postprocessLayer = camera.GetComponent(typeof(PostProcessLayer)) as PostProcessLayer;
            postprocessLayer.volumeLayer = LayerMask.GetMask(layer);

            //Copy the example profile to the Post Processing folder
            if (!Directory.Exists("Assets/Post Processing"))
                AssetDatabase.CreateFolder("Assets", "Post Processing");
            if (AssetDatabase.LoadAssetAtPath("Assets/Post Processing/SilentProfile.asset", typeof(PostProcessProfile)) == null)
            {
                var path = AssetDatabase.GetAssetPath(Resources.Load("VRWorldToolkit/PostProcessing/SilentProfile"));

                if (path != null)
                {
                    AssetDatabase.CopyAsset(path, "Assets/Post Processing/SilentProfile.asset");
                }
            }

            var profileFound = false;

            //Set up the post process volume
            var volume = Instantiate(PostProcessManager.instance.QuickVolume(16, 100f));
            if (File.Exists("Assets/Post Processing/SilentProfile.asset"))
            {
                volume.sharedProfile = (PostProcessProfile) AssetDatabase.LoadAssetAtPath("Assets/Post Processing/SilentProfile.asset", typeof(PostProcessProfile));
                profileFound = true;
            }

            // Set volume name and layer
            volume.gameObject.name = "Post Processing Volume";
            volume.gameObject.layer = LayerMask.NameToLayer(layer);

            // Mark the scene as dirty for saving
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            // Set the created volume as active selection in hierarchy
            Selection.activeGameObject = volume.gameObject;

            // Notify the user if the default profile was not found during setup
            if (!profileFound)
                EditorUtility.DisplayDialog("未找到默认配置文件！", "设置过程中未找到默认的后期处理配置文件，因此未自动设置到后期处理体积中。\n\n请创建你自己的配置文件来完成设置。", "确定");
#endif
        }
    }
}
