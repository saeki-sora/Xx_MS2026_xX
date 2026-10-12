using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MS2026.CharacterSelect.EditorTools
{
    /// <summary>
    /// シーンを組み立て直すときに、調整した数値を引き継ぐ（タイトルの組み立ての「数値を引き継いで作り直す」と同じ考え方）。
    /// キャラ選択の部品の値を「ヒエラルキーの場所＋部品の種類」ごとに写し取り、作り直した後の同じ場所の同じ部品に書き戻す。
    /// シーンの中の物への参照（どの Image を動かすか など）は、作り直した側の物を使う。絵などのアセットへの参照は、写し取った方を使う。
    /// </summary>
    internal sealed class CharacterSelectValueSnapshot
    {
        private static readonly Type[] Types =
        {
            typeof(CharacterSelectController), typeof(CharacterSelectView), typeof(PlayerPanel), typeof(PlayerCircle), typeof(PlayerTag), typeof(StatBar),
            typeof(CharacterSwapFx), typeof(PasteStroke), typeof(CharacterSelectAmbient), typeof(CharacterSelectBurst), typeof(CharacterSelectGlitch), typeof(CharacterSelectScroll)
        };

        private readonly Dictionary<string, string> _json = new Dictionary<string, string>();

        public int Count => _json.Count;

        public static CharacterSelectValueSnapshot Capture(IEnumerable<GameObject> roots)
        {
            var snapshot = new CharacterSelectValueSnapshot();
            foreach (var root in roots.Where(r => r != null))
            {
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component != null && Types.Contains(component.GetType()))
                    {
                        snapshot._json[Key(component)] = EditorJsonUtility.ToJson(component);
                    }
                }
            }

            return snapshot;
        }

        /// <summary>同じ場所の同じ部品に書き戻す。戻り値 = 書き戻した部品の数。</summary>
        public int Apply(IEnumerable<GameObject> roots)
        {
            var applied = 0;
            foreach (var root in roots.Where(r => r != null))
            {
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null || !_json.TryGetValue(Key(component), out var json))
                    {
                        continue;
                    }

                    // シーンの中の物への参照は、作り直した側の物を覚えておいて書き戻す（写し取った方は消えた物を指しているため）。
                    var serialized = new SerializedObject(component);
                    var sceneRefs = new Dictionary<string, Object>();
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null &&
                            !EditorUtility.IsPersistent(property.objectReferenceValue))
                        {
                            sceneRefs[property.propertyPath] = property.objectReferenceValue;
                        }
                    }

                    EditorJsonUtility.FromJsonOverwrite(json, component);
                    serialized.Update();
                    foreach (var pair in sceneRefs)
                    {
                        var target = serialized.FindProperty(pair.Key);
                        if (target != null)
                        {
                            target.objectReferenceValue = pair.Value;
                        }
                    }

                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    applied++;
                }
            }

            return applied;
        }

        private static string Key(Component component)
        {
            var names = new List<string>();
            for (var t = component.transform; t != null; t = t.parent)
            {
                names.Add(t.name);
            }

            names.Reverse();
            return string.Join("/", names) + "|" + component.GetType().FullName;
        }
    }
}
