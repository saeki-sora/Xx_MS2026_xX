using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>画面の一覧（レイヤーごとにまとめ、手前の段が上）。クリックで選択、ダブルクリックで編集。</summary>
    public sealed class ScreenListPanel : VisualElement
    {
        private readonly UiStudioContext _context;
        private int _signature;

        public ScreenListPanel(UiStudioContext context)
        {
            _context = context;
            AddToClassList("sk-list");
            RegisterCallback<AttachToPanelEvent>(_ => _context.Changed += Rebuild);
            RegisterCallback<DetachFromPanelEvent>(_ => _context.Changed -= Rebuild);
        }

        public void Refresh()
        {
            var signature = Signature();
            if (signature != _signature)
            {
                _signature = signature;
                Rebuild();
            }
        }

        private void Rebuild()
        {
            Clear();
            var settings = UiStudioSetup.LayerSettings;
            var byLayer = new Dictionary<string, List<UiScreen>>();
            foreach (var screen in _context.Screens)
            {
                if (screen == null)
                {
                    continue;
                }

                if (!byLayer.TryGetValue(screen.layer ?? "", out var list))
                {
                    byLayer[screen.layer ?? ""] = list = new List<UiScreen>();
                }

                list.Add(screen);
            }

            var layers = new List<UiLayerSettings.Layer>(settings.layers);
            layers.Sort((a, b) => b.sortOrder.CompareTo(a.sortOrder));
            foreach (var layer in layers)
            {
                if (!byLayer.TryGetValue(layer.id, out var screens))
                {
                    continue;
                }

                AddHeader(layer.label, layer.color, layer.description);
                foreach (var screen in screens)
                {
                    Add(BuildRow(screen, layer.color));
                }

                byLayer.Remove(layer.id);
            }

            foreach (var pair in byLayer)
            {
                AddHeader($"不明なレイヤー「{pair.Key}」", Color.gray, "レイヤーの一覧に無い名前の画面です。");
                foreach (var screen in pair.Value)
                {
                    Add(BuildRow(screen, Color.gray));
                }
            }

            if (_context.Screens.Count == 0)
            {
                Add(StudioUi.Styled(new Label("まだ画面がありません。右の雛形から作ってください。"), "sk-muted"));
            }
        }

        private void AddHeader(string text, Color color, string tooltip)
        {
            var header = StudioUi.Styled(new Label(text), "sk-section-title");
            header.style.color = color;
            header.style.marginTop = 8;
            header.style.marginBottom = 2;
            header.tooltip = tooltip;
            Add(header);
        }

        private VisualElement BuildRow(UiScreen screen, Color layerColor)
        {
            var row = StudioUi.Styled(new VisualElement(), "sk-list-item");
            row.EnableInClassList("sk-list-item--selected", screen == _context.SelectedScreen);
            row.tooltip = $"名前: {screen.screenId}\n{(string.IsNullOrEmpty(screen.memo) ? "" : screen.memo + "\n")}ダブルクリックで編集します。";
            var dot = StudioUi.Styled(new VisualElement(), "sk-list-dot");
            dot.style.backgroundColor = layerColor;
            row.Add(dot);
            row.Add(StudioUi.Styled(new Label(screen.Label), "sk-list-label"));
            if (screen.openOnStart)
            {
                row.Add(StudioUi.Chip("起動時", ChipKind.Ok, "ゲームが始まったときに自動で開きます。"));
            }

            if (!EditorUtility.IsPersistent(screen))
            {
                row.Add(StudioUi.Chip("シーン", ChipKind.Info, "シーンに直接置いた画面です（Prefab ではない）。"));
            }

            if (Application.isPlaying && UiRoot.Active != null && UiRoot.Active.IsOpen(screen.screenId))
            {
                row.Add(StudioUi.Chip("表示中", ChipKind.Accent));
            }

            row.RegisterCallback<ClickEvent>(e =>
            {
                _context.SelectScreen(screen);
                if (e.clickCount == 2)
                {
                    UiStudioContext.OpenForEditing(screen);
                }
            });
            return row;
        }

        private int Signature()
        {
            unchecked
            {
                var hash = _context.Screens.Count * 7 + (_context.SelectedScreen != null ? _context.SelectedScreen.GetInstanceID() : 0);
                foreach (var screen in _context.Screens)
                {
                    if (screen != null)
                    {
                        hash = hash * 31 + screen.GetInstanceID() + (screen.layer?.GetHashCode() ?? 0) + (screen.displayName?.GetHashCode() ?? 0) + (screen.openOnStart ? 1 : 0);
                        if (Application.isPlaying && UiRoot.Active != null && UiRoot.Active.IsOpen(screen.screenId))
                        {
                            hash += 13;
                        }
                    }
                }

                return hash;
            }
        }
    }
}
