using System.Linq;
using MS2026.Fortress;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine.UIElements;

namespace MS2026.SoundStudio
{
    /// <summary>上の帯の右側: 「音あり N / M」・音がない所だけ・名前で探す・全体音量・調べ直す。</summary>
    public sealed class SoundHeader : VisualElement
    {
        private readonly SoundStudioContext _context;
        private readonly Label _count;
        private readonly Slider _se;
        private readonly Slider _voice;
        private readonly Slider _bgm;
        private int _shownVersion = -1;

        public SoundHeader(SoundStudioContext context)
        {
            _context = context;
            AddToClassList("ss-header");

            _count = StudioUi.Styled(new Label(), "ss-header-count");
            _count.tooltip = "音が入っている欄の数 / 全部の欄の数（まだ欄がない候補は数えない）。";
            Add(_count);

            var missing = new Toggle("音がない所だけ") { tooltip = "ONにすると、まだ音が入っていない欄だけを出す。" };
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

            _se = Master("SE", "効果音（声以外）全体の音量。ゲームの全部の効果音に効く。", b => b.seVolume, (b, v) => b.seVolume = v);
            _voice = Master("声", "声全体の音量。", b => b.voiceVolume, (b, v) => b.voiceVolume = v);
            _bgm = Master("BGM", "BGM 全体の音量。", b => b.bgmVolume, (b, v) => b.bgmVolume = v);

            Add(StudioUi.Button("調べ直す", () => _context.Rescan(),
                "プロジェクトの音の欄を調べ直す（欄を足した・シーンを変えたとき）。シーンを裏で開くので少し時間がかかる。", small: true));

            schedule.Execute(Refresh).Every(500);
        }

        private Slider Master(string label, string tooltip, System.Func<FortressSoundBalance, float> get, System.Action<FortressSoundBalance, float> set)
        {
            var slider = new Slider(label, 0f, 1f) { tooltip = tooltip, showInputField = false };
            slider.AddToClassList("ss-header-master");
            slider.RegisterValueChangedCallback(e =>
            {
                var balance = _context.Balance;
                if (balance == null)
                {
                    if (UnityEngine.Mathf.Approximately(e.newValue, 1f))
                    {
                        return;
                    }

                    balance = _context.EnsureBalance();
                }

                Undo.RecordObject(balance, "サウンドスタジオ: 全体音量");
                set(balance, e.newValue);
                EditorUtility.SetDirty(balance);
            });
            slider.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                if (_context.Balance != null)
                {
                    AssetDatabase.SaveAssetIfDirty(_context.Balance);
                }
            });
            slider.userData = get;
            Add(slider);
            return slider;
        }

        private void Refresh()
        {
            var balance = _context.Balance;
            SetIfIdle(_se, balance != null ? balance.seVolume : 1f);
            SetIfIdle(_voice, balance != null ? balance.voiceVolume : 1f);
            SetIfIdle(_bgm, balance != null ? balance.bgmVolume : 1f);
            if (_shownVersion == _context.Version)
            {
                return;
            }

            _shownVersion = _context.Version;
            var real = _context.Slots.Where(s => s.Kind != SoundSlotKind.Candidate).ToList();
            _count.text = "音あり " + real.Count(s => s.HasSound) + " / " + real.Count;
        }

        private static void SetIfIdle(Slider slider, float value)
        {
            if (slider.panel?.focusController?.focusedElement != slider)
            {
                slider.SetValueWithoutNotify(value);
            }
        }
    }
}
