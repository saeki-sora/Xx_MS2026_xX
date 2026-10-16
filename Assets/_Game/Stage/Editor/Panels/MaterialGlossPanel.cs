using System;
using System.Collections.Generic;
using System.Linq;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 光ページ: シーンに出ているステージで使われている材質の一覧と、それぞれの「つや」。
    /// 床だけ照り返しを抑える、のように材質ごとに調整したいとき用。
    /// </summary>
    public static class MaterialGlossPanel
    {
        private const string SmoothnessProperty = "_Smoothness";

        public static VisualElement Build(Func<StageRoot> root)
        {
            var card = StudioUi.Card();
            var foldout = new Foldout { text = "材質ごとのつや", value = false };
            foldout.tooltip = "ステージで使っている材質のつや（照り返しの鋭さ）を1つずつ変えます。材質そのものを書き換えるので、同じ材質を使うほかのステージも変わります。";
            var list = new VisualElement();

            void Rebuild()
            {
                list.Clear();
                var entries = Collect(root());
                foldout.text = entries.Count > 0 ? $"材質ごとのつや（{entries.Count}個）" : "材質ごとのつや";
                if (entries.Count == 0)
                {
                    list.Add(StudioUi.Note("ステージがシーンに出ていないか、つやのある材質がありません。", NoteKind.Info));
                    return;
                }

                foreach (var entry in entries)
                {
                    list.Add(Row(entry));
                }
            }

            foldout.Add(StudioUi.Note("材質そのものの値を書き換えます（同じ材質を使うほかのステージにも反映）。Play 中に変えても止めた後に残ります。つやが高いほど照り返しが小さく鋭く、低いほど広くやわらかくなります。", NoteKind.Info));
            foldout.Add(StudioUi.Row(StudioUi.Spacer(), StudioUi.Button("一覧を更新", Rebuild, "背景を足した・材質を差し替えたときに押します。", small: true)));
            foldout.Add(list);
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == foldout && evt.newValue)
                {
                    Rebuild();
                }
            });
            card.Add(foldout);
            return card;
        }

        private sealed class Entry
        {
            public Material Material;
            public int Users;
        }

        private static List<Entry> Collect(StageRoot root)
        {
            var result = new Dictionary<Material, Entry>();
            if (root == null || root.instance == null)
            {
                return new List<Entry>();
            }

            foreach (var renderer in root.instance.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || !material.HasProperty(SmoothnessProperty))
                    {
                        continue;
                    }

                    if (!result.TryGetValue(material, out var entry))
                    {
                        entry = new Entry { Material = material };
                        result.Add(material, entry);
                    }

                    entry.Users++;
                }
            }

            return result.Values.OrderBy(e => e.Material.name, StringComparer.Ordinal).ToList();
        }

        private static VisualElement Row(Entry entry)
        {
            var material = entry.Material;
            var name = StudioUi.Button(material.name, () =>
            {
                EditorGUIUtility.PingObject(material);
                Selection.activeObject = material;
            }, "押すと Project でこの材質を選びます。", small: true);
            name.style.width = 170;
            name.style.unityTextAlign = TextAnchor.MiddleLeft;

            var slider = new Slider(0f, 1f) { value = material.GetFloat(SmoothnessProperty), showInputField = true };
            slider.style.flexGrow = 1;
            slider.tooltip = "つや（0=つや消し、1=鏡のよう）。";
            slider.RegisterValueChangedCallback(evt =>
            {
                if (material == null)
                {
                    return;
                }

                Undo.RecordObject(material, "つやを変える");
                material.SetFloat(SmoothnessProperty, evt.newValue);
                EditorUtility.SetDirty(material);
                SceneView.RepaintAll();
            });

            var row = StudioUi.Row(name, slider, StudioUi.Chip($"{entry.Users}か所", ChipKind.Plain, "この材質を使っている描画の数。"));
            row.style.alignItems = Align.Center;
            if (material.shader == null || material.shader.name != StageShaderIds.PropShaderName)
            {
                row.Add(StudioUi.Chip("専用シェーダー以外", ChipKind.Warn, "この材質には透け・焦げなどの反応や、光ページの背景向けの設定が効きません（オブジェクトページの「専用シェーダーに切り替え」で直せます）。"));
            }

            return row;
        }
    }
}
