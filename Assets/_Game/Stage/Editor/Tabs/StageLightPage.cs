using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>光: ステージごとの太陽・まわりの明るさ・霧。値を変えるとすぐシーンに反映する。</summary>
    public sealed class StageLightPage : StagePageBase
    {
        private VisualElement _host;
        private StageSet _bound;

        public StageLightPage(StageStudioContext context) : base(context)
        {
        }

        public override string Icon => "☀";
        public override string Label => "光";
        public override string Tooltip => "ステージごとの光（太陽の向き・色、まわりの明るさ、霧）。";

        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("光"));
            root.Add(StudioUi.Lead("ステージごとの光です。値を動かすとシーンにすぐ反映されます。「光を当てる」をOFFにすると、シーンの光をそのまま使います。"));
            _host = new VisualElement();
            root.Add(_host);
            return root;
        }

        public override void Refresh()
        {
            if (_bound == Context.Stage && _host.childCount > 0)
            {
                return;
            }

            _bound = Context.Stage;
            _host.Clear();
            _host.Unbind();
            if (_bound == null)
            {
                _host.Add(StudioUi.Note("ステージが選ばれていません。", NoteKind.Info));
                return;
            }

            var so = new SerializedObject(_bound);
            var card = StudioUi.Card();
            card.Add(Field(so, "lighting.apply", "光を当てる"));
            card.Add(StudioUi.Section("太陽（メインの光）"));
            card.Add(Field(so, "lighting.sunColor", "色"));
            card.Add(Field(so, "lighting.sunIntensity", "明るさ"));
            card.Add(Field(so, "lighting.sunDirection", "照らす向き（度）"));
            card.Add(Field(so, "lighting.sunElevation", "高さ（度）"));
            card.Add(Field(so, "lighting.shadowStrength", "影の濃さ"));
            card.Add(StudioUi.Section("まわりの明るさ"));
            card.Add(Field(so, "lighting.skyColor", "上から"));
            card.Add(Field(so, "lighting.equatorColor", "横から"));
            card.Add(Field(so, "lighting.groundColor", "下から（照り返し）"));
            card.Add(StudioUi.Section("霧"));
            card.Add(Field(so, "lighting.fog", "霧を出す"));
            card.Add(Field(so, "lighting.fogColor", "霧の色"));
            card.Add(Field(so, "lighting.fogStart", "かすみ始める距離"));
            card.Add(Field(so, "lighting.fogEnd", "真っ白になる距離"));
            card.Add(StudioUi.Section("BGM"));
            card.Add(Field(so, "bgm", "このステージのBGM"));
            card.Bind(so);
            card.TrackSerializedObjectValue(so, _ => Apply());
            _host.Add(card);
            _host.Add(StudioUi.Note("太陽は「[Stage] Sun」という名前でステージの置き場所の下に自動で作られます。シーンに別の Directional Light があると明るすぎることがあるので、そちらは消すかOFFにしてください。", NoteKind.Info));
        }

        private void Apply()
        {
            var root = Context.Root;
            if (root != null && root.current == _bound)
            {
                root.ApplyEnvironment(applyCamera: false);
                SceneView.RepaintAll();
            }
        }

        private static PropertyField Field(SerializedObject so, string path, string label)
        {
            var property = so.FindProperty(path);
            return new PropertyField(property, label) { tooltip = property?.tooltip };
        }
    }
}
