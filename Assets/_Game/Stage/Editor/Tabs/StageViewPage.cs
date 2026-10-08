using MS2026.Fortress.Cameras;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>見え方: カメラの見え方の切り替え／隠れチェック／透け・影・熱・揺れ・壊れ方の共通設定。</summary>
    public sealed class StageViewPage : StagePageBase
    {
        private CameraPresetPanel _cameras;
        private VisualElement _report;
        private VisualElement _profileHost;
        private StageLookProfile _boundProfile;
        private double _nextReport;
        private string _reportKey;

        public StageViewPage(StageStudioContext context) : base(context)
        {
        }

        public override string Icon => "◐";
        public override string Label => "見え方";
        public override string Tooltip => "カメラの見え方の切り替えと、手前の物の透け方・隠れた敵の影・熱・揺れの共通設定。";

        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("見え方"));
            root.Add(StudioUi.Lead("カメラの見え方（4人それぞれの視点）を選び、手前の背景が邪魔をしないかを確かめます。透け方・影・熱・揺れの強さや色もここで決めます。"));

            root.Add(StudioUi.Section("カメラの見え方", "要塞デザイナーのカメラタブで作った見え方のプリセット。押すと切り替わります。"));
            _cameras = new CameraPresetPanel(Context);
            root.Add(_cameras);

            root.Add(StudioUi.Note("背景の位置・向き・大きさ・高さは「配置」ページで、ゲームの画面を見ながら直接さわって合わせられます。", NoteKind.Info));
            root.Add(StudioUi.Row(StudioUi.Button("配置ページを開く", StageStudioWindow.OpenPage<StageLayoutPage>,
                "ゲームの画面（P1〜P4・全体）の中で背景をつかんで動かすページへ移ります。", small: true)));

            root.Add(StudioUi.Section("隠れチェック", "今のカメラの見え方で、どの背景が砲台・コアを隠しているか。"));
            _report = new VisualElement();
            root.Add(_report);

            root.Add(StudioUi.Section("反応の共通設定", "透け・敵の影・熱・揺れ・壊れ方の強さと色。ステージごとに別の設定を使えます。"));
            _profileHost = new VisualElement();
            root.Add(_profileHost);
            return root;
        }

        public override void Refresh()
        {
            _cameras.Refresh();
            if (EditorApplication.timeSinceStartup >= _nextReport)
            {
                _nextReport = EditorApplication.timeSinceStartup + 1.0;
                RebuildReport();
            }

            var profile = Context.Stage != null ? Context.Stage.lookProfile : null;
            if (profile != _boundProfile || _profileHost.childCount == 0)
            {
                _boundProfile = profile;
                RebuildProfile();
            }
        }

        private void RebuildReport()
        {
            var findings = StageOcclusionReport.Run(_cameras.Current, Context.Props);
            var key = (_cameras.Current != null ? _cameras.Current.GetInstanceID() : 0) + ":" +
                      string.Join(",", findings.ConvertAll(f => $"{f.Viewer}/{f.Prop.GetInstanceID()}/{f.TargetLabel}/{f.Handled}"));
            if (key == _reportKey)
            {
                return; // 変化が無ければ作り直さない（マウスを乗せている説明がちらつかないように）
            }

            _reportKey = key;
            _report.Clear();
            if (_cameras.Current == null)
            {
                _report.Add(StudioUi.Note("カメラの見え方が選ばれていません。", NoteKind.Info));
                return;
            }

            if (findings.Count == 0)
            {
                _report.Add(StudioUi.Note("どの視点でも、背景は砲台・コアを隠していません。", NoteKind.Ok));
                return;
            }

            foreach (var finding in findings)
            {
                var text = $"{ViewerIndex.Label(finding.Viewer)}の視点: 「{finding.Prop.name}」が{finding.TargetLabel}を隠しています";
                var note = StudioUi.Note(finding.Handled ? text + " → 透けるので大丈夫" : text + " → 透けない設定です", finding.Handled ? NoteKind.Ok : NoteKind.Warn);
                var prop = finding.Prop;
                note.tooltip = "クリックでその背景オブジェクトを選びます。";
                note.RegisterCallback<ClickEvent>(_ => Context.Select(prop));
                _report.Add(note);
            }
        }

        private void RebuildProfile()
        {
            _profileHost.Clear();
            var stage = Context.Stage;
            if (stage == null)
            {
                _profileHost.Add(StudioUi.Note("ステージが選ばれていません。", NoteKind.Info));
                return;
            }

            if (_boundProfile == null)
            {
                _profileHost.Add(StudioUi.Note("このステージは既定値を使っています（変えられません）。共通設定のファイルを作ると調整できます。", NoteKind.Info));
                _profileHost.Add(StudioUi.Button("共通設定を使う", () => Assign(StageAssetFactory.GetOrCreateDefaultLookProfile()), "全ステージ共通の設定ファイルを使います（無ければ作ります）。", primary: true));
                return;
            }

            var shared = AssetDatabase.GetAssetPath(_boundProfile) == StageAssetFactory.DefaultLookProfilePath;
            _profileHost.Add(StudioUi.Row(
                StudioUi.Chip(shared ? "全ステージ共通の設定" : "このステージ専用の設定", shared ? ChipKind.Info : ChipKind.Accent),
                StudioUi.Spacer(),
                StudioUi.Button("このステージ専用にする", MakeUnique, "今の設定をコピーして、このステージだけの設定にします（他のステージに影響しなくなります）。", small: true)));
            _profileHost.Add(new InspectorElement(_boundProfile));
        }

        private void MakeUnique()
        {
            var stage = Context.Stage;
            if (stage == null || _boundProfile == null)
            {
                return;
            }

            var copy = Object.Instantiate(_boundProfile);
            var path = StudioAssets.UniquePath(StageAssetFactory.FolderOf(stage), $"StageLookProfile_{StudioAssets.SafeFileName(stage.Label)}.asset");
            AssetDatabase.CreateAsset(copy, path);
            Assign(copy);
        }

        private void Assign(StageLookProfile profile)
        {
            var stage = Context.Stage;
            Undo.RecordObject(stage, "反応の設定を変更");
            stage.lookProfile = profile;
            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssets();
            profile.ApplyGlobals();
        }
    }
}
