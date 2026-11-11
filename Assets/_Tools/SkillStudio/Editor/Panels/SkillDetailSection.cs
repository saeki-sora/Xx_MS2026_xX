using System;
using System.Collections.Generic;
using System.Reflection;
using MS2026.Skills.EditorTools;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// 「詳しい設定」の中の、まとまり1つ。見出しの行（▶ 名前・項目の数）を押すと開く。最初は閉じている。
    /// 枠で囲まず、見出しと余白で区切る（T9）。演出の付け外し・強さ（<see cref="Skills.SkillDialForAttribute"/>）は、
    /// 関係するまとまりの一番上に出す（同じ目的の値を1か所に集める。T1）。
    /// </summary>
    public sealed class SkillDetailSection : VisualElement
    {
        private readonly SkillStudioContext _context;
        private readonly string _key;
        private readonly Action _drawBody;
        private readonly Func<int> _countChanged;
        private readonly Action _resetAll;
        private readonly Label _arrow;
        private readonly Label _changed;
        private readonly Button _resetButton;
        private readonly IMGUIContainer _body;
        private readonly SerializedObject _serializedObject;

        private SkillDetailSection(SkillStudioContext context, string key, string title, int count, string tooltip, SerializedObject serializedObject,
            Action drawBody, Func<int> countChanged, Action resetAll)
        {
            _context = context;
            _key = key;
            _drawBody = drawBody;
            _countChanged = countChanged;
            _resetAll = resetAll;
            _serializedObject = serializedObject;
            style.borderTopWidth = 1;
            style.borderTopColor = new Color(1f, 1f, 1f, 0.07f);
            style.paddingTop = 4;
            style.paddingBottom = 4;

            var header = new VisualElement { tooltip = tooltip };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 3;
            header.style.paddingBottom = 3;
            header.focusable = true;
            _arrow = new Label();
            _arrow.style.width = 16;
            _arrow.style.opacity = 0.7f;
            var name = new Label(title);
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            var number = new Label(count > 0 ? $"  {count}" : "");
            number.style.opacity = 0.5f;
            _changed = new Label { tooltip = "初期値から変えた項目の数（開くと、変えた項目の左に青い印が付いている）。" };
            _changed.style.color = new Color(0.45f, 0.72f, 1f);
            _changed.style.marginLeft = 10;
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            _resetButton = StudioUi.Button("全部戻す", ResetAll, "このまとまりの値を全部、初期値（プログラムに書いてある値）に戻す。Ctrl+Z で取り消せる。", small: true);
            _resetButton.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            header.Add(_arrow);
            header.Add(name);
            header.Add(number);
            header.Add(_changed);
            header.Add(spacer);
            header.Add(_resetButton);
            header.RegisterCallback<ClickEvent>(_ => Toggle());
            header.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Space)
                {
                    Toggle();
                }
            });
            Add(header);

            _body = new IMGUIContainer(DrawBody);
            _body.style.marginLeft = 16;
            _body.style.marginTop = 2;
            _body.style.marginBottom = 6;
            Add(_body);
            Apply();
            RefreshChanged();
        }

        /// <summary>見出しの「変えた N」と「全部戻す」を今の値に合わせる（ページの定期更新から呼ぶ）。戻り値は変えた項目の数。</summary>
        public int RefreshChanged()
        {
            var changed = 0;
            if (_serializedObject.targetObject != null)
            {
                _serializedObject.Update();
                changed = _countChanged();
            }

            _changed.text = changed > 0 ? $"● 変えた {changed}" : "";
            _resetButton.style.display = changed > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            return changed;
        }

        private void ResetAll()
        {
            if (_serializedObject.targetObject == null)
            {
                return;
            }

            _serializedObject.Update();
            _resetAll();
            _serializedObject.ApplyModifiedProperties();
            GUI.FocusControl(null);
            RefreshChanged();
        }

        /// <summary>設定のまとまり1つ（＋そのまとまりに配られた付け外し）。</summary>
        public static SkillDetailSection ForGroup(SkillStudioContext context, string keyPrefix, SerializedObject serializedObject, FieldInfo group)
        {
            var dials = SkillTabs.DialsFor(serializedObject.targetObject.GetType(), group);
            var count = SkillTabs.CountFields(group.FieldType) + dials.Count;

            int CountChanged()
            {
                var changed = SkillDefaults.CountChanged(serializedObject.FindProperty(group.Name));
                foreach (var (dialGroup, dial) in dials)
                {
                    changed += SkillDefaults.CountChanged(serializedObject.FindProperty(dialGroup.Name + "." + dial.Name));
                }

                return changed;
            }

            void ResetAll()
            {
                var groupProperty = serializedObject.FindProperty(group.Name);
                if (groupProperty != null)
                {
                    SkillDefaults.Reset(groupProperty);
                }

                foreach (var (dialGroup, dial) in dials)
                {
                    var property = serializedObject.FindProperty(dialGroup.Name + "." + dial.Name);
                    if (property != null)
                    {
                        SkillDefaults.Reset(property);
                    }
                }
            }

            return new SkillDetailSection(context, keyPrefix + group.Name, SkillTabs.LabelOf(group), count, SkillTabs.TooltipOf(group), serializedObject, () =>
            {
                serializedObject.Update();
                if (dials.Count > 0)
                {
                    foreach (var (dialGroup, dial) in dials)
                    {
                        var property = serializedObject.FindProperty(dialGroup.Name + "." + dial.Name);
                        if (property != null)
                        {
                            SkillInspectorGUI.DrawField(property, dial, SkillTabs.LabelOf(dial) + "（出す・強さ）");
                        }
                    }

                    EditorGUILayout.Space(6f);
                }

                var groupProperty = serializedObject.FindProperty(group.Name);
                if (groupProperty != null)
                {
                    SkillInspectorGUI.DrawGroupContents(groupProperty, group.FieldType);
                }

                serializedObject.ApplyModifiedProperties();
            }, CountChanged, ResetAll);
        }

        /// <summary>設定ファイルの一番上の階層の項目を、名前で選んで1つのまとまりにする（共通部分の溜まり方など）。</summary>
        public static SkillDetailSection ForFields(SkillStudioContext context, string key, SerializedObject serializedObject, string title,
            IReadOnlyList<string> names, string tooltip)
        {
            int CountChanged()
            {
                var changed = 0;
                foreach (var name in names)
                {
                    changed += SkillDefaults.CountChanged(serializedObject.FindProperty(name));
                }

                return changed;
            }

            void ResetAll()
            {
                foreach (var name in names)
                {
                    var property = serializedObject.FindProperty(name);
                    if (property != null)
                    {
                        SkillDefaults.Reset(property);
                    }
                }
            }

            return new SkillDetailSection(context, key, title, names.Count, tooltip, serializedObject, () =>
            {
                serializedObject.Update();
                var type = serializedObject.targetObject.GetType();
                foreach (var name in names)
                {
                    var property = serializedObject.FindProperty(name);
                    if (property != null)
                    {
                        SkillInspectorGUI.DrawField(property, SkillInspectorGUI.Field(type, name));
                    }
                }

                serializedObject.ApplyModifiedProperties();
            }, CountChanged, ResetAll);
        }

        private void Toggle()
        {
            _context.SetOpen(_key, !_context.IsOpen(_key));
            Apply();
        }

        private void Apply()
        {
            var open = _context.IsOpen(_key);
            _arrow.text = open ? "▼" : "▶";
            _body.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void DrawBody()
        {
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(contentRect.width * 0.42f, 160f, 300f);
            if (_serializedObject.targetObject != null)
            {
                SkillDefaults.DrawWithMarks(_drawBody);
            }

            EditorGUIUtility.labelWidth = labelWidth;
        }
    }
}
