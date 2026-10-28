using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「仕掛け」タブの点検: 時間割の全部の時刻（0.5 秒ごと）で仕掛けを動かしてみて、
    /// ・湧き位置からコアへ行けない時間が無いか（経路が全部ふさがると、敵はコアへまっすぐ押してくるだけになる）
    /// ・動く壁が砲台・コア・湧き位置に重なる時間が無いか
    /// を調べる。調べ終わったら元のプレビューの形に戻す。Play していないときだけ。
    /// </summary>
    public sealed class GimmickCheckPanel
    {
        private const float Step = 0.5f;
        private const float TouchRadius = 0.6f;

        private readonly List<string> _results = new List<string>();
        private bool _checked;
        private bool _ok;

        public void Draw(float horizon)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("点検", EditorStyles.miniBoldLabel);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button(new GUIContent($"0〜{horizon:0}秒を {Step}秒ごとに調べる（敵がコアへ行けるか・砲台などに重ならないか）",
                        "時間割の長さ（上で選んだ秒数）の間を、少しずつ動かして調べます。")))
                {
                    Run(horizon);
                }
            }

            if (!_checked)
            {
                return;
            }

            if (_ok)
            {
                EditorGUILayout.HelpBox("問題は見つかりませんでした。どの時刻でも、全部の湧き位置からコアへ行けます。", MessageType.Info);
            }

            foreach (var line in _results)
            {
                EditorGUILayout.HelpBox(line, MessageType.Warning);
            }
        }

        private void Run(float horizon)
        {
            _results.Clear();
            _checked = true;
            var field = Object.FindFirstObjectByType<NavigationField>();
            var spawns = Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None).OrderBy(s => s.spawnPointId).ToArray();
            var gimmicks = GimmickClock.FindAllInScenes();
            var colliders = gimmicks.SelectMany(g => g.GetComponentsInChildren<Collider2D>()).Where(c => c.GetComponentInParent<NavigationObstacle>() != null).ToArray();
            var targets = CollectTargets(spawns);

            var wasActive = GimmickPreview.Active;
            var previous = GimmickPreview.Time;
            var blocked = spawns.ToDictionary(s => s, _ => new List<float>());
            var touching = targets.ToDictionary(t => t.label, _ => new List<float>());
            try
            {
                var count = Mathf.CeilToInt(horizon / Step) + 1;
                for (var i = 0; i < count; i++)
                {
                    var t = Mathf.Min(horizon, i * Step);
                    if (EditorUtility.DisplayCancelableProgressBar("仕掛けの点検", $"{t:0.0}秒", (float)i / count))
                    {
                        _results.Add("途中でやめました。");
                        break;
                    }

                    foreach (var gimmick in gimmicks)
                    {
                        gimmick.ApplyAt(t);
                    }

                    Physics2D.SyncTransforms();
                    if (field != null)
                    {
                        field.RebuildNow();
                        var result = field.GetResult(null);
                        foreach (var spawn in spawns)
                        {
                            if (!result.IsReachable(spawn.transform.position))
                            {
                                blocked[spawn].Add(t);
                            }
                        }
                    }

                    foreach (var (label, position) in targets)
                    {
                        if (colliders.Any(c => c != null && c.enabled && Vector2.Distance(c.ClosestPoint(position), position) < TouchRadius))
                        {
                            touching[label].Add(t);
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (wasActive)
                {
                    GimmickPreview.SetTime(previous);
                }
                else
                {
                    GimmickPreview.ApplyToScenes(0f, null);
                }

                field?.RebuildNow();
            }

            if (field == null)
            {
                _results.Add("NavigationField（経路）がシーンに無いので、コアへ行けるかは調べられませんでした。");
            }

            foreach (var pair in blocked.Where(p => p.Value.Count > 0))
            {
                _results.Add($"湧き位置「{pair.Key.spawnPointId}」からコアへ行けない時間があります: {Ranges(pair.Value)}。" +
                             "その間に湧いた敵は、壁に向かってコアの方へ押し続けます。歯車の向き・時刻をずらして、どこか 1 本は開いているようにしてください。");
            }

            foreach (var pair in touching.Where(p => p.Value.Count > 0))
            {
                _results.Add($"動く壁が「{pair.Key}」に重なる（すぐ近くを通る）時間があります: {Ranges(pair.Value)}。");
            }

            _ok = _results.Count == 0;
        }

        private static List<(string label, Vector2 position)> CollectTargets(EnemySpawnPoint[] spawns)
        {
            var list = new List<(string, Vector2)>();
            foreach (var turret in Object.FindObjectsByType<LaserTurret>(FindObjectsSortMode.None))
            {
                list.Add(($"P{turret.playerIndex + 1}の砲台", turret.transform.position));
            }

            foreach (var core in Object.FindObjectsByType<CoreCrystalController>(FindObjectsSortMode.None))
            {
                list.Add(("コア", core.transform.position));
            }

            foreach (var spawn in spawns)
            {
                list.Add(($"湧き位置 {spawn.spawnPointId}", spawn.transform.position));
            }

            return list;
        }

        /// <summary>秒数の並びを「12〜13.5秒、40秒」のようにまとめる。</summary>
        private static string Ranges(List<float> times)
        {
            var parts = new List<string>();
            var start = times[0];
            var prev = times[0];
            for (var i = 1; i <= times.Count; i++)
            {
                if (i < times.Count && times[i] - prev <= Step + 0.01f)
                {
                    prev = times[i];
                    continue;
                }

                parts.Add(Mathf.Approximately(start, prev) ? $"{start:0.#}秒" : $"{start:0.#}〜{prev:0.#}秒");
                if (i < times.Count)
                {
                    start = prev = times[i];
                }
            }

            return parts.Count > 6 ? string.Join("、", parts.Take(6)) + $" ほか{parts.Count - 6}か所" : string.Join("、", parts);
        }
    }
}
