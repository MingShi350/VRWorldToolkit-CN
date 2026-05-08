#if VRC_SDK_VRCSDK3
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDKBase;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VRWorldToolkit.Editor
{
    public class BuildChecksManager : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => 0;

        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            if (requestedBuildType == VRCSDKRequestedBuildType.Scene)
            {
                if (Object.FindObjectsOfType(typeof(VRC_SceneDescriptor)) is VRC_SceneDescriptor[] descriptors && descriptors.Length > 0)
                {
                    var spawnProblems = false;
                    var descriptor = descriptors[0];

                    if (descriptor.spawns != null)
                    {
                        var spawns = descriptor.spawns.Where(s => s != null).ToArray();
                        var spawnsLength = descriptor.spawns.Length;

                        if (spawnsLength != spawns.Length || spawnsLength == 0)
                        {
                            spawnProblems = true;
                        }
                    }
                    else
                    {
                        spawnProblems = true;
                    }

                    if (spawnProblems)
                    {
                        var selection = EditorUtility.DisplayDialogComplex("VRWorld Toolkit：出生点有问题！", "场景描述符中设置了空或无效的出生点。\r\n\r\n出生到空或无效的出生点会导致你被扔回自己的主世界。\r\n\r\n选择「取消构建」如果你想自己修复问题，或按「跳过」忽略问题并继续。",
                            "修复并继续", "取消构建", "跳过");

                        switch (selection)
                        {
                            case 0:
                                WorldDebugger.FixSpawns(descriptor).Invoke();
                                break;
                            case 1:
                                return false;
                        }
                    }

                    if (Object.FindObjectsOfType(typeof(PipelineManager)) is PipelineManager[] pipelines && pipelines.Length > 1)
                    {
                        var selection = EditorUtility.DisplayDialogComplex("VRWorld Toolkit：存在多个管线管理器！", "场景中发现了多个管线管理器（Pipeline Manager）组件。\r\n\r\n这会破坏上传流程，导致你无法加载进入世界。\r\n\r\n选择「取消构建」如果你想自己修复问题，或按「跳过」忽略问题并继续。",
                            "修复并继续", "取消构建", "跳过");

                        switch (selection)
                        {
                            case 0:
                                WorldDebugger.RemoveBadPipelineManagers(pipelines).Invoke();
                                break;
                            case 1:
                                return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}
#endif