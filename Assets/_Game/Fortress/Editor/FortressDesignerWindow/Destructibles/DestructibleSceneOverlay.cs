using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// Sceneに破壊可能物の情報（名前・耐久・グループの線・連鎖範囲・リサイズハンドル）を重ねて描く。
    /// 表示はタブの「配置」パネルでON/OFFできる。
    /// </summary>
    [InitializeOnLoad]
    public static class DestructibleSceneOverlay
    {
        private const int MaxLabels = 150;
        private static readonly Color LinkColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        private static readonly Color ChainColor = new Color(1f, 0.5f, 0.2f, 0.9f);

        private const double CacheSeconds = 0.3;

        private static GUIStyle _labelStyle;
        private static DestructibleObstacle[] _cache = new DestructibleObstacle[0];
        private static double _cacheTime = -1;

        static DestructibleSceneOverlay()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            var all = FindAllCached();
            if (all.Length == 0)
            {
                return;
            }

            var selected = DestructibleSelection.Get();

            if (Event.current.type == EventType.Repaint)
            {
                if (DestructibleEditorSettings.ShowLabels)
                {
                    DrawLabels(all);
                }

                if (DestructibleEditorSettings.ShowLinks)
                {
                    DrawLinks(all);
                    DrawChainRanges(selected);
                }
            }

            if (DestructibleEditorSettings.ShowResizeHandles && !DestructibleEditorSettings.PlacementMode && !Application.isPlaying)
            {
                foreach (var obstacle in selected)
                {
                    DestructibleResizeHandles.Draw(obstacle);
                }
            }
        }

        /// <summary>SceneGUIは1フレームに何度も呼ばれるので、シーン検索の結果を短時間だけ使い回す。</summary>
        private static DestructibleObstacle[] FindAllCached()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now - _cacheTime > CacheSeconds)
            {
                _cache = DestructibleSelection.FindAllInScene();
                _cacheTime = now;
            }

            return System.Array.FindAll(_cache, o => o != null);
        }

        private static void DrawLabels(DestructibleObstacle[] all)
        {
            if (all.Length > MaxLabels)
            {
                return;
            }

            _labelStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

            foreach (var obstacle in all)
            {
                var scale = obstacle.transform.localScale;
                var anchor = obstacle.transform.position + Vector3.down * (Mathf.Abs(scale.y) * 0.5f + 0.25f);
                Handles.Label(anchor, LabelText(obstacle), _labelStyle);
            }
        }

        private static string LabelText(DestructibleObstacle obstacle)
        {
            var text = $"{obstacle.name}  HP {obstacle.settings.durability.maxHealth:0.#}";
            if (obstacle.protection.startsInvulnerable)
            {
                text += "  無敵";
            }

            if (obstacle.link.HasGroup)
            {
                text += $"  [{obstacle.link.groupId}]";
            }

            return text;
        }

        private static void DrawLinks(DestructibleObstacle[] all)
        {
            var groups = new Dictionary<string, List<DestructibleObstacle>>();
            foreach (var obstacle in all)
            {
                if (!obstacle.link.HasGroup)
                {
                    continue;
                }

                if (!groups.TryGetValue(obstacle.link.groupId, out var members))
                {
                    members = new List<DestructibleObstacle>();
                    groups[obstacle.link.groupId] = members;
                }

                members.Add(obstacle);
            }

            Handles.color = LinkColor;
            foreach (var members in groups.Values)
            {
                for (var i = 1; i < members.Count; i++)
                {
                    Handles.DrawDottedLine(members[i - 1].transform.position, members[i].transform.position, 4f);
                }
            }
        }

        private static void DrawChainRanges(List<DestructibleObstacle> selected)
        {
            Handles.color = ChainColor;
            foreach (var obstacle in selected)
            {
                if (obstacle.link.chainRadius > 0f)
                {
                    Handles.DrawWireDisc(obstacle.transform.position, Vector3.forward, obstacle.link.chainRadius);
                }
            }
        }
    }
}
