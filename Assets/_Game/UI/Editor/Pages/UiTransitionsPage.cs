using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>画面切り替え: 幕の見本が動くカードの一覧／選んだ幕の設定／Playで試す。</summary>
    public sealed class UiTransitionsPage : UiPageBase
    {
        private VisualElement _grid;
        private VisualElement _detail;
        private UiScreenTransition _selected;
        private int _signature;
        private double _nextScan;
        private List<UiScreenTransition> _all = new List<UiScreenTransition>();

        public UiTransitionsPage(UiStudioContext context) : base(context)
        {
        }

        public override string Icon => "◧";
        public override string Label => "画面切り替え";
        public override string Tooltip => "画面やシーンを入れ替えるときの幕（ワイプ・まるく・ブラインド・ひし形・ルール画像・灼ける）。";

        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("画面切り替えの幕"));
            root.Add(StudioUi.Lead("カードの小さな画面で、それぞれの幕の雰囲気が繰り返し再生されます。使うときは、ボタンの役目「幕で画面を切り替える」やロビーの「画面切り替えの幕」に入れます。"));
            root.Add(StudioUi.Row(
                StudioUi.Button("＋ 新しい幕", Create, "新しい幕の設定ファイルを作ります（Assets/_Game/UI/Transitions/）。", primary: true, small: true),
                StudioUi.Button("ひな形をそろえる", () => { UiStudioSetup.EnsureDefaultTransitions(); _signature = 0; }, "幕が1つも無いとき、暗転・ワイプ・まるく・ブラインド・ひし形・灼ける を作ります。", small: true)));

            _grid = StudioUi.Styled(new VisualElement(), "sk-grid");
            _grid.style.marginTop = 8;
            root.Add(_grid);
            _detail = new VisualElement();
            root.Add(_detail);
            return root;
        }

        public override void Refresh()
        {
            if (EditorApplication.timeSinceStartup >= _nextScan || _signature == 0)
            {
                _nextScan = EditorApplication.timeSinceStartup + 2.0;
                _all = UiStudioSetup.FindAll<UiScreenTransition>();
            }

            var all = _all;
            var signature = all.Count;
            foreach (var t in all)
            {
                signature = signature * 31 + t.GetInstanceID();
            }

            signature = signature * 7 + (_selected != null ? _selected.GetInstanceID() : 0);
            if (signature == _signature)
            {
                return;
            }

            _signature = signature;
            Rebuild(all);
        }

        private void Rebuild(List<UiScreenTransition> all)
        {
            _grid.Clear();
            foreach (var transition in all)
            {
                var t = transition;
                var card = StudioUi.Card();
                card.AddToClassList("sk-card--clickable");
                card.EnableInClassList("sk-card--selected", t == _selected);
                card.tooltip = "クリックで選んで、下で設定を変えられます。";
                card.Add(new TransitionPreviewElement(t));
                card.Add(StudioUi.Styled(new Label(t.Label), "sk-card-title"));
                card.Add(StudioUi.Styled(new Label(PatternName(t.pattern)), "sk-muted"));
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    _selected = t;
                    _signature = 0;
                });
                _grid.Add(card);
            }

            _detail.Clear();
            if (_selected == null)
            {
                return;
            }

            _detail.Add(StudioUi.Section($"「{_selected.Label}」の設定"));
            _detail.Add(StudioUi.Row(
                StudioUi.Button("▶ Playで試す", () => Try(_selected), "Play中に、この幕を実際の画面で再生します。", primary: true, small: true),
                StudioUi.Button("複製", () => Duplicate(_selected), "この幕をコピーして、新しい幕を作ります。", small: true),
                StudioUi.Button("ファイルを選ぶ", () => EditorGUIUtility.PingObject(_selected), "プロジェクトでこの幕のファイルを示します。", small: true)));
            _detail.Add(new InspectorElement(_selected));
        }

        private static void Try(UiScreenTransition transition)
        {
            if (!Application.isPlaying || UiRoot.Active == null)
            {
                EditorUtility.DisplayDialog("Play中に試せます", "Play を押してから、もう一度「Playで試す」を押してください。カードの小さな画面では、Playしなくても雰囲気を見られます。", "OK");
                return;
            }

            UiRoot.Active.PlayTransition(transition);
        }

        private void Create()
        {
            var transition = ScriptableObject.CreateInstance<UiScreenTransition>();
            var path = StudioAssets.UniquePath(UiStudioSetup.TransitionsFolder, "Transition_New.asset");
            AssetDatabase.CreateAsset(transition, path);
            AssetDatabase.SaveAssets();
            _selected = transition;
            _signature = 0;
        }

        private void Duplicate(UiScreenTransition source)
        {
            var path = AssetDatabase.GenerateUniqueAssetPath(AssetDatabase.GetAssetPath(source));
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path);
            var copy = AssetDatabase.LoadAssetAtPath<UiScreenTransition>(path);
            copy.displayName = source.Label + "（コピー）";
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();
            _selected = copy;
            _signature = 0;
        }

        private static string PatternName(UiTransitionPattern pattern) => pattern switch
        {
            UiTransitionPattern.Fade => "フェード（暗転）",
            UiTransitionPattern.Wipe => "ワイプ",
            UiTransitionPattern.Iris => "まるく閉じる",
            UiTransitionPattern.Blinds => "ブラインド",
            UiTransitionPattern.Diamonds => "ひし形",
            UiTransitionPattern.RuleTexture => "ルール画像",
            UiTransitionPattern.Burn => "灼ける",
            _ => pattern.ToString()
        };
    }
}
