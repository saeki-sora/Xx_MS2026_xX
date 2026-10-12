using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// 値: ゲームから画面へ渡す値の一覧（分類ごと）。サンプル値を動かすと、Playしなくても画面の文字・ゲージ・色が変わる。
    /// Play中は今の値が並ぶ。どの画面のどの部品が使っているかも出す。
    /// </summary>
    public sealed class UiValuesPage : UiPageBase
    {
        private readonly List<(string key, Label live)> _liveLabels = new List<(string, Label)>();
        private VisualElement _list;
        private int _signature;
        private TextField _search;
        private Toggle _usedOnly;
        private Label _count;

        public UiValuesPage(UiStudioContext context) : base(context)
        {
        }

        public override string Icon => "≡";
        public override string Label => "値";
        public override string Tooltip => "ゲームから画面へ渡す値（熱・コアのHP…）の一覧と、見た目確認用のサンプル値。";

        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("値"));
            root.Add(StudioUi.Lead("ゲームが画面に渡す値の一覧です。部品に「値をゲージで出す」などを付けて、ここの名前を選ぶとつながります。" +
                                   "上の帯の「サンプル値で表示」がONなら、サンプル値を動かすだけで、Playしなくても画面の見え方を確かめられます。"));
            root.Add(StudioUi.Row(
                StudioUi.Button("ゲームの値を一覧に登録", Register, "熱・オーバーヒート・コアのHP・敵の数・通信の状態など、ゲームが書き込む値を一覧に足します（無い物だけ）。", primary: true, small: true),
                StudioUi.Button("＋ 値を足す", AddValue, "自分で値を足します（ゲームのプログラムが同じ名前で書き込むと表示されます）。", small: true)));
            root.Add(BuildFilters());
            _list = new VisualElement { style = { marginTop = 8 } };
            root.Add(_list);
            return root;
        }

        public override void Refresh()
        {
            var catalog = UiStudioSetup.ValueCatalog;
            var signature = catalog.items.Count * 31 + Context.Screens.Count;
            if (signature != _signature)
            {
                _signature = signature;
                Rebuild(catalog);
            }

            if (Application.isPlaying)
            {
                foreach (var (key, live) in _liveLabels)
                {
                    live.text = UiValues.TryGet(key, out var value) ? $"今: {value.Format(null)}" : "今: （まだ無い）";
                }
            }
        }

        // 値はたくさんある（4人分の player.N.* など）ので、名前・表示名・説明・分類で絞り込めるようにする。
        private VisualElement BuildFilters()
        {
            _search = new TextField { tooltip = "値の名前・表示名・説明・分類の一部を入れると、合う物だけ出します（例: heat、コア、lobby）。" };
            _search.AddToClassList("sk-search");
            _search.style.flexGrow = 1;
            _search.style.minWidth = 160;
            _search.RegisterValueChangedCallback(_ => _signature = 0);
            _usedOnly = new Toggle("使っている値だけ") { tooltip = "ONなら、画面の部品がどこかで使っている値だけ出します。" };
            _usedOnly.RegisterValueChangedCallback(_ => _signature = 0);
            _usedOnly.labelElement.style.minWidth = StyleKeyword.Auto;
            _usedOnly.style.marginLeft = 8;
            _count = StudioUi.Styled(new Label(), "sk-muted");
            _count.style.marginLeft = 8;
            var row = StudioUi.Row(new Label("絞り込み") { style = { marginRight = 6 } }, _search, _usedOnly, _count);
            row.style.marginTop = 8;
            return row;
        }

        private bool Matches(UiValueCatalog.Item item, Dictionary<string, int> usage)
        {
            if (_usedOnly != null && _usedOnly.value && (!usage.TryGetValue(item.key ?? "", out var used) || used == 0))
            {
                return false;
            }

            var text = _search?.value?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            foreach (var field in new[] { item.key, item.label, item.description, item.category })
            {
                if (!string.IsNullOrEmpty(field) && field.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void Rebuild(UiValueCatalog catalog)
        {
            _list.Clear();
            _list.Unbind();
            _liveLabels.Clear();
            var usage = CountUsage();
            var so = new SerializedObject(catalog);
            var items = so.FindProperty("items");

            var categories = new List<string>();
            var shown = 0;
            foreach (var item in catalog.items)
            {
                if (item != null && Matches(item, usage))
                {
                    shown++;
                }
            }

            if (_count != null)
            {
                _count.text = $"{shown} / {catalog.items.Count} 個";
            }

            foreach (var item in catalog.items)
            {
                if (item != null && Matches(item, usage) && !categories.Contains(item.category))
                {
                    categories.Add(item.category);
                }
            }

            foreach (var category in categories)
            {
                _list.Add(StudioUi.Section(string.IsNullOrEmpty(category) ? "その他" : category));
                for (var i = 0; i < catalog.items.Count; i++)
                {
                    var item = catalog.items[i];
                    if (item != null && item.category == category && Matches(item, usage))
                    {
                        _list.Add(BuildItem(item, items.GetArrayElementAtIndex(i), usage));
                    }
                }
            }

            if (catalog.items.Count > 0 && shown == 0)
            {
                _list.Add(StudioUi.Note("絞り込みに合う値がありません。言葉を変えるか、「使っている値だけ」を OFF にしてください。", NoteKind.Info));
            }

            if (catalog.items.Count == 0)
            {
                _list.Add(StudioUi.Empty("≡", "まだ値がありません", "「ゲームの値を一覧に登録」を押すと、ゲームが書き込む値がまとめて入ります。"));
            }

            _list.Bind(so);
        }

        private VisualElement BuildItem(UiValueCatalog.Item item, SerializedProperty property, Dictionary<string, int> usage)
        {
            var card = StudioUi.Card();
            var head = StudioUi.Row();
            var title = StudioUi.Styled(new Label(string.IsNullOrEmpty(item.label) ? item.key : item.label), "sk-card-title");
            title.style.flexGrow = 1;
            head.Add(title);
            var key = StudioUi.Chip(item.key, ChipKind.Plain, "値の名前。クリックでコピーします。");
            key.RegisterCallback<ClickEvent>(_ => EditorGUIUtility.systemCopyBuffer = item.key);
            head.Add(key);
            usage.TryGetValue(item.key, out var used);
            head.Add(StudioUi.Chip(used > 0 ? $"{used}か所で使用" : "未使用", used > 0 ? ChipKind.Ok : ChipKind.Plain, "この値を見ている部品の数（画面の一覧の画面の中）。"));
            card.Add(head);
            if (!string.IsNullOrEmpty(item.description))
            {
                card.Add(StudioUi.Styled(new Label(item.description), "sk-card-body"));
            }

            card.Add(SampleField(item, property));
            var live = StudioUi.Styled(new Label(), "sk-muted");
            _liveLabels.Add((item.key, live));
            card.Add(live);

            var details = new Foldout { text = "名前・種類・説明を変える", value = false };
            details.Add(new PropertyField(property.FindPropertyRelative("key"), "値の名前（プログラム用）"));
            details.Add(new PropertyField(property.FindPropertyRelative("label"), "表示名"));
            details.Add(new PropertyField(property.FindPropertyRelative("description"), "説明"));
            details.Add(new PropertyField(property.FindPropertyRelative("kind"), "種類"));
            details.Add(new PropertyField(property.FindPropertyRelative("category"), "分類"));
            details.Add(new PropertyField(property.FindPropertyRelative("range"), "数の範囲（サンプル用）"));
            card.Add(details);
            return card;
        }

        private static VisualElement SampleField(UiValueCatalog.Item item, SerializedProperty property)
        {
            switch (item.kind)
            {
                case UiValueKind.Bool:
                    var toggle = new Toggle("サンプル値") { tooltip = "Playしていないときに使う仮の値。" };
                    toggle.SetValueWithoutNotify(item.sampleNumber > 0.5f);
                    toggle.RegisterValueChangedCallback(e =>
                    {
                        property.FindPropertyRelative("sampleNumber").floatValue = e.newValue ? 1f : 0f;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                    return toggle;
                case UiValueKind.Text:
                    return new PropertyField(property.FindPropertyRelative("sampleText"), "サンプル値");
                case UiValueKind.Color:
                    return new PropertyField(property.FindPropertyRelative("sampleColor"), "サンプル値");
                default:
                    var slider = new Slider("サンプル値", item.range.x, Mathf.Max(item.range.x + 0.0001f, item.range.y)) { showInputField = true, tooltip = "Playしていないときに使う仮の値。動かすと画面のゲージや文字が変わります。" };
                    slider.BindProperty(property.FindPropertyRelative("sampleNumber"));
                    return slider;
            }
        }

        private Dictionary<string, int> CountUsage()
        {
            var result = new Dictionary<string, int>();
            foreach (var screen in Context.Screens)
            {
                if (screen == null)
                {
                    continue;
                }

                foreach (var binding in screen.GetComponentsInChildren<UiBinding>(true))
                {
                    if (!string.IsNullOrEmpty(binding.key))
                    {
                        result.TryGetValue(binding.key, out var n);
                        result[binding.key] = n + 1;
                    }
                }

                foreach (var motion in screen.GetComponentsInChildren<UiElementMotion>(true))
                {
                    foreach (var entry in motion.entries)
                    {
                        if (entry != null && !string.IsNullOrEmpty(entry.valueKey))
                        {
                            result.TryGetValue(entry.valueKey, out var n);
                            result[entry.valueKey] = n + 1;
                        }
                    }
                }
            }

            return result;
        }

        private void Register()
        {
            var catalog = UiStudioSetup.ValueCatalog;
            Undo.RecordObject(catalog, "ゲームの値を登録");
            var added = UiStudioSetup.RegisterGameValues(catalog);
            AssetDatabase.SaveAssets();
            _signature = 0;
            EditorUtility.DisplayDialog("ゲームの値を登録", added > 0 ? $"{added} 個の値を足しました。" : "足す値はありませんでした（全部登録済み）。", "OK");
        }

        private void AddValue()
        {
            var catalog = UiStudioSetup.ValueCatalog;
            Undo.RecordObject(catalog, "値を足す");
            var key = "my.value";
            for (var n = 2; catalog.Find(key) != null; n++)
            {
                key = $"my.value{n}";
            }

            catalog.AddIfMissing(key, "新しい値", "（説明を書いてください）", UiValueKind.Number, "自分で足した値", Vector2.up, 0.5f);
            EditorUtility.SetDirty(catalog);
            _signature = 0;
        }
    }
}
