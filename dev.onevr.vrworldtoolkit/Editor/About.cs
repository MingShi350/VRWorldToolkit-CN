using System;
using UnityEditor;
using UnityEngine;

namespace VRWorldToolkit.Editor
{
    public class About : EditorWindow
    {
        [MenuItem("VRWorld Toolkit/关于 VRWorld Toolkit", false, 40)]
        public static void ShowWindow()
        {
            var window = (About) GetWindow(typeof(About), true, "关于 VRWorld Toolkit");
            window.minSize = new Vector2(600, 380);
            window.maxSize = new Vector2(600, 380);
            window.Show();
        }

        private static GUIStyle header, text;

        private static Texture iconTwitter, iconDiscord, iconGithub;

        [NonSerialized] private int clickCounter;

        public void OnEnable()
        {
            header = new GUIStyle
            {
                normal =
                {
                    background = Resources.Load("VRWorldToolkit/SplashTextures/VRWTSplashLogo") as Texture2D,
                    textColor = Color.white,
                },
                fixedHeight = 140
            };

            iconTwitter = Resources.Load("VRWorldToolkit/SplashTextures/IconTwitter") as Texture2D;
            iconDiscord = Resources.Load("VRWorldToolkit/SplashTextures/IconDiscord") as Texture2D;
            iconGithub = Resources.Load("VRWorldToolkit/SplashTextures/IconGithub") as Texture2D;
        }

        private void OnGUI()
        {
            // Header Image
            if (GUILayout.Button("", header))
            {
                clickCounter++;
                if (clickCounter >= 10)
                {
                    Debug.Log("已切换 VRWorld Toolkit 基准测试模式");
#if VRWT_BENCHMARK
                    ScriptingDefineManager.RemoveScriptingDefine("VRWT_BENCHMARK");
#else
                    ScriptingDefineManager.AddScriptingDefine("VRWT_BENCHMARK");
#endif
                }
            };

            // Information Texts
            GUILayout.Label("欢迎使用 VRWorld Toolkit！", EditorStyles.boldLabel);

            GUILayout.Label("VRWorld Toolkit 旨在帮助大家更快上手世界构建，不必花费时间翻阅各种文档来排查初次建世界时容易犯的各种小错误。即使是有经验的世界构建者，它也能帮你加快后期处理设置等繁琐步骤，让你不会忘记构建世界时需要留意的许多细节。", Styles.RichTextWrap);

            GUILayout.Label("如果你有任何建议、发现工具中的问题、或想查看我的社交频道，可以点击下方的按钮。欢迎随时反馈，让我知道哪里可以改进！", Styles.RichTextWrap);

            GUILayout.FlexibleSpace();

            // Social Buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(iconTwitter, GUIStyle.none)) Application.OpenURL("https://twitter.com/oneVRdev");
            GUILayout.Space(20);
            if (GUILayout.Button(iconDiscord, GUIStyle.none)) Application.OpenURL("https://discord.gg/8w2Tc6C");
            GUILayout.Space(20);
            if (GUILayout.Button(iconGithub, GUIStyle.none)) Application.OpenURL("https://github.com/oneVR/VRWorldToolkit");

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(20);
        }
    }
}