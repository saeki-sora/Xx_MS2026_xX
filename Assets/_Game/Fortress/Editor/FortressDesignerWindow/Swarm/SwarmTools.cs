using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>群衆システムの作成や、設定・敵タイプのアセット取得など、編集用の共通処理。</summary>
    public static class SwarmTools
    {
        private const string PresetFolder = "Assets/_Game/Fortress/Presets";
        private const double CacheSeconds = 2.0;

        private static EnemyTypeDefinition[] _cachedTypes;
        private static double _cachedAt = double.NegativeInfinity;

        public static SwarmSystem CreateSystem()
        {
            var go = new GameObject("SwarmSystem");
            Undo.RegisterCreatedObjectUndo(go, "Create Swarm System");

            var fortressRoot = GameObject.Find("Fortress");
            if (fortressRoot != null)
            {
                Undo.SetTransformParent(go.transform, fortressRoot.transform, "Create Swarm System");
            }

            var system = Undo.AddComponent<SwarmSystem>(go);
            system.settings = LoadOrCreate<SwarmSettings>("Default_SwarmSettings.asset");
            Selection.activeGameObject = go;
            return system;
        }

        public static SwarmSettings CreateSettingsAsset()
        {
            return LoadOrCreate<SwarmSettings>("Default_SwarmSettings.asset");
        }

        public static EnemyTypeDefinition CreateEnemyTypeAsset()
        {
            EnsurePresetFolder();
            var path = AssetDatabase.GenerateUniqueAssetPath($"{PresetFolder}/New_EnemyType.asset");
            var asset = ScriptableObject.CreateInstance<EnemyTypeDefinition>();
            asset.displayName = "新しい敵";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            InvalidateEnemyTypeCache();
            return asset;
        }

        /// <summary>
        /// プロジェクト内の全EnemyTypeDefinition。AssetDatabaseの検索は重いので、結果を数秒キャッシュする
        /// （ウィンドウの再描画のたびに呼ばれるため、キャッシュしないとPlay中のフレーム時間を乱す）。
        /// </summary>
        public static EnemyTypeDefinition[] FindAllEnemyTypes()
        {
            var now = EditorApplication.timeSinceStartup;
            if (_cachedTypes != null && now - _cachedAt < CacheSeconds && _cachedTypes.All(t => t != null))
            {
                return _cachedTypes;
            }

            _cachedTypes = AssetDatabase.FindAssets("t:EnemyTypeDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<EnemyTypeDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null)
                .OrderBy(asset => asset.name, System.StringComparer.Ordinal)
                .ToArray();
            _cachedAt = now;
            return _cachedTypes;
        }

        public static void InvalidateEnemyTypeCache()
        {
            _cachedTypes = null;
        }

        private static T LoadOrCreate<T>(string fileName) where T : ScriptableObject
        {
            EnsurePresetFolder();
            var path = $"{PresetFolder}/{fileName}";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void EnsurePresetFolder()
        {
            if (!AssetDatabase.IsValidFolder(PresetFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Fortress", "Presets");
            }
        }
    }
}
