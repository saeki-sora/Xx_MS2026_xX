using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using Slider = UnityEngine.UIElements.Slider;
using UGui = UnityEngine.UI;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// 編集中の画面の部品を、Photoshop のレイヤーのように並べる（上ほど手前）。
    /// ◎ = 編集中だけ隠す（ゲームには影響しない）、● = ゲームでも表示するか、⊠ = シーンビューで選べなくする（ロック）。
    /// ↑↓ で手前・奥の入れ替え、グループにまとめる・解く、複製、削除、濃さ。
    /// </summary>
    public sealed class ElementLayersPanel : VisualElement
    {
        private readonly UiStudioContext _context;
        private readonly TextField _search;
        private readonly VisualElement _rows;
        private readonly VisualElement _selection;
        private readonly Slider _opacity;
        private readonly TextField _name;
        private int _signature;

        public ElementLayersPanel(UiStudioContext context)
        {
            _context = context;
            _search = new TextField { tooltip = "部品の名前で絞り込みます。" };
            _search.AddToClassList("sk-search");
            _search.RegisterValueChangedCallback(_ => _signature = 0);
            Add(_search);

            Add(StudioUi.Row(
                StudioUi.Button("＋ グループにまとめる", Group, "選んでいる部品を、新しいグループ（空の枠）の中にまとめます。まとめて動かす・隠すのに便利。", small: true),
                StudioUi.Button("グループを解く", Ungroup, "選んでいるグループの中身を外に出して、グループを消します。", small: true),
                StudioUi.Button("複製", Duplicate, "選んでいる部品をコピーします（Ctrl+D と同じ）。", small: true),
                StudioUi.Button("削除", Delete, "選んでいる部品を消します（元に戻すは Ctrl+Z）。", small: true)));

            _rows = new VisualElement { style = { marginTop = 6 } };
            Add(_rows);

            _selection = StudioUi.Card();
            _name = new TextField("名前") { tooltip = "部品の名前（ヒエラルキーに出る名前）。" };
            _name.RegisterValueChangedCallback(e => Rename(e.newValue));
            _opacity = new Slider("濃さ（初期値）", 0f, 1f) { showInputField = true, tooltip = "この部品（と中身）の濃さ。0で透明。CanvasGroup を使います。" };
            _opacity.RegisterValueChangedCallback(e => SetOpacity(e.newValue));
            _selection.Add(_name);
            _selection.Add(_opacity);
            Add(_selection);
        }

        public void Refresh()
        {
            var screen = _context.EditingScreen;
            var signature = Signature(screen);
            if (signature != _signature)
            {
                _signature = signature;
                Rebuild(screen);
            }

            var selected = _context.SelectedElement;
            _selection.style.display = selected != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (selected != null)
            {
                if (_name.panel?.focusController?.focusedElement != _name)
                {
                    _name.SetValueWithoutNotify(selected.name);
                }

                var group = selected.GetComponent<CanvasGroup>();
                _opacity.SetValueWithoutNotify(group != null ? group.alpha : 1f);
            }
        }

        private void Rebuild(UiScreen screen)
        {
            _rows.Clear();
            if (screen == null)
            {
                _rows.Add(UiPageBase.NoEditingScreen());
                return;
            }

            AddChildren(screen.transform, 0);
        }

        private void AddChildren(Transform parent, int depth)
        {
            var filter = _search.value?.Trim() ?? string.Empty;
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child is RectTransform rect && (child.hideFlags & HideFlags.DontSave) == 0)
                {
                    if (filter.Length == 0 || child.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _rows.Add(BuildRow(rect, depth));
                    }

                    AddChildren(child, depth + 1);
                }
            }
        }

        private VisualElement BuildRow(RectTransform element, int depth)
        {
            var go = element.gameObject;
            var visibility = SceneVisibilityManager.instance;
            var row = StudioUi.Styled(new VisualElement(), "sk-list-item");
            row.style.height = 28;
            row.EnableInClassList("sk-list-item--selected", Selection.Contains(go));

            row.Add(IconButton(visibility.IsHidden(go) ? "–" : "◎", "編集中だけ隠す／見せる（ゲームには影響しません）。", () => visibility.ToggleVisibility(go, true)));
            row.Add(IconButton(go.activeSelf ? "●" : "○", "ゲームでも表示するか（OFFにすると、この部品はゲームで出ません）。", () =>
            {
                Undo.RecordObject(go, "表示を切り替え");
                go.SetActive(!go.activeSelf);
            }));
            row.Add(IconButton(visibility.IsPickingDisabled(go) ? "⊠" : "□", "シーンビューでクリックしても選ばれないようにする（ロック）。", () => visibility.TogglePicking(go, true)));

            var indent = new VisualElement { style = { width = 12 * depth } };
            row.Add(indent);
            var type = StudioUi.Styled(new Label(TypeIcon(go)), "sk-list-meta");
            type.style.width = 18;
            type.tooltip = TypeName(go);
            row.Add(type);

            var label = StudioUi.Styled(new Label(go.name), "sk-list-label");
            if (!go.activeInHierarchy)
            {
                label.style.opacity = 0.45f;
            }

            row.Add(label);
            if (go.GetComponent<UiBinding>() != null)
            {
                row.Add(StudioUi.Chip("値", ChipKind.Ok, "ゲームの値につながっています。"));
            }

            if (go.GetComponent<UiElementMotion>() != null)
            {
                row.Add(StudioUi.Chip("動", ChipKind.Info, "動きが付いています。"));
            }

            row.Add(IconButton("↑", "1つ手前へ。", () => Move(element, +1)));
            row.Add(IconButton("↓", "1つ奥へ。", () => Move(element, -1)));

            row.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target is Button)
                {
                    return;
                }

                if (e.ctrlKey || e.commandKey || e.shiftKey)
                {
                    var objects = new List<Object>(Selection.objects) { go };
                    Selection.objects = objects.ToArray();
                }
                else
                {
                    Selection.activeGameObject = go;
                }

                if (e.clickCount == 2 && SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.FrameSelected();
                }

                _signature = 0;
            });
            return row;
        }

        private Button IconButton(string text, string tooltip, System.Action action)
        {
            var button = new Button(() =>
            {
                action();
                _signature = 0;
            }) { text = text, tooltip = tooltip };
            button.style.width = 22;
            button.style.height = 20;
            button.style.marginLeft = 0;
            button.style.marginRight = 1;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            button.style.backgroundColor = Color.clear;
            button.style.borderTopWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 0;
            return button;
        }

        private static void Move(Transform element, int towardFront)
        {
            var index = element.GetSiblingIndex() + towardFront;
            if (index < 0 || element.parent == null || index >= element.parent.childCount)
            {
                return;
            }

            Undo.SetSiblingIndex(element, index, "重なり順を変更");
        }

        private void Group()
        {
            var selected = SelectedElements();
            if (selected.Count == 0)
            {
                return;
            }

            var parent = selected[0].parent;
            var group = new GameObject("Group", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(group, "グループにまとめる");
            var rect = (RectTransform)group.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetSiblingIndex(selected[0].GetSiblingIndex());
            foreach (var element in selected)
            {
                Undo.SetTransformParent(element, rect, "グループにまとめる");
            }

            Selection.activeGameObject = group;
        }

        private void Ungroup()
        {
            var group = _context.SelectedElement;
            if (group == null || group.parent == null)
            {
                return;
            }

            var index = group.GetSiblingIndex();
            var children = new List<Transform>();
            foreach (Transform child in group)
            {
                children.Add(child);
            }

            foreach (var child in children)
            {
                Undo.SetTransformParent(child, group.parent, "グループを解く");
                child.SetSiblingIndex(index++);
            }

            Undo.DestroyObjectImmediate(group.gameObject);
        }

        private void Duplicate()
        {
            var created = new List<Object>();
            foreach (var element in SelectedElements())
            {
                var copy = Object.Instantiate(element.gameObject, element.parent);
                copy.name = element.name + " (コピー)";
                copy.transform.SetSiblingIndex(element.GetSiblingIndex() + 1);
                Undo.RegisterCreatedObjectUndo(copy, "複製");
                created.Add(copy);
            }

            if (created.Count > 0)
            {
                Selection.objects = created.ToArray();
            }
        }

        private void Delete()
        {
            foreach (var element in SelectedElements())
            {
                Undo.DestroyObjectImmediate(element.gameObject);
            }
        }

        private void Rename(string value)
        {
            var selected = _context.SelectedElement;
            if (selected != null && !string.IsNullOrWhiteSpace(value) && selected.name != value)
            {
                Undo.RecordObject(selected.gameObject, "名前を変更");
                selected.name = value;
                _signature = 0;
            }
        }

        private void SetOpacity(float alpha)
        {
            var selected = _context.SelectedElement;
            if (selected == null)
            {
                return;
            }

            var group = selected.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = Undo.AddComponent<CanvasGroup>(selected.gameObject);
            }

            Undo.RecordObject(group, "濃さを変更");
            group.alpha = alpha;
            EditorUtility.SetDirty(group);
        }

        private List<Transform> SelectedElements()
        {
            var result = new List<Transform>();
            var screen = _context.EditingScreen;
            if (screen == null)
            {
                return result;
            }

            foreach (var go in Selection.gameObjects)
            {
                if (go.transform != screen.transform && go.transform.IsChildOf(screen.transform))
                {
                    result.Add(go.transform);
                }
            }

            result.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
            return result;
        }

        private static string TypeIcon(GameObject go)
        {
            if (go.GetComponent<UiBindFill>() != null) return "▬";
            if (go.GetComponent<UGui.Button>() != null) return "◉";
            if (go.GetComponent<UGui.InputField>() != null || go.GetComponent<TMPro.TMP_InputField>() != null) return "⌨";
            if (go.GetComponent<UGui.Text>() != null || go.GetComponent<TMPro.TMP_Text>() != null) return "T";
            if (go.GetComponent<UGui.Image>() != null || go.GetComponent<UGui.RawImage>() != null) return "▣";
            return go.transform.childCount > 0 ? "▤" : "□";
        }

        private static string TypeName(GameObject go) => TypeIcon(go) switch
        {
            "▬" => "ゲージ",
            "◉" => "ボタン",
            "⌨" => "入力欄",
            "T" => "文字",
            "▣" => "画像",
            "▤" => "グループ",
            _ => "空の枠"
        };

        private int Signature(UiScreen screen)
        {
            if (screen == null)
            {
                return -1;
            }

            unchecked
            {
                var visibility = SceneVisibilityManager.instance;
                var hash = screen.GetInstanceID();
                foreach (var t in screen.GetComponentsInChildren<RectTransform>(true))
                {
                    var go = t.gameObject;
                    hash = hash * 31 + go.GetInstanceID();
                    hash = hash * 31 + t.GetSiblingIndex();
                    hash = hash * 31 + go.name.GetHashCode();
                    hash = hash * 31 + (go.activeSelf ? 1 : 0) + (visibility.IsHidden(go) ? 2 : 0) + (visibility.IsPickingDisabled(go) ? 4 : 0) + (Selection.Contains(go) ? 8 : 0);
                }

                return hash + (_search.value?.GetHashCode() ?? 0);
            }
        }
    }
}
