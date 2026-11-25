using System.Linq;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine.UIElements;

namespace MS2026.SoundStudio
{
    /// <summary>
    /// 「音のバランス」: Play 中に鳴った回数（うるさい所）と、素材ごとの大きさのばらつき。
    /// どちらも、行を押すとその音の欄へ飛ぶ。
    /// </summary>
    public sealed class SoundBalancePage : IStudioPage
    {
        private const int ShownPlays = 30;
        private const int BusyPerSecond = 10;

        private readonly SoundStudioContext _context;
        private VisualElement _plays;
        private VisualElement _loudness;
        private int _shownPlays = -1;
        private int _shownVersion = -1;

        public SoundBalancePage(SoundStudioContext context)
        {
            _context = context;
        }

        public string Icon => "量";
        public string Label => "音のバランス";
        public string Tooltip => "うるさい所（Play 中に鳴った回数）と、素材ごとの音の大きさのばらつき。";
        public int Badge => 0;
        public bool BadgeIsWarning => true;

        public VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("音のバランス"));
            root.Add(StudioUi.Lead("うるさすぎる所と、大きさがそろっていない音を見つける。行を押すと、その音の欄へ飛ぶ。"));

            root.Add(StudioUi.Section("うるさい所（Play 中の記録）", "Play して遊ぶと、欄ごとに鳴った回数が記録される。窓を閉じるまで残る。"));
            root.Add(StudioUi.Row(
                StudioUi.Button("記録を消す", () => _context.Stats.Clear(), "鳴った回数の記録を全部消す（遊び直して比べるとき）。", small: true)));
            _plays = new VisualElement();
            root.Add(_plays);

            root.Add(StudioUi.Section("音の大きさのばらつき", "素材の波形を測った大きさ（音そのものの音量も掛けた値）。真ん中の大きさから 6 dB 以上ずれた音に印を付ける。"));
            root.Add(StudioUi.Row(
                StudioUi.Button("測り直す", () =>
                {
                    _context.Library.Reload();
                    _context.Loudness.Measure(_context.Library);
                    _shownVersion = -1;
                }, "素材の大きさを測り直す（音を足した・音量を変えたとき）。", small: true)));
            _loudness = new VisualElement();
            root.Add(_loudness);
            return root;
        }

        public void Refresh()
        {
            if (_plays == null)
            {
                return;
            }

            if (_shownPlays != _context.Stats.Version)
            {
                _shownPlays = _context.Stats.Version;
                BuildPlays();
            }

            if (_shownVersion != _context.Version || !_context.Loudness.HasMeasured)
            {
                if (!_context.Loudness.HasMeasured && _context.Scanned)
                {
                    _context.Loudness.Measure(_context.Library);
                }

                _shownVersion = _context.Version;
                BuildLoudness();
            }
        }

        private void BuildPlays()
        {
            _plays.Clear();
            var stats = _context.Stats;
            var rows = stats.Keys.Select(k => (key: k, slot: _context.Slots.FirstOrDefault(s => s.Key == k)))
                .Where(x => x.slot != null)
                .OrderByDescending(x => stats.MaxPerSecond(x.key)).ThenByDescending(x => stats.Total(x.key))
                .Take(ShownPlays).ToList();
            if (rows.Count == 0)
            {
                _plays.Add(StudioUi.Note(EditorApplication.isPlaying
                    ? "まだ何も鳴っていません。遊ぶと、鳴った欄がここに並びます。"
                    : "Play して遊ぶと、鳴った回数がここに並びます（音が入っている欄だけ）。", NoteKind.Info));
                return;
            }

            foreach (var (key, slot) in rows)
            {
                var max = stats.MaxPerSecond(key);
                var line = Line(SoundPageIds.Label(slot.Page) + " › " + slot.Label + (string.IsNullOrEmpty(slot.OwnerName) ? "" : "（" + slot.OwnerName + "）"),
                    "1秒に最大 " + max + " 回", "合計 " + stats.Total(key) + " 回", () => _context.GoTo(slot));
                if (max >= BusyPerSecond)
                {
                    line.Add(StudioUi.Chip("多い", ChipKind.Warn,
                        "1秒に " + BusyPerSecond + " 回以上鳴っている。うるさいときは、行を押して「連打の間引き」を長くするか「同時に鳴る数」を減らす。"));
                }

                _plays.Add(line);
            }
        }

        private void BuildLoudness()
        {
            _loudness.Clear();
            if (!_context.Loudness.HasMeasured)
            {
                _loudness.Add(StudioUi.Note("音の欄を調べ終わったら、測ります…", NoteKind.Info));
                return;
            }

            AddLoudnessGroup("SE", _context.Loudness.Se, _context.Loudness.SeMedian);
            AddLoudnessGroup("BGM", _context.Loudness.Bgm, _context.Loudness.BgmMedian);
        }

        private void AddLoudnessGroup(string title, System.Collections.Generic.List<LoudnessInfo> infos, float median)
        {
            if (infos.Count == 0)
            {
                return;
            }

            _loudness.Add(StudioUi.Styled(new Label(title + "（真ん中の大きさ " + median.ToString("0") + " dB）"), "ss-balance-group"));
            foreach (var info in infos.OrderByDescending(i => i.Measured ? i.Db : float.MinValue))
            {
                var kind = info.IsBgm ? SoundSlotKind.Bgm : SoundSlotKind.Effect;
                var users = _context.SlotsUsing(info.Data.Id, kind).ToList();
                var size = info.Measured ? info.Db.ToString("0") + " dB" : "測れない";
                var line = Line(SoundLibrary.NameOf(info.Data), size, "使っている所 " + users.Count, users.Count > 0 ? () => _context.GoTo(users[0]) : (System.Action)null);
                if (info.Mark == LoudnessMark.Loud)
                {
                    line.Add(StudioUi.Chip("大きい", ChipKind.Warn, "ほかの音より 6 dB 以上大きい。行を押して、音そのものの音量を下げるとそろう。"));
                }
                else if (info.Mark == LoudnessMark.Quiet)
                {
                    line.Add(StudioUi.Chip("小さい", ChipKind.Info, "ほかの音より 6 dB 以上小さい。行を押して、音そのものの音量を上げるとそろう（1 が上限）。"));
                }

                if (info.Measured && info.PeakDb > -0.5f)
                {
                    line.Add(StudioUi.Chip("割れそう", ChipKind.Error, "一番大きい所がほぼ上限。重なると音が割れて聞こえることがある。"));
                }

                _loudness.Add(line);
            }
        }

        private static VisualElement Line(string name, string value, string sub, System.Action onClick)
        {
            var line = new VisualElement();
            line.AddToClassList("ss-balance-line");
            if (onClick != null)
            {
                line.AddToClassList("ss-balance-line--link");
                line.RegisterCallback<ClickEvent>(_ => onClick());
                line.tooltip = "押すと、その欄へ飛ぶ。";
            }

            line.Add(StudioUi.Styled(new Label(name), "ss-balance-name"));
            line.Add(StudioUi.Styled(new Label(value), "ss-balance-value"));
            line.Add(StudioUi.Styled(new Label(sub), "ss-balance-sub"));
            return line;
        }
    }
}
