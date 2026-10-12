using System;
using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using MS2026.StudioKit;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 配置ページの右の欄: 選んでいる背景の位置・向き・傾き・大きさ・床からの高さ。
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
        private readonly FloatField _tiltX;
        private readonly FloatField _tiltY;
        private readonly Label _tiltNote;
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

            _body.Add(Group("傾き（°）", "見た目のモデルだけを X 軸・Y 軸のまわりに倒します（根元の向きが基準）。床に置いた物は床に置いたまま斜めに立ち、敵が通れない範囲は倒れた形に合わせて作り直されます。"));
            _tiltX = Field("傾き X", "X 軸のまわりに倒す角度（度）。プラスで +Y 側（向き0°のとき床の上側）へ倒れます。名前を左右にドラッグで動かせます。",
                (o, n) => StageSelectionAdjust.Tilt(_context.SelectedProps, new Vector2(Mathf.DeltaAngle(o, n), 0f)));
            _tiltY = Field("傾き Y", "Y 軸のまわりに倒す角度（度）。プラスで −X 側（向き0°のとき左）へ倒れます。名前を左右にドラッグで動かせます。",
                (o, n) => StageSelectionAdjust.Tilt(_context.SelectedProps, new Vector2(0f, Mathf.DeltaAngle(o, n))));
            _body.Add(StudioUi.Row(_tiltX, _tiltY));
            _body.Add(StudioUi.Row(
                Small("X−5°", "X 軸のまわりに −5° 倒します。", () => StageSelectionAdjust.Tilt(_context.SelectedProps, new Vector2(-5f, 0f))),
                Small("X+5°", "X 軸のまわりに +5° 倒します。", () => StageSelectionAdjust.Tilt(_context.SelectedProps, new Vector2(5f, 0f))),
                Small("Y−5°", "Y 軸のまわりに −5° 倒します。", () => StageSelectionAdjust.Tilt(_context.SelectedProps, new Vector2(0f, -5f))),
                Small("Y+5°", "Y 軸のまわりに +5° 倒します。", () => StageSelectionAdjust.Tilt(_context.SelectedProps, new Vector2(0f, 5f))),
                Small("まっすぐ", "傾きを 0° に戻して、まっすぐ立てます。", () => StageSelectionAdjust.SetTilt(_context.SelectedProps, Vector2.zero))));
            _body.Add(Hint("ゲーム画面では、てっぺんのピンクの丸をつかんで倒したい方へ引きます（5°ごと。Ctrl で自由に）。Ctrl+矢印キーで画面の向きに5°ずつ倒れます。"));
            _tiltNote = Hint("通れない範囲を手で直しているので、傾けても形は変わりません（オブジェクトページの「作り直す」で今の形に合わせられます）。");
            _body.Add(_tiltNote);

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

            _body.Add(Group("4人ぶん同じ形に置く", "4人はコアを囲んで遊ぶので、片側に置いた背景を、コアを中心に回した位置にも置くと、どの席からも同じ条件になります。"));
            _body.Add(StudioUi.Row(
                Small("向かい側にも置く", "選んでいる背景のコピーを、コアを中心に 180° 回した所（向かい側）に置きます。",
                    () => UnityEditor.EditorApplication.delayCall += () => StageSymmetryOps.PlaceRotatedCopies(_context.SelectedProps, 2)),
                Small("4方向に置く", "選んでいる背景のコピーを、コアを中心に 90°・180°・270° 回した所に置きます（全部で4つ）。",
                    () => UnityEditor.EditorApplication.delayCall += () => StageSymmetryOps.PlaceRotatedCopies(_context.SelectedProps, 4))));
            _body.Add(Hint("コアが無いときは原点が中心です。向きも一緒に回るので、どの席から見ても同じ形になります。Ctrl+Z で全部戻せます。"));

            _body.Add(Group("見た目のずれ", "遠近のあるカメラでは、背の高い物ほど床に置いた位置より外側にずれて映ります。てっぺんが床の位置から何mずれて見えるか。"));
            _shifts = new VisualElement();
            _body.Add(_shifts);

            _body.Add(Group("砲台・コア・湧き位置との距離", "通れない範囲（無ければ見た目の床の四角）から測った距離。赤は「近すぎ」の基準より近い。"));
            _distances = new VisualElement();
            _body.Add(_distances);

            _body.Add(Group("複製・消す", "選んでいる背景をコピーする／シーンから消す。ゲーム画面をクリックしてから Ctrl+D（複製）・Delete（消す）でも同じです。どちらも Ctrl+Z で戻せます。"));
            _body.Add(StudioUi.Row(
                Small("複製", "選んでいる背景をコピーして、少し右下にずらして置きます（役割・通れない範囲・見た目の反応もそのまま）。Ctrl+D と同じ。",
                    () => UnityEditor.EditorApplication.delayCall += () => StagePropDuplicateOps.Duplicate(_context.SelectedProps)),
                Small("消す", "選んでいる背景をシーンから消します（確認が出ます。Ctrl+Z で戻せます）。Delete キーと同じ。",
                    () => UnityEditor.EditorApplication.delayCall += () => StagePropDuplicateOps.Delete(_context.SelectedProps))));

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
            var tilt = StagePropTransformOps.Tilt(prop);
            Show(_tiltX, tilt.x);
            Show(_tiltY, tilt.y);
            _tiltNote.style.display = prop.footprint != null && prop.footprint.manuallyEdited && tilt.sqrMagnitude > 1e-4f ? DisplayStyle.Flex : DisplayStyle.None;
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
