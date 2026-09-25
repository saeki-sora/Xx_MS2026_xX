using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 選択中の破壊可能物の詳細編集。位置・大きさ・状態のテスト・全設定項目をまとめて扱う。
    /// 複数選択すると、同じ項目を一括で編集できる（値が違う項目は「—」表示）。
    /// </summary>
    public sealed class DestructibleSelectedPanel
    {
        private bool _foldout = true;
        private List<DestructibleObstacle> _targets = new List<DestructibleObstacle>();
        private SerializedObject _settings;
        private SerializedObject _transforms;

        public void Draw()
        {
            var selected = DestructibleSelection.Get();
            var title = selected.Count == 0 ? "選択中の破壊可能物" : $"選択中の破壊可能物（{selected.Count}個）";

            _foldout = EditorGUILayout.BeginFoldoutHeaderGroup(_foldout, title);
            if (_foldout)
            {
                if (selected.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "Sceneで破壊可能物をクリックするか、下の一覧から選ぶと、ここで全項目を編集できます。" +
                        "複数選ぶと一括で編集できます。",
                        MessageType.Info);
                }
                else
                {
                    Rebind(selected);
                    DrawTransform(selected);
                    DrawActions(selected);
                    if (Application.isPlaying)
                    {
                        DrawPlayModeTests(selected);
                    }

                    EditorGUILayout.Space(4);
                    DestructibleSettingsDrawer.Draw(_settings);
                }

                EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void Rebind(List<DestructibleObstacle> selected)
        {
            if (_settings != null && _targets.SequenceEqual(selected))
            {
                return;
            }

            _settings?.Dispose();
            _transforms?.Dispose();

            _targets = selected;
            _settings = new SerializedObject(selected.ToArray<Object>());
            _transforms = new SerializedObject(selected.Select(o => (Object)o.transform).ToArray());
        }

        private void DrawTransform(List<DestructibleObstacle> selected)
        {
            _transforms.Update();
            EditorGUI.BeginChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                LabeledField("X", _transforms.FindProperty("m_LocalPosition.x"));
                LabeledField("Y", _transforms.FindProperty("m_LocalPosition.y"));
                LabeledField("幅", _transforms.FindProperty("m_LocalScale.x"));
                LabeledField("高さ", _transforms.FindProperty("m_LocalScale.y"));
            }

            if (EditorGUI.EndChangeCheck())
            {
                _transforms.ApplyModifiedProperties();
                NavigationObstacle.NotifyChanged();
            }
        }

        private static void LabeledField(string label, SerializedProperty property)
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(label.Length * 12f + 6f));
            EditorGUILayout.PropertyField(property, GUIContent.none, GUILayout.MinWidth(40));
        }

        private void DrawActions(List<DestructibleObstacle> selected)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("グリッドに揃える", "位置を、配置パネルのグリッド間隔に揃えます。")))
                {
                    foreach (var obstacle in selected)
                    {
                        Undo.RecordObject(obstacle.transform, "Snap Destructible");
                        var position = obstacle.transform.position;
                        var snapped = DestructibleEditorSettings.Snap(new Vector2(position.x, position.y));
                        obstacle.transform.position = new Vector3(snapped.x, snapped.y, position.z);
                    }

                    NavigationObstacle.NotifyChanged();
                }

                if (GUILayout.Button(new GUIContent("複製", "少しずらした位置にコピーを作ります。")))
                {
                    DestructibleSelection.Set(selected.Select(o => DestructibleFactory.Duplicate(o, Vector2.one)).ToList());
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(new GUIContent("削除", "選択中の破壊可能物を全て削除します。")))
                {
                    foreach (var obstacle in selected)
                    {
                        Undo.DestroyObjectImmediate(obstacle.gameObject);
                    }

                    GUIUtility.ExitGUI();
                }
            }
        }

        private static void DrawPlayModeTests(List<DestructibleObstacle> selected)
        {
            EditorGUILayout.LabelField("Play中のテスト", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("壊す"))
                {
                    selected.ForEach(o => o.DestroyNow());
                }

                if (GUILayout.Button(new GUIContent("直す（全快）", "壊れていれば再生し、壊れていなければ耐久を全快にします。")))
                {
                    selected.ForEach(o => o.Regenerate());
                }

                if (GUILayout.Button("ダメージ10"))
                {
                    selected.ForEach(o => o.TakeDamage(10f));
                }

                if (GUILayout.Button("無敵を解除"))
                {
                    selected.ForEach(o => o.Unlock());
                }
            }
        }
    }
}
