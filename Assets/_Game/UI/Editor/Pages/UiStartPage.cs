using System.Linq;
using MS2026.StudioKit;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>はじめに: UIスタジオでできることと、6つの手順（終わった手順には自動で✓）。</summary>
    public sealed class UiStartPage : UiPageBase
    {
        private readonly StudioShell _shell;
        private readonly Label[] _steps = new Label[6];

        public UiStartPage(UiStudioContext context, StudioShell shell) : base(context)
        {
            _shell = shell;
        }

        public override string Icon => "★";
        public override string Label => "はじめに";
        public override string Tooltip => "UIスタジオの使い方。初めての人はここから。";

        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("ようこそ、UIスタジオへ"));
            root.Add(StudioUi.Lead(
                "ロビーやゲーム中の表示（HUD）などの画面を、レイヤーで重ねて、動きを付けて、ゲームの値（熱・コアのHP…）とつなぐツールです。" +
                "どの画面も「そのまま動く雛形」から始められます。"));

            var chips = StudioUi.Row(
                StudioUi.Chip("雛形からすぐ動く画面", ChipKind.Accent, "ロビー・HUD・確認ポップアップを、値のつなぎ・動き・ボタンの役目まで入った状態で作れます。"),
                StudioUi.Chip("Photoshopのようなレイヤー", ChipKind.Info, "部品の重なり順・表示・ロック・グループを一覧で整理。"),
                StudioUi.Chip("59種の動きをその場で試す", ChipKind.Info, "D-Drive の定番の動きを、Playせずにクリックで試せます。"),
                StudioUi.Chip("画面切り替えの幕", ChipKind.Warn, "ワイプ・まるく・ブラインド・ひし形・ルール画像・灼ける。"),
                StudioUi.Chip("Playせずに値を確認", ChipKind.Ok, "サンプル値でゲージや文字の見え方を確かめられます。"));
            chips.style.marginBottom = 12;
            root.Add(chips);

            root.Add(StudioUi.Section("6つの手順"));
            root.Add(StudioUi.Step(1, "UIの置き場所を作る",
                "上の帯の「UIの置き場所を作る」。シーンに [UI] ができ、レイヤーごとの Canvas と、ゲームの値を画面へ書き込む部品が付きます。",
                out _steps[0], StudioUi.Button("作る", () => UiStudioSetup.CreateRoot(), primary: true, small: true)));
            root.Add(StudioUi.Step(2, "画面を作る",
                "「画面」ページで雛形（ロビー・HUD・確認ポップアップ・空の画面）を選ぶだけ。Prefab として保存され、名前で開けるようになります。",
                out _steps[1], StudioUi.Button("画面へ", () => _shell.ShowPage<UiScreensPage>(), small: true)));
            root.Add(StudioUi.Step(3, "部品を整える",
                "「この画面を編集」で開き、「レイヤー」ページで部品の重なり順・表示・ロック・グループを整理。絵や文字はシーンビューで普通に直せます。",
                out _steps[2], StudioUi.Button("レイヤーへ", () => _shell.ShowPage<UiLayersPage>(), small: true)));
            root.Add(StudioUi.Step(4, "動きを付ける",
                "「動き」ページで部品を選び、定番の動きをクリックで試して「出たとき」「押したとき」などに付けます。",
                out _steps[3], StudioUi.Button("動きへ", () => _shell.ShowPage<UiMotionPage>(), small: true)));
            root.Add(StudioUi.Step(5, "ゲームの値とつなぐ",
                "「値」ページで、熱・コアのHPなどの値の一覧とサンプル値を確認。部品に「値をゲージで出す」などを付けて名前を選ぶだけでつながります。",
                out _steps[4], StudioUi.Button("値へ", () => _shell.ShowPage<UiValuesPage>(), small: true)));
            root.Add(StudioUi.Step(6, "点検して、Playで試す",
                "「点検」ページの赤を0に。Play すると「起動時に開く」画面が出て、ロビーからつながると HUD に切り替わります。",
                out _steps[5], StudioUi.Button("点検へ", () => _shell.ShowPage<UiCheckPage>(), small: true)));

            root.Add(StudioUi.Section("用語"));
            var words = StudioUi.Card();
            words.Add(StudioUi.Styled(new Label(
                "・画面 … ロビー・HUD・ポーズなど1枚の画面。名前で開け閉めする。\n" +
                "・レイヤー（段）… 画面の重なりの段。背景 → HUD → メニュー → ポップアップ → 通知 → 画面切り替え の順に手前。\n" +
                "・部品 … 画面の中の画像・文字・ボタン・ゲージ。\n" +
                "・値 … ゲームから画面へ渡す数・文字（例: local.heat＝自分の熱）。\n" +
                "・動き … 出る・消える・押した…ときのアニメ（D-Drive の UI の動き）。\n" +
                "・画面切り替えの幕 … 画面やシーンを入れ替えるときに全体を覆う演出。"), "sk-card-body"));
            root.Add(words);
            return root;
        }

        public override void Refresh()
        {
            var root = Context.Root;
            var screens = Context.Screens;
            var anyMotion = screens.Any(s => s != null && s.GetComponentsInChildren<UiElementMotion>(true).Length > 0);
            var anyBinding = screens.Any(s => s != null && s.GetComponentsInChildren<UiBinding>(true).Length > 0);
            var done = new[]
            {
                root != null,
                screens.Count > 0,
                Context.EditingScreen != null || screens.Count > 1,
                anyMotion,
                anyBinding,
                root != null && screens.Count > 0 && StudioIssueList.Count(Context.Issues).errors == 0
            };

            for (var i = 0; i < done.Length; i++)
            {
                StudioUi.SetStepDone(_steps[i], done[i], i + 1);
            }
        }
    }
}
