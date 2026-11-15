using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Cpu.EditorTools
{
    /// <summary>
    /// ページ「試合を回す」: 自動プレイで試合を回し、結果の表（Logs/CPU）の最新のまとめを見る。LAN・性能テストの起動オプションもここ。
    /// </summary>
    internal sealed class CpuRunPage : IStudioPage
    {
        private readonly CpuStudioContext _context;
        private readonly List<Action> _refreshers = new List<Action>();
        private VisualElement _summary;
        private string _shownFile;
        private DateTime _shownTime;

        public CpuRunPage(CpuStudioContext context)
        {
            _context = context;
        }

        public string Icon => "回";
        public string Label => "試合を回す";
        public string Tooltip => "全部の席を CPU にして試合を続けて回し、結果の表で強さや性格を比べる。";
        public int Badge => 0;
        public bool BadgeIsWarning => false;

        private static string LogFolder => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs", "CPU");

        public VisualElement Build()
        {
            _context.Ensure();
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("試合を回す"));
            root.Add(StudioUi.Lead("Game かステージのシーンを開いて「自動プレイで Play する」。試合が終わるたびに結果の表（Logs/CPU）に書く。"));

            var run = StudioUi.Section("自動プレイ");
            run.Add(Track(_context.Field("autoplayMatches", "回す試合の数", out var r1), r1));
            run.Add(Track(_context.Field("autoplayTimeScale", "早送りの倍率", out var r2), r2));
            run.Add(Track(_context.Field("csvMode", "結果の表を書く", out var r3), r3));
            run.Add(StudioUi.Row(
                StudioUi.Button("自動プレイで Play する", () => EditorApplication.ExecuteMenuItem("Tools/CPU/自動プレイで Play する"),
                    "設定の試合数・早送りで、全部の席を CPU にして今のシーンを Play する。", primary: true),
                StudioUi.Button("結果の表のフォルダを開く", () => EditorApplication.ExecuteMenuItem("Tools/CPU/結果の表のフォルダを開く"))));
            root.Add(run);

            var latest = StudioUi.Section("最新の結果の表", "Logs/CPU のいちばん新しい表の「試合の合計」の行を、強さ・操作ごとに平均したもの。");
            _summary = new VisualElement();
            latest.Add(_summary);
            root.Add(latest);

            var options = new Foldout { text = "LAN・性能テストの起動オプション（.exe・bat 用）", value = false };
            AddOption(options, "-cpu 2,3,4", "P2〜P4 を CPU にする（人が入っている席は人が優先）。");
            AddOption(options, "-cpu-all -autoplay 10 -timescale 2", "全部の席を CPU にして 10 試合を 2 倍速で回す。");
            AddOption(options, "-cpu-level hard -cpu-seed 123", "強さと乱数の種（同じ種なら同じ動き）。");
            AddOption(options, "-cpu-csv", "自動プレイでなくても、CPU がいる試合の結果を表に書く。");
            AddOption(options, "-cpu none", "CPU を使わない（通信・性能のテストで人だけにしたいとき）。");
            options.Add(StudioUi.Note("性能テスト: Tools/NetTest の bat に、Host の起動へ「-cpu 2,3,4」を足すと、人がいなくても 4人ぶん撃つ負荷になる（CPU は Host だけで動く）。",
                NoteKind.Info));
            root.Add(options);
            return root;
        }

        public void Refresh()
        {
            _context.Ensure();
            foreach (var refresh in _refreshers)
            {
                refresh();
            }

            RefreshSummary();
        }

        private VisualElement Track(VisualElement row, Action refresh)
        {
            _refreshers.Add(refresh);
            return row;
        }

        private static void AddOption(VisualElement parent, string option, string body)
        {
            var field = new TextField { value = option, isReadOnly = true, tooltip = body };
            field.style.flexGrow = 1f;
            parent.Add(StudioUi.Row(field, StudioUi.Button("写す", () => EditorGUIUtility.systemCopyBuffer = option, "クリップボードに写す。", small: true)));
            parent.Add(new Label(body) { style = { marginLeft = 4, marginBottom = 6, opacity = 0.8f } });
        }

        // いちばん新しい表を読み、試合の合計の行を「強さ × 操作（CPU/人）」で平均する（変わったときだけ読み直す）。
        private void RefreshSummary()
        {
            if (_summary == null)
            {
                return;
            }

            var file = Directory.Exists(LogFolder)
                ? Directory.GetFiles(LogFolder, "cpu_*.csv").OrderByDescending(File.GetLastWriteTime).FirstOrDefault()
                : null;
            var time = file != null ? File.GetLastWriteTime(file) : default;
            if (file == _shownFile && time == _shownTime)
            {
                return;
            }

            _shownFile = file;
            _shownTime = time;
            _summary.Clear();
            if (file == null)
            {
                _summary.Add(StudioUi.Empty("…", "まだ結果の表がない", "自動プレイで試合を回すと、ここにまとめが出る。"));
                return;
            }

            try
            {
                var rows = CpuCsvReader.Read(file);
                _summary.Add(new Label($"{Path.GetFileName(file)}（{time:MM/dd HH:mm}）") { style = { opacity = 0.8f } });
                foreach (var line in CpuCsvReader.Summarize(rows))
                {
                    _summary.Add(new Label(line));
                }
            }
            catch (Exception e)
            {
                _summary.Add(StudioUi.Note("表を読めなかった: " + e.Message, NoteKind.Warn));
            }
        }
    }

    /// <summary>結果の表（CSV）を読む・まとめる（スタジオ用）。</summary>
    internal static class CpuCsvReader
    {
        public static List<Dictionary<string, string>> Read(string path)
        {
            var lines = File.ReadAllLines(path, System.Text.Encoding.UTF8);
            var result = new List<Dictionary<string, string>>();
            if (lines.Length == 0)
            {
                return result;
            }

            var header = Split(lines[0]);
            for (var i = 1; i < lines.Length; i++)
            {
                var cells = Split(lines[i]);
                var row = new Dictionary<string, string>();
                for (var c = 0; c < header.Count && c < cells.Count; c++)
                {
                    row[header[c]] = cells[c];
                }

                result.Add(row);
            }

            return result;
        }

        /// <summary>試合の合計の行を「強さ・操作」ごとに平均した文（試合数・撃破・空振り秒・オーバーヒート・倒れた回）。</summary>
        public static IEnumerable<string> Summarize(List<Dictionary<string, string>> rows)
        {
            var totals = rows.Where(r => r.TryGetValue("区切り", out var part) && part.StartsWith("試合の合計")).ToList();
            if (totals.Count == 0)
            {
                yield return "まだ終わった試合がない。";
                yield break;
            }

            var matches = totals.Select(r => Get(r, "試合")).Distinct().Count();
            var clears = totals.Where(r => r["区切り"].Contains("クリア")).Select(r => Get(r, "試合")).Distinct().Count();
            yield return $"試合 {matches}（クリア {clears}）";
            foreach (var group in totals.GroupBy(r => (Get(r, "強さ"), Get(r, "操作"))).OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2))
            {
                yield return $"{group.Key.Item1}・{group.Key.Item2}: 撃破 {Average(group, "撃破"):0.0}" +
                             $"  空振り {Average(group, CpuStats.WhiffSeconds):0.0}秒  オーバーヒート {Average(group, CpuStats.Overheats):0.0}回" +
                             $"  倒れた {Average(group, "倒れた回"):0.0}回  コア {Average(group, "コアHP%"):0}%";
            }
        }

        private static string Get(Dictionary<string, string> row, string key) => row.TryGetValue(key, out var v) ? v : string.Empty;

        private static float Average(IEnumerable<Dictionary<string, string>> rows, string key)
        {
            var values = rows.Select(r => float.TryParse(Get(r, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f).ToList();
            return values.Count > 0 ? values.Average() : 0f;
        }

        // CSV の1行を分ける（" で囲んだマスの , と "" に対応）。
        private static List<string> Split(string line)
        {
            var cells = new List<string>();
            var cell = new System.Text.StringBuilder();
            var quoted = false;
            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (quoted)
                {
                    if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else if (ch == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        cell.Append(ch);
                    }
                }
                else if (ch == '"')
                {
                    quoted = true;
                }
                else if (ch == ',')
                {
                    cells.Add(cell.ToString());
                    cell.Clear();
                }
                else
                {
                    cell.Append(ch);
                }
            }

            cells.Add(cell.ToString());
            return cells;
        }
    }
}
