using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// 画面の重なりの段（レイヤー）を、手前が上になるように帯で並べる。帯の中にはその段の画面が札で並ぶ。
    /// ↑↓ で段の順番（手前・奥）を入れ替えられる。
    /// </summary>
    public sealed class LayerStackPanel : VisualElement
    {
        private readonly UiStudioContext _context;
        private readonly VisualElement _lanes;
        private readonly Foldout _editor;
        private int _signature;

        public LayerStackPanel(UiStudioContext context)
        {
            _context = context;
            _lanes = new VisualElement();
            Add(_lanes);

            Add(StudioUi.Row(
                StudioUi.Button("Canvas をそろえる", EnsureCanvases, "レイヤーの一覧に合わせて、[UI] の下の Canvas（段ごとの描画の板）を作り直し・並べ直します。", small: true)));

            _editor = new Foldout { text = "レイヤーの一覧を編集（足す・名前・色・基準の画面サイズ）", value = false };
            _editor.Add(new InspectorElement(UiStudioSetup.LayerSettings));
            Add(_editor);
        }

        public void Refresh()
        {
            var settings = UiStudioSetup.LayerSettings;
            var signature = Signature(settings);
            if (signature == _signature)
            {
                return;
            }

            _signature = signature;
            _lanes.Clear();
            var layers = new List<UiLayerSettings.Layer>(settings.layers);
            layers.Sort((a, b) => b.sortOrder.CompareTo(a.sortOrder));
            for (var i = 0; i < layers.Count; i++)
            {
                _lanes.Add(BuildLane(settings, layers, i));
            }
        }

        private VisualElement BuildLane(UiLayerSettings settings, List<UiLayerSettings.Layer> sorted, int index)
        {
            var layer = sorted[index];
            var lane = StudioUi.Card();
            lane.style.flexDirection = FlexDirection.Row;
            lane.style.alignItems = Align.Center;
            lane.style.borderLeftWidth = 5;
            lane.style.borderLeftColor = layer.color;
            lane.tooltip = $"{layer.description}\n描く順番 {layer.sortOrder}（大きいほど手前）";

            var text = new VisualElement { style = { width = 190 } };
            text.Add(StudioUi.Styled(new Label(layer.label), "sk-card-title"));
            text.Add(StudioUi.Styled(new Label($"{layer.id} ・ 順番 {layer.sortOrder}{(layer.oneAtATime ? " ・ 1つずつ" : "")}"), "sk-muted"));
            lane.Add(text);

            var screens = StudioUi.Row();
            screens.style.flexGrow = 1;
            foreach (var screen in _context.Screens)
            {
                if (screen != null && screen.layer == layer.id)
                {
                    var chip = StudioUi.Chip(screen.Label, screen == _context.SelectedScreen ? ChipKind.Accent : ChipKind.Plain, "クリックでこの画面を選びます。");
                    chip.RegisterCallback<ClickEvent>(_ => _context.SelectScreen(screen));
                    screens.Add(chip);
                }
            }

            if (screens.childCount == 0)
            {
                screens.Add(StudioUi.Styled(new Label("（画面なし）"), "sk-muted"));
            }

            lane.Add(screens);
            var up = StudioUi.Button("↑", () => Swap(settings, sorted, index, index - 1), "この段を1つ手前にします。", small: true);
            var down = StudioUi.Button("↓", () => Swap(settings, sorted, index, index + 1), "この段を1つ奥にします。", small: true);
            up.SetEnabled(index > 0);
            down.SetEnabled(index < sorted.Count - 1);
            lane.Add(up);
            lane.Add(down);
            return lane;
        }

        private void Swap(UiLayerSettings settings, List<UiLayerSettings.Layer> sorted, int a, int b)
        {
            if (b < 0 || b >= sorted.Count)
            {
                return;
            }

            Undo.RecordObject(settings, "レイヤーの順番を変更");
            (sorted[a].sortOrder, sorted[b].sortOrder) = (sorted[b].sortOrder, sorted[a].sortOrder);
            EditorUtility.SetDirty(settings);
            EnsureCanvases();
            _signature = 0;
        }

        private static void EnsureCanvases()
        {
            var root = UiStudioSetup.FindRoot();
            if (root != null)
            {
                root.EnsureLayers();
                UiStudioSetup.MarkSceneDirty();
            }
        }

        private int Signature(UiLayerSettings settings)
        {
            unchecked
            {
                var hash = EditorUtility.GetDirtyCount(settings) + settings.layers.Count * 1000;
                foreach (var layer in settings.layers)
                {
                    hash = hash * 31 + layer.sortOrder + (layer.label?.GetHashCode() ?? 0);
                }

                foreach (var screen in _context.Screens)
                {
                    hash = hash * 17 + (screen != null ? screen.GetInstanceID() + (screen.layer?.GetHashCode() ?? 0) : 0);
                }

                return hash + (_context.SelectedScreen != null ? _context.SelectedScreen.GetInstanceID() : 0);
            }
        }
    }
}
