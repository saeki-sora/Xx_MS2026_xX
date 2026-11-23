using MS2026.Fortress;
using MS2026.StudioKit;
using UnityEngine.UIElements;

namespace MS2026.SoundStudio
{
    /// <summary>
    /// 1行: 印・名前（その人／声の小さな印）・音（無ければオレンジの「音を入れる…」）・この場所の音量のバー・▶。
    /// 候補の行は紫の点線で、名前と「コードを足すと使えます」だけ。行に wav を落とすと登録してこの欄に入れる。
    /// </summary>
    public sealed class SoundRow : VisualElement
    {
        private const long WriteDelayMs = 400;

        private readonly SoundStudioContext _context;
        private readonly SoundSlot _slot;
        private readonly Label _mark;
        private IVisualElementScheduledItem _pendingWrite;
        private float _pendingScale;
        private bool _hasPending;

        public SoundRow(SoundStudioContext context, SoundSlot slot, bool showOwner)
        {
            _context = context;
            _slot = slot;
            AddToClassList("ss-row");
            RegisterCallback<ClickEvent>(_ => _context.Select(_slot));

            _mark = StudioUi.Styled(new Label("●"), "ss-row-mark");
            _mark.tooltip = "この場所の設定（音量・鳴らすPC）を初期値から変えています。右クリックで戻せます。";
            Add(_mark);

            var name = new VisualElement();
            name.AddToClassList("ss-row-name");
            name.Add(StudioUi.Styled(new Label(slot.Label), "ss-row-label"));
            if (showOwner && !string.IsNullOrEmpty(slot.OwnerName))
            {
                name.Add(StudioUi.Styled(new Label(slot.OwnerName), "ss-row-owner"));
            }

            if (slot.Personal)
            {
                name.Add(StudioUi.Chip("その人", ChipKind.Info, "その人の事の音。鳴らすPCを選べる（初期は、その人のPCだけで鳴らす）。"));
            }

            if (slot.Voice)
            {
                name.Add(StudioUi.Chip("声", ChipKind.Accent, "声の欄。上の帯の「声」の音量が効く。"));
            }

            name.tooltip = string.IsNullOrEmpty(slot.Tooltip) ? slot.Label : slot.Tooltip;
            Add(name);

            if (slot.Kind == SoundSlotKind.Candidate)
            {
                AddToClassList("ss-row--candidate");
                Add(StudioUi.Chip(slot.Candidate.Number, ChipKind.Plain, "設計書の候補の番号。"));
                Add(StudioUi.Styled(new Label("コードを足すと使えます"), "ss-row-candidate-note"));
                _mark.AddToClassList("ss-hidden");
                return;
            }

            var soundName = _context.SoundName(slot);
            var sound = StudioUi.Button(soundName ?? "音を入れる…", () => SePicker.Open(_context, _slot),
                soundName == null ? "まだ音が入っていない。押して音を選ぶか、wav をこの行に落とす。" : "入っている音。押すと別の音を選べる。", small: true);
            sound.AddToClassList("ss-row-sound");
            if (soundName == null)
            {
                sound.AddToClassList("ss-row-sound--empty");
                AddToClassList("ss-row--missing");
            }

            Add(sound);

            if (slot.Kind == SoundSlotKind.Effect && slot.Value is FortressEffect effect)
            {
                var scale = new Slider(0f, 1f) { value = effect.volumeScale, tooltip = "この場所だけの音量（0〜1）。ここだけ小さくしたいときに下げる。大きくしたいときは、行を押して「音そのもの」の音量を上げる。" };
                scale.AddToClassList("ss-row-scale");
                scale.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                scale.RegisterValueChangedCallback(e => QueueScale(e.newValue));
                Add(scale);
            }
            else
            {
                Add(StudioUi.Styled(new VisualElement(), "ss-row-scale"));
            }

            var play = StudioUi.Button("▶", () => PlayNow(), "聞く（Play していなくても鳴る）。", small: true);
            play.AddToClassList("ss-row-play");
            Add(play);

            this.AddManipulator(new ContextualMenuManipulator(menu =>
            {
                menu.menu.AppendAction("この場所の設定を初期値に戻す", _ =>
                {
                    _context.Report(SoundSlotWriter.ResetPlace(_slot));
                }, _ => SoundSlotWriter.IsChanged(_slot) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                menu.menu.AppendAction("音を外す", _ => SoundDetailPanel.Clear(_context, _slot),
                    _ => _slot.HasSound ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }));

            WavDrop.Attach(this, _context, _slot);

            // 書く前にページが替わった・描き直した（行が外れた）ときも、動かした音量は書く。
            RegisterCallback<DetachFromPanelEvent>(_ => FlushScale());
            UpdateMark();
        }

        public void SetSelected(bool selected)
        {
            EnableInClassList("ss-row--selected", selected);
        }

        /// <summary>Play 中に鳴ったとき、少しの間光らせる。</summary>
        public void Flash()
        {
            AddToClassList("ss-row--flash");
            schedule.Execute(() => RemoveFromClassList("ss-row--flash")).StartingIn(900);
        }

        private void PlayNow()
        {
            var problem = _context.Preview.Play(_slot, _context.Library);
            if (problem != null)
            {
                _context.Notify(problem);
            }
        }

        // バーを動かしている間は書かず、止めて少し経ってから1回だけ書く（シーンの欄は書くたびにシーンを開くため）。
        private void QueueScale(float value)
        {
            _pendingScale = value;
            _hasPending = true;
            if (_pendingWrite == null)
            {
                _pendingWrite = schedule.Execute(WriteScale);
            }

            _pendingWrite.ExecuteLater(WriteDelayMs);
        }

        private void FlushScale()
        {
            if (_pendingWrite != null && _hasPending)
            {
                _pendingWrite.Pause();
                WriteScale();
            }
        }

        private void WriteScale()
        {
            if (!_hasPending)
            {
                return;
            }

            _hasPending = false;
            var result = SoundSlotWriter.SetVolumeScale(_slot, _pendingScale);
            if (result != SoundWriteResult.Saved)
            {
                _context.Report(result);
            }

            UpdateMark();
        }

        private void UpdateMark()
        {
            _mark.EnableInClassList("ss-hidden", !SoundSlotWriter.IsChanged(_slot));
        }
    }
}
