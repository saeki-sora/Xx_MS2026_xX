using System.Collections.Generic;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「仕掛け」タブの時間割とプレビュー（2026-10-29）。
    /// 仕掛け 1 つにつき 1 段。動いている時間帯を帯で描き、赤い線が今の秒数。帯の所をクリックするとその秒数の形をシーンに出す。
    /// Play 中は試合の時計（<see cref="GimmickClock"/>）を表示し、Host・オフラインなら止める・速める・飛ばすができる。
    /// </summary>
    public sealed class GimmickTimelinePanel
    {
        private const float LabelWidth = 150f;
        private const float LaneHeight = 18f;
        private static readonly float[] Horizons = { 30f, 60f, 120f, 300f };
        private static readonly string[] HorizonLabels = { "30秒", "60秒", "2分", "5分" };
        private static readonly Color GearColor = new Color(0.95f, 0.72f, 0.3f);
        private static readonly Color MoverColor = new Color(0.4f, 0.8f, 1f);
        private static readonly Color LaneBack = new Color(0f, 0f, 0f, 0.18f);

        private readonly List<(float start, float end)> _intervals = new List<(float start, float end)>();
        private int _horizonIndex = 1;
        private bool _dragging;

        public float Horizon => Horizons[_horizonIndex];

        public void Draw(IReadOnlyList<GimmickBehaviour> gimmicks)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("時間割とプレビュー", EditorStyles.miniBoldLabel);
            if (Application.isPlaying)
            {
                DrawPlayControls();
            }
            else
            {
                DrawPreviewControls();
            }

            DrawTimeline(gimmicks);
        }

        private void DrawPreviewControls()
        {
            GimmickPreview.Horizon = Horizon;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var t = EditorGUILayout.Slider(new GUIContent("プレビューの秒数", "動かすと、シーンの歯車・動く障害物がその秒数の形になります（試合の始めが 0 秒）。"),
                    GimmickPreview.Time, 0f, Horizon);
                if (EditorGUI.EndChangeCheck())
                {
                    GimmickPreview.SetTime(t);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(GimmickPreview.Playing ? "■ 止める" : "▶ 時間を進めて見る", GUILayout.Width(130)))
                {
                    GimmickPreview.TogglePlay();
                }

                GimmickPreview.PlaySpeed = SpeedButtons(GimmickPreview.PlaySpeed);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!GimmickPreview.Active))
                {
                    if (GUILayout.Button(new GUIContent("プレビューをやめる（0秒の形に戻す）", "シーンに保存されるのは 0 秒の形です。プレビュー中に保存しても 0 秒の形で書かれます。")))
                    {
                        GimmickPreview.Stop();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _horizonIndex = GUILayout.Toolbar(_horizonIndex, HorizonLabels, GUILayout.Width(240));
                GUILayout.Space(8);
                NavigationSceneOverlay.ShowSpawnPaths = EditorGUILayout.ToggleLeft(
                    new GUIContent("敵の予想ルートも見る", "Scene ビューに、湧き位置からコアまでの予想ルート（経路・障害物タブと同じ線）を出します。プレビューの形で計算し直されます。"),
                    NavigationSceneOverlay.ShowSpawnPaths);
            }

            if (GimmickPreview.Active)
            {
                EditorGUILayout.HelpBox(
                    $"プレビュー中（{GimmickPreview.Time:0.0}秒の形）。この間は動く障害物を手で動かしても出発点は変わりません。" +
                    "置き直すときは「プレビューをやめる」を押してから動かしてください。", MessageType.Info);
            }
        }

        private void DrawPlayControls()
        {
            var state = GimmickClock.Running ? (GimmickClock.Paused ? "止めています（テスト）" : "進んでいます") : "止まっています（試合の前後・始まりの演出中）";
            EditorGUILayout.LabelField("仕掛けの時計", $"{GimmickClock.Time:0.0}秒　{state}　速さ ×{GimmickClock.Speed:0.#}");
            if (!GimmickClock.CanControl)
            {
                EditorGUILayout.HelpBox("このPCは Client なので、時計は Host から届いた値です（止める・速めるは Host で）。", MessageType.None);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(GimmickClock.Paused ? "▶ 動かす" : "‖ 止める（テスト）", GUILayout.Width(130)))
                {
                    GimmickClock.SetDebugPaused(!GimmickClock.Paused);
                }

                var speed = SpeedButtons(GimmickClock.Speed);
                if (!Mathf.Approximately(speed, GimmickClock.Speed))
                {
                    GimmickClock.SetDebugSpeed(speed);
                }

                if (GUILayout.Button("+5秒", GUILayout.Width(50)))
                {
                    GimmickClock.DebugJump(GimmickClock.Time + 5f);
                }

                if (GUILayout.Button("0秒に戻す", GUILayout.Width(80)))
                {
                    GimmickClock.DebugJump(0f);
                }
            }

            EditorGUILayout.HelpBox("時間割の帯の所をクリックすると、その秒数へ飛びます（全員のPCに届きます）。", MessageType.None);
        }

        private static float SpeedButtons(float current)
        {
            foreach (var speed in new[] { 1f, 2f, 4f })
            {
                var on = Mathf.Approximately(current, speed);
                if (GUILayout.Toggle(on, $"×{speed:0}", EditorStyles.miniButton, GUILayout.Width(34)) && !on)
                {
                    current = speed;
                }
            }

            return current;
        }

        private void DrawTimeline(IReadOnlyList<GimmickBehaviour> gimmicks)
        {
            if (gimmicks.Count == 0)
            {
                return;
            }

            var horizon = Horizon;
            var now = Application.isPlaying ? GimmickClock.Time : GimmickPreview.Time;
            var showNow = Application.isPlaying || GimmickPreview.Active;

            // 目盛り
            var scale = GUILayoutUtility.GetRect(10f, 16f, GUILayout.ExpandWidth(true));
            var bar = new Rect(scale.x + LabelWidth, scale.y, scale.width - LabelWidth, scale.height);
            var step = EnemyWaveStats.TickStep(horizon, 12);
            for (var t = 0f; t <= horizon + 0.001f; t += step)
            {
                var x = bar.x + bar.width * (t / horizon);
                EditorGUI.DrawRect(new Rect(x, bar.yMax - 4f, 1f, 4f), Color.gray);
                GUI.Label(new Rect(x + 2f, bar.y - 1f, 40f, 14f), $"{t:0}", EditorStyles.miniLabel);
            }

            var top = bar.yMax;
            foreach (var gimmick in gimmicks)
            {
                if (gimmick == null)
                {
                    continue;
                }

                var row = GUILayoutUtility.GetRect(10f, LaneHeight, GUILayout.ExpandWidth(true));
                var label = new Rect(row.x, row.y, LabelWidth - 4f, row.height);
                var lane = new Rect(row.x + LabelWidth, row.y + 2f, row.width - LabelWidth, row.height - 4f);
                var selected = Selection.activeGameObject != null && Selection.activeGameObject.transform.IsChildOf(gimmick.transform);
                var kind = gimmick is GimmickGear ? "⚙ " : "↔ ";
                if (GUI.Button(label, new GUIContent(kind + gimmick.name, gimmick.Describe()), selected ? EditorStyles.boldLabel : EditorStyles.label))
                {
                    Selection.activeGameObject = gimmick.gameObject;
                    EditorGUIUtility.PingObject(gimmick.gameObject);
                }

                EditorGUI.DrawRect(lane, LaneBack);
                _intervals.Clear();
                gimmick.CollectIntervals(horizon, _intervals);
                var color = gimmick is GimmickGear ? GearColor : MoverColor;
                foreach (var (start, end) in _intervals)
                {
                    var x0 = lane.x + lane.width * Mathf.Clamp01(start / horizon);
                    var x1 = lane.x + lane.width * Mathf.Clamp01(end / horizon);
                    EditorGUI.DrawRect(new Rect(x0, lane.y, Mathf.Max(2f, x1 - x0), lane.height), color);
                }
            }

            var bottom = GUILayoutUtility.GetLastRect().yMax;
            var area = new Rect(bar.x, top, bar.width, bottom - top);
            if (showNow)
            {
                var x = bar.x + bar.width * Mathf.Clamp01(now / horizon);
                EditorGUI.DrawRect(new Rect(x - 1f, top, 2f, bottom - top), new Color(1f, 0.25f, 0.25f));
            }

            HandleClick(area, horizon);
            EditorGUILayout.LabelField("帯 = 動いている時間（橙 = 歯車、水色 = 動く障害物）。名前を押すとその物を選びます。", EditorStyles.miniLabel);
        }

        private void HandleClick(Rect area, float horizon)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition))
            {
                _dragging = true;
            }

            if (e.type == EventType.MouseUp)
            {
                _dragging = false;
            }

            if (!_dragging || (e.type != EventType.MouseDown && e.type != EventType.MouseDrag))
            {
                return;
            }

            var t = Mathf.Clamp01((e.mousePosition.x - area.x) / area.width) * horizon;
            if (Application.isPlaying)
            {
                if (e.type == EventType.MouseDown)
                {
                    GimmickClock.DebugJump(t);
                }
            }
            else
            {
                GimmickPreview.SetTime(t);
            }

            e.Use();
        }
    }
}
