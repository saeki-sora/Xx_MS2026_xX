using MS2026.Fortress;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// コア（全ステージ共通）: コアの見た目を3Dモデル（歯）にして、置き方・汚れ・ふちの光・ひびを整え、HP を変えて試す。
    /// コアは Game シーンにあるので、ステージを選んでいなくても使える。中身は独立したパネルで、ここは並べるだけ。
    /// </summary>
    public sealed class StageCorePage : StagePageBase
    {
        private VisualElement _host;
        private string _boundKey;
        private CoreCrystalController _boundCore;
        private CoreToothModel _boundHolder;

        public StageCorePage(StageStudioContext context) : base(context)
        {
        }

        public override string Icon => "◎";
        public override string Label => "コア";
        public override string Tooltip => "全ステージ共通のコアの見た目（3Dモデル・汚れ・ふちの光・ひび）。Game シーンのコアを変えます。";

        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("コア（全ステージ共通）"));
            root.Add(StudioUi.Lead("コアの見た目を3Dモデル（歯）にします。モデルを入れると、向き・大きさ・床の高さを自動で合わせ、ステージの物と同じトゥーンの塗りにします。HP が減るほど汚れ、ふちの光が赤く速くなり、ひびが入ります。"));
            root.Add(StudioUi.Note("ここで変えるのは Game シーンのコアです。どのステージでも同じ見た目になります。変えたら下の「Game シーンを保存」を押してください。", NoteKind.Info));
            _host = new VisualElement();
            root.Add(_host);
            return root;
        }

        public override void Refresh()
        {
            // Play 中は変えない（Play 用の写しのコアを変えても残らず、止めると消えてしまう）。
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (_boundKey != PlayKey)
                {
                    _boundKey = PlayKey;
                    _boundCore = null;
                    _boundHolder = null;
                    _host.Clear();
                    _host.Add(StudioUi.Empty("◎", "Play 中は変えられません",
                        "Play を止めると、ここに戻ります。Play 中に変えた値は残らないので、止めてから調整してください。"));
                }

                return;
            }

            var core = CoreToothSetup.FindCore();
            var holder = CoreToothSetup.FindModel(core);
            var key = StateKey(core);

            // 同じ番号でも、前に作ったときのコアが消えて作り直されていたら（Play を止めたあとなど）作り直す。
            var sameObjects = ReferenceEquals(core, _boundCore) && ReferenceEquals(holder, _boundHolder);
            if (key == _boundKey && sameObjects && _host.childCount > 0)
            {
                CoreSavePanel.Refresh(_host, core);
                return;
            }

            _boundKey = key;
            _boundCore = core;
            _boundHolder = holder;
            _host.Clear();
            if (core == null)
            {
                _host.Add(StudioUi.Empty("◎", "コアが見つかりません",
                    "コアは Game シーンにあります。Game シーンを今のシーンに足して開くと、ここで変えられます。",
                    "Game シーンを足して開く", () => { CoreToothSetup.OpenGameScene(); _boundKey = null; }));
                return;
            }

            _host.Add(CoreViewPanel.Build());
            _host.Add(CoreModelPanel.Build(core, holder, Invalidate));
            if (holder != null)
            {
                _host.Add(CoreLookPanel.Build(core));
            }

            _host.Add(CorePreviewPanel.Build(core));
            _host.Add(CoreSavePanel.Build(core));
        }

        private const string PlayKey = "playing";

        private void Invalidate()
        {
            _boundKey = null;
        }

        // コア・モデルの入れ物・入っているモデルが変わったら作り直す。
        private static string StateKey(CoreCrystalController core)
        {
            if (core == null)
            {
                return "none";
            }

            var holder = CoreToothSetup.FindModel(core);
            var model = holder != null && holder.transform.childCount > 0 ? holder.transform.GetChild(0).GetInstanceID() : 0;
            return $"{core.GetInstanceID()}/{(holder != null ? holder.GetInstanceID() : 0)}/{model}";
        }
    }
}
