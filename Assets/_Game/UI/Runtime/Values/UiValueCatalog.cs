using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.UI
{
    /// <summary>
    /// 画面に渡せる値（キー）の一覧表。プランナーはここから選ぶだけでよく、打ち間違いが起きない。
    /// 「サンプル値」は Play していないときに部品の見た目を確かめるための仮の値。
    /// </summary>
    [CreateAssetMenu(menuName = "UI/Value Catalog", fileName = "UiValueCatalog")]
    public sealed class UiValueCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Item
        {
            [Tooltip("値の名前（ゲームのプログラムが使う、英数字と . の名前）。例: core.hp01")]
            public string key;

            [Tooltip("プランナー向けの表示名。例: コアの残りHP（割合）")]
            public string label;

            [Tooltip("どんな値か・いつ変わるか。")]
            [TextArea(1, 3)]
            public string description;

            [Tooltip("値の種類。")]
            public UiValueKind kind = UiValueKind.Number;

            [Tooltip("数のときの範囲（サンプル値のスライダー用）。")]
            public Vector2 range = new Vector2(0f, 1f);

            [Tooltip("Playしていないときに見た目を確かめるための仮の値（数・ON/OFF）。")]
            public float sampleNumber = 0.7f;

            [Tooltip("Playしていないときに見た目を確かめるための仮の値（文字）。")]
            public string sampleText = "";

            [Tooltip("Playしていないときに見た目を確かめるための仮の値（色）。")]
            public Color sampleColor = Color.white;

            [Tooltip("分類（一覧の見出し）。例: プレイヤー / コア / ロビー")]
            public string category = "その他";

            public UiValue Sample => kind switch
            {
                UiValueKind.Text => UiValue.Of(sampleText),
                UiValueKind.Color => UiValue.Of(sampleColor),
                UiValueKind.Bool => UiValue.Of(sampleNumber > 0.5f),
                _ => UiValue.Of(sampleNumber)
            };
        }

        public List<Item> items = new List<Item>();

        public Item Find(string key)
        {
            foreach (var item in items)
            {
                if (item != null && item.key == key)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>無ければ足す（既にあれば説明だけ更新しない）。戻り値は足したか。</summary>
        public bool AddIfMissing(string key, string label, string description, UiValueKind kind, string category, Vector2 range, float sample)
        {
            if (Find(key) != null)
            {
                return false;
            }

            items.Add(new Item
            {
                key = key,
                label = label,
                description = description,
                kind = kind,
                category = category,
                range = range,
                sampleNumber = sample
            });
            return true;
        }

        /// <summary>全部のサンプル値を掲示板に書く（編集中の見た目確認用）。</summary>
        public void PublishSamples()
        {
            foreach (var item in items)
            {
                if (item != null && !string.IsNullOrEmpty(item.key))
                {
                    UiValues.Set(item.key, item.Sample);
                }
            }
        }
    }

    /// <summary>この文字列の欄は「値の名前（キー）」。エディタで一覧から選べるようになる。</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class UiValueKeyAttribute : PropertyAttribute
    {
    }
}
