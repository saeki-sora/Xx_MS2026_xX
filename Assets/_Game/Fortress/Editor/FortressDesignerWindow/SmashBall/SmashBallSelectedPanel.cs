using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>選択中のスマッシュボールの詳細編集。複数選択すると一括編集になる。</summary>
    public sealed class SmashBallSelectedPanel
    {
        private bool _foldout = true;
        private List<SmashBallModule> _targets = new List<SmashBallModule>();
        private SerializedObject _serialized;

        public void Draw()
        {
            var selected = SmashBallSelection.Get();
            var title = selected.Count == 0 ? "選択中のスマッシュボール" : $"選択中のスマッシュボール（{selected.Count}個）";

            _foldout = EditorGUILayout.BeginFoldoutHeaderGroup(_foldout, title);
            if (_foldout)
            {
                if (selected.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "Sceneでスマッシュボールをクリックするか、下の一覧から選ぶと、ここで設定を編集できます。",
                        MessageType.Info);
                }
                else
                {
                    Rebind(selected);
                    if (Application.isPlaying)
                    {
                        DrawPlayModeTests(selected);
                    }

                    SmashBallSettingsDrawer.Draw(_serialized);
                }

                EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void Rebind(List<SmashBallModule> selected)
        {
            if (_serialized != null && _targets.SequenceEqual(selected))
            {
                return;
            }

            _serialized?.Dispose();
            _targets = selected;
            _serialized = new SerializedObject(selected.ToArray<Object>());
        }

        private static void DrawPlayModeTests(List<SmashBallModule> selected)
        {
            EditorGUILayout.LabelField("Play中のテスト", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (var player = 0; player < 4; player++)
                {
                    var label = $"P{player + 1}が割る";
                    var previous = GUI.backgroundColor;
                    GUI.backgroundColor = FortressColors.PlayerColor(player);
                    if (GUILayout.Button(label))
                    {
                        var attacker = new DestructibleAttacker(player, null);
                        foreach (var module in selected)
                        {
                            module.Obstacle.DestroyNow(DamageSource.Script, attacker);
                        }
                    }

                    GUI.backgroundColor = previous;
                }
            }

            if (GUILayout.Button("直す（全快・再出現）"))
            {
                selected.ForEach(m => m.Obstacle.Regenerate());
            }
        }
    }
}
