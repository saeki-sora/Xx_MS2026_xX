using System.Linq;
using MS2026.StudioKit;
using UnityEngine.UIElements;

namespace MS2026.SoundStudio
{
    /// <summary>上の帯の右側: 「音あり N / M」・音がない所だけ・名前で探す・止める・調べ直す。全体の音量は「はじめに」にある。</summary>
    public sealed class SoundHeader : VisualElement
    {
        private readonly SoundStudioContext _context;
        private readonly Label _count;
        private readonly Button _stop;
        private int _shownVersion = -1;

        public SoundHeader(SoundStudioContext context)
        {
            _context = context;
            AddToClassList("ss-header");

            _count = StudioUi.Styled(new Label(), "ss-header-count");
            _count.tooltip = "音が入っている欄の数 / 全部の欄の数（使っていない欄は数えない）。";
            Add(_count);

            var missing = new Toggle { text = "音がない所だけ", tooltip = "ONにすると、まだ音が入っていない欄だけを出す（使っていない欄は出さない）。" };
            missing.AddToClassList("ss-header-toggle");
            missing.RegisterValueChangedCallback(e =>
            {
                _context.MissingOnly = e.newValue;
                _context.Touch();
            });
            Add(missing);

            var search = new TextField { tooltip = "名前・小見出し・音の名前で探す（全部のページに効く）。" };
            search.AddToClassList("ss-header-search");
            search.textEdition.placeholder = "名前で探す";
            search.RegisterValueChangedCallback(e =>
            {
                _context.Search = e.newValue ?? string.Empty;
                _context.Touch();
            });
            Add(search);

            _stop = StudioUi.Button("■ 止める", () => _context.Preview.Stop(), "試聴の音を全部止める（BGM も止まる）。", small: true);
            _stop.SetEnabled(false);
            Add(_stop);

            Add(StudioUi.Button("調べ直す", () => _context.TryRescan(),
                "プロジェクトの音の欄を調べ直す（欄を足した・シーンを変えたとき）。シーンを裏で開くので少し時間がかかる。", small: true));

            schedule.Execute(Refresh).Every(500);
        }

        private void Refresh()
        {
            _stop.SetEnabled(_context.Preview.AnyPlaying);
            if (_shownVersion == _context.Version)
            {
                return;
            }

            _shownVersion = _context.Version;
            if (!_context.Scanned)
            {
                _count.text = "調べています…";
                return;
            }

            var real = _context.Slots.Where(s => s.Kind != SoundSlotKind.Candidate && s.Active).ToList();
            _count.text = "音あり " + real.Count(s => s.HasSound) + " / " + real.Count;
        }
    }
}
