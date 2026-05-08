using UnityEditor;
using UnityEngine;

namespace VRWorldToolkit.Editor
{
    public class CustomEditorManager : MonoBehaviour
    {
        [MenuItem("VRWorld Toolkit/自定义编辑器/启用", false, 3)]
        private static void EnableCustomEditors()
        {
            ScriptingDefineManager.RemoveScriptingDefine("VRWT_DISABLE_EDITORS");
        }

        [MenuItem("VRWorld Toolkit/自定义编辑器/启用", true)]
        private static bool EnableCustomEditorsValidate()
        {
#if VRWT_DISABLE_EDITORS
            return true;
#else
            return false;
#endif
        }

        [MenuItem("VRWorld Toolkit/自定义编辑器/禁用", false, 4)]
        private static void DisableCustomEditors()
        {
            ScriptingDefineManager.AddScriptingDefine("VRWT_DISABLE_EDITORS");
        }

        [MenuItem("VRWorld Toolkit/自定义编辑器/禁用", true)]
        private static bool DisableCustomEditorsValidate()
        {
#if !VRWT_DISABLE_EDITORS
            return true;
#else
            return false;
#endif
        }
    }
}