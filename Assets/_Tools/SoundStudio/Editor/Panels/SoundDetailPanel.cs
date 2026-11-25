using System.Linq;
using DDrive.Foundation.Identity;
using DDrive.Runtime.Audio;
using MS2026.Fortress;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.SoundStudio
{
    /// <summary>
    /// 行を選んだときに下に出る細かい設定: 音そのもの（その音を使う所すべてに効く）・この場所だけ・同じ音を使う所・ボタン。
    /// </summary>
    public sealed class SoundDetailPanel : VisualElement
    {
        private static readonly string[] AudienceChoices = { "おまかせ（その人のPCだけ）", "全員のPC", "その人のPCだけ" };

        private readonly SoundStudioContext _context;
        private SoundSlot _slot;
        private int _shownVersion = -1;

        public SoundDetailPanel(SoundStudioContext context)
        {
            _context = context;
            AddToClassList("ss-detail");
        }

        public void Show(SoundSlot slot)
        {
            _slot = slot;
            _shownVersion = -1;
            Refresh();
        }

        public void Refresh()
        {
            if (_shownVersion == _context.Version)
            {
                return;
            }

            _shownVersion = _context.Version;
            Clear();
            EnableInClassList("ss-hidden", _slot == null);
            if (_slot == null)
            {
                return;
            }

            // 行の名前は真上の行に出ているので、ここでは繰り返さない（どこの欄かだけ小さく）。
            Add(StudioUi.Styled(new Label(Where(_slot)), "ss-detail-where"));

            if (_slot.Kind == SoundSlotKind.Candidate)
            {
                Add(StudioUi.Note("まだゲームに欄がないタイミングです（候補 " + _slot.Candidate.Number + "）。コードに欄を足すと、ここで音を入れられます。", NoteKind.Info));
                return;
            }

            // 「音そのもの」と「この場所だけ」は横に並べる（縦に長くならないように）。
            var columns = new VisualElement();
            columns.AddToClassList("ss-detail-columns");
            Add(columns);
            AddSoundItself(columns);
            if (_slot.Kind == SoundSlotKind.Effect && _slot.Value is FortressEffect effect)
            {
                AddPlace(columns, effect);
            }

            AddUsedBy();
            AddButtons();
        }

        /// <summary>欄から音を外す。</summary>
        public static void Clear(SoundStudioContext context, SoundSlot slot)
        {
            var result = slot.Kind == SoundSlotKind.Bgm
                ? SoundSlotWriter.SetBgm(slot, AssetId<BgmMarker>.Invalid)
                : SoundSlotWriter.SetSe(slot, AssetId<SeMarker>.Invalid);
            context.Report(result);
        }

        private static string Where(SoundSlot slot)
        {
            var page = SoundPageIds.Label(slot.Page) + " › " + slot.Group;
            return string.IsNullOrEmpty(slot.OwnerName) ? page : page + "　（設定: " + slot.OwnerName + "）";
        }

        private void AddSoundItself(VisualElement parent)
        {
            var id = SoundStudioContext.SoundIdOf(_slot);
            var card = DetailCard("音そのもの", "この音を使う所すべてに効く。");
            parent.Add(card);
            if (_slot.Kind == SoundSlotKind.Bgm)
            {
                var bgm = _context.Library.Bgm(id);
                if (bgm == null)
                {
                    card.Add(StudioUi.Note("BGM が入っていません。", NoteKind.Warn));
                    return;
                }

                card.Add(StudioUi.Styled(new Label(SoundLibrary.NameOf(bgm)), "ss-detail-sound"));
                card.Add(DataSlider("音量", "この BGM の音量（0〜1）。", bgm, bgm.Volume, v => bgm.Volume = v));
                return;
            }

            var se = _context.Library.Se(id);
            if (se == null)
            {
                card.Add(StudioUi.Note("音が入っていません。［別の音を選ぶ］で選ぶか、wav を行に落としてください。", NoteKind.Warn));
                return;
            }

            card.Add(StudioUi.Styled(new Label(SoundLibrary.NameOf(se)), "ss-detail-sound"));
            card.Add(DataSlider("音量", "この音の音量（0〜1）。この音を使う所すべてに効く。", se, se.Volume, v => se.Volume = v));

            var pitch = new MinMaxSlider("高さのばらつき", se.PitchRange.x, se.PitchRange.y, 0.5f, 2f)
            {
                tooltip = "鳴らすたびに、この範囲で音の高さを少し変える（1 = そのまま）。同じ音の連打が機械的に聞こえるときに広げる。",
            };
            var pitchValue = StudioUi.Styled(new Label(PitchText(se.PitchRange)), "ss-detail-pitch");
            pitchValue.tooltip = pitch.tooltip;
            pitch.RegisterValueChangedCallback(e =>
            {
                EditData(se, () => se.PitchRange = e.newValue);
                pitchValue.text = PitchText(e.newValue);
            });
            pitch.RegisterCallback<PointerCaptureOutEvent>(_ => AssetDatabase.SaveAssetIfDirty(se));
            pitch.style.flexGrow = 1;
            var pitchRow = new VisualElement();
            pitchRow.AddToClassList("ss-detail-pitch-row");
            pitchRow.Add(pitch);
            pitchRow.Add(pitchValue);
            card.Add(pitchRow);

            var cooldown = new FloatField("連打の間引き（秒）") { value = se.CooldownSec, tooltip = "この秒数の中で2回目が来たら鳴らさない。たくさん同時に起きる音（敵を倒した等）がうるさいときに長くする。" };
            cooldown.RegisterValueChangedCallback(e => EditData(se, () => se.CooldownSec = Mathf.Max(0f, e.newValue)));
            card.Add(cooldown);

            var concurrent = new IntegerField("同時に鳴る数") { value = se.MaxConcurrent, tooltip = "同じ音を同時に鳴らせる数。超えたら古いものから止まる。" };
            concurrent.RegisterValueChangedCallback(e => EditData(se, () => se.MaxConcurrent = Mathf.Max(1, e.newValue)));
            card.Add(concurrent);
        }

        private void AddPlace(VisualElement parent, FortressEffect effect)
        {
            var card = DetailCard("この場所だけ", "ほかの所で同じ音を使っていても、ここだけに効く。");
            parent.Add(card);

            var scale = new Slider("音量の倍率", 0f, 1f) { value = effect.volumeScale, showInputField = true, tooltip = "この場所だけの音量（0〜1）。大きくしたいときは、上の「音そのもの」の音量を上げる。" };
            scale.RegisterCallback<PointerCaptureOutEvent>(_ => _context.Report(SoundSlotWriter.SetVolumeScale(_slot, scale.value)));
            scale.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (!Mathf.Approximately(scale.value, effect.volumeScale))
                {
                    _context.Report(SoundSlotWriter.SetVolumeScale(_slot, scale.value));
                }
            });
            card.Add(scale);

            if (_slot.Personal)
            {
                var audience = new DropdownField("鳴らすPC", AudienceChoices.ToList(), (int)effect.audience)
                {
                    tooltip = "その人の事の音を、どのPCで鳴らすか。会場では4台が並ぶので、全員のPCにすると同じ音が4台から重なって聞こえる。CPU の音は Host のPCで鳴る。",
                };
                audience.RegisterValueChangedCallback(_ => _context.Report(SoundSlotWriter.SetAudience(_slot, (SoundAudience)audience.index)));
                card.Add(audience);
            }
            else
            {
                card.Add(StudioUi.Styled(new Label("鳴らすPC: 全員のPC（みんなに起きる事）"), "ss-detail-fixed"));
            }
        }

        private void AddUsedBy()
        {
            var id = SoundStudioContext.SoundIdOf(_slot);
            var others = _context.SlotsUsing(id, _slot.Kind).Where(s => s != _slot).ToList();
            if (others.Count == 0)
            {
                return;
            }

            var card = DetailCard("同じ音を使っている所（" + others.Count + "）", "押すとその行へ飛ぶ。");
            foreach (var other in others.Take(12))
            {
                var link = StudioUi.Button(SoundPageIds.Label(other.Page) + " › " + other.Label, () => _context.GoTo(other), "この行へ飛ぶ。", small: true);
                link.AddToClassList("ss-detail-link");
                card.Add(link);
            }

            Add(card);
        }

        private void AddButtons()
        {
            var id = SoundStudioContext.SoundIdOf(_slot);
            // よく使う操作だけ大きく、作る人向けの操作は小さく右へ。止めるは行の ■ と上の帯にある。
            var row = StudioUi.Row(
                StudioUi.Button("別の音を選ぶ", () => SePicker.Open(_context, _slot), "入れる音を一覧から選ぶ。", primary: true),
                StudioUi.Button("音を外す", () => Clear(_context, _slot), "この欄を空にする（音のファイルは消さない）。"),
                StudioUi.Spacer(),
                StudioUi.Button("D-Drive で開く", () => OpenData(id), "音のデータを Inspector で開く（D-Drive の専用エディタへ進める）。", small: true),
                StudioUi.Button("設定のある所を見せる", () => Reveal(), "この欄を持っている設定ファイル・シーンを Project で見せる（作る人向け）。", small: true));
            row.AddToClassList("ss-detail-buttons");
            Add(row);
        }

        // 細かい設定の中の箱（枠は1重だけ。外の箱は行とつながった背景だけ）。
        private static VisualElement DetailCard(string title, string body)
        {
            var card = new VisualElement();
            card.AddToClassList("ss-detail-card");
            card.Add(StudioUi.Styled(new Label(title), "ss-detail-card-title"));
            card.Add(StudioUi.Styled(new Label(body), "ss-detail-card-body"));
            return card;
        }

        private VisualElement DataSlider(string label, string tooltip, Object data, float value, System.Action<float> set)
        {
            var slider = new Slider(label, 0f, 1f) { value = value, showInputField = true, tooltip = tooltip };
            slider.RegisterValueChangedCallback(e => EditData(data, () => set(e.newValue)));
            slider.RegisterCallback<PointerCaptureOutEvent>(_ => AssetDatabase.SaveAssetIfDirty(data));
            return slider;
        }

        private static string PitchText(Vector2 range)
        {
            return Mathf.Approximately(range.x, range.y) ? range.x.ToString("0.00") + "（そのまま）" : range.x.ToString("0.00") + " 〜 " + range.y.ToString("0.00");
        }

        private static void EditData(Object data, System.Action change)
        {
            Undo.RecordObject(data, "サウンドスタジオ: 音そのもの");
            change();
            EditorUtility.SetDirty(data);
        }

        private void OpenData(ulong id)
        {
            Object data = _slot.Kind == SoundSlotKind.Bgm ? (Object)_context.Library.Bgm(id) : _context.Library.Se(id);
            if (data == null)
            {
                _context.Notify("音が入っていません。");
                return;
            }

            Selection.activeObject = data;
            AssetDatabase.OpenAsset(data);
        }

        private void Reveal()
        {
            var target = _slot.Owner != null ? _slot.Owner : AssetDatabase.LoadMainAssetAtPath(_slot.AssetPath);
            if (target == null)
            {
                _context.Notify("設定が見つかりませんでした。");
                return;
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }
    }
}
