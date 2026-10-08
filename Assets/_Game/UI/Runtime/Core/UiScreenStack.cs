using System;
using System.Collections.Generic;

namespace MS2026.UI
{
    /// <summary>
    /// どの画面が開いているか・戻る（Esc）でどれを閉じるかを決める純粋な計算（Unityに依存しない、テスト可能）。
    /// 開いている画面は「段の順番 → 開いた順」で並ぶ。戻るでは一番手前の「戻るで閉じる」画面を閉じる。
    /// </summary>
    public sealed class UiScreenStack
    {
        public readonly struct Item
        {
            public readonly string Id;
            public readonly string Layer;
            public readonly int LayerOrder;
            public readonly long OpenedAt;
            public readonly bool ClosesOnBack;

            public Item(string id, string layer, int layerOrder, long openedAt, bool closesOnBack)
            {
                Id = id;
                Layer = layer;
                LayerOrder = layerOrder;
                OpenedAt = openedAt;
                ClosesOnBack = closesOnBack;
            }
        }

        private readonly List<Item> _open = new List<Item>();
        private long _counter;

        public IReadOnlyList<Item> Open => _open;

        public bool IsOpen(string id) => _open.Exists(i => i.Id == id);

        /// <summary>
        /// 開いたことにする。oneAtATime の段なら、同じ段の他の画面を閉じる一覧を返す（呼び出し側が閉じる）。
        /// </summary>
        public List<string> Push(string id, string layer, int layerOrder, bool oneAtATime, bool closesOnBack)
        {
            var toClose = new List<string>();
            if (oneAtATime)
            {
                foreach (var item in _open)
                {
                    if (item.Layer == layer && item.Id != id)
                    {
                        toClose.Add(item.Id);
                    }
                }
            }

            _open.RemoveAll(i => i.Id == id || toClose.Contains(i.Id));
            _open.Add(new Item(id, layer, layerOrder, ++_counter, closesOnBack));
            _open.Sort(Compare);
            return toClose;
        }

        public bool Remove(string id) => _open.RemoveAll(i => i.Id == id) > 0;

        /// <summary>戻るで閉じる画面（無ければ null）。一番手前の段の、一番最後に開いた「戻るで閉じる」画面。</summary>
        public string TopForBack()
        {
            for (var i = _open.Count - 1; i >= 0; i--)
            {
                if (_open[i].ClosesOnBack)
                {
                    return _open[i].Id;
                }
            }

            return null;
        }

        public void Clear() => _open.Clear();

        private static int Compare(Item a, Item b)
        {
            var byLayer = a.LayerOrder.CompareTo(b.LayerOrder);
            return byLayer != 0 ? byLayer : a.OpenedAt.CompareTo(b.OpenedAt);
        }
    }
}
