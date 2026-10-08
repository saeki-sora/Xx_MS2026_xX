using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>見た目の反応（透け・敵の影・熱・揺れ）の設定と、Play中に試すボタン。</summary>
    public sealed class PropLookSection : PropSection
    {
        private VisualElement _playButtons;
        private Label _live;

        public PropLookSection() : base("見た目の反応", "この物が、手前で邪魔したとき・レーザーが当たったとき・敵に押されたときにどう見えるか。")
        {
        }

        protected override void Build(StageProp prop)
        {
            var so = new SerializedObject(prop);
            var fields = new VisualElement();
            fields.Add(Field(so, "look.occlusion", "砲台・コアを隠したとき"));
            fields.Add(Field(so, "look.wholeFadeAlpha", "全体を薄くしたときの濃さ"));
            fields.Add(Field(so, "look.showHiddenEnemies", "裏に隠れた敵を影で見せる"));
            fields.Add(Field(so, "look.heatReactive", "レーザーで光る・焦げる"));
            fields.Add(Field(so, "look.heatScale", "光り・焦げの強さ"));
            fields.Add(Field(so, "look.wobble", "敵に押されて揺れる"));
            fields.Add(Field(so, "look.wobbleStrength", "揺れの大きさ"));
            fields.Bind(so);
            Body.Add(fields);

            Body.Add(StudioUi.Note("「裏に隠れた敵を影で見せる」を変えたら、「モデル」の「専用シェーダーに切り替え」を押すと反映されます（マテリアルの印を付け替えるため）。", NoteKind.Info));

            _live = StudioUi.Styled(new Label(), "sk-muted");
            Body.Add(_live);
            _playButtons = StudioUi.Row(
                StudioUi.Button("熱を当ててみる", HeatTest, "Play中に、この物の手前側にレーザーが2秒当たった状態を作ります。", small: true),
                StudioUi.Button("揺らしてみる", () => Wobble()?.Poke(Random.insideUnitCircle, 6f), "Play中に、この物をぷるんと揺らします。", small: true),
                StudioUi.Button("焦げを消す", () => Heat()?.ClearMarks(), "Play中に、焦げ跡を消します。", small: true));
            Body.Add(_playButtons);
        }

        public override void Refresh()
        {
            if (Prop == null || _playButtons == null)
            {
                return;
            }

            var playing = Application.isPlaying;
            _playButtons.SetEnabled(playing);
            var wobble = Wobble();
            var heat = Heat();
            _live.text = playing
                ? $"今: まわりの敵 {(wobble != null ? wobble.LastPressure : 0):0}体 / 傾き {(wobble != null ? wobble.CurrentTilt.magnitude : 0):0.0}° / 熱の点 {(heat != null ? heat.Buffer.Count : 0)}個"
                : "Playを押すと、ここで反応を試せます。";
        }

        private void HeatTest()
        {
            var heat = Heat();
            if (heat == null || !Prop.TryGetComponent<StagePropRenderer>(out var renderer))
            {
                return;
            }

            var bounds = renderer.ComputeBounds();
            var point = new Vector3(bounds.center.x, bounds.min.y, -StageLookProfile.Current.heatHeightOffset);
            for (var i = 0; i < 60; i++)
            {
                heat.ReceiveLaser(point, 0.3f, 1f / 30f);
            }
        }

        private StagePropWobble Wobble() => Prop != null && Prop.TryGetComponent<StagePropWobble>(out var w) ? w : null;

        private StagePropHeat Heat() => Prop != null && Prop.TryGetComponent<StagePropHeat>(out var h) ? h : null;
    }
}
