using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>シーンにある破壊可能物の一覧。検索・選択（Ctrl/Shiftで複数）・一括選択・複製/削除。</summary>
    public sealed class DestructibleListPanel
    {
        private bool _foldout = true;
        private string _filter = "";

        public void Draw()
        {
            var all = DestructibleSelection.FindAllInScene()
                .OrderBy(o => o.name, System.StringComparer.Ordinal)
                .ToArray();
            var shown = all.Where(Matches).ToArray();

            _foldout = EditorGUILayout.BeginFoldoutHeaderGroup(_foldout, $"シーン内の破壊可能物（{all.Length}個）");
            if (_foldout)
            {
                DrawToolbar(all, shown);

                var selected = DestructibleSelection.Get();
                foreach (var obstacle in shown)
                {
                    DrawRow(obstacle, selected.Contains(obstacle));
                }

                if (all.Length == 0)
                {
                    EditorGUILayout.HelpBox("破壊可能物がありません。上の「配置」から置いてください。", MessageType.Info);
                }

                EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawToolbar(DestructibleObstacle[] all, DestructibleObstacle[] shown)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _filter = EditorGUILayout.TextField(
                    new GUIContent("検索", "名前・グループ名・プリセット名で絞り込みます。"), _filter);

                if (GUILayout.Button("表示中を全選択", GUILayout.Width(100)))
                {
                    DestructibleSelection.Set(shown);
                }

                if (GUILayout.Button(new GUIContent("同じ種類を選択", "選択中の物と同じプリセットを使っている物を全部選びます。"), GUILayout.Width(100)))
                {
                    var presets = DestructibleSelection.Get().Select(o => o.sourcePreset).Where(p => p != null).ToHashSet();
                    DestructibleSelection.Set(all.Where(o => o.sourcePreset != null && presets.Contains(o.sourcePreset)));
                }
            }
        }

        private void DrawRow(DestructibleObstacle obstacle, bool isSelected)
        {
            var previousColor = GUI.backgroundColor;
            if (isSelected)
            {
                GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = previousColor;

                var label = new GUIContent(obstacle.name, RowTooltip(obstacle));
                if (GUILayout.Button(label, EditorStyles.label, GUILayout.MinWidth(90)))
                {
                    Pick(obstacle, isSelected);
                }

                var scale = obstacle.transform.localScale;
                EditorGUILayout.LabelField($"{Mathf.Abs(scale.x):0.#}×{Mathf.Abs(scale.y):0.#}", GUILayout.Width(56));
                EditorGUILayout.LabelField($"HP {obstacle.settings.durability.maxHealth:0.#}", GUILayout.Width(62));
                EditorGUILayout.LabelField(obstacle.link.HasGroup ? $"[{obstacle.link.groupId}]" : "", GUILayout.Width(70));

                if (Application.isPlaying)
                {
                    EditorGUILayout.LabelField(StateText(obstacle), GUILayout.Width(80));
                }

                if (GUILayout.Button(new GUIContent("表示", "Sceneビューでこの物を画面に映します。"), GUILayout.Width(40)))
                {
                    Selection.activeGameObject = obstacle.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }

                if (GUILayout.Button(new GUIContent("複製", "少しずらした位置にコピーを作ります。"), GUILayout.Width(40)))
                {
                    Selection.activeGameObject = DestructibleFactory.Duplicate(obstacle, Vector2.one).gameObject;
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(new GUIContent("削除", "この破壊可能物を削除します。"), GUILayout.Width(40)))
                {
                    Undo.DestroyObjectImmediate(obstacle.gameObject);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private static void Pick(DestructibleObstacle obstacle, bool isSelected)
        {
            var e = Event.current;
            if (e.control || e.command)
            {
                var current = DestructibleSelection.Get();
                if (isSelected)
                {
                    current.Remove(obstacle);
                }
                else
                {
                    current.Add(obstacle);
                }

                DestructibleSelection.Set(current);
            }
            else
            {
                Selection.activeGameObject = obstacle.gameObject;
            }
        }

        private bool Matches(DestructibleObstacle obstacle)
        {
            if (string.IsNullOrWhiteSpace(_filter))
            {
                return true;
            }

            var text = _filter.Trim();
            return Contains(obstacle.name, text)
                   || Contains(obstacle.link.groupId, text)
                   || (obstacle.sourcePreset != null && Contains(obstacle.sourcePreset.displayName, text));
        }

        private static bool Contains(string source, string text)
        {
            return !string.IsNullOrEmpty(source) && source.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string RowTooltip(DestructibleObstacle obstacle)
        {
            var preset = obstacle.sourcePreset != null ? obstacle.sourcePreset.displayName : "なし";
            return $"クリックで選択（Ctrl+クリックで複数選択）\nプリセット: {preset}";
        }

        private static string StateText(DestructibleObstacle obstacle)
        {
            if (obstacle.IsDestroyed)
            {
                return $"破壊 {obstacle.RegenProgress01:P0}";
            }

            return obstacle.IsInvulnerable ? "無敵" : $"{obstacle.CurrentHealth:0.#}";
        }
    }
}
