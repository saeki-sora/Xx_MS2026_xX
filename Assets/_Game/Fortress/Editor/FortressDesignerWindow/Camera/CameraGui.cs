using System;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>カメラタブのパネルで共通に使う小さなIMGUI部品(視点の選択ボタン・折りたたみ見出し・色付きボタン)。</summary>
    public static class CameraGui
    {
        private static GUIStyle _selectedButtonStyle;

        /// <summary>「全体 / P1〜P4」の切り替えボタン列。押された視点番号を返す(押されなければcurrent)。</summary>
        public static int ViewerButtons(int current, float height = 24f, Func<int, string> suffix = null)
        {
            _selectedButtonStyle ??= new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };

            var result = current;
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var viewer in ViewerIndex.All)
                {
                    var selected = viewer == current;
                    var label = ViewerIndex.LongLabel(viewer) + (suffix != null ? suffix(viewer) : string.Empty);
                    var style = selected ? _selectedButtonStyle : GUI.skin.button;

                    if (ColoredButton(label, ViewerIndex.Color(viewer), selected ? 1f : 0.35f, style, GUILayout.Height(height)))
                    {
                        result = viewer;
                    }
                }
            }

            return result;
        }

        public static bool ColoredButton(string label, Color color, float strength, GUIStyle style, params GUILayoutOption[] options)
        {
            var previous = GUI.backgroundColor;
            GUI.backgroundColor = Color.Lerp(Color.white, color, strength);
            var pressed = GUILayout.Button(label, style, options);
            GUI.backgroundColor = previous;
            return pressed;
        }

        /// <summary>開閉状態を覚えている見出し。開いていればtrue。</summary>
        public static bool Section(string key, string title, bool defaultOpen = true)
        {
            EditorGUILayout.Space(6);
            var open = CameraToolState.GetFoldout(key, defaultOpen);
            var next = EditorGUILayout.BeginFoldoutHeaderGroup(open, title);
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (next != open)
            {
                CameraToolState.SetFoldout(key, next);
            }

            return next;
        }

        /// <summary>色見本の小さな四角。</summary>
        public static void ColorChip(Color color, float size = 12f)
        {
            var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            rect.y += 3f;
            EditorGUI.DrawRect(rect, color);
        }
    }
}
