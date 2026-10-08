using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 敵が通れない範囲（輪郭）の設定。値を動かすと少し待ってから自動で作り直すので、
    /// シーンビューのオレンジの輪郭がその場で変わる（「変えたらすぐ作り直す」をOFFにすると手動）。
    /// </summary>
    public sealed class PropFootprintSection : PropSection
    {
        private const string PrefAutoRebuild = "MS2026.StageStudio.FootprintAutoRebuild";
        private const double RebuildDelay = 0.35;

        private VisualElement _stats;
        private VisualElement _vertices;
        private VisualElement _area;
        private VisualElement _height;
        private Label _state;
        private VisualElement _disabledNote;
        private double _rebuildAt = -1;
        private FootprintSettings _lastSettings;

        public PropFootprintSection() : base("通れない範囲", "モデルの形から作る、敵が通れない（遅くなる）範囲の輪郭。シーンビューではオレンジ（遅くなる地帯は水色）の線で見えます。")
        {
        }

        protected override void Build(StageProp prop)
        {
            _disabledNote = StudioUi.Note("この役割は通れない範囲を使いません（敵は素通りします）。", NoteKind.Info);
            Body.Add(_disabledNote);
            if (prop.footprint == null)
            {
                Body.Add(StudioUi.Note("Footprint がありません。「点検」の「足りない部品を足す」で直せます。", NoteKind.Error));
                return;
            }

            _state = StudioUi.Chip("", ChipKind.Ok);
            _vertices = StudioUi.Stat("-", "頂点の数", "輪郭の頂点の数。多いほど正確ですが、経路の計算が少し重くなります。");
            _area = StudioUi.Stat("-", "面積", "通れない範囲の広さ（ワールド単位²）。");
            _height = StudioUi.Stat("-", "物の高さ", "この物の見た目の高さ。「壁とみなす高さ」の目安にしてください。");
            _stats = StudioUi.Row(_vertices, _area, _height, StudioUi.Spacer(), _state);
            _stats.style.marginBottom = 6;
            Body.Add(_stats);

            _lastSettings = prop.footprint.settings;
            var so = new SerializedObject(prop.footprint);
            var fields = new VisualElement();
            fields.Add(Field(so, "settings.blockHeight", "壁とみなす高さ"));
            fields.Add(Field(so, "settings.padding", "外側への余白"));
            fields.Add(Field(so, "settings.closeGaps", "狭いすき間を埋める幅"));
            fields.Add(Field(so, "settings.simplify", "角の丸め"));
            fields.Add(Field(so, "settings.resolution", "調べる細かさ"));
            fields.Add(Field(so, "settings.fillHoles", "内側の穴を埋める"));
            fields.Add(Field(so, "settings.minArea", "小さな切れ端を捨てる"));
            fields.Add(Field(so, "manuallyEdited", "手で直した（自動で上書きしない）"));
            fields.Bind(so);
            fields.TrackSerializedObjectValue(so, _ => ScheduleRebuild());
            Body.Add(fields);

            var auto = new Toggle("変えたらすぐ作り直す")
            {
                value = EditorPrefs.GetBool(PrefAutoRebuild, true),
                tooltip = "ONなら、上の値を変えると自動で輪郭を作り直します。大きなモデルで重いときはOFFにして「作り直す」を押します。"
            };
            auto.RegisterValueChangedCallback(e => EditorPrefs.SetBool(PrefAutoRebuild, e.newValue));
            Body.Add(auto);

            Body.Add(StudioUi.Row(
                StudioUi.Button("作り直す", () => StagePropComposer.RebuildFootprint(Prop), "モデルの今の形から輪郭を作り直します。", primary: true, small: true),
                StudioUi.Button("根元を真ん中へ", () => StagePropComposer.RecenterPivot(Prop), "揺れ・回転の中心（根元）を、見た目の真下の床に移します。見た目の位置は変わりません。", small: true),
                StudioUi.Button("形を手で直す", EditByHand, "輪郭を選択します。インスペクターの PolygonCollider2D の「Edit Collider」で頂点を動かせます（自動の作り直しはOFFになります）。", small: true),
                StudioUi.Button("シーンで見る", () => StageStudioContext.Frame(Prop), "シーンビューにこの物を映します。", small: true)));
        }

        public override void Refresh()
        {
            if (Prop == null || Prop.footprint == null || _stats == null)
            {
                return;
            }

            var uses = Prop.role.NeedsFootprint();
            _disabledNote.style.display = uses ? DisplayStyle.None : DisplayStyle.Flex;

            var footprint = Prop.footprint;
            StudioUi.SetStat(_vertices, footprint.HasShape ? footprint.BuiltVertexCount.ToString() : "なし");
            StudioUi.SetStat(_area, footprint.HasShape ? footprint.BuiltArea.ToString("0.00") : "-");
            var bounds = Prop.TryGetComponent<StagePropRenderer>(out var renderer) ? renderer.ComputeBounds() : default;
            StudioUi.SetStat(_height, bounds.size.z.ToString("0.00"));

            // Play中は揺れで見た目が動くので「古い」の判定はしない。
            var stale = !Application.isPlaying && footprint.IsStale(Prop.modelRoot != null ? Prop.modelRoot : Prop.visualRoot);
            _state.text = !footprint.HasShape ? "形がありません" : footprint.manuallyEdited ? "手で直した形" : stale ? "古い（作り直してください）" : "最新";
            StudioUi.SetChipKind(_state, !footprint.HasShape ? ChipKind.Error : footprint.manuallyEdited ? ChipKind.Info : stale ? ChipKind.Warn : ChipKind.Ok);

            if (_rebuildAt > 0 && EditorApplication.timeSinceStartup >= _rebuildAt)
            {
                _rebuildAt = -1;
                if (!footprint.manuallyEdited)
                {
                    StagePropComposer.RebuildFootprint(Prop);
                }
            }
        }

        private void ScheduleRebuild()
        {
            // 作り直し自体も保存値（作ったときの印）を変えるので、設定が本当に変わったときだけ予約する。
            if (Prop == null || Prop.footprint == null || Prop.footprint.settings.Equals(_lastSettings))
            {
                return;
            }

            _lastSettings = Prop.footprint.settings;
            if (EditorPrefs.GetBool(PrefAutoRebuild, true))
            {
                _rebuildAt = EditorApplication.timeSinceStartup + RebuildDelay;
            }
        }

        private void EditByHand()
        {
            Undo.RecordObject(Prop.footprint, "形を手で直す");
            Prop.footprint.manuallyEdited = true;
            EditorUtility.SetDirty(Prop.footprint);
            Selection.activeGameObject = Prop.footprint.gameObject;
        }
    }
}
