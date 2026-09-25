using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>破壊可能物のプリセット（種類）の検索・適用・保存・サンプル作成。</summary>
    public static class DestructiblePresetTools
    {
        public const string Folder = "Assets/_Game/Fortress/Presets/Destructibles";

        public static List<DestructiblePreset> FindAll()
        {
            return AssetDatabase.FindAssets("t:DestructiblePreset")
                .Select(guid => AssetDatabase.LoadAssetAtPath<DestructiblePreset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(preset => preset != null)
                .OrderBy(preset => preset.displayName, System.StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>プリセットの設定を対象へコピーする（位置・大きさ・連動・無敵など個体ごとの設定は変えない）。</summary>
        public static void Apply(DestructiblePreset preset, IEnumerable<DestructibleObstacle> targets)
        {
            using (var presetObject = new SerializedObject(preset))
            {
                var source = presetObject.FindProperty("settings").boxedValue;

                foreach (var target in targets)
                {
                    using (var targetObject = new SerializedObject(target))
                    {
                        targetObject.FindProperty("settings").boxedValue = source;
                        targetObject.FindProperty("sourcePreset").objectReferenceValue = preset;
                        targetObject.ApplyModifiedProperties();
                    }
                }
            }
        }

        /// <summary>対象の現在の設定を、新しいプリセットアセットとして保存する。キャンセルならnull。</summary>
        public static DestructiblePreset SaveAsPreset(DestructibleObstacle source)
        {
            EnsureFolder();
            var suggested = source.sourcePreset != null ? source.sourcePreset.displayName : source.name;
            var path = EditorUtility.SaveFilePanelInProject(
                "プリセットとして保存", suggested, "asset", "破壊可能物のプリセット名を決めてください。", Folder);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var preset = ScriptableObject.CreateInstance<DestructiblePreset>();
            preset.displayName = System.IO.Path.GetFileNameWithoutExtension(path);
            var scale = source.transform.localScale;
            preset.defaultSize = new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y));

            AssetDatabase.CreateAsset(preset, path);
            using (var sourceObject = new SerializedObject(source))
            using (var presetObject = new SerializedObject(preset))
            {
                presetObject.FindProperty("settings").boxedValue = sourceObject.FindProperty("settings").boxedValue;
                presetObject.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();
            return preset;
        }

        /// <summary>最初から使えるサンプルのプリセット（木箱・岩・ガラス）を作る。既にあれば作らない。</summary>
        public static void CreateSamples()
        {
            EnsureFolder();
            CreateSample("木箱", new Vector2(2f, 2f), new Color(0.72f, 0.52f, 0.34f), 30f, 1f, true, 10f);
            CreateSample("岩", new Vector2(3f, 3f), new Color(0.5f, 0.5f, 0.55f), 120f, 0.6f, true, 25f);
            CreateSample("ガラス", new Vector2(2f, 1f), new Color(0.6f, 0.85f, 0.95f, 0.8f), 8f, 2f, false, 0f);
            AssetDatabase.SaveAssets();
        }

        private static void CreateSample(
            string displayName, Vector2 size, Color color, float health, float damageMultiplier, bool regenerates, float regenSeconds)
        {
            var path = $"{Folder}/{displayName}.asset";
            if (AssetDatabase.LoadAssetAtPath<DestructiblePreset>(path) != null)
            {
                return;
            }

            var preset = ScriptableObject.CreateInstance<DestructiblePreset>();
            preset.displayName = displayName;
            preset.defaultSize = size;

            var settings = preset.settings;
            settings.durability.maxHealth = health;
            settings.durability.damageMultiplier = damageMultiplier;
            settings.durability.regenerates = regenerates;
            settings.durability.regenDelaySeconds = regenSeconds;
            settings.visual.placeholderColor = color;
            settings.feedback.debrisColor = color;

            AssetDatabase.CreateAsset(preset, path);
        }

        private static void EnsureFolder()
        {
            var parts = Folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
