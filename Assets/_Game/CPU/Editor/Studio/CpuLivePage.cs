using System.Linq;
using MS2026.StudioKit;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Cpu.EditorTools
{
    /// <summary>
    /// ページ「頭の中」: Play 中の CPU の様子（性格・今の行動・握力・熱・気持ち・分野ごとの上手さ）。ゲームの画面の F10 の表示の切り替えもここ。
    /// </summary>
    internal sealed class CpuLivePage : IStudioPage
    {
        private VisualElement _list;
        private Label _header;

        public string Icon => "頭";
        public string Label => "頭の中";
        public string Tooltip => "Play 中の CPU が今何をしようとしているか、どれだけ慣れたかを見る。";
        public int Badge => 0;
        public bool BadgeIsWarning => false;

        public VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("頭の中"));
            root.Add(StudioUi.Lead("Play 中だけ。上手さは 0（初めて遊ぶ人）〜 3（とても上手い人）の目盛り。ゲームの画面には F10 でも出せる。"));
            _header = new Label();
            root.Add(_header);
            root.Add(StudioUi.Row(
                StudioUi.Button("ゲームの画面の表示を切り替える（F10 と同じ）", CycleOverlay, "出さない → 表 → 表と線。")));
            _list = new VisualElement();
            root.Add(_list);
            return root;
        }

        public void Refresh()
        {
            if (_list == null)
            {
                return;
            }

            _list.Clear();
            var director = CpuDirector.Current;
            if (!Application.isPlaying || director == null)
            {
                _header.text = string.Empty;
                _list.Add(StudioUi.Empty("…", "Play していない", "Game かステージのシーンを開いて Play すると、ここに CPU の様子が出る。"));
                return;
            }

            var recorder = director.Recorder;
            _header.text = $"CPU の席: {CpuSeatRules.Describe(director.CpuMask)}（{CpuLevels.Label(director.Level)}）" +
                           (recorder != null && recorder.InMatch ? $"・試合 {recorder.MatchNumber}" : string.Empty) +
                           (director.Autoplay.Active ? $"・自動プレイ {director.Autoplay.DoneMatches}/{director.Autoplay.TotalMatches}" : string.Empty);

            for (var p = 0; p < CpuSeatRules.PlayerCount; p++)
            {
                var cpu = director.GetPlayer(p);
                if (cpu == null)
                {
                    continue;
                }

                var info = new CpuTurretInfo(cpu.Context.Turret);
                var current = cpu.Brain.Current;
                var mood = cpu.Context.Mood;
                var section = StudioUi.Section($"P{p + 1}　{cpu.Context.Personality.Label}");
                section.Add(new Label($"今の行動 {(current != null ? current.Id : "なし")}　握力 {cpu.Hands.Grip01:0.00}　熱 {(info.Valid ? info.Heat01 * 100f : 0f):0}%" +
                                      $"　見える敵 {cpu.Eyes.Perceived.Total:0}　協力 {mood.Coop:0.00}　稼ぐ {mood.Greed:0.00}"));
                var areas = string.Join("　", CpuPlugins.SkillAreas.Select(a => $"{a.Label} {cpu.Context.ScaleOf(a.Id):0.00}"));
                section.Add(new Label("上手さ　" + areas) { style = { whiteSpace = WhiteSpace.Normal } });
                _list.Add(section);
            }
        }

        private static void CycleOverlay()
        {
            var director = CpuDirector.Current;
            var overlay = director != null ? director.GetComponent<CpuDebugOverlay>() : null;
            if (overlay != null)
            {
                overlay.Mode = (overlay.Mode + 1) % 3;
            }
        }
    }
}
