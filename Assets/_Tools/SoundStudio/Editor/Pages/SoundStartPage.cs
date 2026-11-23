using System;
using System.Linq;
using MS2026.StudioKit;
using UnityEngine.UIElements;

namespace MS2026.SoundStudio
{
    /// <summary>「はじめに」: 使い方3行・素材の数・音がない欄の多いシーン・wav の置き場所。</summary>
    public sealed class SoundStartPage : IStudioPage
    {
        private readonly SoundStudioContext _context;
        private readonly Action<SoundPageId> _open;
        private VisualElement _stats;
        private VisualElement _scenes;
        private int _shownVersion = -1;

        public SoundStartPage(SoundStudioContext context, Action<SoundPageId> open)
        {
            _context = context;
            _open = open;
        }

        public string Icon => "?";
        public string Label => SoundPageIds.Label(SoundPageId.Start);
        public string Tooltip => "使い方と、まだ音が入っていない欄の数。";
        public int Badge => 0;
        public bool BadgeIsWarning => true;

        public VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("サウンドスタジオ"));
            root.Add(StudioUi.Lead("1. 左で場面を選ぶ　2. 行の［音を入れる…］で音を選ぶ（wav を行に落としてもよい）　3. ▶ で聞いて、音量を合わせる"));

            _stats = StudioUi.Row();
            root.Add(_stats);

            root.Add(StudioUi.Section("音がない欄の多い場面"));
            _scenes = new VisualElement();
            root.Add(_scenes);

            root.Add(StudioUi.Section("新しい音を入れる"));
            var drop = new SoundDropZone(_context, null);
            root.Add(drop);
            return root;
        }

        public void Refresh()
        {
            if (_shownVersion == _context.Version || _stats == null)
            {
                return;
            }

            _shownVersion = _context.Version;
            var real = _context.Slots.Where(s => s.Kind != SoundSlotKind.Candidate).ToList();
            _stats.Clear();
            _stats.Add(StudioUi.Stat(real.Count.ToString(), "音の欄", "ゲームにある音の欄の数（BGM を含む）。"));
            _stats.Add(StudioUi.Stat(SoundStudioContext.CountMissing(real).ToString(), "音がない欄", "まだ音が入っていない欄の数。"));
            _stats.Add(StudioUi.Stat(_context.Library.AllSe.Count.ToString(), "SE の素材", "D-Drive に登録されている SE の数。"));
            _stats.Add(StudioUi.Stat(_context.Library.AllBgm.Count.ToString(), "BGM の素材", "D-Drive に登録されている BGM の数。"));

            _scenes.Clear();
            var byPage = real.Where(s => !s.HasSound).GroupBy(s => s.Page).OrderByDescending(g => g.Count()).Take(4).ToList();
            if (byPage.Count == 0)
            {
                _scenes.Add(StudioUi.Note("全部の欄に音が入っています。", NoteKind.Ok));
                return;
            }

            foreach (var group in byPage)
            {
                var page = group.Key;
                _scenes.Add(StudioUi.ClickCard(SoundPageIds.Label(page), "音がない欄 " + group.Count() + " 個", () => _open(page),
                    "押すとその場面を開く。"));
            }
        }
    }
}
