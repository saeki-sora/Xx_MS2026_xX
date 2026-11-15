using System;
using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MS2026.Cpu.EditorTools
{
    /// <summary>
    /// ページ「強さ」: CPU の強さを選び、その強さの上手さ（試合の始め → 上限）を見る。4つの目安を直すのは「詳しく」の奥。
    /// 最初に見える操作は 強さ・試合の始めの慣れ・学ぶ・性格 の4つだけ（ツールの決まり T2）。
    /// </summary>
    internal sealed class CpuStrengthPage : IStudioPage
    {
        private readonly CpuStudioContext _context;
        private readonly List<Action> _refreshers = new List<Action>();
        private readonly List<(Label label, Func<CpuSkillValues, float> get, string format, string unit)> _rows =
            new List<(Label, Func<CpuSkillValues, float>, string, string)>();

        private Action _refreshSegment;

        public CpuStrengthPage(CpuStudioContext context)
        {
            _context = context;
        }

        public string Icon => "強";
        public string Label => "強さ";
        public string Tooltip => "CPU の強さ（やさしい・ふつう・つよい）と、その強さの上手さを見る・直す。";
        public int Badge => 0;
        public bool BadgeIsWarning => false;

        public VisualElement Build()
        {
            _context.Ensure();
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("強さ"));
            root.Add(StudioUi.Lead("強さはゲームの画面では選ばない。ここ（または .exe の起動オプション -cpu-level）で決める。" +
                                   "CPU はウェーブごとに慣れて、表の「試合の始め」から「上限」へ上手くなる。"));

            var choose = CpuStudioContext.Group(root, "CPU の強さ");
            choose.Add(StudioUi.Segment(new[]
                {
                    (CpuLevel.Easy, "1", "やさしい", "初めて遊ぶ人 → 少し慣れた人"),
                    (CpuLevel.Normal, "2", "ふつう", "少し慣れた人 → 上手い人（初期値）"),
                    (CpuLevel.Hard, "3", "つよい", "上手い人 → とても上手い人")
                },
                () => _context.Settings.level,
                level =>
                {
                    Undo.RecordObject(_context.Settings, "CPU の強さ");
                    _context.Settings.level = level;
                    EditorUtility.SetDirty(_context.Settings);
                    _context.Object.Update();
                    Refresh();
                },
                out _refreshSegment));
            choose.Add(Track(_context.Field("startFamiliarity", "試合の始めの慣れ", out var r1), r1));
            choose.Add(Track(_context.Field("learning.enabled", "ウェーブごとに学ぶ", out var r2), r2));
            choose.Add(Track(_context.Field("personalityMix", "性格の組み合わせ", out var r3), r3));

            var table = CpuStudioContext.Group(root, "この強さの上手さ（試合の始め → 上限）",
                "4つの目安（初めて遊ぶ人／少し慣れた人／上手い人／とても上手い人）のうち、選んだ強さが受け持つ区間。");
            AddRow(table, "反応", v => v.reactionSeconds, "0.00", " 秒（見えてから動くまで）");
            AddRow(table, "狙い", v => v.leadSeconds, "0.00", " 秒（砲身が来る前から握る）");
            AddRow(table, "熱の読み", v => v.heatPanic01 * 100f, "0", "%（この熱で必ず冷ます）");
            AddRow(table, "スキルの判断", v => v.skillPatienceSeconds, "0.0", " 秒（良い時を待つ長さ）");
            AddRow(table, "周りを見る", v => v.smashBallAttention * 100f, "0", "%（ボールを狙う気持ち）");
            AddRow(table, "歯みがき", v => v.brushPulsesPerSecond, "0.0", " 回/秒");

            var detail = new Foldout { text = "詳しく（4つの目安の数値を直す）", value = false };
            detail.tooltip = "数値ごとの4つの目安。直すと、3つの強さがまとめて変わる。移動・救助（実験中）の数値は、それぞれの設定（CpuMoveSettings・CpuRescueSettings）で直す。";
            var profile = new PropertyField(_context.Object.FindProperty("profile"), "4つの目安");
            profile.Bind(_context.Object);
            detail.Add(profile);
            detail.Add(StudioUi.Button("CPU の設定をインスペクターで開く", () =>
            {
                Selection.activeObject = _context.Settings;
                EditorGUIUtility.PingObject(_context.Settings);
            }, "ほかの設定（考え直す間隔・見直す間隔・学びの伸び方など）もまとめて見る。"));
            root.Add(detail);
            return root;
        }

        public void Refresh()
        {
            _context.Ensure();
            _refreshSegment?.Invoke();
            foreach (var refresh in _refreshers)
            {
                refresh();
            }

            var level = _context.Settings.level;
            var start = _context.Settings.profile.Evaluate(_ => CpuLevels.Scale(level, 0f));
            var cap = _context.Settings.profile.Evaluate(_ => CpuLevels.Scale(level, 1f));
            foreach (var (label, get, format, unit) in _rows)
            {
                label.text = $"{get(start).ToString(format)} → {get(cap).ToString(format)}{unit}";
            }
        }

        private VisualElement Track(VisualElement row, Action refresh)
        {
            _refreshers.Add(refresh);
            return row;
        }

        private void AddRow(VisualElement parent, string name, Func<CpuSkillValues, float> get, string format, string unit)
        {
            // 値は名前の横の残りの幅に収め、足りなければ折り返す（狭いウィンドウで横にはみ出さない）。
            var value = new Label { style = { whiteSpace = WhiteSpace.Normal, flexGrow = 1f, flexShrink = 1f, flexBasis = 0f } };
            var row = StudioUi.Row(new Label(name) { style = { width = 110, flexShrink = 0f } }, value);
            row.style.flexWrap = Wrap.NoWrap;
            parent.Add(row);
            _rows.Add((value, get, format, unit));
        }
    }
}
