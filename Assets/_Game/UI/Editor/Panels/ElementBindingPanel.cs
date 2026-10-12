using System;
using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UGui = UnityEngine.UI;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// レイヤーページの「選んだ部品の値のつなぎ」。選んだ部品に付いている値の部品（文字・ゲージ・色・表示・押せるか）を
    /// その場で見て直し、ボタン1つで新しくつなぐ・外す（インスペクターを探さなくてよい）。
    /// 中身は各部品のインスペクター（日本語の項目名）をそのまま埋め込む。
    /// </summary>
    public sealed class ElementBindingPanel : VisualElement
    {
        private static readonly (Type type, string label, string tooltip)[] Kinds =
        {
            (typeof(UiBindText), "＋ 文字で出す", "値を文字で出します（例: 敵 {0:N0}）。この部品に文字（Text / TextMeshPro）があるときに使えます。"),
            (typeof(UiBindFill), "＋ ゲージで出す", "値の大きさでゲージ（画像の塗り）を伸び縮みさせます。"),
            (typeof(UiBindColor), "＋ 値で色を変える", "値の大きさで色を変えます（例: 熱が上がるほど赤く）。"),
            (typeof(UiBindVisible), "＋ 値で出す／隠す", "値が ON のときだけ見せる、などの切り替えをします。"),
            (typeof(UiBindInteractable), "＋ 値で押せる／押せない", "値でボタンを押せる・押せないにします（例: 全員準備OKのときだけ開始ボタン）。ボタンなど押せる部品に使えます。")
        };

        private readonly UiStudioContext _context;
        private readonly List<(UiBinding binding, Label chip)> _keyChips = new List<(UiBinding, Label)>();
        private int _signature = -1;

        public ElementBindingPanel(UiStudioContext context)
        {
            _context = context;
        }

        public void Refresh()
        {
            var element = _context.SelectedElement;
            var signature = Signature(element);
            if (signature == _signature)
            {
                RefreshKeys();
                return;
            }

            _signature = signature;
            Rebuild(element);
        }

        // 見る値の名前は、入力中に作り直すと欄から文字が打てなくなるので、作り直さずに札の文字だけ合わせる。
        public void RefreshKeys()
        {
            foreach (var (binding, chip) in _keyChips)
            {
                if (binding == null)
                {
                    continue;
                }

                var empty = string.IsNullOrEmpty(binding.key);
                chip.text = empty ? "値が未設定" : binding.key;
                StudioUi.SetChipKind(chip, empty ? ChipKind.Warn : ChipKind.Ok);
            }
        }

        private void Rebuild(RectTransform element)
        {
            Clear();
            _keyChips.Clear();
            if (element == null)
            {
                Add(StudioUi.Styled(new Label("部品を選ぶと、その部品がどのゲームの値を見ているかが出て、ここで直せます。"), "sk-muted"));
                return;
            }

            var bindings = element.GetComponents<UiBinding>();
            if (bindings.Length == 0)
            {
                Add(StudioUi.Note($"「{element.name}」はまだゲームの値とつながっていません。下のボタンでつなげます。", NoteKind.Info));
            }

            foreach (var binding in bindings)
            {
                var card = StudioUi.Card();
                var head = StudioUi.Row(StudioUi.Styled(new Label(KindLabel(binding)), "sk-card-title"));
                var keyChip = StudioUi.Chip("", ChipKind.Plain, "見ている値の名前。下の「見る値」の ▼ で選び直せます。");
                keyChip.style.marginLeft = 6;
                head.Add(keyChip);
                _keyChips.Add((binding, keyChip));
                head.Add(StudioUi.Spacer());
                var target = binding;
                head.Add(StudioUi.Button("外す", () =>
                {
                    Undo.DestroyObjectImmediate(target);
                    _signature = -1;
                }, "この値のつなぎを外します（Ctrl+Z で戻せます）。", small: true));
                card.Add(head);
                card.Add(new InspectorElement(binding));
                Add(card);
            }

            var buttons = StudioUi.Row();
            foreach (var (type, label, tooltip) in Kinds)
            {
                var button = StudioUi.Button(label, () => AddBinding(element, type), tooltip, small: true);
                button.SetEnabled(CanAdd(element, type));
                buttons.Add(button);
            }

            Add(buttons);
            RefreshKeys();
        }

        private void AddBinding(RectTransform element, Type type)
        {
            if (element == null)
            {
                return;
            }

            Undo.AddComponent(element.gameObject, type);
            _signature = -1;
        }

        private static bool CanAdd(RectTransform element, Type type)
        {
            if (type == typeof(UiBindText))
            {
                return element.GetComponent<UGui.Text>() != null || element.GetComponent<TMPro.TMP_Text>() != null;
            }

            if (type == typeof(UiBindInteractable))
            {
                return element.GetComponent<UGui.Selectable>() != null && element.GetComponent<UiBindInteractable>() == null;
            }

            if (type == typeof(UiBindVisible))
            {
                return element.GetComponent<UiBindVisible>() == null;
            }

            return true;
        }

        private static string KindLabel(UiBinding binding) => binding switch
        {
            UiBindText _ => "値を文字で出す",
            UiBindFill _ => "値をゲージで出す",
            UiBindColor _ => "値で色を変える",
            UiBindVisible _ => "値で出す／隠す",
            UiBindInteractable _ => "値で押せる／押せない",
            _ => ObjectNames.NicifyVariableName(binding.GetType().Name)
        };

        private static int Signature(RectTransform element)
        {
            if (element == null)
            {
                return 0;
            }

            unchecked
            {
                var hash = element.GetInstanceID();
                var list = new List<UiBinding>();
                element.GetComponents(list);
                foreach (var binding in list)
                {
                    hash = hash * 31 + binding.GetInstanceID();
                }

                // 足せるボタンの有効・無効は、文字・押せる部品の有無で変わる。
                hash = hash * 31 + (element.GetComponent<UGui.Text>() != null || element.GetComponent<TMPro.TMP_Text>() != null ? 1 : 0);
                hash = hash * 31 + (element.GetComponent<UGui.Selectable>() != null ? 2 : 0);

                return hash;
            }
        }
    }
}
