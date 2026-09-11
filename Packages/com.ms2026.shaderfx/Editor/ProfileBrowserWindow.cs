using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS2026.ShaderFX.Editor
{
    public sealed class ProfileBrowserWindow : EditorWindow
    {
        [MenuItem("Tools/ShaderFX/Profile Browser")]
        private static void Open()
        {
            var window = GetWindow<ProfileBrowserWindow>("ShaderFX Profiles");
            window.minSize = new Vector2(340, 240);
            window.Refresh();
        }

        private string searchText = "";
        private Vector2 scroll;
        private readonly List<EffectProfile> profiles = new();

        private void OnEnable() => Refresh();

        private void Refresh()
        {
            profiles.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:EffectProfile"))
            {
                var profile = AssetDatabase.LoadAssetAtPath<EffectProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile != null) profiles.Add(profile);
            }
            profiles.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Search", GUILayout.Width(45));
                searchText = EditorGUILayout.TextField(searchText, EditorStyles.toolbarSearchField);
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60))) Refresh();
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            int shown = 0;
            foreach (var profile in profiles)
            {
                if (profile == null) continue;
                if (!string.IsNullOrEmpty(searchText) &&
                    profile.name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0) continue;

                shown++;
                DrawRow(profile);
            }

            if (shown == 0)
            {
                EditorGUILayout.HelpBox(profiles.Count == 0
                    ? "プロジェクト内に EffectProfile が見つかりません。"
                    : "検索条件に一致する EffectProfile がありません。", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawRow(EffectProfile profile)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var icon = EditorGUIUtility.ObjectContent(profile, typeof(EffectProfile)).image;
                GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(profile.name, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(BuildSummary(profile), EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Ping", GUILayout.Width(50))) EditorGUIUtility.PingObject(profile);
                if (GUILayout.Button("Select", GUILayout.Width(55))) Selection.activeObject = profile;
            }
        }

        private static string BuildSummary(EffectProfile profile)
        {
            int total = profile.modules.Count;
            int active = 0;
            foreach (var module in profile.modules)
            {
                if (module != null && module.enabled) active++;
            }
            return $"{total} Effects ({active} Active) — {AssetDatabase.GetAssetPath(profile)}";
        }
    }
}
