using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MS2026.ShaderFX.Editor
{
    [CustomEditor(typeof(EffectProfile))]
    public sealed class EffectProfileEditor : UnityEditor.Editor
    {
        private const float HeaderHeight = 20f;
        private const float RowSpacing = 2f;

        private static List<(Type type, string displayName, string category)> moduleTypeCache;
        private static GUIStyle summaryStyle;
        private static GUIStyle deleteButtonStyle;
        private static GUIStyle foldoutStyle;

        private SerializedProperty modulesProp;
        private ReorderableList list;

        private void OnEnable()
        {
            modulesProp = serializedObject.FindProperty("modules");
            list = new ReorderableList(serializedObject, modulesProp, true, false, false, false)
            {
                drawElementCallback = DrawElement,
                elementHeightCallback = GetElementHeight,
                onReorderCallback = _ =>
                {
                    serializedObject.ApplyModifiedProperties();
                    NotifyDirector();
                },
            };
        }

        public override void OnInspectorGUI()
        {
            InitStyles();
            serializedObject.Update();

            DrawToolbar();
            EditorGUILayout.Space(4);

            if (modulesProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("エフェクトが登録されていません。「+ Add Effect」から追加してください。", MessageType.Info);
            }
            else
            {
                list.DoLayoutList();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawToolbar()
        {
            var profile = (EffectProfile)target;
            int active = 0;
            foreach (var module in profile.modules)
            {
                if (module != null && module.enabled) active++;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{modulesProp.arraySize} Effects ({active} Active)", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ Add Effect", EditorStyles.toolbarDropDown, GUILayout.Width(110)))
                {
                    ShowAddMenu();
                }
            }
        }

        private void ShowAddMenu()
        {
            var menu = new GenericMenu();
            var types = GetModuleTypes();
            foreach (var entry in types)
            {
                var capturedType = entry.type;
                menu.AddItem(new GUIContent($"{entry.category}/{entry.displayName}"), false, () => AddModule(capturedType));
            }
            if (types.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("利用可能な EffectModule がありません"));
            }

            // プリセットは「オブジェクト系/画面系」ごとの複数モジュール組み合わせを一括追加する
            // (通常の1エフェクトずつの追加とは別枠)。プリセット適用後もそのまま個別モジュールを
            // 足したり調整したりできるので、プリセットは「完成形」ではなく「出発点」として使えます。
            foreach (var preset in EffectPresetLibrary.Presets)
            {
                var capturedPreset = preset;
                menu.AddItem(new GUIContent($"プリセット/{preset.Category}/{preset.Name}"), false, () => AddPreset(capturedPreset));
            }

            menu.ShowAsContext();
        }

        private void AddModule(Type type)
        {
            var profile = (EffectProfile)target;
            Undo.RecordObject(profile, "Add Effect");
            profile.modules.Add((EffectModule)Activator.CreateInstance(type));
            EditorUtility.SetDirty(profile);
            serializedObject.Update();
            NotifyDirector();
        }

        private void AddPreset(EffectPreset preset)
        {
            var profile = (EffectProfile)target;
            Undo.RecordObject(profile, $"Add Preset: {preset.Name}");
            foreach (var module in preset.Build())
            {
                profile.modules.Add(module);
            }
            EditorUtility.SetDirty(profile);
            serializedObject.Update();
            NotifyDirector();
        }

        private void RemoveModule(int index)
        {
            var profile = (EffectProfile)target;
            Undo.RecordObject(profile, "Remove Effect");
            profile.modules.RemoveAt(index);
            EditorUtility.SetDirty(profile);
            serializedObject.Update();
            NotifyDirector();
        }

        // Add/Remove/Reorder mutate profile.modules directly (SerializeReference list elements
        // can't be inserted as a new polymorphic type through the SerializedProperty API alone),
        // which bypasses the normal Inspector edit pipeline that OnValidate rides on. Without this,
        // objects using this Profile keep showing the pre-edit material/screen effect until
        // something else happens to touch the Director (e.g. entering Play Mode).
        private void NotifyDirector()
        {
            EffectDirector.Instance.RefreshUsers((EffectProfile)target);
        }

        private float GetElementHeight(int index)
        {
            if (index >= modulesProp.arraySize) return HeaderHeight;

            var element = modulesProp.GetArrayElementAtIndex(index);
            float height = HeaderHeight + RowSpacing * 2f;

            if (element.isExpanded)
            {
                var module = element.managedReferenceValue as EffectModule;
                string description = module != null ? GetDescription(module) : null;
                if (!string.IsNullOrEmpty(description))
                {
                    height += EditorStyles.helpBox.CalcHeight(new GUIContent(description), EditorGUIUtility.currentViewWidth - 40) + RowSpacing * 2f;
                }

                foreach (var child in GetBodyFields(element))
                {
                    height += EditorGUI.GetPropertyHeight(child, true) + RowSpacing;
                }
                height += 4f;
            }

            return height;
        }

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index >= modulesProp.arraySize) return;

            var element = modulesProp.GetArrayElementAtIndex(index);
            var module = element.managedReferenceValue as EffectModule;
            if (module == null) return;

            var headerRect = new Rect(rect.x, rect.y + RowSpacing, rect.width, HeaderHeight);
            EditorGUI.DrawRect(headerRect, GetCategoryColor(module));

            var enabledProp = element.FindPropertyRelative("enabled");
            var toggleRect = new Rect(headerRect.x + 20, headerRect.y + 2, 16, 16);
            EditorGUI.PropertyField(toggleRect, enabledProp, GUIContent.none);

            var deleteRect = new Rect(headerRect.xMax - 22, headerRect.y + 1, 18, 18);
            var summaryRect = new Rect(deleteRect.x - 132, headerRect.y, 128, HeaderHeight);
            var foldoutRect = new Rect(toggleRect.xMax + 4, headerRect.y, Mathf.Max(0, summaryRect.x - toggleRect.xMax - 8), HeaderHeight);

            GUI.enabled = enabledProp.boolValue;
            element.isExpanded = EditorGUI.Foldout(foldoutRect, element.isExpanded, GetDisplayName(module), true, foldoutStyle);
            GUI.Label(summaryRect, module.GetSummary(), summaryStyle);
            GUI.enabled = true;

            if (GUI.Button(deleteRect, "×", deleteButtonStyle))
            {
                RemoveModule(index);
                return;
            }

            if (element.isExpanded)
            {
                float y = headerRect.yMax + RowSpacing;

                string description = GetDescription(module);
                if (!string.IsNullOrEmpty(description))
                {
                    var descRect = new Rect(rect.x + 20, y, rect.width - 24,
                        EditorStyles.helpBox.CalcHeight(new GUIContent(description), rect.width - 24));
                    EditorGUI.HelpBox(descRect, description, MessageType.None);
                    y = descRect.yMax + RowSpacing * 2f;
                }

                EditorGUI.indentLevel++;
                foreach (var child in GetBodyFields(element))
                {
                    float h = EditorGUI.GetPropertyHeight(child, true);
                    var fieldRect = new Rect(rect.x + 20, y, rect.width - 24, h);
                    EditorGUI.PropertyField(fieldRect, child, true);
                    y += h + RowSpacing;
                }
                EditorGUI.indentLevel--;
            }
        }

        private static IEnumerable<SerializedProperty> GetBodyFields(SerializedProperty element)
        {
            var iterator = element.Copy();
            var end = element.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (iterator.name == "enabled") continue;
                yield return iterator.Copy();
            }
        }

        private static Color GetCategoryColor(EffectModule module)
        {
            var category = module.GetType().GetCustomAttribute<EffectModuleInfoAttribute>()?.Category;
            return category switch
            {
                "オブジェクト系" => new Color(0.20f, 0.42f, 0.55f, 0.5f),
                "画面系" => new Color(0.55f, 0.28f, 0.45f, 0.5f),
                _ => new Color(0.3f, 0.3f, 0.3f, 0.5f),
            };
        }

        private static string GetDisplayName(EffectModule module)
        {
            var info = module.GetType().GetCustomAttribute<EffectModuleInfoAttribute>();
            return info?.DisplayName ?? ObjectNames.NicifyVariableName(module.GetType().Name);
        }

        private static string GetDescription(EffectModule module)
        {
            return module.GetType().GetCustomAttribute<EffectModuleInfoAttribute>()?.Description;
        }

        private static List<(Type type, string displayName, string category)> GetModuleTypes()
        {
            if (moduleTypeCache != null) return moduleTypeCache;

            moduleTypeCache = new List<(Type, string, string)>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

                foreach (var type in types)
                {
                    if (type.IsAbstract || !typeof(EffectModule).IsAssignableFrom(type)) continue;
                    var info = type.GetCustomAttribute<EffectModuleInfoAttribute>();
                    moduleTypeCache.Add((type, info?.DisplayName ?? ObjectNames.NicifyVariableName(type.Name), info?.Category ?? "General"));
                }
            }
            moduleTypeCache.Sort((a, b) => string.CompareOrdinal(a.displayName, b.displayName));
            return moduleTypeCache;
        }

        private static void InitStyles()
        {
            if (summaryStyle != null) return;

            summaryStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.35f, 0.35f, 0.35f) },
            };
            deleteButtonStyle = new GUIStyle(EditorStyles.miniButton) { fontSize = 11, padding = new RectOffset(0, 0, 0, 2) };
            foldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
        }
    }
}
