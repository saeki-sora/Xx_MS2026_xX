using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 取り込み: FBXを落とす → 向き・大きさ・中心を合わせる → 予定表で役割を直す → 取り込む → 目印に砲台・コアを合わせる。
    /// </summary>
    public sealed class StageImportPage : StagePageBase
    {
        // カメラの既定（正射影サイズ14・横長）で、画面に見える範囲のおおよその大きさ。大きさ合わせの目安に出す。
        private const float VisibleWidth = 50f;
        private const float VisibleHeight = 28f;

        private readonly StageImportOptions _options = new StageImportOptions();
        private GameObject _source;
        private StageImportPlan _plan;
        private StageModelImporter.Result _lastResult;

        private VisualElement _notice;
        private VisualElement _body;
        private ModelDropZone _drop;
        private ObjectField _sourceField;
        private VisualElement _sizeNote;
        private Label _sizeText;
        private ImportPlanTable _table;
        private Button _importButton;
        private VisualElement _resultCard;
        private PopupField<string> _fitTarget;
        private FloatField _fitWidth;

        public StageImportPage(StageStudioContext context) : base(context)
        {
        }

        public override string Icon => "⇩";
        public override string Label => "取り込み";
        public override string Tooltip => "Mayaのモデル（FBX）を、背景オブジェクトとして取り込みます。";

        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("モデルを取り込む"));
            root.Add(StudioUi.Lead("FBXを落とすと中身が一覧になります。向きと大きさを合わせて「取り込む」を押すと、1つずつ背景オブジェクトになり、敵が通れない範囲も自動で作られます。"));

            _notice = new VisualElement();
            root.Add(_notice);

            _body = new VisualElement();
            root.Add(_body);

            _drop = new ModelDropZone(SetSource);
            _body.Add(_drop);
            _sourceField = new ObjectField("取り込むモデル") { objectType = typeof(GameObject), allowSceneObjects = false, tooltip = "取り込むFBXまたはPrefab。上の枠に落としても選べます。" };
            _sourceField.RegisterValueChangedCallback(e => SetSource(e.newValue as GameObject));
            _body.Add(_sourceField);

            _body.Add(StudioUi.Section("向きと大きさ"));
            _body.Add(BuildOptions());

            _body.Add(StudioUi.Section("中身（取り込む物と役割）", "名前から役割を推測しています。行ごとに直せます。"));
            _table = new ImportPlanTable();
            _body.Add(_table);

            _importButton = StudioUi.Button("取り込む", Import, "予定表のチェックが入っている物を、背景オブジェクトとして今のステージに入れます（元に戻すは Ctrl+Z）。", primary: true);
            _importButton.style.marginTop = 10;
            _importButton.style.height = 34;
            _body.Add(_importButton);

            _resultCard = new VisualElement();
            _body.Add(_resultCard);
            Rebuild();
            return root;
        }

        public override void Refresh()
        {
            var missing = UpdateMissingNotice(_notice);
            _body.style.display = missing ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private VisualElement BuildOptions()
        {
            var card = StudioUi.Card();

            var split = new Toggle("中の物を別々にする") { value = _options.splitChildren, tooltip = Tip(nameof(StageImportOptions.splitChildren)) };
            split.RegisterValueChangedCallback(e => { _options.splitChildren = e.newValue; Rebuild(); });
            card.Add(split);

            var axes = new Toggle("Mayaの向きを床に合わせる") { value = _options.convertMayaAxes, tooltip = Tip(nameof(StageImportOptions.convertMayaAxes)) };
            axes.RegisterValueChangedCallback(e => { _options.convertMayaAxes = e.newValue; Rebuild(); });
            card.Add(axes);

            var yaw = StudioUi.Segment(
                new (int, string, string, string)[]
                {
                    (0, "↑", "そのまま", "回さない"),
                    (1, "←", "左に90°", "床の上で左に90度回す"),
                    (2, "↓", "180°", "床の上で180度回す"),
                    (3, "→", "右に90°", "床の上で右に90度回す")
                },
                () => _options.yawSteps,
                v => { _options.yawSteps = v; Rebuild(); },
                out var refreshYaw);
            yaw.RegisterCallback<ClickEvent>(_ => refreshYaw());
            card.Add(StudioUi.Styled(new Label("床の上での向き"), "sk-muted"));
            card.Add(yaw);

            var scale = new FloatField("大きさの倍率") { value = _options.scale, tooltip = Tip(nameof(StageImportOptions.scale)) };
            scale.RegisterValueChangedCallback(e => { _options.scale = Mathf.Max(0.0001f, e.newValue); Rebuild(); });
            card.Add(scale);

            // 「この物の幅を○○にする」で倍率を自動計算する。
            _fitTarget = new PopupField<string>("基準にする物", new List<string> { "（モデル全体）" }, 0) { tooltip = "大きさ合わせの基準にする物。例: 円い床を選び、その幅をゲームの何マスにするかを入れる。" };
            _fitWidth = new FloatField("その幅をこの大きさに") { value = 30f, tooltip = "基準にした物の横幅を、ゲームのこの大きさ（ワールド単位）にします。参考: 画面に見える範囲は横およそ50。" };
            var fit = StudioUi.Button("大きさを合わせる", () => { FitScale(); scale.SetValueWithoutNotify(_options.scale); }, "上の2つから倍率を計算して入れます。", small: true);
            card.Add(_fitTarget);
            card.Add(StudioUi.Row(_fitWidth, fit));

            var center = new Toggle("全体の真ん中をゲームの中心に") { value = _options.centerOnOrigin, tooltip = Tip(nameof(StageImportOptions.centerOnOrigin)) };
            center.RegisterValueChangedCallback(e => { _options.centerOnOrigin = e.newValue; Rebuild(); });
            card.Add(center);

            var materials = new Toggle("専用シェーダーに切り替える") { value = _options.convertMaterials, tooltip = Tip(nameof(StageImportOptions.convertMaterials)) };
            materials.RegisterValueChangedCallback(e => _options.convertMaterials = e.newValue);
            card.Add(materials);

            _sizeNote = StudioUi.Note("", NoteKind.Info);
            _sizeText = _sizeNote.Q<Label>(className: "sk-note-text");
            card.Add(_sizeNote);
            return card;
        }

        private void SetSource(GameObject source)
        {
            _source = source;
            _sourceField.SetValueWithoutNotify(source);
            _drop.SetCaption(source != null ? $"「{source.name}」を選択中（別のFBXを落とすと入れ替わります）" : "FBX をここにドラッグ＆ドロップ");
            _lastResult = null;
            Rebuild();
        }

        private void Rebuild()
        {
            if (_table == null)
            {
                return;
            }

            _plan = _source != null ? StageModelImporter.BuildPlan(_source, _options) : null;
            _table.Show(_plan);

            var names = new List<string> { "（モデル全体）" };
            if (_plan != null)
            {
                foreach (var node in _plan.Nodes)
                {
                    if (!node.IsMarker && node.TriangleCount > 0)
                    {
                        names.Add(node.Name);
                    }
                }
            }

            _fitTarget.choices = names;
            if (!names.Contains(_fitTarget.value))
            {
                _fitTarget.SetValueWithoutNotify(names[0]);
            }

            UpdateSizeNote();
            var count = 0;
            if (_plan != null)
            {
                foreach (var node in _plan.Nodes)
                {
                    if (node.Include && !node.IsMarker)
                    {
                        count++;
                    }
                }
            }

            _importButton.text = count > 0 ? $"取り込む（{count}個）" : "取り込む";
            _importButton.SetEnabled(_plan != null && count > 0);
            ShowResult();
        }

        private void UpdateSizeNote()
        {
            if (_plan == null || _plan.Nodes.Count == 0)
            {
                StudioUi.SetNote(_sizeNote, "モデルを選ぶと、取り込んだ後の大きさがここに出ます。", NoteKind.Info);
                return;
            }

            var size = _plan.TotalBounds.size;
            var text = $"取り込んだ後の全体の大きさ: 横 {size.x:0.#} × 縦 {size.y:0.#} × 高さ {size.z:0.#}（画面に見える範囲は およそ 横{VisibleWidth:0} × 縦{VisibleHeight:0}）";
            var kind = NoteKind.Ok;
            if (Mathf.Max(size.x, size.y) < VisibleWidth * 0.1f)
            {
                text += "\n小さすぎるようです。Mayaのcmのまま（1cm=0.01）かもしれません。「大きさを合わせる」を使ってください。";
                kind = NoteKind.Warn;
            }
            else if (Mathf.Max(size.x, size.y) > VisibleWidth * 6f)
            {
                text += "\n大きすぎるようです。倍率を下げてください。";
                kind = NoteKind.Warn;
            }

            StudioUi.SetNote(_sizeNote, text, kind);
        }

        private void FitScale()
        {
            if (_plan == null)
            {
                return;
            }

            var reference = _plan.TotalBounds.size;
            foreach (var node in _plan.Nodes)
            {
                if (node.Name == _fitTarget.value)
                {
                    reference = node.Size;
                }
            }

            var width = Mathf.Max(reference.x, reference.y);
            if (width <= 0f)
            {
                return;
            }

            _options.scale = Mathf.Max(0.0001f, _options.scale * _fitWidth.value / width);
            Rebuild();
        }

        private void Import()
        {
            if (_plan == null)
            {
                return;
            }

            // 画面の情報が古いこともあるので、押した瞬間にシーンを読み直してから入れる先を決める。
            Context.RecheckNow();
            var container = StageSceneService.PropsContainer(Context.Root);
            if (container == null)
            {
                EditorUtility.DisplayDialog("取り込めません",
                    Context.Root == null
                        ? "開いているシーンに「[Stage]」（ステージの置き場所）が見つかりません。\nGame シーンを開いているか、Prefab の編集画面になっていないか確かめてください。"
                        : "ステージがシーンに出ていません。上の帯でステージを選んで「シーンに出す」を押してください。",
                    "OK");
                return;
            }

            _lastResult = StageModelImporter.Import(_plan, _options, container, Context.Stage);
            Context.MarkPropsDirty();
            if (_lastResult.Props.Count > 0)
            {
                Context.Select(_lastResult.Props[0]);
            }

            ShowResult();
        }

        private void ShowResult()
        {
            _resultCard.Clear();
            if (_lastResult == null)
            {
                return;
            }

            var card = StudioUi.Card("取り込みました", $"背景オブジェクト {_lastResult.Props.Count} 個を作りました。通れない範囲も作ってあります。「オブジェクト」ページで役割や形を確かめてください。");
            _resultCard.Add(StudioUi.Note("取り込んだ物はまだステージ（Prefab）に保存されていません。上の帯の「ステージに保存」で保存します。", NoteKind.Warn));
            if (_lastResult.Markers.Count > 0)
            {
                var spawns = new Toggle("湧き位置も合わせる") { value = false, tooltip = "ONなら、MARK_Spawn… の目印に、シーンの湧き位置（名前順）を合わせます。" };
                var log = StudioUi.Styled(new Label(), "sk-card-body");
                card.Add(StudioUi.Styled(new Label($"目印が {_lastResult.Markers.Count} 個ありました。"), "sk-card-body"));
                card.Add(spawns);
                card.Add(StudioUi.Button("砲台・コアを目印に合わせる", () =>
                {
                    var moved = StageMarkerAligner.Align(_lastResult.Markers, spawns.value);
                    log.text = moved.Count > 0 ? string.Join("\n", moved) : "合わせる相手がシーンにありませんでした。";
                }, "シーンのコア・各プレイヤーの砲台を、目印の位置（床の上のX・Y）へ動かします。元に戻すは Ctrl+Z。", primary: true));
                card.Add(log);
            }

            _resultCard.Add(card);
        }

        private static string Tip(string field)
        {
            var info = typeof(StageImportOptions).GetField(field);
            var attribute = info != null ? (TooltipAttribute)System.Attribute.GetCustomAttribute(info, typeof(TooltipAttribute)) : null;
            return attribute != null ? attribute.tooltip : string.Empty;
        }
    }
}
