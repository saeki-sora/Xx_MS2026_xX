using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// シーン内の破壊可能物の一覧から、どれをスマッシュボールにするかを選ぶ。
    /// 既にスマッシュボールの物は状態（出現中・待機中・割れた）も表示する。
    /// </summary>
    public sealed class SmashBallListPanel
    {
        private bool _foldout = true;
        private bool _onlySmashBalls;

        public void Draw()
        {
            var all = DestructibleSelection.FindAllInScene()
                .OrderBy(o => o.name, System.StringComparer.Ordinal)
                .ToArray();
            var shown = _onlySmashBalls ? all.Where(o => o.GetComponent<SmashBallModule>() != null).ToArray() : all;

            var count = all.Count(o => o.GetComponent<SmashBallModule>() != null);
            _foldout = EditorGUILayout.BeginFoldoutHeaderGroup(_foldout, $"破壊可能物の一覧（スマッシュボール {count}個）");
            if (_foldout)
            {
                _onlySmashBalls = EditorGUILayout.ToggleLeft("スマッシュボールのみ表示", _onlySmashBalls);

                foreach (var obstacle in shown)
                {
                    DrawRow(obstacle);
                }

                if (shown.Length == 0)
                {
                    EditorGUILayout.HelpBox(
                        all.Length == 0
                            ? "破壊可能物がありません。先に「破壊可能物」タブで置いてください。"
                            : "スマッシュボールがありません。行の「スマッシュボール化」ボタンで作れます。",
                        MessageType.Info);
                }

                EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawRow(DestructibleObstacle obstacle)
        {
            var module = obstacle.GetComponent<SmashBallModule>();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var label = new GUIContent(obstacle.name, obstacle.gameObject.activeSelf ? "" : "（現在は非表示＝ローテーション待機中）");
                if (GUILayout.Button(label, EditorStyles.label, GUILayout.MinWidth(90)))
                {
                    Selection.activeGameObject = obstacle.gameObject;
                }

                EditorGUILayout.LabelField(StatusText(obstacle, module), GUILayout.Width(150));

                if (GUILayout.Button(new GUIContent("表示", "Sceneビューでこの物を画面に映します。"), GUILayout.Width(40)))
                {
                    Selection.activeGameObject = obstacle.gameObject;
                    if (obstacle.gameObject.activeInHierarchy)
                    {
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }
                }

                if (module == null)
                {
                    if (GUILayout.Button(new GUIContent("スマッシュボール化", "このオブジェクトにスマッシュボールの機能を追加します。"), GUILayout.Width(110)))
                    {
                        Undo.AddComponent<SmashBallModule>(obstacle.gameObject);
                        Selection.activeGameObject = obstacle.gameObject;
                    }
                }
                else if (GUILayout.Button(new GUIContent("解除", "スマッシュボールの機能を外します（破壊可能物としては残ります）。"), GUILayout.Width(60)))
                {
                    var floater = obstacle.GetComponent<SmashBallFloater>();
                    if (floater != null)
                    {
                        Undo.DestroyObjectImmediate(floater);
                    }

                    Undo.DestroyObjectImmediate(module);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private static string StatusText(DestructibleObstacle obstacle, SmashBallModule module)
        {
            if (module == null)
            {
                return "";
            }

            if (!Application.isPlaying)
            {
                return "スマッシュボール";
            }

            if (module.IsWaitingInRotation)
            {
                return "待機中";
            }

            if (obstacle.IsDestroyed)
            {
                return obstacle.settings.durability.regenerates
                    ? $"割れた（再生まで{obstacle.RegenProgress01:P0}）"
                    : "割れた（固定）";
            }

            return "出現中";
        }
    }
}
