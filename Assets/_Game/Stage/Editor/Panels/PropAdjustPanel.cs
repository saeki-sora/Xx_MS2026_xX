using System;
using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using MS2026.StudioKit;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 配置ページの右の欄: 選んでいる背景の位置・向き・大きさ・床からの高さ。
    /// 数字は打たなくてよい: 欄の名前（「向き」など）を左右にドラッグすると値が動き、ボタンで決まった量だけ動かせる。
    /// 複数選んでいるときは、変えた分を全部に同じだけ掛ける。下に、各視点での見た目のずれと、砲台・コアとの距離。
    /// </summary>
    public sealed class PropAdjustPanel : VisualElement
    {
        private readonly StageStudioContext _context;
        private readonly Action _openDetails;
        private readonly VisualElement _empty;
        private readonly VisualElement _body;
        private readonly Label _title;
        private readonly Label _role;
        private readonly FloatField _x;
        private readonly FloatField _y;
        private readonly FloatField _yaw;
        private readonly FloatField _width;
        private readonly FloatField _depth;
        private readonly FloatField _height;
        private readonly FloatField _lift;
        private readonly Label _underNote;
        private readonly VisualElement _shifts;
        private readonly VisualElement _distances;
        private string _infoKey;

        public PropAdjustPanel(StageStudioContext context, Action openDetails)
        {
            _context = context;
            _openDetails = openDetails;

            _empty = StudioUi.Note("ゲーム画面の背景をクリックすると、ここで位置・向き・大きさ・高さを調整できます。", NoteKind.Info);
            Add(_empty);

            _body = new VisualElement();
            Add(_body);

            var head = StudioUi.Row();
            _title = StudioUi.Styled(new Label(), "sk-card-title");
            _title.style.flexShrink = 1;
            _role = StudioUi.Chip("", ChipKind.Plain);
            _role.style.marginLeft = 6;
            head.Add(_title);
            head.Add(_role);
            _body.Add(head);
            _body.Add(Hint("欄の名前を左右にドラッグすると値が動きます。"));

            _body.Add(Group("位置（m）", "床の上の位置。ゲーム画面でつかんで動かすのが一番早いです。矢印キーでも少しずつ動きます。"));
            _x = Field("横 X", "床の上の横の位置（m）。名前を左右にドラッグで動かせます。", (o, n) => StageSelectionAdjust.Nudge(_context.SelectedProps, new Vector2(n - o, 0f)));
            _y = Field("縦 Y", "床の上の縦の位置（m）。名前を左右にドラッグで動かせます。", (o, n) => StageSelectionAdjust.Nudge(_context.SelectedProps, new Vector2(0f, n - o)));
            _body.Add(StudioUi.Row(_x, _y));

            _body.Add(Group("向き（°）", "床の上での向き。ゲーム画面ではホイール、Q / E キーでも回せます。"));
            _yaw = Field("向き", "向き（度）。名前を左右にドラッグで回せます。", (o, n) => StageSelectionAdjust.Rotate(_context.SelectedProps, Mathf.DeltaAngle(o, n)));
            _body.Add(_yaw);
            _body.Add(StudioUi.Row(
                Small("↺90°", "左に90°回します。", () => StageSelectionAdjust.Rotate(_context.SelectedProps, 90f)),
                Small("↺15°", "左に15°回します。", () => StageSelectionAdjust.Rotate(_context.SelectedProps, 15f)),
                Small("0°", "向きを0°に戻します。", () => StageSelectionAdjust.SetYaw(_context.SelectedProps, 0f)),
                Small("↻15°", "右に15°回します。", () => StageSelectionAdjust.Rotate(_context.SelectedProps, -15f)),
                Small("↻90°", "右に90°回します。", () => StageSelectionAdjust.Rotate(_context.SelectedProps, -90f))));

            _body.Add(Group("大きさ（m）", "見た目の幅・奥行き・高さ。どれを変えても形はそのまま全体が大きく／小さくなります（足元が中心）。ゲーム画面では Shift+ホイール、- / + キー。"));
            _width = Field("幅", "見た目の横幅（m）。変えると全体がその幅になるように大きさが変わります。", (o, n) => ScaleTo(o, n));
            _depth = Field("奥行", "見た目の奥行き（m）。変えると全体がその奥行きになるように大きさが変わります。", (o, n) => ScaleTo(o, n));
            _height = Field("高さ", "見た目の高さ（m）。変えると全体がその高さになるように大きさが変わります。", (o, n) => ScaleTo(o, n));
            _body.Add(_width);
            _body.Add(_depth);
            _body.Add(_height);
            _body.Add(StudioUi.Row(
                Small("−10%", "1割小さくします。", () => StageSelectionAdjust.Scale(_context.SelectedProps, 0.9f)),
                Small("−1%", "少し小さくします。", () => StageSelectionAdjust.Scale(_context.SelectedProps, 0.99f)),
                Small("+1%", "少し大きくします。", () => StageSelectionAdjust.Scale(_context.SelectedProps, 1.01f)),
                Small("+10%", "1割大きくします。", () => StageSelectionAdjust.Scale(_context.SelectedProps, 1.1f))));

            _body.Add(Group("床からの高さ（m）", "見た目のいちばん下が床からどれだけ浮いているか（マイナスは床に沈んでいる）。ゲーム画面では Alt+ホイール、PageUp / PageDown。"));
            _lift = Field("床から", "床からの高さ（m）。名前を左右にドラッグで上げ下げできます。", (o, n) => StageSelectionAdjust.Lift(_context.SelectedProps, n - o));
            _body.Add(_lift);
            _body.Add(StudioUi.Row(
                Small("▼5cm", "5cm 下げます。", () => StageSelectionAdjust.Lift(_context.SelectedProps, -0.05f)),
                Small("床に下ろす", "見た目のいちばん下を床（高さ0）にそろえます。", () => StageSelectionAdjust.SetLift(_context.SelectedProps, 0f)),
                Small("▲5cm", "5cm 上げます。", () => StageSelectionAdjust.Lift(_context.SelectedProps, 0.05f))));
            _underNote = Hint("床から高いので、敵は下をくぐれます。");
            _body.Add(_underNote);

            _body.Add(Group("見た目のずれ", "遠近のあるカメラでは、背の高い物ほど床に置いた位置より外側にずれて映ります。てっぺんが床の位置から何mずれて見えるか。"));
            _shifts = new VisualElement();
            _body.Add(_shifts);

            _body.Add(Group("砲台・コア・湧き位置との距離", "通れない範囲（無ければ見た目の床の四角）から測った距離。赤は「近すぎ」の基準より近い。"));
            _distances = new VisualElement();
            _body.Add(_distances);

            _body.Add(StudioUi.Row(
                StudioUi.Button("シーンで見る", () => StageStudioContext.Frame(_context.Primary), "選んでいる背景をシーンビューの真ん中に映します（ゲーム画面で F キーでも）。", small: true),
                StudioUi.Button("役割・反応を変える", _openDetails, "オブジェクトページで、役割・通れない範囲・見た目の反応・モデルを変えます。", small: true)));
        }

        public void Refresh()
        {
            var prop = _context.Primary;
            _empty.style.display = prop == null ? DisplayStyle.Flex : DisplayStyle.None;
            _body.style.display = prop == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (prop == null)
            {
                return;
            }

            var count = _context.SelectedProps.Count;
            _title.text = count > 1 ? $"{prop.name} ほか{count - 1}個" : prop.name;
            _role.text = $"{StageRoleStyle.Icon(prop.role)} {prop.role.DisplayName()}";
            _role.style.color = StageRoleStyle.Color(prop.role);

            var p = prop.transform.position;
            var size = StagePropTransformOps.Size(prop);
            var lift = StagePropTransformOps.Lift(prop);
            Show(_x, p.x);
            Show(_y, p.y);
            Show(_yaw, Mathf.Repeat(StagePropTransformOps.Yaw(prop), 360f));
            Show(_width, size.x);
            Show(_depth, size.y);
            Show(_height, size.z);
            Show(_lift, lift);
            _underNote.style.display = prop.footprint != null && lift > prop.footprint.settings.blockHeight ? DisplayStyle.Flex : DisplayStyle.None;

            RefreshInfo(prop);
        }

        // ずれと距離は、値が変わったときだけ作り直す（マウスを乗せている説明がちらつかないように）。
        private void RefreshInfo(StageProp prop)
        {
            var shifts = new List<(int viewer, float shift)>();
            foreach (var viewer in ViewerIndex.All)
            {
                if (StageGameCamera.TryGetView(viewer, out var view) && StageGameCamera.HasParallax(view))
                {
                    shifts.Add((viewer, StageParallaxGuide.TopShift(prop, viewer)));
                }
            }

            var readings = StageDistanceGuide.Measure(prop);
            var key = prop.GetInstanceID() + "|" + string.Join(",", shifts.ConvertAll(s => $"{s.viewer}:{s.shift:0.0}")) + "|" +
                      string.Join(",", readings.ConvertAll(r => r.Text));
            if (key == _infoKey)
            {
                return;
            }

            _infoKey = key;
            _shifts.Clear();
            if (shifts.Count == 0)
            {
                _shifts.Add(Hint("どの視点も真上から遠近なしで見るので、ずれません。"));
            }
            else
            {
                var row = StudioUi.Row();
                foreach (var (viewer, shift) in shifts)
                {
                    var chip = StudioUi.Chip(shift < 0.05f ? $"{ViewerIndex.Label(viewer)} ほぼ無し" : $"{ViewerIndex.Label(viewer)} {shift:0.0}m",
                        ChipKind.Plain, $"{ViewerIndex.LongLabel(viewer)}の画面で、てっぺんが床の位置からずれて見える量。");
                    chip.style.color = ViewerIndex.Color(viewer);
                    chip.style.marginBottom = 4;
                    row.Add(chip);
                }

                _shifts.Add(row);
            }

            _distances.Clear();
            if (readings.Count == 0)
            {
                _distances.Add(Hint($"{StagePlaceSettings.DistanceRange:0}m 以内に砲台・コア・湧き位置はありません。"));
            }

            for (var i = 0; i < readings.Count && i < 5; i++)
            {
                var label = new Label(readings[i].Text);
                label.style.color = readings[i].TooClose ? StageDistanceGuide.NearColor : new Color(0.85f, 0.86f, 0.88f);
                label.style.marginBottom = 2;
                _distances.Add(label);
            }
        }

        private void ScaleTo(float oldValue, float newValue)
        {
            if (oldValue > 1e-3f && newValue > 1e-3f)
            {
                StageSelectionAdjust.Scale(_context.SelectedProps, newValue / oldValue);
            }
        }

        private static void Show(FloatField field, float value)
        {
            // 入力中・ドラッグ中の欄は上書きしない。
            if (field.focusController != null && field.focusController.focusedElement == field)
            {
                return;
            }

            field.SetValueWithoutNotify((float)Math.Round(value, 3));
        }

        private FloatField Field(string label, string tooltip, Action<float, float> onChange)
        {
            var field = new FloatField(label) { tooltip = tooltip, formatString = "0.00" };
            field.labelElement.style.minWidth = 46;
            field.labelElement.style.width = 46;
            field.style.flexGrow = 1;
            field.style.minWidth = 110;
            field.RegisterValueChangedCallback(e =>
            {
                if (_context.SelectedProps.Count > 0 && !Mathf.Approximately(e.previousValue, e.newValue))
                {
                    onChange(e.previousValue, e.newValue);
                }
            });
            return field;
        }

        private static VisualElement Group(string title, string tooltip)
        {
            var label = new Label(title) { tooltip = tooltip };
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 10;
            label.style.marginBottom = 2;
            label.style.color = new Color(0.62f, 0.64f, 0.68f);
            return label;
        }

        private static Label Hint(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = 10;
            label.style.color = new Color(0.62f, 0.64f, 0.68f);
            label.style.marginBottom = 2;
            return label;
        }

        private static Button Small(string text, string tooltip, Action onClick) => StudioUi.Button(text, onClick, tooltip, small: true);
    }
}
