using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「敵ウェーブ」タブの「全体」ページ。試合の流れ（WAVE → 合間 → …）の帯、ウェーブごとの難しさのグラフ、
    /// 試合全体の決まり（制限時間・発表・撤退）と演出、気になる所の一覧。
    /// </summary>
    public sealed class WavePlanOverviewPanel
    {
        private static readonly Color TargetColor = new Color(1f, 0.45f, 0.45f, 0.9f);
        private static readonly Color RateColor = new Color(0.45f, 0.8f, 1f, 0.9f);
        private static readonly Color BreakColor = new Color(0.5f, 0.9f, 0.6f, 0.85f);
        private static readonly Color WaveColor = new Color(0.95f, 0.6f, 0.35f, 0.85f);

        private bool _effectsFoldout;
        private readonly WaveDisplayPanel _display = new WaveDisplayPanel();

        /// <param name="onSelectWave">帯のウェーブを押したとき（そのタブへ）。</param>
        public void Draw(WavePlan plan, SerializedObject serialized, List<WavePlanIssue> issues, System.Action<int> onSelectWave)
        {
            DrawFlowStrip(plan, onSelectWave);
            EditorGUILayout.Space(6);
            DrawGraph(plan);
            EditorGUILayout.Space(6);
            DrawIssues(issues);
            EditorGUILayout.Space(6);
            DrawRules(serialized);
            EditorGUILayout.Space(4);
            _display.Draw(plan, serialized);
            EditorGUILayout.Space(4);
            _effectsFoldout = EditorGUILayout.Foldout(_effectsFoldout, "演出（D-Drive の番号札。各PCで鳴らす。空なら鳴らさない）", true);
            if (_effectsFoldout)
            {
                using (new WaveLabelWidth())
                using (new EditorGUI.IndentLevelScope())
                {
                    Prop(serialized, "onAnnounce", "発表");
                    Prop(serialized, "onFinalAnnounce", "最終ウェーブの発表");
                    Prop(serialized, "onCountdownTick", "3・2・1 の1つごと");
                    Prop(serialized, "onWaveStart", "ウェーブ開始（GO!）");
                    Prop(serialized, "onWaveClear", "ウェーブクリア");
                    Prop(serialized, "onTimeWarning", "残り時間の警告（10秒前から）");
                }
            }

            EditorGUILayout.PropertyField(serialized.FindProperty("memo"), new GUIContent("メモ"));
        }

        private static void DrawFlowStrip(WavePlan plan, System.Action<int> onSelectWave)
        {
            EditorGUILayout.LabelField("試合の流れ（押すとそのウェーブへ）", EditorStyles.miniBoldLabel);
            var rect = GUILayoutUtility.GetRect(10f, 38f, GUILayout.ExpandWidth(true));
            var count = plan.Count;
            if (count == 0)
            {
                return;
            }

            // ウェーブの箱と合間の箱を交互に並べる（合間は少し細め）。
            const float waveUnits = 2f;
            const float breakUnits = 1.4f;
            var units = count * waveUnits + (count - 1) * breakUnits;
            var unit = rect.width / units;
            var x = rect.x;
            for (var i = 0; i < count; i++)
            {
                var wave = plan.waves[i];
                var box = new Rect(x, rect.y, unit * waveUnits - 2f, rect.height);
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(box, i == count - 1 ? new Color(0.95f, 0.35f, 0.4f, 0.85f) : WaveColor);
                }

                var label = $"{wave?.DisplayLabel(i + 1)}\n目標 {wave?.targetKills}";
                if (GUI.Button(box, new GUIContent(label, "このウェーブのページへ"), CenteredLabel))
                {
                    onSelectWave?.Invoke(i);
                }

                x += unit * waveUnits;
                if (i == count - 1)
                {
                    break;
                }

                var gap = new Rect(x, rect.y + 3f, unit * breakUnits - 2f, rect.height - 6f);
                var ev = wave?.intervalEvent;
                var seconds = ev != null ? ev.DurationSeconds : wave?.restSeconds ?? 0f;
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(gap, BreakColor);
                }

                var tip = ev != null ? $"WAVE {i + 1} の後: {ev.DisplayName} {seconds:0.#}秒" : $"WAVE {i + 1} の後: 休む {seconds:0.#}秒";
                GUI.Label(gap, new GUIContent(ev != null ? $"{ShortName(ev)}\n{seconds:0}秒" : $"休む\n{seconds:0}秒", tip), CenteredLabel);
                x += unit * breakUnits;
            }

            var limit = plan.timeLimitSeconds > 0f ? $"制限時間 {Clock(plan.timeLimitSeconds)}（{(plan.clockRunsDuringBreaks ? "合間も減る" : "戦っている間だけ減る")}）" : "制限時間なし";
            EditorGUILayout.LabelField(
                $"{limit} ・ 目標の合計 {plan.TotalTargetKills()}体 ・ 発表・撤退・合間の合計 約{WavePlanChecks.FixedSeconds(plan):0}秒",
                EditorStyles.miniLabel);
        }

        private static string ShortName(WaveIntervalEvent ev)
        {
            // 細い箱に収まるよう4文字まで（「歯みがきタイム」→「歯みがき」）。全部の名前は箱の上にマウスを乗せると出る。
            var name = ev.DisplayName;
            return name.Length > 4 ? name.Substring(0, 4) : name;
        }

        // ウェーブごとの「目標」（赤）と「1秒に出る数」（青）を棒で並べる。難しさの上がり方を見る。
        private static void DrawGraph(WavePlan plan)
        {
            EditorGUILayout.LabelField("難しさの上がり方（赤＝目標撃破数、青＝1秒に出る数）", EditorStyles.miniBoldLabel);
            var rect = GUILayoutUtility.GetRect(10f, 90f, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint || plan.Count == 0)
            {
                return;
            }

            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.18f));
            var maxTarget = Mathf.Max(1, plan.waves.Max(w => w?.targetKills ?? 0));
            var maxRate = Mathf.Max(0.01f, plan.waves.Max(w => WavePlanChecks.EnemiesPerSecond(w)));
            var slot = rect.width / plan.Count;
            var barWidth = Mathf.Min(26f, slot * 0.3f);
            var labelHeight = 14f;
            var height = rect.height - labelHeight - 4f;
            for (var i = 0; i < plan.Count; i++)
            {
                var wave = plan.waves[i];
                var cx = rect.x + slot * (i + 0.5f);
                var target = (wave?.targetKills ?? 0) / (float)maxTarget;
                var rate = WavePlanChecks.EnemiesPerSecond(wave) / maxRate;
                var t = new Rect(cx - barWidth - 1f, rect.y + 2f + height * (1f - target), barWidth, height * target);
                var r = new Rect(cx + 1f, rect.y + 2f + height * (1f - rate), barWidth, height * rate);
                EditorGUI.DrawRect(t, TargetColor);
                EditorGUI.DrawRect(r, RateColor);
                GUI.Label(new Rect(cx - slot * 0.5f, rect.yMax - labelHeight, slot, labelHeight),
                    $"W{i + 1}  {wave?.targetKills}体 / {WavePlanChecks.EnemiesPerSecond(wave):0.#}/秒", CenteredMini);
            }
        }

        private static void DrawIssues(List<WavePlanIssue> issues)
        {
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("気になる所はありません。", MessageType.None);
                return;
            }

            foreach (var issue in issues)
            {
                var where = issue.Wave >= 0 ? $"WAVE {issue.Wave + 1}: " : "";
                EditorGUILayout.HelpBox(where + issue.Message, WaveDefinitionPanel.ToMessageType(issue.Level));
            }
        }

        private static void DrawRules(SerializedObject serialized)
        {
            // 箱の見出し（試合全体・始まり・発表・ウェーブクリア → 撤退）は、データの見出しがそのまま出る（ここで書くと二重になる）。
            using (new WaveLabelWidth())
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Prop(serialized, "timeLimitSeconds", "制限時間(秒・0でなし)");
                Prop(serialized, "timeUpRule", "時間切れのとき");
                Prop(serialized, "clockRunsDuringBreaks", "合間の間も時間を減らす");
            }

            using (new WaveLabelWidth())
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Prop(serialized, "startDelaySeconds", "全員そろってから最初の発表まで(秒)");
                Prop(serialized, "maxWaitForPlayersSeconds", "全員を待つ最長(秒)");
                Prop(serialized, "announceSeconds", "発表の長さ(秒)");
                Prop(serialized, "countdownSeconds", "最後の 3・2・1 (秒)");
                Prop(serialized, "showRoutes", "発表の間、敵のルートを赤い線で見せる");
            }

            using (new WaveLabelWidth())
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Prop(serialized, "clearBannerSeconds", "「WAVE CLEAR!」の最短(秒)");
                Prop(serialized, "retreatMaxSeconds", "逃げ切るのを待つ最長(秒)");
                Prop(serialized, "retreatSpeedMultiplier", "逃げる速さの倍率");
            }
        }

        private static void Prop(SerializedObject serialized, string path, string label)
        {
            var property = serialized.FindProperty(path);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
            }
        }

        public static string Clock(float seconds)
        {
            var total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        private static GUIStyle _centered;
        private static GUIStyle _centeredMini;

        private static GUIStyle CenteredLabel => _centered ??= new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            clipping = TextClipping.Clip,
            normal = { textColor = Color.black }
        };

        private static GUIStyle CenteredMini => _centeredMini ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
    }
}
