using System.Collections.Generic;
using System.Linq;
using DDrive.Foundation.Data;
using DDrive.Foundation.Identity;
using DDrive.Runtime.Audio;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.SoundStudio
{
    /// <summary>検索付きで SE（BGM の欄なら BGM）を選ぶ小さな窓。選ぶとすぐ欄に入る。</summary>
    public sealed class SePicker : EditorWindow
    {
        private const int MaxShown = 200;

        private SoundStudioContext _context;
        private SoundSlot _slot;
        private ScrollView _list;
        private string _search = string.Empty;

        public static void Open(SoundStudioContext context, SoundSlot slot)
        {
            if (slot == null || slot.Kind == SoundSlotKind.Candidate)
            {
                return;
            }

            var window = CreateInstance<SePicker>();
            window._context = context;
            window._slot = slot;
            window.titleContent = new GUIContent(slot.Kind == SoundSlotKind.Bgm ? "BGM を選ぶ" : "音を選ぶ");
            window.minSize = new Vector2(360, 420);
            window.ShowUtility();
            window.Build();
        }

        private void Build()
        {
            rootVisualElement.Clear();
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StudioShell.StylePath);
            if (sheet != null)
            {
                rootVisualElement.styleSheets.Add(sheet);
            }

            rootVisualElement.AddToClassList("sk-root");
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 6;
            var own = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_Tools/SoundStudio/Editor/SoundStudio.uss");
            if (own != null)
            {
                rootVisualElement.styleSheets.Add(own);
            }
            rootVisualElement.Add(StudioUi.Lead("「" + _slot.Label + "」に入れる音を選んでください。"));
            var search = new TextField { tooltip = "名前で探す。" };
            search.textEdition.placeholder = "名前で探す";
            search.RegisterValueChangedCallback(e =>
            {
                _search = e.newValue ?? string.Empty;
                Fill();
            });
            rootVisualElement.Add(search);
            _list = new ScrollView();
            _list.style.flexGrow = 1;
            rootVisualElement.Add(_list);
            Fill();
            search.Focus();
        }

        private void Fill()
        {
            _list.Clear();
            IEnumerable<AssetDataBase> all = _slot.Kind == SoundSlotKind.Bgm
                ? _context.Library.AllBgm.Cast<AssetDataBase>()
                : _context.Library.AllSe.Cast<AssetDataBase>();
            var shown = all.Where(d => string.IsNullOrEmpty(_search)
                                       || SoundLibrary.NameOf(d).IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0
                                       || d.name.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0).Take(MaxShown).ToList();
            if (shown.Count == 0)
            {
                _list.Add(StudioUi.Note("見つかりません。wav を行に落とすと、新しい音として登録できます。", NoteKind.Info));
                return;
            }

            foreach (var data in shown)
            {
                var item = data;
                var key = "picker|" + item.Id;
                var line = new VisualElement();
                line.AddToClassList("ss-picker-line");
                Button play = null;
                play = StudioUi.Button("▶", () =>
                {
                    if (_context.Preview.IsPlayingKey(key))
                    {
                        _context.Preview.Stop();
                    }
                    else
                    {
                        var problem = _context.Preview.PlayData(item, key);
                        if (problem != null)
                        {
                            ShowNotification(new GUIContent(problem));
                        }
                    }
                }, "入れる前に聞く。鳴っている間は ■ になり、押すと止まる。", small: true);
                play.AddToClassList("ss-picker-play");
                play.schedule.Execute(() => play.text = _context.Preview.IsPlayingKey(key) ? "■" : "▶").Every(200);
                line.Add(play);
                var pick = StudioUi.Button(SoundLibrary.NameOf(item), () => Pick(item), item.name + "（押すとこの音を入れる）");
                pick.AddToClassList("ss-picker-name");
                line.Add(pick);
                _list.Add(line);
            }
        }

        private void OnDisable()
        {
            _context?.Preview.Stop();
        }

        private void Pick(AssetDataBase data)
        {
            var context = _context;
            var slot = _slot;
            Close();
            SoundBulk.SetSound(context, slot, data.Id);
        }
    }
}
