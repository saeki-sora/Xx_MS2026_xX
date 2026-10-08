using System.Linq;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>はじめに: このツールでできることと、5つの手順（終わった手順には自動で✓が付く）。</summary>
    public sealed class StageStartPage : StagePageBase
    {
        private readonly StudioShell _shell;
        private readonly Label[] _stepNumbers = new Label[5];
        private VisualElement _stageCard;
        private StageSet _boundStage;

        public StageStartPage(StageStudioContext context, StudioShell shell) : base(context)
        {
            _shell = shell;
        }

        public override string Icon => "★";
        public override string Label => "はじめに";
        public override string Tooltip => "このツールの使い方。初めての人はここから。";

        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("ようこそ、ステージ背景スタジオへ"));
            root.Add(StudioUi.Lead(
                "Mayaで作った3Dの背景（シャンプー・歯ブラシ・床…）を置き、敵の群れがその間をさらさらと避けて流れるようにするツールです。" +
                "上から順に進めれば、ステージが1つできあがります。"));

            var features = StudioUi.Row(
                StudioUi.Chip("形から通路を自動生成", ChipKind.Accent, "モデルの形から、敵が通れない範囲を自動で作ります。浮いている部分の下はくぐれます。"),
                StudioUi.Chip("手前の物は自動で透ける", ChipKind.Info, "砲台やコアを隠す所だけ、丸く透けます。4人それぞれの視点で自動。"),
                StudioUi.Chip("隠れた敵は影で見える", ChipKind.Info, "背景の裏に入った敵は、単色の影で透けて見えます。"),
                StudioUi.Chip("レーザーで光る・焦げる", ChipKind.Warn, "当たった所が赤く光り、当て続けると焦げ跡が残ります。"),
                StudioUi.Chip("群れに押されて揺れる", ChipKind.Ok, "敵が押し寄せるとプルプル揺れます。"));
            features.style.marginBottom = 12;
            root.Add(features);

            root.Add(StudioUi.Section("5つの手順"));
            root.Add(StudioUi.Step(1, "ステージを作る（選ぶ）",
                "上の帯の「＋ 新しいステージ」で作るか、一覧から選んで「シーンに出す」。洗面所・台所…のように、ステージはいくつでも作れます。",
                out _stepNumbers[0]));
            root.Add(StudioUi.Step(2, "Mayaのモデルを取り込む",
                "FBXを落とすだけ。中の物（シャンプー・歯ブラシ…）が1つずつ背景オブジェクトになり、通れない範囲も自動で作られます。",
                out _stepNumbers[1], StudioUi.Button("取り込みへ", () => _shell.ShowPage<StageImportPage>(), primary: true, small: true)));
            root.Add(StudioUi.Step(3, "役割を決める",
                "床・壁・壊せる壁・遅くなる地帯（水たまり等）・飾り。浮いている部分の高さや、輪郭の余白もここで。",
                out _stepNumbers[2], StudioUi.Button("オブジェクトへ", () => _shell.ShowPage<StagePropsPage>(), small: true)));
            root.Add(StudioUi.Step(4, "見え方を確かめる",
                "カメラの見え方のプリセットを選び、手前の物の透け方・敵の影・熱・揺れを調整します。",
                out _stepNumbers[3], StudioUi.Button("見え方へ", () => _shell.ShowPage<StageViewPage>(), small: true)));
            root.Add(StudioUi.Step(5, "点検して保存",
                "道が塞がっていないか、砲台にかぶっていないか…を自動で点検。最後に上の帯の「ステージに保存」。",
                out _stepNumbers[4], StudioUi.Button("点検へ", () => _shell.ShowPage<StageCheckPage>(), small: true)));

            root.Add(StudioUi.Section("このステージ"));
            _stageCard = StudioUi.Card();
            root.Add(_stageCard);

            root.Add(StudioUi.Section("Mayaで決めておくと楽になる名前"));
            var names = StudioUi.Card();
            names.Add(StudioUi.Styled(new Label(
                "・MARK_Core … コアの位置の目印（空のロケーターでOK）\n" +
                "・MARK_Turret1〜4（MARK_P1〜P4 も可）… 各プレイヤーの砲台の位置\n" +
                "・MARK_Spawn… … 敵の湧き位置\n" +
                "・floor / plate / 床 / 皿 が名前に入る物 … 自動で「床」\n" +
                "・water / foam / 水 / 泡 … 「遅くなる地帯」、glass / break / 割 … 「壊せる壁」、deco / bg / 飾り … 「飾り」\n" +
                "・それ以外 … 「壁」。どれも取り込むときの表で直せます。"), "sk-card-body"));
            root.Add(names);
            return root;
        }

        public override void Refresh()
        {
            var root = Context.Root;
            var placed = root != null && root.instance != null;
            var props = Context.Props;
            var issues = Context.Issues;
            var errors = issues.Count(i => i.Severity == StudioIssueSeverity.Error);
            var done = new[]
            {
                placed,
                placed && props.Count > 0,
                placed && props.Count > 0 && props.Any(p => p != null && p.role != StagePropRole.Wall),
                placed && Context.Stage != null && Context.Stage.cameraPreset != null,
                placed && props.Count > 0 && errors == 0 && !StageSceneService.HasUnsavedChanges(root)
            };

            for (var i = 0; i < done.Length; i++)
            {
                StudioUi.SetStepDone(_stepNumbers[i], done[i], i + 1);
            }

            if (_boundStage != Context.Stage)
            {
                _boundStage = Context.Stage;
                RebuildStageCard();
            }
        }

        private void RebuildStageCard()
        {
            _stageCard.Clear();
            if (_boundStage == null)
            {
                _stageCard.Add(StudioUi.Styled(new Label("ステージが選ばれていません。上の帯で選ぶか作ってください。"), "sk-card-body"));
                return;
            }

            var so = new SerializedObject(_boundStage);
            _stageCard.Add(Field(so, "displayName", "ステージの名前"));
            _stageCard.Add(Field(so, "memo", "メモ"));
            _stageCard.Add(Field(so, "thumbnail", "一覧に出す絵"));
            _stageCard.Bind(so);
        }

        private static PropertyField Field(SerializedObject so, string path, string label)
        {
            var property = so.FindProperty(path);
            return new PropertyField(property, label) { tooltip = property.tooltip };
        }
    }
}
