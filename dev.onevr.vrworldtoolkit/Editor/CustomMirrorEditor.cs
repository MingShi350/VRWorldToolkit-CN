#if VRC_SDK_VRCSDK3
#if !VRWT_DISABLE_EDITORS
using VRC.SDKBase;
using UnityEditor;
using UnityEngine;

namespace VRWorldToolkit.Editor
{
    /// <summary>
    /// Custom editor for VRC_MirrorReflection with added quick actions
    /// </summary>
    [CustomEditor(typeof(VRC_MirrorReflection), true, isFallback = false)]
    [CanEditMultipleObjects]
    public class CustomMirrorEditor : UnityEditor.Editor
    {
        private bool showExplanations;
        private SerializedProperty mirrorMask;

        private void OnEnable()
        {
            mirrorMask = serializedObject.FindProperty("m_ReflectLayers");
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            serializedObject.Update();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("VRWorld Toolkit 扩展功能", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("快速设置反射层：");

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("仅显示玩家")) MirrorLayerChange(262656);

            if (GUILayout.Button("显示玩家/世界")) MirrorLayerChange(264705);

            EditorGUILayout.EndHorizontal();

            if (Selection.gameObjects.Length == 1)
            {
                var currentMirror = (VRC_MirrorReflection) target;

                if ((LightmapSettings.lightProbes != null && LightmapSettings.lightProbes.positions.Length == 0 && currentMirror.m_DisablePixelLights) || (LightmapSettings.lightProbes is null && currentMirror.m_DisablePixelLights))
                    EditorGUILayout.HelpBox("在光照数据中未找到烘焙的光照探针。如果没有烘焙的光照探针，动态对象（如玩家和拾取物）在镜子中将不会显示光照效果。", MessageType.Warning);

                if (mirrorMask.intValue == -1025)
                    EditorGUILayout.HelpBox("此镜子使用的是默认层设置。应禁用不必要的层以节省性能。", MessageType.Info);

                if (Helper.LayerIncludedInMask(LayerMask.NameToLayer("UiMenu"), mirrorMask.intValue))
                    EditorGUILayout.HelpBox("镜子中启用了 UiMenu 层会导致 VRChat UI 元素被渲染两次，在有人的实例中会造成明显的性能下降。", MessageType.Warning);

                if (!Helper.LayerIncludedInMask(LayerMask.NameToLayer("MirrorReflection"), mirrorMask.intValue))
                    EditorGUILayout.HelpBox("禁用 MirrorReflection 层会导致玩家在镜子中看不到自己。", MessageType.Warning);

                if (Helper.LayerIncludedInMask(LayerMask.NameToLayer("PlayerLocal"), mirrorMask.intValue))
                    EditorGUILayout.HelpBox("PlayerLocal 仅用于第一人称视角，不应在镜子中启用。", MessageType.Error);
            }

            showExplanations = EditorGUILayout.Foldout(showExplanations, "VRChat 专用层说明");

            if (showExplanations)
            {
                GUILayout.Label("<b>Player：</b>\n此层用于显示除自己之外的其他玩家。", Styles.RichTextWrap);
                GUILayout.Label("<b>PlayerLocal：</b>\n此层仅用于第一人称视角，不应在镜子中启用。", Styles.RichTextWrap);
                GUILayout.Label("<b>Environment：</b>\n此层用于世界中的静态网格和物体。与 Default 层共享相同的属性。", Styles.RichTextWrap);
                GUILayout.Label("<b>MirrorReflection：</b>\n此层用于在镜子中完整显示你自己。", Styles.RichTextWrap);
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Change selected Reflect Layers on selected VRC_MirrorReflections to the supplied LayerMask value
        /// </summary>
        /// <param name="layerMask">New LayerMask value to set for Reflect Layers</param>
        private void MirrorLayerChange(int layerMask)
        {
            mirrorMask.intValue = layerMask;
        }
    }
}
#endif
#endif