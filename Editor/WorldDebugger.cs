#if VRC_SDK_VRCSDK3
#define VRWT_IS_VRC
using VRC.Core;
using VRC.SDKBase;
#endif

#if UNITY_EDITOR_WIN
using Microsoft.Win32;
#endif

#if UNITY_POST_PROCESSING_STACK_V2
using UnityEngine.Rendering.PostProcessing;
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

namespace VRWorldToolkit.Editor
{
    public class WorldDebugger : EditorWindow
    {
        private static Texture badFPS;
        private static Texture goodFPS;
        private static Texture tips;
        private static Texture info;
        private static Texture error;
        private static Texture warning;

        private static bool recheck = true;
        private static bool autoRecheck = true;

        private enum MessageType
        {
            BadFPS = 0,
            GoodFPS = 1,
            Tips = 2,
            Error = 3,
            Warning = 4,
            Info = 5
        }

        static Texture GetDebuggerIcon(MessageType infoType)
        {
            if (!badFPS)
                badFPS = Resources.Load<Texture>("VRWorldToolkit/DebuggerIcons/Bad_FPS_Icon");
            if (!goodFPS)
                goodFPS = Resources.Load<Texture>("VRWorldToolkit/DebuggerIcons/Good_FPS_Icon");
            if (!tips)
                tips = Resources.Load<Texture>("VRWorldToolkit/DebuggerIcons/Performance_Tips");
            if (!info)
                info = Resources.Load<Texture>("VRWorldToolkit/DebuggerIcons/Performance_Info");
            if (!error)
                error = Resources.Load<Texture>("VRWorldToolkit/DebuggerIcons/Error_Icon");
            if (!warning)
                warning = Resources.Load<Texture>("VRWorldToolkit/DebuggerIcons/Warning_Icon");

            switch (infoType)
            {
                case MessageType.BadFPS:
                    return badFPS;
                case MessageType.GoodFPS:
                    return goodFPS;
                case MessageType.Tips:
                    return tips;
                case MessageType.Info:
                    return info;
                case MessageType.Error:
                    return error;
                case MessageType.Warning:
                    return warning;
            }

            return info;
        }

        [Serializable]
        private class SingleMessage
        {
            public string variable;
            public string variable2;
            public GameObject[] selectObjects;
            public Action AutoFix;
            public string assetPath;

            public SingleMessage(string variable)
            {
                this.variable = variable;
            }

            public SingleMessage(string variable, string variable2)
            {
                this.variable = variable;
                this.variable2 = variable2;
            }

            public SingleMessage(GameObject[] objs)
            {
                selectObjects = objs;
            }

            public SingleMessage(GameObject obj)
            {
                selectObjects = new[] { obj };
            }

            public SingleMessage(Action autoFix)
            {
                AutoFix = autoFix;
            }

            public SingleMessage SetSelectObject(GameObject[] objs)
            {
                selectObjects = objs;
                return this;
            }

            public SingleMessage SetSelectObject(GameObject obj)
            {
                selectObjects = new[] { obj };
                return this;
            }

            public SingleMessage SetAutoFix(Action autoFix)
            {
                AutoFix = autoFix;
                return this;
            }

            public SingleMessage SetAssetPath(string path)
            {
                assetPath = path;
                return this;
            }
        }

        [Serializable]
        private class MessageGroup : IEquatable<MessageGroup>
        {
            public readonly string Message;
            public readonly string CombinedMessage;
            public readonly string AdditionalInfo;

            private bool? disableCombinedSelection = null;
            private int? objectCount = null;

            public readonly MessageType MessageType;

            public string Documentation;

            public Action GroupAutoFix;

            public readonly List<SingleMessage> MessageList = new List<SingleMessage>();

            public MessageGroup(string message, MessageType messageType)
            {
                Message = message;
                MessageType = messageType;
            }

            public MessageGroup(string message, string combinedMessage, MessageType messageType)
            {
                Message = message;
                CombinedMessage = combinedMessage;
                MessageType = messageType;
            }

            public MessageGroup(string message, string combinedMessage, string additionalInfo, MessageType messageType)
            {
                Message = message;
                CombinedMessage = combinedMessage;
                AdditionalInfo = additionalInfo;
                MessageType = messageType;
            }

            public MessageGroup SetGroupAutoFix(Action groupAutoFix)
            {
                GroupAutoFix = groupAutoFix;
                return this;
            }

            public MessageGroup SetDocumentation(string documentation)
            {
                Documentation = documentation;
                return this;
            }

            public MessageGroup AddSingleMessage(SingleMessage message)
            {
                MessageList.Add(message);
                return this;
            }

            public int GetTotalCount()
            {
                if (objectCount is null)
                {
                    var count = 0;

                    for (var i = 0; i < MessageList.Count; i++)
                    {
                        var item = MessageList[i];
                        if (item.selectObjects != null)
                        {
                            count += item.selectObjects.Count();
                        }
                        else
                        {
                            if (item.assetPath != null)
                            {
                                count++;
                            }
                        }
                    }

                    objectCount = count;
                }

                return (int)objectCount;
            }

            public bool HasSelectGameObjects()
            {
                if (disableCombinedSelection is null)
                {
                    for (var i = 0; i < MessageList.Count; i++)
                    {
                        var item = MessageList[i];
                        if (item.selectObjects != null && item.selectObjects.Any())
                        {
                            disableCombinedSelection = true;
                        }
                    }

                    if (disableCombinedSelection == null)
                        disableCombinedSelection = false;
                }

                return (bool)disableCombinedSelection;
            }

            public GameObject[] GetSelectObjects()
            {
                var objs = new List<GameObject>();
                foreach (var item in MessageList.Where(o => o.selectObjects != null))
                {
                    objs.AddRange(item.selectObjects);
                }

                return objs.ToArray();
            }

            public string[] GetAssetPaths()
            {
                return MessageList.Where(a => a.assetPath != null).Select(item => item.assetPath).ToArray();
            }

            public Action[] GetSeparateActions()
            {
                return MessageList.Where(m => m.AutoFix != null).Select(m => m.AutoFix).ToArray();
            }

            public bool Buttons()
            {
                return GetSelectObjects().Any() || GetAssetPaths().Any() || GroupAutoFix != null || GetSeparateActions().Any() || GroupAutoFix != null || Documentation != null;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as MessageGroup);
            }

            public bool Equals(MessageGroup other)
            {
                return other != null &&
                       Message == other.Message &&
                       CombinedMessage == other.CombinedMessage &&
                       AdditionalInfo == other.AdditionalInfo &&
                       MessageType == other.MessageType;
            }

            public override int GetHashCode()
            {
                var hashCode = 842570769;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Message);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(CombinedMessage);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AdditionalInfo);
                hashCode = hashCode * -1521134295 + MessageType.GetHashCode();
                return hashCode;
            }

            public static bool operator ==(MessageGroup group1, MessageGroup group2)
            {
                return EqualityComparer<MessageGroup>.Default.Equals(group1, group2);
            }

            public static bool operator !=(MessageGroup group1, MessageGroup group2)
            {
                return !(group1 == group2);
            }
        }

        [Serializable]
        private class MessageCategory
        {
            public string listName;

            [SerializeField] public List<MessageGroup> MessageGroups;
            private Dictionary<int, bool> expandedGroups;
            [SerializeField] public bool disabled;

            public MessageCategory()
            {
                MessageGroups = new List<MessageGroup>();
                expandedGroups = new Dictionary<int, bool>();
            }

            public MessageCategory(string listName)
            {
                MessageGroups = new List<MessageGroup>();
                expandedGroups = new Dictionary<int, bool>();

                this.listName = listName;
            }

            public MessageGroup AddMessageGroup(MessageGroup debuggerMessage)
            {
                MessageGroups.Add(debuggerMessage);

                return debuggerMessage;
            }

            public void ClearMessages()
            {
                MessageGroups.Clear();
            }

            public bool HasMessages()
            {
                var count = 0;

                for (var i = 0; i < MessageGroups.Count; i++)
                {
                    var group = MessageGroups[i];

                    if (group.CombinedMessage != null && group.GetTotalCount() > 0)
                    {
                        count++;
                    }
                    else if (group.CombinedMessage is null)
                    {
                        count++;
                    }
                }

                return count > 0;
            }

            public bool IsExpanded(MessageGroup mg)
            {
                var hash = mg.GetHashCode();
                return expandedGroups.ContainsKey(hash) && expandedGroups[hash];
            }

            public void SetExpanded(MessageGroup mg, bool expanded)
            {
                var hash = mg.GetHashCode();
                if (expandedGroups.ContainsKey(hash))
                {
                    expandedGroups[hash] = expanded;
                }
                else
                {
                    expandedGroups.Add(hash, expanded);
                }
            }
        }

        [Serializable]
        private class MessageCategoryList
        {
            [SerializeField] public List<MessageCategory> messageCategory = new List<MessageCategory>();
            private List<MessageCategory> drawList = new List<MessageCategory>();

            [SerializeField] private Vector2 scrollPos;

            public MessageCategory CreateOrGetCategory(string listName)
            {
                var oldMessageCategory = messageCategory.Find(x => x.listName == listName);

                if (oldMessageCategory is null)
                {
                    var newMessageCategory = new MessageCategory(listName);
                    messageCategory.Add(newMessageCategory);
                    return newMessageCategory;
                }

                return oldMessageCategory;
            }

            public void DrawTabSelector()
            {
                EditorGUILayout.BeginHorizontal();

                for (var i = 0; i < messageCategory.Count; i++)
                {
                    var item = messageCategory[i];

                    var button = "miniButtonMid";

                    if (messageCategory.First() == item)
                    {
                        button = "miniButtonLeft";
                    }
                    else if (messageCategory.Last() == item)
                    {
                        button = "miniButtonRight";
                    }

                    item.disabled = GUILayout.Toggle(item.disabled, item.listName, button);
                }

                EditorGUILayout.EndHorizontal();
            }

            public bool HasCategories()
            {
                return messageCategory.Count > 0;
            }

            public void ClearCategories()
            {
                messageCategory.ForEach(m => m.ClearMessages());
            }

            private const int ButtonWidth = 75;
            private const int ButtonHeight = 20;

            public void DrawMessages()
            {
                if (Event.current.type == EventType.Layout)
                {
                    drawList = messageCategory;
                }

                using (var scrollView = new EditorGUILayout.ScrollViewScope(scrollPos))
                {
                    scrollPos = scrollView.scrollPosition;

                    for (var i = 0; i < drawList.Count; i++)
                    {
                        if (drawList[i].disabled) continue;

                        var group = drawList[i];

                        GUILayout.Label(group.listName, EditorStyles.boldLabel);

                        if (!group.HasMessages())
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                DrawMessage("未找到 " + group.listName + " 的消息。", MessageType.Info);
                            }

                            continue;
                        }

                        for (var l = 0; l < group.MessageGroups.Count; l++)
                        {
                            var messageGroup = group.MessageGroups[l];

                            if (messageGroup.MessageList is null || messageGroup.CombinedMessage != null && messageGroup.MessageList.Count == 0) continue;

                            var singleCombinedMessage = messageGroup.MessageList.Count == 1;
                            var expanded = !singleCombinedMessage && group.IsExpanded(messageGroup);
                            var hasButtons = messageGroup.Buttons();

                            string finalMessage;

                            if (messageGroup.MessageList.Count == 0)
                            {
                                finalMessage = messageGroup.Message;
                            }
                            else
                            {
                                finalMessage = singleCombinedMessage ? string.Format(messageGroup.Message, messageGroup.MessageList[0].variable, messageGroup.MessageList[0].variable2) : string.Format(messageGroup.CombinedMessage ?? string.Empty, messageGroup.GetTotalCount().ToString());
                            }

                            if (messageGroup.AdditionalInfo != null)
                            {
                                finalMessage += " " + messageGroup.AdditionalInfo;
                            }

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                DrawMessage(finalMessage, messageGroup.MessageType);

                                if (hasButtons)
                                {
                                    if (singleCombinedMessage)
                                    {
                                        var message = messageGroup.MessageList[0];
                                        DrawButtons(message.selectObjects, messageGroup.Documentation, message.assetPath, message.AutoFix, messageGroup.HasSelectGameObjects());
                                    }
                                    else
                                    {
                                        DrawButtons(messageGroup.GetSelectObjects(), messageGroup.Documentation, null, messageGroup.GroupAutoFix, messageGroup.HasSelectGameObjects());
                                    }
                                }
                            }

                            if (messageGroup.MessageList.Count > 1)
                            {
                                expanded = EditorGUILayout.Foldout(expanded, "显示单独消息");
                                group.SetExpanded(messageGroup, expanded);

                                if (!expanded) continue;

                                for (var j = 0; j < messageGroup.MessageList.Count; j++)
                                {
                                    var message = messageGroup.MessageList[j];

                                    var finalSingleMessage = string.Format(messageGroup.Message, message.variable, message.variable2);

                                    using (new EditorGUILayout.HorizontalScope())
                                    {
                                        DrawPaddedMessage(finalSingleMessage);
                                        DrawButtons(message.selectObjects, null, message.assetPath, message.AutoFix, true);
                                    }
                                }

                                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                            }
                        }
                    }

                    GUILayout.FlexibleSpace();
                }

                void DrawPaddedMessage(string messageText)
                {
                    var box = new GUIContent(messageText);
                    GUILayout.Box(box, Styles.HelpBoxPadded, GUILayout.ExpandHeight(true), GUILayout.MinWidth(EditorGUIUtility.currentViewWidth - 116));
                }

                void DrawMessage(string messageText, MessageType type)
                {
                    var box = new GUIContent(messageText, GetDebuggerIcon(type));
                    GUILayout.Box(box, Styles.HelpBoxRichText, GUILayout.ExpandHeight(true), GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 18));
                }

                void DrawButtons(GameObject[] selectObjects, string infoLink, string assetPath, Action autoFix, bool hasGameObjects)
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        var infoLinkSet = infoLink != null;
                        var autoFixSet = autoFix != null;
                        var assetPathSet = assetPath != null;

                        if (infoLinkSet && GUILayout.Button("更多信息", GUILayout.Width(ButtonWidth), GUILayout.Height(ButtonHeight)))
                        {
                            Application.OpenURL(infoLink);
                        }

                        if (!infoLinkSet || assetPathSet || hasGameObjects)
                        {
                            using (new EditorGUI.DisabledScope(!assetPathSet && !hasGameObjects))
                            {
                                if (assetPathSet)
                                {
                                    if (GUILayout.Button("定位资源", GUILayout.Width(ButtonWidth), GUILayout.Height(ButtonHeight)))
                                    {
                                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(assetPath));
                                    }
                                }
                                else
                                {
                                    if (GUILayout.Button("选中", GUILayout.Width(ButtonWidth), GUILayout.Height(ButtonHeight)))
                                    {
                                        if (selectObjects != null)
                                        {
                                            Selection.objects = selectObjects;
                                        }
                                    }
                                }
                            }
                        }

                        if (!(infoLinkSet && (assetPathSet || hasGameObjects)))
                        {
                            using (new EditorGUI.DisabledScope(!autoFixSet))
                            {
                                if (GUILayout.Button("自动修复", GUILayout.Width(ButtonWidth), GUILayout.Height(ButtonHeight)))
                                {
                                    autoFix?.Invoke();

                                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

                                    autoRecheck = true;
                                    recheck = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        [SerializeField] private int tab;

        [MenuItem("VRWorld Toolkit/世界调试器", false, 25)]
        public static void ShowWindow()
        {
            var window = GetWindow(typeof(WorldDebugger));
            window.titleContent = new GUIContent("世界调试器");
            window.minSize = new Vector2(520, 600);
            window.Show();
        }

        #region Actions

        public static Action SelectAsset(GameObject obj)
        {
            return () => { Selection.activeObject = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(obj)); };
        }

        public static Action SetGenerateLightmapUV(ModelImporter importer)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("启用光照贴图 UV 生成？", "此操作将在网格 \"" + Path.GetFileName(AssetDatabase.GetAssetPath(importer)) + "\" 上启用光照贴图 UV 生成。\n\n是否继续？", "是", "取消"))
                {
                    importer.generateSecondaryUV = true;
                    importer.SaveAndReimport();
                }
            };
        }

        public static Action SetGenerateLightmapUV(List<ModelImporter> importers)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("启用光照贴图 UV 生成？", "此操作将在 " + importers.Count + " 个网格上启用光照贴图 UV 生成。\n\n是否继续？", "是", "取消"))
                {
                    importers.ForEach(i =>
                    {
                        i.generateSecondaryUV = true;
                        i.SaveAndReimport();
                    });
                }
            };
        }

#if VRWT_IS_VRC
        public static Action RemoveBadPipelineManagers(PipelineManager[] pipelineManagers)
        {
            return () =>
            {
                foreach (var pipelineManager in pipelineManagers)
                {
                    if (pipelineManager.gameObject.GetComponent<VRC_SceneDescriptor>())
                        continue;

                    DestroyImmediate(pipelineManager.gameObject.GetComponent<PipelineManager>());
                }
            };
        }
#endif

        public static Action SetLegacyBlendShapeNormals(ModelImporter importer)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("启用 Legacy Blend Shape Normals？", "此操作将在模型 \"" + Path.GetFileName(AssetDatabase.GetAssetPath(importer)) + "\" 上启用 Legacy Blend Shape Normals。\n\n是否继续？", "是", "取消"))
                {
                    ModelImporterUtil.SetLegacyBlendShapeNormals(importer, true);
                    importer.SaveAndReimport();
                }
            };
        }

        public static Action SetLegacyBlendShapeNormals(ModelImporter[] importers)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("启用 Legacy Blend Shape Normals？", "此操作将在 " + importers.Length + " 个模型上启用 Legacy Blend Shape Normals。根据模型数量和大小，这可能需要一些时间。\n\n是否继续？", "是", "取消"))
                {
                    for (var i = 0; i < importers.Length; i++)
                    {
                        ModelImporterUtil.SetLegacyBlendShapeNormals(importers[i], true);
                        importers[i].SaveAndReimport();
                    }
                }
            };
        }

        public static Action DisableComponent(Behaviour behaviour)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("禁用组件？", "此操作将禁用游戏对象 \"" + behaviour.gameObject.name + "\" 上的 " + behaviour.GetType() + " 组件。\n\n是否继续？", "是", "取消"))
                {
                    Undo.RegisterCompleteObjectUndo(behaviour, "Disable Component");
                    behaviour.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                }
            };
        }

        public static Action DisableComponent(Behaviour[] behaviours)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("禁用组件？", "此操作将禁用 " + behaviours.Count().ToString() + " 个游戏对象上的 " + behaviours[0].GetType() + " 组件。\n\n是否继续？", "是", "取消"))
                {
                    Undo.RegisterCompleteObjectUndo(behaviours.ToArray<Object>(), "Mass Disable Components");

                    for (var i = 0; i < behaviours.Length; i++)
                    {
                        var b = behaviours.ToList()[i];
                        b.enabled = false;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(b);
                    }
                }
            };
        }

        public static Action SetObjectLayer(GameObject obj, string layer)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("更改层？", "此操作将把 " + obj.name + " 的层更改为 " + layer + "。\n\n是否继续？", "是", "取消"))
                {
                    Undo.RegisterCompleteObjectUndo(obj, "Layer Change");
                    obj.layer = LayerMask.NameToLayer(layer);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
                }
            };
        }

        public static Action SetObjectLayer(GameObject[] objs, string layer)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("更改层？", "此操作将把 " + objs.Length + " 个游戏对象的层更改为 " + layer + "。\n\n是否继续？", "是", "取消"))
                {
                    Undo.RegisterCompleteObjectUndo(objs.ToArray<Object>(), "Mass Layer Change");

                    for (var index = 0; index < objs.ToList().Count; index++)
                    {
                        var o = objs.ToList()[index];
                        o.layer = LayerMask.NameToLayer(layer);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(o);
                    }
                }
            };
        }

        public static Action SetSelectableNavigationMode(Selectable selectable, Navigation.Mode mode)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("更改导航模式？", "此操作将把 UI 元素 \"" + selectable.gameObject.name + "\" 的导航模式更改为 " + mode.ToString() + "。\n\n是否继续？", "是", "取消"))
                {
                    Undo.RegisterCompleteObjectUndo(selectable, "Navigation Mode Change");

                    var navigation = selectable.navigation;

                    navigation.mode = Navigation.Mode.None;

                    selectable.navigation = navigation;

                    PrefabUtility.RecordPrefabInstancePropertyModifications(selectable);
                }
            };
        }

        public static Action SetSelectableNavigationMode(Selectable[] selectables, Navigation.Mode mode)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("更改导航模式？", "此操作将把 " + selectables.Length + " 个 UI 元素的导航模式更改为 " + mode.ToString() + "。\n\n是否继续？", "是", "取消"))
                {
                    Undo.RegisterCompleteObjectUndo(selectables.ToArray<Object>(), "Mass Navigation Mode Change");

                    for (var i = 0; i < selectables.Length; i++)
                    {
                        var navigation = selectables[i].navigation;

                        navigation.mode = Navigation.Mode.None;

                        selectables[i].navigation = navigation;

                        PrefabUtility.RecordPrefabInstancePropertyModifications(selectables[i]);
                    }
                }
            };
        }

        public static Action SetScrollRectScrollSensitivity(ScrollRect scrollRect, float scrollSensitivity)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("更改滚动灵敏度？", "此操作将把 ScrollRect 组件 \"" + scrollRect.gameObject.name + "\" 的滚动灵敏度更改为 " + scrollSensitivity + "。\n\n是否继续？", "是", "取消"))
                {
                    Undo.RegisterCompleteObjectUndo(scrollRect, "ScrollRect Scroll Sensitivity Change");

                    scrollRect.scrollSensitivity = scrollSensitivity;

                    PrefabUtility.RecordPrefabInstancePropertyModifications(scrollRect);
                }
            };
        }

        public static Action SetScrollRectScrollSensitivity(ScrollRect[] scrollRects, float scrollSensitivity)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("更改滚动灵敏度？", "此操作将把 " + scrollRects.Length + " 个 ScrollRect 组件的滚动灵敏度更改为 " + scrollSensitivity + "。\n\n是否继续？", "是", "取消"))
                {
                    Undo.RegisterCompleteObjectUndo(scrollRects.ToArray<Object>(), "Mass ScrollRect Scroll Sensitivity Change");

                    for (var i = 0; i < scrollRects.Length; i++)
                    {
                        var scrollRect = scrollRects[i];

                        scrollRect.scrollSensitivity = scrollSensitivity;

                        PrefabUtility.RecordPrefabInstancePropertyModifications(scrollRects[i]);
                    }
                }
            };
        }

        public static Action SetParticleSystemAllowRoll(ParticleSystemRenderer particleSystemRenderer, bool allowRoll)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("更改粒子系统 Allow Roll？", "此操作将把粒子系统 \"" + particleSystemRenderer.gameObject.name + "\" 的 Allow Roll 更改为 " + allowRoll + "。\n\n是否继续？", "是", "取消"))
                {
                    Undo.RegisterCompleteObjectUndo(particleSystemRenderer, "Particle System Allow Roll Change");

                    particleSystemRenderer.allowRoll = allowRoll;

                    PrefabUtility.RecordPrefabInstancePropertyModifications(particleSystemRenderer);
                }
            };
        }

        public static Action SetParticleSystemAllowRoll(ParticleSystemRenderer[] particleSystemRenderers, bool allowRoll)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("更改粒子系统 Allow Roll？", "此操作将把 " + particleSystemRenderers.Length + " 个粒子系统的 Allow Roll 更改为 " + allowRoll + "。\n\n是否继续？", "是", "取消"))
                {
                    Undo.RegisterCompleteObjectUndo(particleSystemRenderers.ToArray<Object>(), "Mass Particle System Allow Roll Change");

                    for (var i = 0; i < particleSystemRenderers.Length; i++)
                    {
                        var particleSystemRenderer = particleSystemRenderers[i];

                        particleSystemRenderer.allowRoll = allowRoll;

                        PrefabUtility.RecordPrefabInstancePropertyModifications(particleSystemRenderer);
                    }
                }
            };
        }

        public static Action SetLightmapSize(int newSize)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("更改光照贴图尺寸？", "此操作将把光照贴图尺寸从 " + Lightmapping.lightingSettings.lightmapMaxSize + " 更改为 " + newSize + "。\n\n是否继续？", "是", "取消"))
                {
                    Lightmapping.lightingSettings.lightmapMaxSize = newSize;
                }
            };
        }

        public static Action SetLightmapOverrideForAndroid(TextureImporter[] textureImporters)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("设置光照贴图压缩覆盖？", "此操作将为所有光照贴图（" + textureImporters.Length + " 个）设置 Android 平台的 ASTC 4x4 块格式覆盖。\n\n警告：根据光照贴图的大小和数量，这可能需要一些时间。\n\n是否继续？", "是", "取消"))
                {
                    foreach (var item in textureImporters)
                    {
                        var settings = item.GetPlatformTextureSettings("Android");

                        settings.overridden = true;

                        settings.format = TextureImporterFormat.ASTC_4x4;

                        item.SetPlatformTextureSettings(settings);

                        item.SaveAndReimport();
                    }
                }
            };
        }

        public static Action SetLightmapOverrideForAndroid(TextureImporter textureImporter, string lightmapName)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("设置光照贴图压缩覆盖？", "此操作将为 \"" + lightmapName + "\" 设置 Android 平台的 ASTC 4x4 块格式覆盖。\n\n警告：根据光照贴图的大小，这可能需要一些时间。\n\n是否继续？", "是", "取消"))
                {
                    var settings = textureImporter.GetPlatformTextureSettings("Android");

                    settings.overridden = true;

                    settings.format = TextureImporterFormat.ASTC_4x4;

                    textureImporter.SetPlatformTextureSettings(settings);

                    textureImporter.SaveAndReimport();
                }
            };
        }

        public static Action SetEnviromentReflections(DefaultReflectionMode reflections)
        {
            return () => { RenderSettings.defaultReflectionMode = reflections; };
        }

        public static Action SetAmbientMode(AmbientMode ambientMode)
        {
            return () => { RenderSettings.ambientMode = ambientMode; };
        }

        public static Action SetGameObjectTag(GameObject obj, string tag)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("更改标签？", "此操作将把 " + obj.name + " 的标签更改为 " + tag + "。\n\n是否继续？", "是", "取消"))
                {
                    Undo.RegisterCompleteObjectUndo(obj, "Change Tag");
                    obj.tag = tag;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
                }
            };
        }

        public static Action SetGameObjectTag(GameObject[] objs, string tag)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("更改标签？", "此操作将把 " + objs.Length + " 个游戏对象的标签更改为 " + tag + "。\n\n是否继续？", "是", "取消"))
                {
                    Undo.RegisterCompleteObjectUndo(objs.ToArray<Object>(), "Mass Change Tag");

                    for (var i = 0; i < objs.ToList().Count; i++)
                    {
                        var o = objs.ToList()[i];
                        o.tag = tag;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(o);
                    }
                }
            };
        }

        public static Action ChangeShader(Material material, string shader)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("更改着色器？", "此操作将把材质 " + material.name + " 的着色器更改为 " + shader + "。\n\n是否继续？", "是", "取消"))
                {
                    var standard = Shader.Find(shader);
                    Undo.RegisterCompleteObjectUndo(material, "Changed Shader");
                    material.shader = standard;
                }
            };
        }

        public static Action ChangeShader(Material[] materials, string shader)
        {
            return () =>
            {
                if (EditorUtility.DisplayDialog("更改着色器？", "此操作将把 " + materials.Length + " 个材质的着色器更改为 " + shader + "。\n\n是否继续？", "是", "取消"))
                {
                    var newShader = Shader.Find(shader);
                    Undo.RegisterCompleteObjectUndo(materials.ToArray<Object>(), "Changed Shaders");
                    materials.ToList().ForEach(m => m.shader = newShader);
                }
            };
        }

        public static Action RemoveOverlappingLightProbes(LightProbeGroup lightProbeGroup)
        {
            return () =>
            {
                Undo.RegisterCompleteObjectUndo(lightProbeGroup, "Removed Overlapping Light Probes");
                if (EditorUtility.DisplayDialog("移除重叠的光照探针？", "此操作将移除光照探针组 \"" + lightProbeGroup.gameObject.name + "\" 中的所有重叠光照探针。\n\n是否继续？", "是", "取消"))
                {
                    lightProbeGroup.probePositions = lightProbeGroup.probePositions.Distinct().ToArray();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(lightProbeGroup);
                }
            };
        }

        public static Action RemoveOverlappingLightProbes(LightProbeGroup[] lightProbeGroups)
        {
            return () =>
            {
                Undo.RegisterCompleteObjectUndo(lightProbeGroups, "Removed Overlapping Light Probes");
                if (EditorUtility.DisplayDialog("移除重叠的光照探针？", "此操作将移除当前场景中发现的所有重叠光照探针。\n\n是否继续？", "是", "取消"))
                {
                    foreach (var lpg in lightProbeGroups)
                    {
                        lpg.probePositions = lpg.probePositions.Distinct().ToArray();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(lpg);
                    }
                }
            };
        }

        public static Action RemoveRedundantLightProbes(LightProbeGroup[] lightProbeGroups)
        {
            return () =>
            {
                if (LightmapSettings.lightProbes != null)
                {
                    var probes = LightmapSettings.lightProbes.positions;
                    if (EditorUtility.DisplayDialog("移除冗余的光照探针？", "此操作将尝试移除当前场景中的所有冗余光照探针。在执行此操作前请先烘焙光照，以避免误删正确的光照探针。\n\n是否继续？", "是", "取消"))
                    {
                        foreach (var lpg in lightProbeGroups)
                        {
                            lpg.probePositions = lpg.probePositions.Distinct().Where(p => !probes.Contains(p)).ToArray();
                        }
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("未找到已烘焙的光照探针！", "请先烘焙光照，然后再尝试移除冗余的光照探针。", "确定");
                }
            };
        }

        public static Action ClearOcclusionCache(long fileCount)
        {
            return async () =>
            {
                if (EditorUtility.DisplayDialog("清除遮挡剔除缓存？", "这将清除你的遮挡剔除缓存，当前有 " + fileCount + " 个文件。删除大量文件可能需要一些时间。\n\n是否继续？", "是", "取消"))
                {
                    long deleteCount = 0;

                    var tokenSource = new CancellationTokenSource();

                    var deleteFiles = new Progress<string>(fileName =>
                    {
                        deleteCount++;
                        if (EditorUtility.DisplayCancelableProgressBar("正在清除遮挡剔除缓存", fileName, (float)deleteCount / fileCount))
                        {
                            tokenSource.Cancel();
                        }
                    });

                    var token = tokenSource.Token;

                    await Task.Run(() => DeleteFiles(deleteFiles, token), token);
                    EditorUtility.ClearProgressBar();

                    occlusionCacheFiles = 0;
                    EditorUtility.DisplayDialog("文件已删除", "已删除 " + deleteCount + " 个文件。", "确定");
                }
            };
        }

        public static void DeleteFiles(IProgress<string> deleted, CancellationToken cancellationToken)
        {
            Parallel.ForEach(Directory.EnumerateFiles("Library/Occlusion/"), (file, state) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    state.Break();
                }

                File.Delete(file);
                deleted.Report(file);
            });
        }

#if VRWT_IS_VRC
        public static Action FixSpawns(VRC_SceneDescriptor descriptor)
        {
            return () =>
            {
                Undo.RegisterCompleteObjectUndo(descriptor, "Spawn Points Fixed");
                if (descriptor.spawns is null || descriptor.spawns.Length == 0)
                {
                    descriptor.spawns = new[] { descriptor.gameObject.transform };
                }

                descriptor.spawns = descriptor.spawns.Where(c => c != null).ToArray();

                PrefabUtility.RecordPrefabInstancePropertyModifications(descriptor);
            };
        }

        public static Action ChangeRespawnHeight(VRC_SceneDescriptor descriptor, float newHeight)
        {
            return () =>
            {
                Undo.RegisterCompleteObjectUndo(descriptor, "Respawn Height Change");

                descriptor.RespawnHeightY = newHeight;

                PrefabUtility.RecordPrefabInstancePropertyModifications(descriptor);
            };
        }
#endif

        public static Action SanitizeBuildPath()
        {
            return () =>
            {
                PlayerSettings.companyName = UnityWebRequest.UnEscapeURL(PlayerSettings.companyName).Trim();
                PlayerSettings.productName = UnityWebRequest.UnEscapeURL(PlayerSettings.productName).Trim();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            };
        }

        public static Action SetErrorPause(bool enabled)
        {
            return () => { ConsoleFlagUtil.SetConsoleErrorPause(enabled); };
        }

#if VRWT_IS_VRC
        public static Action SetVRChatLayers()
        {
            return UpdateLayers.SetupEditorLayers;
        }

        public static Action SetVRChatCollisionMatrix()
        {
            return UpdateLayers.SetupCollisionLayerMatrix;
        }

        public static Action SetReferenceCamera(VRC_SceneDescriptor descriptor, Camera camera)
        {
            return () =>
            {
                Undo.RegisterCompleteObjectUndo(descriptor, "Reference Camera Set");
                descriptor.ReferenceCamera = camera.gameObject;
                PrefabUtility.RecordPrefabInstancePropertyModifications(descriptor);
            };
        }
#endif

        public enum RemovePpEffect
        {
            AmbientOcclusion = 0,
            ScreenSpaceReflections = 1,
            BloomDirt = 2
        }

#if UNITY_POST_PROCESSING_STACK_V2
        public static Action DisablePostProcessEffect(PostProcessProfile postprocessProfile, RemovePpEffect effect)
        {
            return () =>
            {
                switch (effect)
                {
                    case RemovePpEffect.AmbientOcclusion:
                        postprocessProfile.GetSetting<AmbientOcclusion>().active = false;
                        break;
                    case RemovePpEffect.ScreenSpaceReflections:
                        postprocessProfile.GetSetting<ScreenSpaceReflections>().active = false;
                        break;
                    case RemovePpEffect.BloomDirt:
                        postprocessProfile.GetSetting<Bloom>().dirtTexture.overrideState = false;
                        postprocessProfile.GetSetting<Bloom>().dirtIntensity.overrideState = false;
                        break;
                }
            };
        }

        public static Action SetPostProcessingInScene(SceneView.SceneViewState sceneViewState, bool isActive)
        {
            return () => { sceneViewState.showImageEffects = isActive; };
        }

        public static Action SetPostProcessingLayerResources(PostProcessLayer postProcessLayer, PostProcessResources resources)
        {
            return () => { postProcessLayer.Init(resources); };
        }
#endif

        #endregion

        #region Texts

        private const string NoSceneDescriptor = "当前场景没有场景描述符(Scene Descriptor)。请添加一个,或将 VRCWorld 预制体拖入场景中。";

        private const string TooManySceneDescriptors = "发现多个场景描述符。一个场景中只能存在一个场景描述符。";

        private const string TooManyPipelineManagers = "当前场景中存在多个管线管理器(Pipeline Manager)。这会破坏世界上传流程,可能导致你无法加载进入世界。";

        private const string WorldDescriptorFar = "场景描述符距离 Unity 原点 {0} 个单位。世界中心离原点这么远会导致模型出现明显的抖动。建议将你的世界移近场景原点。";

        private const string WorldDescriptorOff = "场景描述符距离 Unity 原点 {0} 个单位。通常建议尽可能靠近绝对零点,以避免浮点精度误差。";

        private const string DifferingSanitizedBuildPath = "上次构建路径与 VRCSDK 看到的路径不一致。某些字符仅在「构建并发布」过程中被去除时会导致此问题。构建路径由项目 Player Settings 中的公司名和产品名组成。";

        private const string LastBuildFailed = "上次构建失败!请检查控制台中的编译错误以找到原因。如果错误脚本在 SDK 中,请尝试重新导入。否则请移除或更新有问题的资源。";

        private const string NoSpawnPointSet = "场景描述符中没有设置出生点(Spawn Point)。出生到没有出生点的世界会导致你被扔回主世界。";

        private const string NullSpawnPoint = "场景描述符中设置了空出生点。出生到空出生点会导致你被扔回主世界。";

        private const string ReferenceCameraClearFlagsNotSkyboxOrColor = "当前引用摄像机的清除标志未设置为天空盒(Skybox)或纯色(Solid Color)。这可能在游戏中导致渲染问题。";

        private const string ReferenceCameraClippingPlaneRatio = "引用摄像机的近裁剪面({0})和远裁剪面({1})之间的比率过高,可能在游戏中导致渲染问题。";

        private const string ReferenceCameraNearClipPlaneOver = "当前引用摄像机的近裁剪面值为 {0}。此值将被限制在 0.01 到 0.05 之间。";

        private const string NoReferenceCameraSetGeneral = "场景描述符中未设置引用摄像机。使用引用摄像机可以通过调整摄像机的近远裁剪面来改变世界的渲染距离。";

        private const string ReferenceCameraHasNoCameraComponent = "场景描述符中设置为引用摄像机的游戏对象「{0}」没有摄像机组件。这可能在游戏中导致各种问题。";

        private const string ColliderUnderSpawnIsTrigger = "出生点 {1} 下方的碰撞体「{0}」已设置为触发器(Is Trigger)。";
        private const string ColliderUnderSpawnIsTriggerCombined = "发现 {0} 个出生点下方有设置为触发器的碰撞体。";
        private const string ColliderUnderSpawnIsTriggerInfo = "出生到一个脚下无站立地面的世界会导致玩家不停坠落。";

        private const string SpawnUnderRespawnHeight = "出生点「{0}」位于场景描述符设置的重生高度下方 {1} 个单位处。";
        private const string SpawnUnderRespawnHeightCombined = "发现 {0} 个出生点在场景描述符设置的重生高度下方。";
        private const string SpawnUnderRespawnHeightInfo = "出生在重生高度下方会导致玩家无限卡住重生。";

        private const string NoColliderUnderSpawn = "出生点「{0}」下方没有碰撞体。";
        private const string NoColliderUnderSpawnCombined = "发现 {0} 个出生点下方无碰撞体。";
        private const string NoColliderUnderSpawnInfo = "出生到一个脚下无站立地面的世界会导致玩家不停坠落。";

        private const string RespawnHeightAboveCollider = "出生点「{1}」下方的碰撞体位于场景描述符设置的重生高度之下。";
        private const string RespawnHeightAboveColliderCombined = "发现 {0} 个出生点的碰撞体位于重生高度之下。";
        private const string RespawnHeightAboveColliderInfo = "这将导致玩家无限卡住重生。";

        private const string MirrorONByDefault = "镜子「{0}」默认处于开启状态。";
        private const string MirrorONByDefaultCombined = "场景中有 {0} 面镜子默认处于开启状态。";
        private const string MirrorONByDefaultInfo = "这是非常不好的做法。世界中的任何镜子默认都应该关闭。";

        private const string MirrorWithDefaultLayers = "镜子「{0}」使用的是默认反射层设置。";
        private const string MirrorWithDefaultLayersCombined = "你有 {0} 面镜子使用的是默认反射层设置。";
        private const string MirrorWithDefaultLayersInfo = "只启用镜子实际需要的层可以节省大量帧率,尤其是在有人的实例中。";

        private const string LegacyBlendShapeIssues = "发现带蒙皮的网格渲染器,模型 {0}({1})未启用 Legacy Blend Shape Normals。";
        private const string LegacyBlendShapeIssuesCombined = "发现 {0} 个模型未启用 Legacy Blend Shape Normals。";
        private const string LegacyBlendShapeIssuesInfo = "这会显著增加世界体积。";

        private const string BakedOcclusionCulling = "发现已烘焙的遮挡剔除(Occlusion Culling)。";

        private const string NoOcclusionAreas = "未发现遮挡区域(Occlusion Area)。建议添加遮挡区域,帮助在摄像机可能位于的位置生成更高精度的数据。如果不设置,系统会自动创建一个包含所有遮挡物和被遮挡物的区域。";

        private const string DisabledOcclusionArea = "遮挡区域 {0} 发现已禁用「Is View Volume」。";
        private const string DisabledOcclusionAreaCombined = "发现已禁用「Is View Volume」的遮挡区域。";
        private const string DisabledOcclusionAreaInfo = "如果不启用此项,遮挡区域将不会被用于遮挡烘焙。";

        private const string NoOcclusionCulling = "当前场景没有烘焙遮挡剔除。遮挡剔除通常会带来显著的性能提升,尤其是在有多个房间或区域的大型世界中。";

        private const string OcclusionCullingCacheWarning = "当前项目的遮挡剔除缓存中有 {0} 个文件。当缓存文件过多时,烘焙遮挡剔除会比预期花费更长时间。可以安全清除,不会有不良影响。";

        private const string ActiveCameraOutputtingToRenderTexture = "活动摄像机「{0}」正在输出到渲染纹理(Render Texture)。";
        private const string ActiveCameraOutputtingToRenderTextureCombined = "当前场景中有 {0} 个活动摄像机正在输出到渲染纹理。";
        private const string ActiveCameraOutputtingToRenderTextureInfo = "这会对性能产生负面影响,因为会导致更多的绘制调用。它们应该只在需要时启用。";

        private const string ActiveCameraWithOverZeroDepth = "活动摄像机「{0}」目标显示器为 1,渲染深度超过 0。";
        private const string ActiveCameraWithOverZeroDepthCombined = "当前场景中有 {0} 个活动摄像机目标显示器为 1,渲染深度超过 0。";
        private const string ActiveCameraWithOverZeroDepthInfo = "这会导致它在上传界面上渲染,阻止你完成上传。";

        private const string NoToonShaders = "构建世界时应避免使用卡通着色器(Toon Shader),因为它们缺少构建世界所需的关键功能。推荐使用 Standard 着色器。";

        private const string NonCrunchedTextures = "场景中 {0}% 的纹理未经过 Crunch 压缩。Crunch 压缩可以显著减少世界下载大小。可以在纹理的导入设置中找到此选项。";

        private const string SingleColorEnvironmentLighting = "建议将环境光照源从单色(Color)改为渐变(Gradient),以获得更好的环境光照效果。";

        private const string DarkEnvironmentLighting = "使用过暗的环境光照颜色会让化身看起来不自然。仅在世界本身光线较暗的情况下才使用暗色环境光照。";

        private const string CustomEnvironmentReflectionsNull = "当前场景的环境反射已设置为自定义(Custom),但未定义自定义立方体贴图(Cubemap)。";

        private const string NoLightmapUV = "场景中的模型「{0}」设置为可接受光照贴图,但没有光照贴图 UV。";
        private const string NoLightmapUVCombined = "当前场景中有 {0} 个设置为接受光照贴图但没有光照贴图 UV 的模型。";
        private const string NoLightmapUVInfo = "如果主 UV 不适合光照贴图,在烘焙光照时可能会出问题。你可以在模型的导入设置中启用生成光照贴图 UV。";

        private const string LightsNotBaked = "当前场景使用的是实时光照。建议使用烘焙光照以获得更好的性能。";

        private const string NoLightingSettingsAsset = "当前场景使用烘焙光照,但没有光照设置资源(Lighting Settings Asset)。";

        private const string ConsiderLargerLightmaps = "检测到可能未优化的光照设置:独立光照贴图数量较多,而当前设置的光照贴图尺寸相对较小。\n建议将光照贴图尺寸从 {0} 增加到 2048 或更大,并调整网格渲染器上的「Scale In Lightmap」值以在更少的光照贴图上容纳更多内容。";

        private const string ConsiderSmallerLightmaps = "使用 GPU Progressive 烘焙 4096 分辨率的光照贴图会静默回退到 CPU Progressive。烤 4K 光照贴图需要超过 12GB GPU 显存。";

        private const string NonBakedBakedLight = "灯光 {0} 设置为烘焙/混合模式,但尚未烘焙!";
        private const string NonBakedBakedLightCombined = "场景中有 {0} 个设置为烘焙/混合模式但尚未烘焙的灯光!";
        private const string NonBakedBakedLightInfo = "尚未烘焙的烘焙灯光在游戏中会作为实时光照运行。";

        private const string LightingDataAssetInfo = "当前场景的光照数据资源占用了世界体积的 {0} MB。它包含场景的光照信息,如光照探针数据和实时全局光照数据。";

        private const string NoLightingDataAsset = "你似乎在使用烘焙光照,但当前场景没有光照数据资源。请烘焙光照以生成一个。";

        private const string NoLightProbes = "当前场景中未找到光照探针(Light Probe)。没有光照探针,烘焙灯光将无法影响动态对象(如玩家和拾取物)。";

        private const string LightProbeCountNotBaked = "当前场景包含 {0} 个光照探针,但其中 {1} 个尚未烘焙。";

        private const string LightProbesRemovedNotReBaked = "上次烘焙后移除了一些光照探针。请重新烘焙以更新场景的光照数据。光照数据中包含 {0} 个已烘焙的光照探针,而当前场景有 {1} 个光照探针。";

        private const string LightProbeCount = "当前场景包含 {0} 个已烘焙的光照探针。";

        private const string OverlappingLightProbes = "光照探针组「{0}」有 {1} 个重叠的光照探针。";
        private const string OverlappingLightProbesCombined = "发现 {0} 个有重叠光照探针的光照探针组。";
        private const string OverlappingLightProbesInfo = "这些探针不会被烘焙,因为 Unity 会跳过任何额外重叠的探针,并可能在编辑器中造成不必要的性能下降。";

        private const string NoReflectionProbes = "当前场景中没有活动的反射探针(Reflection Probe)。反射探针是让反射材质获得正确反射效果所必需的。";

        private const string ReflectionProbesSomeUnbaked = "反射探针「{0}」未烘焙。";
        private const string ReflectionProbesSomeUnbakedCombined = "当前场景中有 {0} 个未烘焙的反射探针。";

        private const string ReflectionProbeCountText = "当前场景有 {0} 个反射探针。";

        private const string PostProcessingImportedButNotSetup = "当前项目已导入后期处理(Post Processing),但你尚未进行设置。";

        private const string PostProcessingNotSupported = "针对当前所选平台构建时,VRChat 不支持后期处理。";

        private const string PostProcessingGenericProjectNotice = "后期处理检查目前尚未针对没有 VRChat Worlds SDK 的项目实现,因为它是为 VRChat 内容创建设计的。";

        private const string PostProcessingDisabledInSceneView = "后期处理在场景视图中被禁用。你需要先启用它才能预览后期处理效果。";

        private const string PostProcessingNoResourcesSet = "「{0}」上的后期处理层(Post Process Layer)的资源字段未正确设置。这会导致后期处理报错。可以通过在游戏对象上重新创建后期处理层来修复。";

        private const string NoReferenceCameraSetPp = "当前场景的场景描述符未设置引用摄像机。没有引用摄像机,后期处理在游戏中将不可见。";

        private const string NoPostProcessingVolumes = "场景中未找到启用的后期处理体积(Post Processing Volume)。需要后期处理体积来对摄像机的后期处理层应用效果。";

        private const string ReferenceCameraNoPostProcessingLayer = "当前引用摄像机上没有后期处理层。需要后期处理层才能让后期处理体积影响摄像机。";

        private const string PostProcessLayerUsingReservedLayer = "你当前的后期处理层使用了 VRChat 的保留层之一。使用这些层会在游戏中破坏后期处理。";

        private const string VolumeBlendingLayerNotSet = "你没有在后期处理层中设置体积混合层(Volume Blending Layer),因此后期处理不会工作。建议使用 Water 或 PostProcessing 层。";

        private const string PostProcessingVolumeNotGlobalNoCollider = "后期处理体积「{0}」未标记为全局(Global),且没有碰撞体。";
        private const string PostProcessingVolumeNotGlobalNoColliderCombined = "发现 {0} 个未标记为全局且没有碰撞体的后期处理体积。";
        private const string PostProcessingVolumeNotGlobalNoColliderInfo = "没有设置其中一项,该体积将无法影响摄像机。";

        private const string NoProfileSet = "后期处理体积「{0}」未设置配置文件。";
        private const string NoProfileSetCombined = "发现 {0} 个未设置配置文件的后期处理体积。";

        private const string NoMatchingLayersFound = "未找到与主后期处理层匹配的启用后的后期处理体积。当前设置的层:{0}";
         
        private const string TonemapperMissing = "在任何后期处理体积中都未找到色调映射器（Tonemapper）。没有设置色调映射器，场景中的颜色会失真。如果你不确定选哪个，可以试试 Neutral 或 ACES。";

        private const string GlobalTonemapperMissing = "未找到带有色调映射器的全局后期处理体积。建议设置一个带有色调映射器的全局后期处理体积作为后备方案，以防用户处于当前后期处理体积覆盖范围之外，避免颜色失真。";

        private const string TooHighBloomIntensity = "不要把 Bloom 强度调得太高！最好使用较低的 Bloom 强度，在 0.01 到 0.3 之间。";

        private const string TooHighBloomThreshold = "应避免将 Bloom 阈值设置得太高，因为它可能对高亮化身造成意外问题。理想情况下应保持在 0，但始终低于 1.0。";

        private const string NoBloomDirtInVR = "避免使用 Bloom Dirt，在 VR 中看起来非常糟糕！";

        private const string NoAmbientOcclusion = "不要在 VRChat 中使用后期处理环境光遮蔽（Ambient Occlusion）！VRChat 使用的是前向渲染，环境光遮蔽会被叠加在所有内容之上，非常糟糕！而且在 VR 中的渲染开销也极高。";

        private const string DepthOfFieldWarning = "景深（Depth of Field）性能开销很高，而且在 VR 中非常令人眩晕。如果你确实想用景深，默认应保持禁用状态。";

        private const string ScreenSpaceReflectionsWarning = "屏幕空间反射（Screen-space Reflections）仅在延迟渲染下工作。因为 VRChat 使用的是前向渲染，所以不应使用此功能。";

        private const string VignetteWarning = "后期处理暗角（Vignette）应少量使用。强烈的暗角效果在 VR 中可能引起不适。";

        private const string AndroidBakedLightingWarning = "Android 内容应避免使用实时光照，建议使用正确烘焙的光照设置以获得最佳性能。";

        private const string AmbientModeSetToCustom = "当前场景的环境光照设置已损坏。这将用黑色环境光覆盖场景中的所有光照探针。请将其更改为其他选项。";

        private const string NoProblemsFoundInPp = "你的后期处理设置中未发现任何问题。在某些情况下，后期处理在编辑器中正常但在游戏中不工作，可能是某些导入的资源导致的问题。";

#if BAKERY_INCLUDED && VRWT_IS_VRC
        private const string BakeryLightNotSetEditorOnly = "你的 Bakery 灯光「{0}」未设置为 EditorOnly。";
        private const string BakeryLightNotSetEditorOnlyCombined = "你有 {0} 个 Bakery 灯光未设置为 EditorOnly。";
        private const string BakeryLightNotSetEditorOnlyInfo = "这会导致加载 VRChat 世界时输出不必要的错误日志，因为外部脚本在上传过程中会被移除。";

        private const string BakeryLightUnityLight = "你的 Bakery 灯光「{0}」上有一个启用的 Unity 灯光组件。";
        private const string BakeryLightUnityLightCombined = "你有 {0} 个 Bakery 灯光上有启用的 Unity 灯光组件。";
        private const string BakeryLightUnityLightInfo = "这些灯光不会被 Bakery 烘焙，即使设置为烘焙模式也会持续作为实时光照运行。";

        private const string ShrnmDirectionalModeBakeryError = "检测到 Bakery 使用了 SH 或 RNM 方向模式。VRChat 默认不支持这些方向模式，强烈建议改用 Mono SH。否则需要使用扩展插件（如 z3y 的 UdonBakeryAdapter）来实现 VRChat 兼容。";
#endif

        private const string AndroidLightmapCompressionOverride = "光照贴图「{0}」没有为 Android 设置平台特定覆盖。";
        private const string AndroidLightmapCompressionOverrideCombined = "有 {0} 个光照贴图未为 Android 设置平台特定覆盖。";
        private const string AndroidLightmapCompressionOverrideInfo = "在为 Android 构建时，没有设置合适的平台特定覆盖，光照贴图可能会出现明显的色带。建议格式为「ASTC 4x4 块」。";

        private const string MissingShaderWarning = "场景中的材质「{0}」有丢失或损坏的着色器。";
        private const string MissingShaderWarningCombined = "当前场景中发现 {0} 个有丢失或损坏着色器的材质。";
        private const string MissingShaderWarningInfo = "这些材质将回退为粉色错误着色器。";

        private const string ErrorPauseWarning = "你的控制台中启用了「出错时暂停」（Error Pause）。这会通过中断构建流程导致世界上传失败。";

        private const string MultipleScenesLoaded = "加载了多个场景，VRChat 不支持此操作，可能导致世界上传失败。每次创建世界应只使用一个场景。";

        private const string LayersNotSetup = "项目层尚未为 VRChat 设置。";

        private const string CollisionMatrixNotSetup = "项目的碰撞矩阵尚未为 VRChat 设置。";

        private const string VRChatSDKIssue = "调用 VRChat SDK 内置函数时出现问题。SDK 包可能已损坏或缺少文件。关闭项目并用 VRChat Creator Companion 备份以防出现问题，然后在「Manage Project」中，从「Manage Packages」标题旁的更多选项下拉菜单里选择「Reinstall All Packages」。";

        private const string MaterialWithGrabPassShader = "场景中的材质（{0}）由于着色器「{1}」带有一个活动的 GrabPass。";
        private const string MaterialWithGrabPassShaderCombined = "发现 {0} 个使用 GrabPass 的材质。";
        private const string MaterialWithGrabPassShaderInfoPC = "GrabPass 会暂停渲染以将屏幕内容复制到纹理中供着色器读取，对性能有明显影响。";
        private const string MaterialWithGrabPassShaderInfoAndroid = "请更换此材质的着色器。当着色器在 Android 上使用 GrabPass 时，会产生严重视觉伪影，因为 Android 上不支持 GrabPass。";

        private const string MaterialWithNonWhitelistedShader = "材质「{0}」使用了不支持的着色器「{1}」。";
        private const string MaterialWithNonWhitelistedShaderCombined = "发现 {0} 个使用不支持的着色器的材质。";
        private const string MaterialWithNonWhitelistedShaderInfo = "不支持的着色器如果使用不当可能在 Android 平台上导致问题。";

        private const string UIElementWithNavigationNotNone = "UI 元素「{0}」的导航模式未设置为 None。";
        private const string UIElementWithNavigationNotNoneCombined = "发现 {0} 个导航模式未设置为 None 的 UI 元素。";
        private const string UIElementWithNavigationNotNoneInfo = "将 UI 元素的导航模式设置为 None 可以防止玩家在走动时意外与之交互。";

        private const string ScrollRectWithScrollSensitivityNotZero = "ScrollRect 组件「{0}」的滚动灵敏度未设置为 0。";
        private const string ScrollRectWithScrollSensitivityNotZeroCombined = "发现 {0} 个滚动灵敏度未设置为 0 的 ScrollRect 组件。";
        private const string ScrollRectWithScrollSensitivityNotZeroInfo = "将 ScrollRect 组件的滚动灵敏度设置为 0 可以防止玩家在走动时意外与之交互。";

        private const string ParticlesWithAllowRoll = "粒子系统「{0}」的 Allow Roll 设置为 true。";
        private const string ParticlesWithAllowRollCombined = "发现 {0} 个 Allow Roll 设置为 true 的粒子系统。";
        private const string ParticlesWithAllowRollInfo = "这会导致粒子在 VR 中意外旋转。建议在粒子系统上禁用 Allow Roll。";

        private const string TextMeshLightmapStatic = "文本网格「{0}」被标记为光照贴图静态。";
        private const string TextMeshLightmapStaticCombined = "发现 {0} 个被标记为光照贴图静态的文本网格。";
        private const string TextMeshLightmapStaticInfo = "这会产生警告，因为网格没有法线。";

        private const string UnsupportedCompressionFormatAndroid = "纹理 {0} 使用了 Android 不支持的压缩格式 {1}。";
        private const string UnsupportedCompressionFormatAndroidCombined = "发现 {0} 个使用了 Android 不支持压缩格式的纹理。";
        private const string UnsupportedCompressionFormatAndroidInfo = "这些纹理在编辑器中看起来正常，但在游戏中会显示为黑色。";

        private const string HeyYouFoundABug = "嘿，你发现了一个 Bug！请反馈给我，以便我修复！查看「关于 VRWorld Toolkit」了解所有联系我的方式。「{0}」在第 {1} 行。";

        #endregion

        private static long occlusionCacheFiles;

        // TODO: Better check threading
        private void CountOcclusionCacheFiles()
        {
            occlusionCacheFiles = Directory.EnumerateFiles("Library/Occlusion/").Count();

            OcclusionMessageCheck();
        }

        private void OcclusionMessageCheck()
        {
            if (occlusionCacheFiles > 0)
            {
                // Set the message type depending on how many files found
                var cacheWarningType = MessageType.Info;
                if (occlusionCacheFiles > 50000)
                {
                    cacheWarningType = MessageType.Error;
                }
                else if (occlusionCacheFiles > 5000)
                {
                    cacheWarningType = MessageType.Warning;
                }

                optimization.AddMessageGroup(new MessageGroup(OcclusionCullingCacheWarning, cacheWarningType).AddSingleMessage(new SingleMessage(occlusionCacheFiles.ToString()).SetAutoFix(ClearOcclusionCache(occlusionCacheFiles))));
            }
        }

        private class CheckedShaderProperties
        {
            public bool IncludesGrabPass;
            public readonly List<string> GrabPassLightModeTags = new();
        }

        private void CheckScene()
        {
            mainList.ClearCategories();

            try
            {
                // Cache repeatedly used values
                var androidBuildPlatform = Helper.BuildPlatform() == RuntimePlatform.Android;

#if VRWT_IS_VRC
                // Get Descriptors
                var descriptors = FindObjectsOfType(typeof(VRC_SceneDescriptor)) as VRC_SceneDescriptor[];
                var pipelines = FindObjectsOfType(typeof(PipelineManager)) as PipelineManager[];

                var cameraMain = Camera.main;

                // Check if a descriptor exists
                if (descriptors.Length == 0)
                {
                    general.AddMessageGroup(new MessageGroup(NoSceneDescriptor, MessageType.Error));
                    return;
                }

                var sceneDescriptor = descriptors[0];

                // General Checks

                // Make sure only one descriptor exists
                if (descriptors.Length > 1)
                {
                    general.AddMessageGroup(new MessageGroup(TooManySceneDescriptors, MessageType.Info).AddSingleMessage(new SingleMessage(Array.ConvertAll(descriptors, s => s.gameObject))));
                    return;
                }

                // Check for multiple pipeline managers
                if (pipelines.Length > 1)
                {
                    general.AddMessageGroup(new MessageGroup(TooManyPipelineManagers, MessageType.Error).AddSingleMessage(new SingleMessage(Array.ConvertAll(pipelines, s => s.gameObject)).SetAutoFix(RemoveBadPipelineManagers(pipelines))));
                }

                // Check how far the descriptor is from zero point for floating point errors
                var descriptorRemoteness = (int)Vector3.Distance(sceneDescriptor.transform.position, new Vector3(0.0f, 0.0f, 0.0f));

                if (descriptorRemoteness > 1500)
                {
                    general.AddMessageGroup(new MessageGroup(WorldDescriptorFar, MessageType.Error).AddSingleMessage(new SingleMessage(descriptorRemoteness.ToString()).SetSelectObject(Array.ConvertAll(descriptors, s => s.gameObject))));
                }
                else if (descriptorRemoteness > 500)
                {
                    general.AddMessageGroup(new MessageGroup(WorldDescriptorOff, MessageType.Tips).AddSingleMessage(new SingleMessage(descriptorRemoteness.ToString()).SetSelectObject(Array.ConvertAll(descriptors, s => s.gameObject))));
                }
#endif

                var lastVRCPath = $"{PlayerSettings.productName}/{PlayerSettings.companyName}";
                if (!string.IsNullOrEmpty(lastVRCPath))
                {
                    var lastEscapedVRCPath = UnityWebRequest.UnEscapeURL(lastVRCPath);
                    if (lastVRCPath != lastEscapedVRCPath)
                    {
                        general.AddMessageGroup(new MessageGroup(DifferingSanitizedBuildPath, MessageType.Error).AddSingleMessage(new SingleMessage(lastVRCPath, lastEscapedVRCPath).SetAutoFix(SanitizeBuildPath())));
                    }
                }

#if VRWT_IS_VRC
                try
                {
                    if (!UpdateLayers.AreLayersSetup())
                    {
                        general.AddMessageGroup(new MessageGroup(LayersNotSetup, MessageType.Error).SetGroupAutoFix(SetVRChatLayers()));
                    }

                    if (!UpdateLayers.IsCollisionLayerMatrixSetup())
                    {
                        general.AddMessageGroup(new MessageGroup(CollisionMatrixNotSetup, MessageType.Error).SetGroupAutoFix(SetVRChatCollisionMatrix()));
                    }
                }
                catch (Exception)
                {
                    general.AddMessageGroup(new MessageGroup(VRChatSDKIssue, MessageType.Error));
                }
#endif

                if ((buildReportWindows != null && buildReportWindows.summary.result == BuildResult.Failed) || (buildReportAndroid != null && buildReportAndroid.summary.result == BuildResult.Failed) || (buildReportiOS != null && buildReportiOS.summary.result == BuildResult.Failed) )
                {
                    general.AddMessageGroup(new MessageGroup(LastBuildFailed, MessageType.Error).SetDocumentation("https://github.com/oneVR/VRWorldToolkit/wiki/Fixing-Build-Problems"));
                }

                // Check if multiple scenes loaded
                if (SceneManager.sceneCount > 1)
                {
                    general.AddMessageGroup(new MessageGroup(MultipleScenesLoaded, MessageType.Error));
                }

                // Check if console has error pause on
                if (ConsoleFlagUtil.GetConsoleErrorPause())
                {
                    general.AddMessageGroup(new MessageGroup(ErrorPauseWarning, MessageType.Error).AddSingleMessage(new SingleMessage(SetErrorPause(false))));
                }

// TODO STUB NOVRC: Reference Camera may be obtained from the tagged Main Camera of the scene in non-VRC projects.
#if VRWT_IS_VRC
                // Check reference camera for possible problems
                if (sceneDescriptor.ReferenceCamera != null)
                {
                    var camera = sceneDescriptor.ReferenceCamera.GetComponent<Camera>();
                    if (camera != null)
                    {
                        if (camera.clearFlags != CameraClearFlags.Skybox && camera.clearFlags != CameraClearFlags.SolidColor)
                        {
                            general.AddMessageGroup(new MessageGroup(ReferenceCameraClearFlagsNotSkyboxOrColor, MessageType.Warning).AddSingleMessage(new SingleMessage(sceneDescriptor.ReferenceCamera)));
                        }

                        // TODO: Investigate better sanity value
                        if (camera.farClipPlane / camera.nearClipPlane > 200000f)
                        {
                            general.AddMessageGroup(new MessageGroup(ReferenceCameraClippingPlaneRatio, MessageType.Warning).AddSingleMessage(new SingleMessage(camera.nearClipPlane.ToString(CultureInfo.InvariantCulture), camera.farClipPlane.ToString(CultureInfo.InvariantCulture)).SetSelectObject(camera.gameObject)));
                        }

                        if (camera.nearClipPlane > 0.05f)
                        {
                            general.AddMessageGroup(new MessageGroup(ReferenceCameraNearClipPlaneOver, MessageType.Tips).AddSingleMessage(new SingleMessage(camera.nearClipPlane.ToString(CultureInfo.InvariantCulture)).SetSelectObject(camera.gameObject)));
                        }
                    }
                    else
                    {
                        general.AddMessageGroup(new MessageGroup(ReferenceCameraHasNoCameraComponent, MessageType.Error)).AddSingleMessage(new SingleMessage(sceneDescriptor.ReferenceCamera.name).SetSelectObject(sceneDescriptor.ReferenceCamera).SetAutoFix(() =>
                        {
                            sceneDescriptor.ReferenceCamera = null;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(sceneDescriptor.gameObject);
                        }));
                    }
                }
                else
                {
                    general.AddMessageGroup(new MessageGroup(NoReferenceCameraSetGeneral, MessageType.Tips).AddSingleMessage(new SingleMessage(sceneDescriptor.gameObject)));
                }
#endif

#if VRWT_IS_VRC
                // Get spawn points for any possible problems
                if (sceneDescriptor.spawns != null && sceneDescriptor.spawns.Length > 0)
                {
                    var spawns = sceneDescriptor.spawns.Where(s => s != null).ToArray();
                    var spawnsLength = sceneDescriptor.spawns.Length;
                    var emptySpawns = spawnsLength != spawns.Length;

                    if (emptySpawns)
                    {
                        general.AddMessageGroup(new MessageGroup(NullSpawnPoint, MessageType.Error).AddSingleMessage(new SingleMessage(sceneDescriptor.gameObject).SetAutoFix(FixSpawns(sceneDescriptor))));
                    }

                    var spawnUnderRespawnHeight = general.AddMessageGroup(new MessageGroup(SpawnUnderRespawnHeight, SpawnUnderRespawnHeightCombined, SpawnUnderRespawnHeightInfo, MessageType.Error));
                    var noColliderUnderSpawn = general.AddMessageGroup(new MessageGroup(NoColliderUnderSpawn, NoColliderUnderSpawnCombined, NoColliderUnderSpawnInfo, MessageType.Error));
                    var colliderUnderSpawnTrigger = general.AddMessageGroup(new MessageGroup(ColliderUnderSpawnIsTrigger, ColliderUnderSpawnIsTriggerCombined, ColliderUnderSpawnIsTriggerInfo, MessageType.Error));
                    var respawnHeightAboveCollider = general.AddMessageGroup(new MessageGroup(RespawnHeightAboveCollider, RespawnHeightAboveColliderCombined, RespawnHeightAboveColliderInfo, MessageType.Error));

                    for (var i = 0; i < spawns.Length; i++)
                    {
                        var spawn = spawns[i];

                        if (spawn.position.y < sceneDescriptor.RespawnHeightY)
                        {
                            spawnUnderRespawnHeight.AddSingleMessage(new SingleMessage(spawn.gameObject.name, Math.Abs(spawn.position.y - sceneDescriptor.RespawnHeightY).ToString(CultureInfo.InvariantCulture)).SetSelectObject(spawn.gameObject));
                        }

                        if (!Physics.Raycast(spawn.position + new Vector3(0, 0.01f, 0), Vector3.down, out RaycastHit hit, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore))
                        {
                            if (Physics.Raycast(spawn.position + new Vector3(0, 0.01f, 0), Vector3.down, out hit, Mathf.Infinity))
                            {
                                if (hit.collider.isTrigger)
                                {
                                    colliderUnderSpawnTrigger.AddSingleMessage(new SingleMessage(hit.collider.name, spawn.gameObject.name).SetSelectObject(spawn.gameObject));
                                }
                            }
                            else
                            {
                                noColliderUnderSpawn.AddSingleMessage(new SingleMessage(spawn.gameObject.name).SetSelectObject(spawn.gameObject));
                            }
                        }
                        // Round respawn height to 2 decimals to reflect in-game functionality
                        else if (Math.Round(hit.point.y, 2) <= Math.Round(sceneDescriptor.RespawnHeightY, 2))
                        {
                            respawnHeightAboveCollider.AddSingleMessage(new SingleMessage(hit.collider.gameObject.name, spawn.gameObject.name).SetSelectObject(spawn.gameObject).SetAutoFix(ChangeRespawnHeight(sceneDescriptor, hit.point.y - 100)));
                        }
                    }
                }
                else
                {
                    general.AddMessageGroup(new MessageGroup(NoSpawnPointSet, MessageType.Error).AddSingleMessage(new SingleMessage(sceneDescriptor.gameObject).SetAutoFix(FixSpawns(sceneDescriptor))));
                }
#endif

                // Optimization Checks

                // Check for occlusion culling
                if (StaticOcclusionCulling.umbraDataSize > 0)
                {
                    optimization.AddMessageGroup(new MessageGroup(BakedOcclusionCulling, MessageType.GoodFPS));

                    var occlusionAreas = FindObjectsOfType<OcclusionArea>();

                    if (occlusionAreas.Length == 0)
                    {
                        optimization.AddMessageGroup(new MessageGroup(NoOcclusionAreas, MessageType.Tips).SetDocumentation("https://docs.unity3d.com/2022.3/Documentation/Manual/class-OcclusionArea.html"));
                    }
                    else
                    {
                        var disabledOcclusionAreasGroup = optimization.AddMessageGroup(new MessageGroup(DisabledOcclusionArea, DisabledOcclusionAreaCombined, DisabledOcclusionAreaInfo, MessageType.Warning));

                        foreach (var occlusionArea in occlusionAreas)
                        {
                            var so = new SerializedObject(occlusionArea);
                            var sp = so.FindProperty("m_IsViewVolume");

                            if (!sp.boolValue)
                            {
                                disabledOcclusionAreasGroup.AddSingleMessage(new SingleMessage(occlusionArea.name).SetSelectObject(occlusionArea.gameObject));
                            }
                        }
                    }
                }
                else
                {
                    optimization.AddMessageGroup(new MessageGroup(NoOcclusionCulling, MessageType.Tips).SetDocumentation("https://gitlab.com/s-ilent/SCSS/-/wikis/Other/Occlusion-Culling"));
                }

                OcclusionMessageCheck();

                // Check for possible camera problems
                var cameras = FindObjectsOfType<Camera>();

                if (cameras.Length > 0)
                {
                    var activeCamerasMessages = optimization.AddMessageGroup(new MessageGroup(ActiveCameraOutputtingToRenderTexture, ActiveCameraOutputtingToRenderTextureCombined, ActiveCameraOutputtingToRenderTextureInfo, MessageType.BadFPS));
                    var cameraDepthWarning = general.AddMessageGroup(new MessageGroup(ActiveCameraWithOverZeroDepth, ActiveCameraWithOverZeroDepthCombined, ActiveCameraWithOverZeroDepthInfo, MessageType.Error));

                    for (var i = 0; i < cameras.Length; i++)
                    {
                        var camera = cameras[i];

                        if (!camera.enabled) continue;

                        if (camera.targetTexture)
                        {
                            activeCamerasMessages.AddSingleMessage(new SingleMessage(camera.name).SetSelectObject(camera.gameObject));
                        }
                        else if (camera.depth > 0 && camera.targetDisplay == 0)
                        {
                            cameraDepthWarning.AddSingleMessage(new SingleMessage(camera.name).SetSelectObject(camera.gameObject));
                        }
                    }
                }

#if VRWT_IS_VRC
                // Get active mirrors in the world and complain about them
                var mirrors = FindObjectsOfType(typeof(VRC_MirrorReflection)) as VRC_MirrorReflection[];

                if (mirrors.Length > 0)
                {
                    var activeCamerasMessage = new MessageGroup(MirrorONByDefault, MirrorONByDefaultCombined, MirrorONByDefaultInfo, MessageType.BadFPS);
                    for (var i = 0; i < mirrors.Length; i++)
                    {
                        if (mirrors[i].enabled)
                        {
                            activeCamerasMessage.AddSingleMessage(new SingleMessage(mirrors[i].name).SetSelectObject(mirrors[i].gameObject));
                        }
                    }

                    optimization.AddMessageGroup(activeCamerasMessage);
                }
#endif

                // Lighting Checks

                switch (RenderSettings.ambientMode)
                {
                    case AmbientMode.Custom:
                        lighting.AddMessageGroup(new MessageGroup(AmbientModeSetToCustom, MessageType.Error).AddSingleMessage(new SingleMessage(SetAmbientMode(AmbientMode.Skybox))));
                        break;
                    case AmbientMode.Flat:
                        lighting.AddMessageGroup(new MessageGroup(SingleColorEnvironmentLighting, MessageType.Tips));
                        break;
                }

                if (Helper.GetBrightness(RenderSettings.ambientLight) < 0.1f && RenderSettings.ambientMode.Equals(AmbientMode.Flat) ||
                    Helper.GetBrightness(RenderSettings.ambientSkyColor) < 0.1f && RenderSettings.ambientMode.Equals(AmbientMode.Trilight) ||
                    Helper.GetBrightness(RenderSettings.ambientEquatorColor) < 0.1f && RenderSettings.ambientMode.Equals(AmbientMode.Trilight) ||
                    Helper.GetBrightness(RenderSettings.ambientGroundColor) < 0.1f && RenderSettings.ambientMode.Equals(AmbientMode.Trilight))
                {
                    lighting.AddMessageGroup(new MessageGroup(DarkEnvironmentLighting, MessageType.Tips));
                }

                if (RenderSettings.defaultReflectionMode.Equals(DefaultReflectionMode.Custom) && !RenderSettings.customReflectionTexture)
                {
                    lighting.AddMessageGroup(new MessageGroup(CustomEnvironmentReflectionsNull, MessageType.Error).AddSingleMessage(new SingleMessage(SetEnviromentReflections(DefaultReflectionMode.Skybox))));
                }

                var bakedLighting = false;
                var xatlasUnwrapper = false;

#if BAKERY_INCLUDED && VRWT_IS_VRC
    if (Helper.GetTypeFromName("ftRenderLightmap") != null)
    {
        var bakeryLights = BakeryCompat.GetBakeryLights().Distinct().ToList();

        var (bakerySettingsStorageObject, ftRenderLightmapType) = BakeryCompat.TryGetSettings();

        if (BakeryCompat.IsRenderDirRNMOrSH(bakerySettingsStorageObject, ftRenderLightmapType))
        {
            const string merlinBakeryAdapter = "Merlin.VRCBakeryAdapter";
            const string udonBakeryAdapter = "UdonBakeryAdapter";

            if (Helper.GetTypeFromName(merlinBakeryAdapter) is null &&
                Helper.GetTypeFromName(udonBakeryAdapter) is null)
            {
                lighting.AddMessageGroup(new MessageGroup(ShrnmDirectionalModeBakeryError, MessageType.Error).SetDocumentation("https://github.com/z3y/UdonBakeryAdapter"));
            }
        }

        if (BakeryCompat.UsesXatlas(bakerySettingsStorageObject))
        {
            xatlasUnwrapper = true;
        }

        if (bakeryLights.Count > 0)
        {
            var notEditorOnly = new List<GameObject>();
            var unityLightOnBakeryLight = new List<GameObject>();

            bakedLighting = true;

            foreach (var gameObject in bakeryLights)
            {
                if (!gameObject.CompareTag("EditorOnly"))
                {
                    notEditorOnly.Add(gameObject);
                }

                var light = gameObject.GetComponent<Light>();
                if (light != null && !light.bakingOutput.isBaked && light.enabled)
                {
                    unityLightOnBakeryLight.Add(gameObject);
                }
            }

            if (notEditorOnly.Count > 0)
            {
                var notEditorOnlyGroup = new MessageGroup(BakeryLightNotSetEditorOnly, BakeryLightNotSetEditorOnlyCombined, BakeryLightNotSetEditorOnlyInfo, MessageType.Warning);

                foreach (var item in notEditorOnly)
                {
                    notEditorOnlyGroup.AddSingleMessage(new SingleMessage(item.name).SetAutoFix(SetGameObjectTag(item, "EditorOnly")).SetSelectObject(item));
                }

                lighting.AddMessageGroup(notEditorOnlyGroup.SetGroupAutoFix(SetGameObjectTag(notEditorOnly.ToArray(), "EditorOnly")));
            }

            if (unityLightOnBakeryLight.Count > 0)
            {
                var unityLightGroup = new MessageGroup(BakeryLightUnityLight, BakeryLightUnityLightCombined, BakeryLightUnityLightInfo, MessageType.Warning);

                foreach (var item in unityLightOnBakeryLight)
                {
                    unityLightGroup.AddSingleMessage(new SingleMessage(item.name).SetAutoFix(DisableComponent(item.GetComponent<Light>())).SetSelectObject(item));
                }

                lighting.AddMessageGroup(unityLightGroup.SetGroupAutoFix(DisableComponent(Array.ConvertAll(unityLightOnBakeryLight.ToArray(), s => (Behaviour)s.GetComponent<Light>()))));
            }
        }
    }
#endif

                // Get lights in scene
                var lights = FindObjectsOfType<Light>();

                var nonBakedLights = new List<GameObject>();

                // Go trough the lights to check if the scene contains lights set to be baked
                for (var i = 0; i < lights.Length; i++)
                {
                    // Skip checking realtime lights
                    if (lights[i].lightmapBakeType == LightmapBakeType.Realtime) continue;

                    bakedLighting = true;

                    if (!lights[i].bakingOutput.isBaked && lights[i].GetComponent<Light>().enabled)
                    {
                        nonBakedLights.Add(lights[i].gameObject);
                    }
                }

                if (LightmapSettings.lightmaps.Length > 0)
                {
                    bakedLighting = true;

                    if (androidBuildPlatform && EditorUserBuildSettings.androidBuildSubtarget == MobileTextureSubtarget.Generic)
                    {
                        var lightmaps = LightmapSettings.lightmaps;

                        var androidCompressionGroup = lighting.AddMessageGroup(new MessageGroup(AndroidLightmapCompressionOverride, AndroidLightmapCompressionOverrideCombined, AndroidLightmapCompressionOverrideInfo, MessageType.Tips).SetDocumentation("https://docs.unity3d.com/2022.3/Documentation/Manual/class-TextureImporter.html"));

                        for (var i = 0; i < lightmaps.Length; i++)
                        {
                            Object lightmap = lightmaps[i].lightmapColor;

                            var textureImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(lightmaps[i].lightmapColor)) as TextureImporter;

                            if (textureImporter != null)
                            {
                                var platformSettings = textureImporter.GetPlatformTextureSettings("Android");

                                if (!platformSettings.overridden)
                                {
                                    androidCompressionGroup.AddSingleMessage(new SingleMessage(lightmap.name).SetAssetPath(textureImporter.assetPath));
                                }
                            }
                        }
                    }
                }

                // If the scene has baked lights complain about stuff important to baked lighting missing
                if (bakedLighting)
                {
                    if (Lightmapping.TryGetLightingSettings(out var lightingSettings))
                    {
                        // Count lightmaps and suggest to use bigger lightmaps if needed
                        var lightMapSize = lightingSettings.lightmapMaxSize;
                        if (lightMapSize < 2048 && LightmapSettings.lightmaps.Length >= 4)
                        {
                            if (LightmapSettings.lightmaps[0] != null)
                            {
                                var lightmap = LightmapSettings.lightmaps[0];

                                if (lightmap.lightmapColor != null && lightmap.lightmapColor.height != 4096)
                                {
                                    lighting.AddMessageGroup(new MessageGroup(ConsiderLargerLightmaps, MessageType.Tips).AddSingleMessage(new SingleMessage(lightMapSize.ToString())));
                                }
                            }
                        }

                        if (lightingSettings.lightmapper.Equals(LightingSettings.Lightmapper.ProgressiveGPU) && lightMapSize == 4096 && SystemInfo.graphicsMemorySize < 12000)
                        {
                            lighting.AddMessageGroup(new MessageGroup(ConsiderSmallerLightmaps, MessageType.Warning).AddSingleMessage(new SingleMessage(lightMapSize.ToString()).SetAutoFix(SetLightmapSize(2048))));
                        }
                    }
                    else
                    {
                        lighting.AddMessageGroup(new MessageGroup(NoLightingSettingsAsset, MessageType.Info).SetDocumentation("https://docs.unity3d.com/2022.3/Documentation/Manual/class-LightingSettings.html"));
                    }

                    // Count how many light probes the scene has
                    long probeCounter = 0;
                    var probes = LightmapSettings.lightProbes;
                    long bakedProbes = probes != null ? probes.count : 0;

                    var overlappingLightProbesGroup = new MessageGroup(OverlappingLightProbes, OverlappingLightProbesCombined, OverlappingLightProbesInfo, MessageType.Info);

                    var lightProbeGroups = FindObjectsOfType<LightProbeGroup>();
                    for (var i = 0; i < lightProbeGroups.Length; i++)
                    {
                        if (lightProbeGroups[i].probePositions.GroupBy(p => p).Any(g => g.Count() > 1))
                        {
                            overlappingLightProbesGroup.AddSingleMessage(new SingleMessage(lightProbeGroups[i].name, (lightProbeGroups[i].probePositions.Length - lightProbeGroups[i].probePositions.Distinct().ToArray().Length).ToString()).SetSelectObject(lightProbeGroups[i].gameObject).SetAutoFix(RemoveOverlappingLightProbes(lightProbeGroups[i])));
                        }

                        probeCounter += lightProbeGroups[i].probePositions.Length;
                    }

                    if (probeCounter > 0)
                    {
                        if (probeCounter - bakedProbes < 0)
                        {
                            lighting.AddMessageGroup(new MessageGroup(LightProbesRemovedNotReBaked, MessageType.Warning).AddSingleMessage(new SingleMessage(bakedProbes.ToString(), probeCounter.ToString())));
                        }
                        else
                        {
                            if (bakedProbes - (0.9 * probeCounter) < 0)
                            {
                                lighting.AddMessageGroup(new MessageGroup(LightProbeCountNotBaked, MessageType.Info).AddSingleMessage(new SingleMessage(probeCounter.ToString("n0"), (probeCounter - bakedProbes).ToString("n0"))));
                            }
                            else
                            {
                                lighting.AddMessageGroup(new MessageGroup(LightProbeCount, MessageType.Info).AddSingleMessage(new SingleMessage(probeCounter.ToString("n0"))));
                            }
                        }

                        if (overlappingLightProbesGroup.GetTotalCount() > 0)
                        {
                            if (overlappingLightProbesGroup.GetTotalCount() > 1)
                            {
                                overlappingLightProbesGroup.SetGroupAutoFix(RemoveOverlappingLightProbes(lightProbeGroups));
                            }

                            lighting.AddMessageGroup(overlappingLightProbesGroup);
                        }

                        if (Lightmapping.lightingDataAsset != null)
                        {
                            // Check lighting data asset size
                            var pathTo = AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset);
                            var length = new FileInfo(pathTo).Length;
                            lighting.AddMessageGroup(new MessageGroup(LightingDataAssetInfo, MessageType.Info).AddSingleMessage(new SingleMessage((length / 1024.0f / 1024.0f).ToString("F2"))).SetDocumentation("https://docs.unity3d.com/2022.3/Documentation/Manual/LightmapSnapshot.html"));
                        }
                        else
                        {
                            lighting.AddMessageGroup(new MessageGroup(NoLightingDataAsset, MessageType.Info).SetDocumentation("https://docs.unity3d.com/2022.3/Documentation/Manual/LightmapSnapshot.html"));
                        }
                    }
                    // Since the scene has baked lights complain if there's no light probes
                    else
                    {
                        lighting.AddMessageGroup(new MessageGroup(NoLightProbes, MessageType.Info).SetDocumentation("https://docs.unity3d.com/2022.3/Documentation/Manual/LightProbes.html"));
                    }

                    if (nonBakedLights.Count != 0)
                    {
                        var nonBakedLightsGroup = new MessageGroup(NonBakedBakedLight, NonBakedBakedLightCombined, NonBakedBakedLightInfo, MessageType.Warning);
                        for (var i = 0; i < nonBakedLights.Count; i++)
                        {
                            nonBakedLightsGroup.AddSingleMessage(new SingleMessage(nonBakedLights[i].name).SetSelectObject(nonBakedLights[i].gameObject));
                        }

                        lighting.AddMessageGroup(nonBakedLightsGroup);
                    }
                }
                else
                {
                    lighting.AddMessageGroup(new MessageGroup(androidBuildPlatform ? AndroidBakedLightingWarning : LightsNotBaked, androidBuildPlatform ? MessageType.Warning : MessageType.Tips).SetDocumentation("https://gitlab.com/s-ilent/SCSS/-/wikis/Other/Light-Baking"));
                }

                // ReflectionProbes
                var reflectionProbes = FindObjectsOfType<ReflectionProbe>();
                var unbakedProbes = new List<GameObject>();
                var reflectionProbeCount = reflectionProbes.Count();
                for (var i = 0; i < reflectionProbes.Length; i++)
                {
                    if (!reflectionProbes[i].bakedTexture && reflectionProbes[i].mode == ReflectionProbeMode.Baked)
                    {
                        unbakedProbes.Add(reflectionProbes[i].gameObject);
                    }
                }

                if (reflectionProbeCount == 0)
                {
                    lighting.AddMessageGroup(new MessageGroup(NoReflectionProbes, MessageType.Tips).SetDocumentation("https://gitlab.com/s-ilent/SCSS/-/wikis/Other/Reflection-Probes"));
                }
                else if (reflectionProbeCount > 0)
                {
                    lighting.AddMessageGroup(new MessageGroup(ReflectionProbeCountText, MessageType.Info).AddSingleMessage(new SingleMessage(reflectionProbeCount.ToString())));

                    if (unbakedProbes.Count > 0)
                    {
                        var probesUnbakedGroup = new MessageGroup(ReflectionProbesSomeUnbaked, ReflectionProbesSomeUnbakedCombined, MessageType.Warning);

                        foreach (var item in unbakedProbes)
                        {
                            probesUnbakedGroup.AddSingleMessage(new SingleMessage(item.name).SetSelectObject(item));
                        }

                        lighting.AddMessageGroup(probesUnbakedGroup);
                    }
                }

                // Post Processing Checks
#if UNITY_POST_PROCESSING_STACK_V2 && VRWT_IS_VRC
                var postProcessVolumes = FindObjectsOfType(typeof(PostProcessVolume)) as PostProcessVolume[];
                PostProcessLayer mainPostProcessLayer = null;

                // Attempt to find the main post process layer
                if (sceneDescriptor.ReferenceCamera != null)
                {
                    var postProcessLayer = sceneDescriptor.ReferenceCamera.gameObject.GetComponent(typeof(PostProcessLayer)) as PostProcessLayer;
                    if (postProcessLayer)
                    {
                        mainPostProcessLayer = postProcessLayer;
                    }
                    else
                    {
                        if (cameraMain != null)
                        {
                            if (postProcessLayer)
                            {
                                mainPostProcessLayer = postProcessLayer;
                            }
                        }
                    }
                }

                // Check if the post processing layer has resources properly set
                if (mainPostProcessLayer)
                {
                    var resourcesInfo = typeof(PostProcessLayer).GetField("m_Resources", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (resourcesInfo.GetValue(mainPostProcessLayer) is not PostProcessResources postProcessResources)
                    {
                        var singleMessage = new SingleMessage(mainPostProcessLayer.gameObject.name).SetSelectObject(mainPostProcessLayer.gameObject);

                        postProcessing.AddMessageGroup(new MessageGroup(PostProcessingNoResourcesSet, MessageType.Error).AddSingleMessage(singleMessage));

                        var resources = (PostProcessResources)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath("d82512f9c8e5d4a4d938b575d47f88d4"), typeof(PostProcessResources));

                        if (resources != null) singleMessage.SetAutoFix(SetPostProcessingLayerResources(mainPostProcessLayer, resources));
                    }
                }

                // If post processing is imported but no setup isn't detected show a message
                if (postProcessVolumes.Length == 0 && mainPostProcessLayer is null)
                {
                    postProcessing.AddMessageGroup(new MessageGroup(PostProcessingImportedButNotSetup, MessageType.Info));
                }
                else
                {
                    // Check the scene view for post processing effects being off
                    var sceneViewState = SceneView.lastActiveSceneView.sceneViewState;
                    if (!sceneViewState.showImageEffects)
                    {
                        postProcessing.AddMessageGroup(new MessageGroup(PostProcessingDisabledInSceneView, MessageType.Info).SetGroupAutoFix(SetPostProcessingInScene(sceneViewState, true)));
                    }

                    // Start by checking if reference camera has been set in the Scene Descriptor
                    if (!sceneDescriptor.ReferenceCamera)
                    {
                        var noReferenceCameraMessage = new SingleMessage(sceneDescriptor.gameObject);

                        if (cameraMain && cameraMain.GetComponent<PostProcessLayer>())
                        {
                            noReferenceCameraMessage.SetAutoFix(SetReferenceCamera(sceneDescriptor, cameraMain));
                        }

                        postProcessing.AddMessageGroup(new MessageGroup(NoReferenceCameraSetPp, MessageType.Warning).AddSingleMessage(noReferenceCameraMessage));
                    }
                    else
                    {
                        // Check for post process volumes in the scene
                        if (postProcessVolumes.Length == 0)
                        {
                            postProcessing.AddMessageGroup(new MessageGroup(NoPostProcessingVolumes, MessageType.Info));
                        }
                        else
                        {
                            var postprocessLayer = sceneDescriptor.ReferenceCamera.GetComponent(typeof(PostProcessLayer)) as PostProcessLayer;
                            if (postprocessLayer is null)
                            {
                                postProcessing.AddMessageGroup(new MessageGroup(ReferenceCameraNoPostProcessingLayer, MessageType.Error).AddSingleMessage(new SingleMessage(sceneDescriptor.ReferenceCamera.gameObject)));
                            }

                            if (postprocessLayer)
                            {
                                var volumeLayer = postprocessLayer.volumeLayer;
                                if (volumeLayer == 0)
                                {
                                    postProcessing.AddMessageGroup(new MessageGroup(VolumeBlendingLayerNotSet, MessageType.Error).AddSingleMessage(new SingleMessage(sceneDescriptor.ReferenceCamera.gameObject)));
                                }

                                // Check for usage of reserved layers since they break post processing
                                var numbersFromMask = Helper.GetAllLayerNumbersFromMask(volumeLayer);
                                if (numbersFromMask.Contains(19) | numbersFromMask.Contains(20) | numbersFromMask.Contains(21))
                                {
                                    postProcessing.AddMessageGroup(new MessageGroup(PostProcessLayerUsingReservedLayer, MessageType.Error).AddSingleMessage(new SingleMessage(postprocessLayer.gameObject.name).SetSelectObject(postprocessLayer.gameObject)));
                                }

                                var noProfileSet = postProcessing.AddMessageGroup(new MessageGroup(NoProfileSet, NoProfileSetCombined, MessageType.Error));
                                var volumeNoGlobalNoCollider = postProcessing.AddMessageGroup(new MessageGroup(PostProcessingVolumeNotGlobalNoCollider, PostProcessingVolumeNotGlobalNoColliderCombined, PostProcessingVolumeNotGlobalNoColliderInfo, MessageType.Error));
                                var matchingVolumes = new List<PostProcessVolume>();
                                foreach (var postProcessVolume in postProcessVolumes)
                                {
                                    // Check if the layer matches the cameras post processing layer
                                    if (volumeLayer != 0 && (postprocessLayer.volumeLayer == (postprocessLayer.volumeLayer | (1 << postProcessVolume.gameObject.layer))))
                                    {
                                        matchingVolumes.Add(postProcessVolume);
                                    }

                                    // Check if the volume has a profile set
                                    if (postProcessVolume.profile is null && postProcessVolume.sharedProfile is null)
                                    {
                                        noProfileSet.AddSingleMessage(new SingleMessage(postProcessVolume.gameObject.name).SetSelectObject(postProcessVolume.gameObject));
                                    }

                                    // Check if the collider is either global or has a collider on it
                                    if (!postProcessVolume.isGlobal && !postProcessVolume.GetComponent<Collider>())
                                    {
                                        volumeNoGlobalNoCollider.AddSingleMessage(new SingleMessage(postProcessVolume.name).SetSelectObject(postProcessVolume.gameObject));
                                    }
                                }

                                if (matchingVolumes.Count == 0)
                                {
                                    postProcessing.AddMessageGroup(new MessageGroup(NoMatchingLayersFound, MessageType.Warning).AddSingleMessage(new SingleMessage(Helper.GetAllLayersFromMask(postprocessLayer.volumeLayer)).SetSelectObject(postprocessLayer.gameObject)));
                                }

                                var noTonemapper = true;

                                var noGlobalTonemapper = true;

                                // Go trough the profile settings and see if any bad one's are used
                                foreach (var postProcessVolume in matchingVolumes)
                                {
                                    var postProcessProfile = postProcessVolume.profile ? postProcessVolume.profile : postProcessVolume.sharedProfile;

                                    if (postProcessProfile is null) continue;

                                    var ambientOcclusion = postProcessProfile.GetSetting<AmbientOcclusion>();
                                    if (ambientOcclusion && ambientOcclusion.enabled && ambientOcclusion.active)
                                    {
                                        postProcessing.AddMessageGroup(new MessageGroup(NoAmbientOcclusion, MessageType.Error).AddSingleMessage(new SingleMessage(DisablePostProcessEffect(postProcessProfile, RemovePpEffect.AmbientOcclusion)).SetSelectObject(postProcessVolume.gameObject)));
                                    }

                                    var screenSpaceReflections = postProcessProfile.GetSetting<ScreenSpaceReflections>();
                                    if (screenSpaceReflections && screenSpaceReflections.enabled && screenSpaceReflections.active)
                                    {
                                        postProcessing.AddMessageGroup(new MessageGroup(ScreenSpaceReflectionsWarning, MessageType.Warning).AddSingleMessage(new SingleMessage(DisablePostProcessEffect(postProcessProfile, RemovePpEffect.ScreenSpaceReflections)).SetSelectObject(postProcessVolume.gameObject)));
                                    }

                                    var vignette = postProcessProfile.GetSetting<Vignette>();
                                    if (vignette && vignette.enabled && vignette.active)
                                    {
                                        postProcessing.AddMessageGroup(new MessageGroup(VignetteWarning, MessageType.Warning).AddSingleMessage(new SingleMessage(postProcessVolume.gameObject)));
                                    }

                                    var colorGrading = postProcessProfile.GetSetting<ColorGrading>();
                                    if (colorGrading && colorGrading.enabled && colorGrading.active)
                                    {
                                        if (colorGrading.tonemapper.overrideState && colorGrading.tonemapper.value != Tonemapper.None)
                                        {
                                            if (postProcessVolume.isGlobal)
                                            {
                                                noGlobalTonemapper = false;
                                            }

                                            noTonemapper = false;
                                        }
                                    }

                                    if (postProcessVolume.isGlobal)
                                    {
                                        var bloom = postProcessProfile.GetSetting<Bloom>();
                                        if (bloom && bloom.enabled && bloom.active)
                                        {
                                            if (bloom.intensity.overrideState && bloom.intensity.value > 0.3f)
                                            {
                                                postProcessing.AddMessageGroup(new MessageGroup(TooHighBloomIntensity, MessageType.Warning).AddSingleMessage(new SingleMessage(postProcessVolume.gameObject)));
                                            }

                                            if (bloom.threshold.overrideState && bloom.threshold.value > 1f)
                                            {
                                                postProcessing.AddMessageGroup(new MessageGroup(TooHighBloomThreshold, MessageType.Warning).AddSingleMessage(new SingleMessage(postProcessVolume.gameObject)));
                                            }

                                            if (bloom.dirtTexture.overrideState && bloom.dirtTexture.value || bloom.dirtIntensity.overrideState && bloom.dirtIntensity.value > 0)
                                            {
                                                postProcessing.AddMessageGroup(new MessageGroup(NoBloomDirtInVR, MessageType.Error).AddSingleMessage(new SingleMessage(DisablePostProcessEffect(postProcessProfile, RemovePpEffect.BloomDirt)).SetSelectObject(postProcessVolume.gameObject)));
                                            }
                                        }

                                        var depthOfField = postProcessProfile.GetSetting<DepthOfField>();
                                        if (depthOfField && depthOfField.enabled && depthOfField.active)
                                        {
                                            postProcessing.AddMessageGroup(new MessageGroup(DepthOfFieldWarning, MessageType.Warning).AddSingleMessage(new SingleMessage(postProcessVolume.gameObject)));
                                        }
                                    }
                                }

                                if (noTonemapper)
                                {
                                    postProcessing.AddMessageGroup(new MessageGroup(TonemapperMissing, MessageType.Warning).SetDocumentation("https://gitlab.com/s-ilent/SCSS/-/wikis/Other/Post-Processing#colour-grading"));
                                }
                                else if (noGlobalTonemapper)
                                {
                                    postProcessing.AddMessageGroup(new MessageGroup(GlobalTonemapperMissing, MessageType.Tips).SetDocumentation("https://gitlab.com/s-ilent/SCSS/-/wikis/Other/Post-Processing#colour-grading"));
                                }
                            }
                        }
                    }

                    if (!postProcessing.HasMessages())
                    {
                        postProcessing.AddMessageGroup(new MessageGroup(NoProblemsFoundInPp, MessageType.Info));
                    }
                }
#elif VRWT_IS_VRC && (UNITY_IOS || UNITY_ANDROID)
                postProcessing.AddMessageGroup(new MessageGroup(PostProcessingNotSupported, MessageType.Info));
#else
                postProcessing.AddMessageGroup(new MessageGroup(PostProcessingGenericProjectNotice, MessageType.Info));
#endif

                // GameObject checks

                var importers = new List<ModelImporter>();

                var unCrunchedTextures = new List<Texture>();
                var badShaders = 0;
                var textureCount = 0;

                var missingShaders = new List<Material>();
                var selectablesNotNone = new List<Selectable>();
                var scrollRectsScrollSensitivityNotZero = new List<ScrollRect>();
                var legacyBlendShapes = new List<ModelImporter>();
                var particleSysAllowRoll = new List<ParticleSystemRenderer>();

                var checkedMaterials = new List<Material>();
                var checkedShaders = new Dictionary<Shader, CheckedShaderProperties>();

                var mirrorsDefaultLayers = optimization.AddMessageGroup(new MessageGroup(MirrorWithDefaultLayers, MirrorWithDefaultLayersCombined, MirrorWithDefaultLayersInfo, MessageType.Tips));
                var legacyBlendShapeIssues = general.AddMessageGroup(new MessageGroup(LegacyBlendShapeIssues, LegacyBlendShapeIssuesCombined, LegacyBlendShapeIssuesInfo, MessageType.Warning));
                var grabPassShaders = general.AddMessageGroup(new MessageGroup(MaterialWithGrabPassShader, MaterialWithGrabPassShaderCombined, androidBuildPlatform ? MaterialWithGrabPassShaderInfoPC : MaterialWithGrabPassShaderInfoAndroid, androidBuildPlatform ? MessageType.Error : MessageType.Info));
                var materialWithNonWhitelistedShader = general.AddMessageGroup(new MessageGroup(MaterialWithNonWhitelistedShader, MaterialWithNonWhitelistedShaderCombined, MaterialWithNonWhitelistedShaderInfo, MessageType.Warning).SetDocumentation("https://creators.vrchat.com/platforms/android/quest-content-limitations/#shaders"));
                var uiElementNavigation = general.AddMessageGroup(new MessageGroup(UIElementWithNavigationNotNone, UIElementWithNavigationNotNoneCombined, UIElementWithNavigationNotNoneInfo, MessageType.Tips));
                var scrollRectScrollSensitivity = general.AddMessageGroup(new MessageGroup(ScrollRectWithScrollSensitivityNotZero, ScrollRectWithScrollSensitivityNotZeroCombined, ScrollRectWithScrollSensitivityNotZeroInfo, MessageType.Tips));
                var textMeshStatic = general.AddMessageGroup(new MessageGroup(TextMeshLightmapStatic, TextMeshLightmapStaticCombined, TextMeshLightmapStaticInfo, MessageType.Warning));
                var unsupportedCompressionFormatAndroid = general.AddMessageGroup(new MessageGroup(UnsupportedCompressionFormatAndroid, UnsupportedCompressionFormatAndroidCombined, UnsupportedCompressionFormatAndroidInfo, MessageType.Error).SetDocumentation("https://docs.unity3d.com/2022.3/Documentation/Manual/class-TextureImporterOverride.html"));
                var particlesAllowRoll = general.AddMessageGroup(new MessageGroup(ParticlesWithAllowRoll, ParticlesWithAllowRollCombined, ParticlesWithAllowRollInfo, MessageType.Tips));

                var allGameObjects = Resources.FindObjectsOfTypeAll(typeof(GameObject));
                for (var i = 0; i < allGameObjects.Length; i++)
                {
                    var gameObject = allGameObjects[i] as GameObject;

                    if (gameObject.hideFlags != HideFlags.None || EditorUtility.IsPersistent(gameObject.transform.root.gameObject)) continue;

                    var staticEditorFlags = GameObjectUtility.GetStaticEditorFlags(gameObject);
                    var hasMeshRenderer = false;
                    var renderers = gameObject.GetComponents<Renderer>();

                    for (var k = 0; k < renderers.Length; k++)
                    {
                        var renderer = renderers[k];

                        if (renderer.GetType() == typeof(MeshRenderer))
                        {
                            hasMeshRenderer = true;

                            // If baked lighting in the scene check for lightmap uvs
                            if (bakedLighting && (staticEditorFlags & StaticEditorFlags.ContributeGI) != 0 && !xatlasUnwrapper)
                            {
                                var meshFilter = gameObject.GetComponent<MeshFilter>();

                                if (meshFilter != null)
                                {
                                    var sharedMesh = meshFilter.sharedMesh;

                                    if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sharedMesh)) != null)
                                    {
                                        var modelImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sharedMesh)) as ModelImporter;

                                        if (!importers.Contains(modelImporter))
                                        {
                                            if (modelImporter != null)
                                            {
                                                var so = new SerializedObject(renderer);

                                                if (!modelImporter.generateSecondaryUV && sharedMesh.uv2.Length == 0 && so.FindProperty("m_ScaleInLightmap").floatValue != 0)
                                                {
                                                    importers.Add(modelImporter);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (renderer.GetType() == typeof(SkinnedMeshRenderer))
                        {
                            var skinnedMesh = (SkinnedMeshRenderer)renderer;
                            var sharedMesh = skinnedMesh.sharedMesh;
                            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sharedMesh)) as ModelImporter;

                            if (importer != null)
                            {
                                if (sharedMesh.blendShapeCount > 0 && importer.importBlendShapeNormals == ModelImporterNormals.Calculate && !ModelImporterUtil.GetLegacyBlendShapeNormals(importer))
                                {
                                    legacyBlendShapes.Add(importer);
                                    legacyBlendShapeIssues.AddSingleMessage(new SingleMessage(Path.GetFileName(AssetDatabase.GetAssetPath(sharedMesh)), EditorUtility.FormatBytes(Profiler.GetRuntimeMemorySizeLong(sharedMesh))).SetAssetPath(importer.assetPath).SetAutoFix(SetLegacyBlendShapeNormals(importer)));
                                }
                            }
                        }

                        // Check materials for problems
                        for (var l = 0; l < renderer.sharedMaterials.Length; l++)
                        {
                            var material = renderer.sharedMaterials[l];

                            if (checkedMaterials.Contains(material) || material == null) continue;

                            checkedMaterials.Add(material);

                            var shader = material.shader;

                            if (androidBuildPlatform && !Validation.WorldShaderWhiteList.Contains(shader.name))
                            {
                                var singleMessage = new SingleMessage(material.name, shader.name);

                                if (AssetDatabase.GetAssetPath(material).EndsWith(".mat"))
                                {
                                    singleMessage.SetAssetPath(AssetDatabase.GetAssetPath(material));
                                }
                                else
                                {
                                    singleMessage.SetSelectObject(gameObject);
                                }

                                materialWithNonWhitelistedShader.AddSingleMessage(singleMessage);
                            }

                            if (!checkedShaders.ContainsKey(shader) && AssetDatabase.GetAssetPath(shader) != null)
                            {
                                var assetPath = AssetDatabase.GetAssetPath(shader);

                                if (File.Exists(assetPath))
                                {
                                    var checkedShaderProperties = new CheckedShaderProperties();

                                    // Read shader file to string
                                    var word = File.ReadAllText(assetPath);

                                    // Strip comments
                                    word = Regex.Replace(word, "(\\/\\/.*)|(\\/\\*)(.*)(\\*\\/)", "");

                                    // Match for GrabPass and check if it's active
                                    var grabPassMatch = Regex.Match(word, "GrabPass\\s*{[\\s\\S]*?}");
                                    if (grabPassMatch.Success)
                                    {
                                        checkedShaderProperties.IncludesGrabPass = true;
                                        var lightModeTags = Regex.Matches(grabPassMatch.Value, "[\"|']LightMode[\"|']\\s*=\\s*[\"|'](\\w*)[\"|']");

                                        if (lightModeTags.Count > 0)
                                        {
                                            for (var j = 0; j < lightModeTags.Count; j++)
                                            {
                                                checkedShaderProperties.GrabPassLightModeTags.Add(lightModeTags[j].Groups[1].Value);
                                            }
                                        }
                                    }

                                    checkedShaders.Add(shader, checkedShaderProperties);
                                }
                            }

                            if (checkedShaders.TryGetValue(shader, out var checkedShader))
                            {
                                if (checkedShader.IncludesGrabPass)
                                {
                                    var grabPassActive = false;
                                    if (checkedShader.GrabPassLightModeTags.Count > 0)
                                    {
                                        for (var j = 0; j < checkedShader.GrabPassLightModeTags.Count; j++)
                                        {
                                            if (material.GetShaderPassEnabled(checkedShader.GrabPassLightModeTags[j])) grabPassActive = true;
                                        }
                                    }
                                    else
                                    {
                                        grabPassActive = true;
                                    }

                                    if (grabPassActive) grabPassShaders.AddSingleMessage(new SingleMessage(material.name, shader.name).SetAssetPath(AssetDatabase.GetAssetPath(material)));
                                }
                            }

                            if (shader.name == "Hidden/InternalErrorShader" && !missingShaders.Contains(material))
                                missingShaders.Add(material);

                            if (shader.name.StartsWith(".poiyomi") || shader.name.StartsWith("poiyomi") || shader.name.StartsWith("arktoon") || shader.name.StartsWith("Cubedparadox") || shader.name.StartsWith("Silent's Cel Shading") || shader.name.StartsWith("Xiexe"))
                                badShaders++;

                            for (var j = 0; j < ShaderUtil.GetPropertyCount(shader); j++)
                            {
                                if (ShaderUtil.GetPropertyType(shader, j) == ShaderUtil.ShaderPropertyType.TexEnv)
                                {
                                    var texture = material.GetTexture(ShaderUtil.GetPropertyName(shader, j));

                                    if (AssetDatabase.GetAssetPath(texture) != "" && !unCrunchedTextures.Contains(texture))
                                    {
                                        var assetPath = AssetDatabase.GetAssetPath(texture);
                                        var textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;

                                        if (textureImporter != null)
                                        {
                                            if (!unCrunchedTextures.Contains(texture))
                                            {
                                                textureCount++;
                                            }

                                            var platformTextureSettings = textureImporter.GetPlatformTextureSettings("Android");
                                            if (platformTextureSettings.overridden && Validation.UnsupportedCompressionFormatsAndroid.Contains(platformTextureSettings.format))
                                            {
                                                unsupportedCompressionFormatAndroid.AddSingleMessage(new SingleMessage(texture.name, platformTextureSettings.format.ToString()).SetAssetPath(assetPath));
                                            }

                                            if (!textureImporter.crunchedCompression && !unCrunchedTextures.Contains(texture) && !textureImporter.textureCompression.Equals(TextureImporterCompression.Uncompressed) && EditorTextureUtil.GetStorageMemorySize(texture) > 500000)
                                            {
                                                unCrunchedTextures.Add(texture);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (hasMeshRenderer)
                    {
                        if ((staticEditorFlags & StaticEditorFlags.ContributeGI) != 0 && gameObject.GetComponent<TextMesh>())
                        {
                            textMeshStatic.AddSingleMessage(new SingleMessage(gameObject.name).SetSelectObject(gameObject));
                        }

#if VRWT_IS_VRC
                        var mirror = gameObject.GetComponent<VRC_MirrorReflection>();
                        if (mirror != null)
                        {
                            var mirrorMask = mirror.m_ReflectLayers;

                            if (mirrorMask.value == -1025)
                            {
                                mirrorsDefaultLayers.AddSingleMessage(new SingleMessage(gameObject.name).SetSelectObject(gameObject));
                            }
                        }
#endif
                    }

                    var selectable = gameObject.GetComponent<Selectable>();
                    if (selectable != null)
                    {
                        if (selectable.navigation.mode != Navigation.Mode.None)
                        {
                            uiElementNavigation.AddSingleMessage(new SingleMessage(gameObject.name).SetSelectObject(gameObject).SetAutoFix(SetSelectableNavigationMode(selectable, Navigation.Mode.None)));

                            selectablesNotNone.Add(selectable);
                        }
                    }

                    var scrollRect = gameObject.GetComponent<ScrollRect>();
                    if (scrollRect != null)
                    {
                        if (scrollRect.scrollSensitivity != 0)
                        {
                            scrollRectScrollSensitivity.AddSingleMessage(new SingleMessage(gameObject.name).SetSelectObject(gameObject).SetAutoFix(SetScrollRectScrollSensitivity(scrollRect, 0)));

                            scrollRectsScrollSensitivityNotZero.Add(scrollRect);
                        }
                    }

                    // Check for 'Allow Roll' on particle systems.
                    // It's not necessary to change this for non-VR projects
#if VRWT_IS_VRC
                    var particleSysRenderer = gameObject.GetComponent<ParticleSystemRenderer>();
                    if (particleSysRenderer)
                    {
                        if (particleSysRenderer.allowRoll)
                        {
                            particlesAllowRoll.AddSingleMessage(new SingleMessage(gameObject.name).SetSelectObject(gameObject).SetAutoFix(SetParticleSystemAllowRoll(particleSysRenderer, false)));

                            particleSysAllowRoll.Add(particleSysRenderer);
                        }
                    }
#endif
                }

                if (legacyBlendShapes.Count > 1)
                {
                    legacyBlendShapeIssues.SetGroupAutoFix(SetLegacyBlendShapeNormals(legacyBlendShapes.ToArray()));
                }

                if (selectablesNotNone.Count > 1)
                {
                    uiElementNavigation.SetGroupAutoFix(SetSelectableNavigationMode(selectablesNotNone.ToArray(), Navigation.Mode.None));
                }

                if (scrollRectsScrollSensitivityNotZero.Count > 1)
                {
                    scrollRectScrollSensitivity.SetGroupAutoFix(SetScrollRectScrollSensitivity(scrollRectsScrollSensitivityNotZero.ToArray(), 0));
                }

                if (particleSysAllowRoll.Count > 1)
                {
                    particlesAllowRoll.SetGroupAutoFix(SetParticleSystemAllowRoll(particleSysAllowRoll.ToArray(), false));
                }

                // If more than 10% of shaders used in scene are toon shaders to leave room for people using them for avatar displays
                if (checkedMaterials.Count > 0)
                {
                    if (badShaders / checkedMaterials.Count * 100 > 10)
                    {
                        optimization.AddMessageGroup(new MessageGroup(NoToonShaders, MessageType.Warning));
                    }
                }

                // Suggest to crunch textures if there are any uncrunched textures found
                if (textureCount > 0)
                {
                    var percent = (int)((float)unCrunchedTextures.Count / (float)textureCount * 100f);
                    if (percent > 20)
                    {
                        optimization.AddMessageGroup(new MessageGroup(NonCrunchedTextures, MessageType.Tips).AddSingleMessage(new SingleMessage(percent.ToString()).SetAutoFix(MassTextureImporter.ShowWindow)));
                    }
                }

                var modelsCount = importers.Count;
                if (modelsCount > 0)
                {
                    var noUVGroup = new MessageGroup(NoLightmapUV, NoLightmapUVCombined, NoLightmapUVInfo, MessageType.Warning);
                    for (var i = 0; i < modelsCount; i++)
                    {
                        var modelImporter = importers[i];

                        noUVGroup.AddSingleMessage(new SingleMessage(Path.GetFileName(AssetDatabase.GetAssetPath(modelImporter))).SetAutoFix(SetGenerateLightmapUV(modelImporter)).SetAssetPath(modelImporter.assetPath));
                    }

                    lighting.AddMessageGroup(noUVGroup.SetGroupAutoFix(SetGenerateLightmapUV(importers)).SetDocumentation("https://docs.unity3d.com/2022.3/Documentation/Manual/LightingGiUvs-GeneratingLightmappingUVs.html"));
                }

                var missingShadersCount = missingShaders.Count;
                if (missingShadersCount > 0)
                {
                    var missingShadersGroup = new MessageGroup(MissingShaderWarning, MissingShaderWarningCombined, MissingShaderWarningInfo, MessageType.Error);
                    for (var i = 0; i < missingShaders.Count; i++)
                    {
                        missingShadersGroup.AddSingleMessage(new SingleMessage(missingShaders[i].name).SetAssetPath(AssetDatabase.GetAssetPath(missingShaders[i])).SetAutoFix(ChangeShader(missingShaders[i], "Standard")));
                    }

                    general.AddMessageGroup(missingShadersGroup.SetGroupAutoFix(ChangeShader(missingShaders.ToArray(), "Standard")));
                }
            }
            catch (Exception exception)
            {
                general.AddMessageGroup(new MessageGroup(HeyYouFoundABug, MessageType.Error)).AddSingleMessage(new SingleMessage(exception.Message.Replace("\n", " ").Replace("\r", ""), Regex.Matches(exception.StackTrace, "(?<=\\.cs:).*(?<=\\S)")[0].ToString()));
                Debug.LogError(exception);
                autoRecheck = false;
            }
        }

        private void OnFocus()
        {
            if (initDone)
            {
                RefreshBuild();
            }

            recheck = true;
        }

        private const string LastBuild = "Library/LastBuild.buildreport";

        private const string BuildReportDir = "Assets/_LastBuild/";

        private const string LastBuildReportPath = "Assets/_LastBuild/LastBuild.buildreport";
        private const string WindowsBuildReportPath = "Assets/_LastBuild/LastWindowsBuild.buildreport";
        private const string AndroidBuildReportPath = "Assets/_LastBuild/LastAndroidBuild.buildreport";
        private const string iOSBuildReportPath = "Assets/_LastBuild/LastiOSBuild.buildreport";

        [SerializeField] private BuildReport buildReportWindows;
        [SerializeField] private BuildReport buildReportAndroid;
        [SerializeField] private BuildReport buildReportiOS;

        [SerializeField] private TreeViewState treeViewState;
        [SerializeField] private MultiColumnHeaderState multiColumnHeaderState;

        private BuildReportTreeView buildReportTreeView;
        private SearchField searchField;

        private void RefreshBuild()
        {
#if VRWT_BENCHMARK
            CheckTime.Restart();
#endif
            if (!Directory.Exists(BuildReportDir))
                Directory.CreateDirectory(BuildReportDir);
            if (File.Exists(LastBuild) && (!File.Exists(LastBuildReportPath) || File.GetLastWriteTime(LastBuild) > File.GetLastWriteTime(LastBuildReportPath)))
            {
                File.Copy(LastBuild, LastBuildReportPath, true);
                AssetDatabase.ImportAsset(LastBuildReportPath);
            }

            var newBuildSet = false;
            if (File.Exists(LastBuildReportPath))
            {
                switch (AssetDatabase.LoadAssetAtPath<BuildReport>(LastBuildReportPath).summary.platform)
                {
                    case BuildTarget.StandaloneWindows:
                    case BuildTarget.StandaloneWindows64:
                        if (File.GetLastWriteTime(LastBuildReportPath) > File.GetLastWriteTime(WindowsBuildReportPath))
                        {
                            AssetDatabase.CopyAsset(LastBuildReportPath, WindowsBuildReportPath);
                            buildReportWindows = (BuildReport)AssetDatabase.LoadAssetAtPath(WindowsBuildReportPath, typeof(BuildReport));
                            newBuildSet = true;
                        }

                        break;
                    case BuildTarget.Android:
                        if (File.GetLastWriteTime(LastBuildReportPath) > File.GetLastWriteTime(AndroidBuildReportPath))
                        {
                            AssetDatabase.CopyAsset(LastBuildReportPath, AndroidBuildReportPath);
                            buildReportAndroid = (BuildReport)AssetDatabase.LoadAssetAtPath(AndroidBuildReportPath, typeof(BuildReport));
                            newBuildSet = true;
                        }

                        break;
                    case BuildTarget.iOS:
                        if (File.GetLastWriteTime(LastBuildReportPath) > File.GetLastWriteTime(iOSBuildReportPath))
                        {
                            AssetDatabase.CopyAsset(LastBuildReportPath, iOSBuildReportPath);
                            buildReportiOS = (BuildReport)AssetDatabase.LoadAssetAtPath(iOSBuildReportPath, typeof(BuildReport));
                            newBuildSet = true;
                        }

                        break;
                }
            }

            if (buildReportWindows is null && File.Exists(WindowsBuildReportPath))
            {
                buildReportWindows = (BuildReport)AssetDatabase.LoadAssetAtPath(WindowsBuildReportPath, typeof(BuildReport));
            }

            if (buildReportAndroid is null && File.Exists(AndroidBuildReportPath))
            {
                buildReportAndroid = (BuildReport)AssetDatabase.LoadAssetAtPath(AndroidBuildReportPath, typeof(BuildReport));
            }

            if (buildReportiOS is null && File.Exists(iOSBuildReportPath))
            {
                buildReportiOS = (BuildReport)AssetDatabase.LoadAssetAtPath(iOSBuildReportPath, typeof(BuildReport));
            }

            if (buildReportInitDone)
            {
                BuildReport report = null;

                if (newBuildSet)
                {
                    switch (Helper.BuildPlatform())
                    {
                        case RuntimePlatform.WindowsPlayer:
                            report = buildReportWindows;
                            selectedBuildReport = BuildReportType.Windows;
                            break;
                        case RuntimePlatform.Android:
                            report = buildReportAndroid;
                            selectedBuildReport = BuildReportType.Android;
                            break;
                        case RuntimePlatform.IPhonePlayer:
                            report = buildReportiOS;
                            selectedBuildReport = BuildReportType.iOS;
                            break;
                    }
                }
                else
                {
                    if (selectedBuildReport == BuildReportType.iOS && buildReportiOS != null)
                    {
                        report = buildReportiOS;
                    }
                    else if (selectedBuildReport == BuildReportType.Android && buildReportAndroid != null)
                    {
                        report = buildReportAndroid;
                    }
                    else
                    {
                        selectedBuildReport = BuildReportType.Windows;
                        report = buildReportWindows;
                    }
                }

                buildReportTreeView.SetReport(report);
            }

#if VRWT_BENCHMARK
            CheckTime.Stop();
            Debug.Log($"Refreshed build reports in: {CheckTime.ElapsedMilliseconds} ms.");
#endif
        }

        [NonSerialized] private bool initDone;
        [NonSerialized] private bool buildReportInitDone;

        [SerializeField] private bool firstRefresh = true;

        [SerializeField] private MessageCategoryList mainList;

        [SerializeField] private MessageCategory general;
        [SerializeField] private MessageCategory optimization;
        [SerializeField] private MessageCategory lighting;
        [SerializeField] private MessageCategory postProcessing;

        private void InitWhenNeeded()
        {
            if (!initDone)
            {
#if VRWT_BENCHMARK
                CheckTime.Restart();
#endif
                RefreshBuild();

                if (mainList is null)
                    mainList = new MessageCategoryList();

                general = mainList.CreateOrGetCategory("常规");

                optimization = mainList.CreateOrGetCategory("优化");

                lighting = mainList.CreateOrGetCategory("光照");

                postProcessing = mainList.CreateOrGetCategory("后期处理");

#if UDON
                projectType = ProjectType.World;
#elif VRWT_IS_VRC
                projectType = ProjectType.Avatar;
#else
                projectType = ProjectType.Generic;
#endif

                initDone = true;
#if VRWT_BENCHMARK
                CheckTime.Stop();
                Debug.Log($"Main initialization done in: {CheckTime.ElapsedMilliseconds} ms.");
#endif
            }

            if (!buildReportInitDone && tab == 1)
            {
#if VRWT_BENCHMARK
                CheckTime.Restart();
#endif
                var firstInit = multiColumnHeaderState == null;
                var headerState = BuildReportTreeView.CreateDefaultMultiColumnHeaderState(EditorGUIUtility.currentViewWidth - 121);
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(multiColumnHeaderState, headerState))
                    MultiColumnHeaderState.OverwriteSerializedFields(multiColumnHeaderState, headerState);
                multiColumnHeaderState = headerState;

                var multiColumnHeader = new MultiColumnHeader(headerState);
                if (firstInit)
                    multiColumnHeader.ResizeToFit();

                if (treeViewState is null)
                {
                    treeViewState = new TreeViewState();
                }

                BuildReport report;
                if (selectedBuildReport == BuildReportType.iOS && buildReportiOS != null)
                {
                    report = buildReportiOS;
                }
                else if (selectedBuildReport == BuildReportType.Android && buildReportAndroid != null)
                {
                    report = buildReportAndroid;
                }
                else
                {
                    selectedBuildReport = BuildReportType.Windows;
                    report = buildReportWindows;
                }

                buildReportTreeView = new BuildReportTreeView(treeViewState, multiColumnHeader, report);
                searchField = new SearchField();
                searchField.downOrUpArrowKeyPressed += buildReportTreeView.SetFocusAndEnsureSelectedItem;

                buildReportInitDone = true;
#if VRWT_BENCHMARK
                CheckTime.Stop();
                Debug.Log($"Build report initialization done in: {CheckTime.ElapsedMilliseconds} ms.");
#endif
            }
        }

        private static readonly Stopwatch CheckTime = new();

        private void Refresh()
        {
            if (tab == 0 && recheck && autoRecheck && !EditorApplication.isPlaying)
            {
                // Check for bloat in occlusion cache
                if (occlusionCacheFiles == 0 && Directory.Exists("Library/Occlusion/"))
                {
                    Task.Run(CountOcclusionCacheFiles);
                }

                CheckTime.Restart();

                switch (projectType)
                {
                    case ProjectType.Generic:
                    case ProjectType.World:
                        CheckScene();
                        break;
                }

                CheckTime.Stop();

                if (firstRefresh)
                {
                    firstRefresh = false;
                }
                else if (CheckTime.ElapsedMilliseconds >= 500)
                {
                    autoRecheck = false;
                }

#if VRWT_BENCHMARK
                Debug.Log("Checks done in: " + CheckTime.ElapsedMilliseconds + " ms.");
#endif

                recheck = false;
            }
        }

        private enum ProjectType
        {
            NotDetected,
            Generic,
            World,
            Avatar
        }

        private ProjectType projectType = ProjectType.NotDetected;

        private static readonly string[] MainToolbar =
        {
            "消息", "构建报告"
        };

        private enum BuildReportType
        {
            Windows = 0,
            Android = 1,
            iOS = 2
        }

        private static readonly string[] BuildReportSelectionDropdown =
        {
            "Windows", "Android", "iOS"
        };

        [SerializeField] private BuildReportType selectedBuildReport;
        [SerializeField] private BuildReportType previousSelectedBuildReport;
        [SerializeField] private bool overallStatsFoldout;
        [SerializeField] private bool buildReportMessagesFoldout;

        // This is used to delay when the first scene check happens since for some reason
        // doing it too early in Unity 2022 causes noticeable lag especially in bigger scenes
        private static readonly Stopwatch InitializationDelayTimer = new();

        private void OnGUI()
        {
            var current = Event.current;

            if (current.type == EventType.Layout)
            {
                InitWhenNeeded();
                if (!firstRefresh)
                {
                    Refresh();
                }
                else
                {
                    InitializationDelayTimer.Start();
                    if (InitializationDelayTimer.ElapsedMilliseconds  >= 500)
                    {
                        InitializationDelayTimer.Stop();
                        Refresh();
                    }
                }
            }

            DrawBuildReportOverviews(current);

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            tab = GUILayout.Toolbar(tab, MainToolbar);

            switch (tab)
            {
                case 0:
                    MessagesTab();
                    break;
                case 1:
                    BuildReportTab();
                    break;
            }
        }

        private void DrawBuildReportOverviews(Event current)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (buildReportWindows)
                {
                    DrawOverview(buildReportWindows, BuildReportType.Windows);
                }

                if (buildReportAndroid)
                {
                    DrawOverview(buildReportAndroid, BuildReportType.Android);
                }

                if (buildReportiOS)
                {
                    DrawOverview(buildReportiOS, BuildReportType.iOS);
                }
            }

            void DrawOverview(BuildReport report, BuildReportType type)
            {
                using (var verticalScope = new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label($"上次 {type.ToString()} 构建：", EditorStyles.boldLabel);

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        GUILayout.Label("<b>构建大小：</b> " + EditorUtility.FormatBytes((long)report.summary.totalSize), Styles.LabelRichText);

                        var currentCulture = CultureInfo.CurrentCulture;
                        var dateTimeFormat = currentCulture.DateTimeFormat;

                        GUILayout.Label("<b>构建日期：</b> " + report.summary.buildEndedAt.ToLocalTime().ToString(dateTimeFormat.ShortDatePattern), Styles.LabelRichText);

                        GUILayout.Label("<b>构建时间：</b> " + report.summary.buildEndedAt.ToLocalTime().ToString(dateTimeFormat.ShortTimePattern), Styles.LabelRichText);

                        GUILayout.Label("<b>构建耗时：</b> " + (report.summary.buildEndedAt - report.summary.buildStartedAt).ToString(@"hh\:mm\:ss"), Styles.LabelRichText);

                        GUILayout.Label("<b>构建中的错误：</b> " + report.summary.totalErrors, Styles.LabelRichText);

                        GUILayout.Label("<b>构建中的警告：</b> " + report.summary.totalWarnings, Styles.LabelRichText);

                        GUILayout.Label("<b>构建结果：</b> " + report.summary.result, Styles.LabelRichText);
                    }

                    if (current.type == EventType.MouseUp && verticalScope.rect.Contains(current.mousePosition))
                    {
                        tab = 1;
                        selectedBuildReport = type;
                    }
                }

                EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
            }
        }

        private void MessagesTab()
        {
            switch (projectType)
            {
                case ProjectType.NotDetected:
                    ProjectTypeNotDetected();
                    break;
                case ProjectType.Generic:
                case ProjectType.World:
                    if (EditorApplication.isPlaying)
                    {
                        GUILayout.FlexibleSpace();

                        EditorGUILayout.LabelField("编辑器当前处于运行模式。", Styles.CenteredNoticeLabel, GUILayout.ExpandWidth(true), GUILayout.Height(20));
                        EditorGUILayout.LabelField("停止运行以查看消息。", Styles.CenteredNoticeLabel, GUILayout.ExpandWidth(true), GUILayout.Height(20));

                        GUILayout.FlexibleSpace();
                    }
                    else if (firstRefresh)
                    {
                        GUILayout.FlexibleSpace();

                        EditorGUILayout.LabelField("加载中...", Styles.CenteredNoticeLabel, GUILayout.ExpandWidth(true), GUILayout.Height(20));

                        GUILayout.FlexibleSpace();
                    }
                    else
                    {
                        if (!autoRecheck && GUILayout.Button("刷新"))
                        {
                            recheck = true;
                            autoRecheck = true;
                        }

                        mainList.DrawTabSelector();

                        mainList.DrawMessages();
                    }

                    break;
                case ProjectType.Avatar:
                    ProjectTypeNotSupportedYet();
                    break;
            }
        }

        private void ProjectTypeNotDetected()
        {
            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField($"当前项目类型未检测到。", Styles.CenteredNoticeLabel, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.Height(40));

            GUILayout.FlexibleSpace();
        }

        private void ProjectTypeNotSupportedYet()
        {
            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField($"{projectType} 项目\n尚未完全支持。", Styles.CenteredNoticeLabel, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.Height(40));

            GUILayout.FlexibleSpace();
        }

        private void BuildReportTab()
        {
            if (buildReportInitDone)
            {
                GUILayout.BeginVertical();

                GUILayout.BeginHorizontal(EditorStyles.toolbar);

                selectedBuildReport = (BuildReportType)EditorGUILayout.Popup((int)selectedBuildReport, BuildReportSelectionDropdown, EditorStyles.toolbarPopup);

                if (selectedBuildReport != previousSelectedBuildReport)
                {
                    switch (selectedBuildReport)
                    {
                        case BuildReportType.Windows:
                            buildReportTreeView.SetReport(buildReportWindows);
                            break;
                        case BuildReportType.Android:
                            buildReportTreeView.SetReport(buildReportAndroid);
                            break;
                        case BuildReportType.iOS:
                            buildReportTreeView.SetReport(buildReportiOS);
                            break;
                    }

                    previousSelectedBuildReport = selectedBuildReport;
                }

                GUILayout.Space(10);

                GUILayout.FlexibleSpace();

                overallStatsFoldout = GUILayout.Toggle(overallStatsFoldout, "统计", EditorStyles.toolbarButton);

                buildReportMessagesFoldout = GUILayout.Toggle(buildReportMessagesFoldout, "消息", EditorStyles.toolbarButton);

                if (GUILayout.Button("刷新", EditorStyles.toolbarButton))
                {
                    RefreshBuild();

                    if (buildReportTreeView.BuildSucceeded)
                    {
                        buildReportTreeView.Reload();
                    }
                    else
                    {
                        if (buildReportWindows != null)
                        {
                            buildReportTreeView.SetReport(buildReportWindows);
                        }
                        else if (buildReportAndroid != null)
                        {
                            buildReportTreeView.SetReport(buildReportAndroid);
                        }
                        else if (buildReportiOS != null)
                        {
                            buildReportTreeView.SetReport(buildReportiOS);
                        }
                    }
                }

                GUILayout.Space(5);

                buildReportTreeView.searchString = searchField.OnToolbarGUI(buildReportTreeView.searchString);

                GUILayout.Space(5);

                GUILayout.EndHorizontal();

                GUILayout.EndVertical();

                if (buildReportMessagesFoldout)
                {
                    buildReportTreeView.DrawMessages();
                }
                else
                {
                    if (overallStatsFoldout)
                    {
                        buildReportTreeView.DrawOverallStats();
                    }

                    var treeViewRect = EditorGUILayout.BeginVertical();

                    if (buildReportTreeView.BuildSucceeded)
                    {
                        buildReportTreeView.OnGUI(treeViewRect);
                    }
                    else
                    {
                        GUILayout.FlexibleSpace();

                        if (!buildReportTreeView.HasReport)
                        {
                            EditorGUILayout.LabelField($"未找到上次构建", Styles.CenteredNoticeLabel, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.Height(40));
                        }
                        else
                        {
                            EditorGUILayout.LabelField($"上次 {selectedBuildReport.ToString()} 构建失败", Styles.CenteredNoticeLabel, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.Height(40));
                        }

                        GUILayout.FlexibleSpace();
                    }

                    GUILayout.FlexibleSpace();

                    EditorGUILayout.EndVertical();
                }
            }
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }
    }
}
