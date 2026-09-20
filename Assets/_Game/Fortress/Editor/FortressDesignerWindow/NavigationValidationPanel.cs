using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「コアへ到達できない湧き位置がないか」を検査して一覧表示する。
    /// ウェーブで使われている敵の種類ごとの経路の性格（大型など）も考慮して判定する。
    /// </summary>
    public sealed class NavigationValidationPanel
    {
        private static readonly List<Vector2> PathBuffer = new List<Vector2>();
        private bool _foldout = true;

        public void Draw(NavigationField field)
        {
            _foldout = EditorGUILayout.BeginFoldoutHeaderGroup(_foldout, "到達チェック（湧き位置 → コア）");
            if (_foldout)
            {
                DrawContents(field);
                EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawContents(NavigationField field)
        {
            field.EnsureBuilt();

            if (field.CurrentGoals.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "目的地がありません。シーンにCoreCrystalControllerを置くか、Extra Goalsを設定してください。",
                    MessageType.Warning);
                return;
            }

            var spawnPoints = Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None)
                .OrderBy(p => p.name, System.StringComparer.Ordinal)
                .ToArray();

            if (spawnPoints.Length == 0)
            {
                EditorGUILayout.HelpBox("チェック対象の沸き位置がありません。", MessageType.Info);
                return;
            }

            var profiles = CollectUsedProfiles();
            var defaultResult = field.GetResult(NavigationSceneOverlay.PreviewProfile);
            var problemCount = 0;

            foreach (var spawnPoint in spawnPoints)
            {
                var position = (Vector2)spawnPoint.transform.position;
                var failedProfiles = FindFailedProfiles(field, position, profiles);
                var inBounds = field.Data.ContainsWorld(position);
                var hasProblem = !inBounds || failedProfiles.Count > 0;
                if (hasProblem)
                {
                    problemCount++;
                }

                DrawRow(spawnPoint, position, defaultResult, inBounds, failedProfiles);
            }

            if (problemCount == 0)
            {
                EditorGUILayout.HelpBox("全ての湧き位置からコアへ到達できます。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"{problemCount}箇所の湧き位置に問題があります。障害物で完全に塞がれていないか、" +
                    "対象範囲(Area)に収まっているか確認してください。", MessageType.Warning);
            }
        }

        private static List<string> FindFailedProfiles(
            NavigationField field, Vector2 position, List<NavigationProfile> profiles)
        {
            var failed = new List<string>();
            foreach (var profile in profiles)
            {
                if (!field.GetResult(profile).IsReachable(position))
                {
                    failed.Add(profile != null ? profile.name : "既定");
                }
            }

            return failed;
        }

        private static void DrawRow(
            EnemySpawnPoint spawnPoint,
            Vector2 position,
            FlowFieldResult previewResult,
            bool inBounds,
            List<string> failedProfiles)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(spawnPoint.ResolvedId, GUILayout.MinWidth(80));

                if (!inBounds)
                {
                    EditorGUILayout.LabelField("範囲外", EditorStyles.boldLabel, GUILayout.Width(150));
                }
                else if (failedProfiles.Count > 0)
                {
                    EditorGUILayout.LabelField(
                        $"到達不可: {string.Join(", ", failedProfiles)}", EditorStyles.boldLabel, GUILayout.Width(200));
                }
                else
                {
                    var reached = previewResult.TracePath(position, PathBuffer);
                    EditorGUILayout.LabelField(
                        reached ? $"OK  経路長 {PathLength():0.0}" : "OK",
                        GUILayout.Width(150));
                }

                if (GUILayout.Button("表示", GUILayout.Width(40)))
                {
                    Selection.activeGameObject = spawnPoint.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }
        }

        private static float PathLength()
        {
            var length = 0f;
            for (var i = 1; i < PathBuffer.Count; i++)
            {
                length += Vector2.Distance(PathBuffer[i - 1], PathBuffer[i]);
            }

            return length;
        }

        /// <summary>ウェーブで使われている敵の種類のプロファイル（重複なし）。何も無ければ既定(null)のみ。</summary>
        private static List<NavigationProfile> CollectUsedProfiles()
        {
            var profiles = new List<NavigationProfile> { null };

            var director = Object.FindFirstObjectByType<EnemySpawnDirector>();
            if (director == null || director.wave == null)
            {
                return profiles;
            }

            foreach (var entry in director.wave.spawnEntries)
            {
                var profile = entry.enemyType != null ? entry.enemyType.navigationProfile : null;
                if (!profiles.Contains(profile))
                {
                    profiles.Add(profile);
                }
            }

            return profiles;
        }
    }
}
