using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.ObjectToon
{
    /// <summary>
    /// 「どのシーンの、どの材質を、どのトゥーン材質に差し替えるか」の表。
    /// シーン・Prefab・元の材質は一切書き換えず、実行時（とエディタのプレビュー中）だけ材質を差し替える。
    /// 置き場所は Resources/ObjectToon/ObjectToonProfile.asset（調整ウィンドウが作る）。
    /// 差し替えの順番: 物ごとの個別の材質（objectOverrides）→ 材質ごとの表（3D は entries / 2D は spriteEntries）。
    /// </summary>
    public sealed class ObjectToonProfile : ScriptableObject
    {
        public const string ResourcePath = "ObjectToon/ObjectToonProfile";

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("ステージで使っている元の材質（変えない）")]
            public Material original;

            [Tooltip("差し替えるトゥーンの材質（Assets/3DObjectshader/Materials/ に作られる）")]
            public Material toon;

            [Tooltip("オフにすると、この材質は元のまま")]
            public bool enabled = true;

            [Tooltip("オンの間は「共通の設定」の値（色・テクスチャ・鏡以外）に自動で合わせる")]
            public bool useCommon = true;
        }

        /// <summary>1つの物（の材質の枠1つ）だけに使う材質。物はシーンの名前と、シーンの一番上からの名前の道すじで探す。</summary>
        [Serializable]
        public sealed class ObjectOverride
        {
            public string scene;

            [Tooltip("シーンの一番上からの名前の道すじ（同じ名前が並ぶときは「名前#2」のように何番目かを付ける）")]
            public string path;

            [Tooltip("その物の何番目の材質か（0から）")]
            public int slot;

            public Material original;
            public Material toon;
            public bool enabled = true;
        }

        [Tooltip("ゲームを動かしたときにトゥーンにする")]
        public bool enabledInPlay = true;

        [Tooltip("3Dの物を差し替えるシーンの名前（例: Stage1）")]
        public List<string> targetScenes = new List<string> { "Stage1" };

        [Tooltip("3Dの物に配る値（色・テクスチャ・鏡以外）を持つ材質")]
        public Material commonSettings;

        public List<Entry> entries = new List<Entry>();

        [Tooltip("2Dの絵（SpriteRenderer）もトゥーンにする")]
        public bool spriteEnabled = true;

        [Tooltip("2Dの絵を差し替えるシーン。3Dのシーン（targetScenes）のどれかが読み込まれているときだけ差し替える")]
        public List<string> spriteScenes = new List<string> { "Stage1", "Game" };

        [Tooltip("2Dの絵に配る値を持つ材質")]
        public Material spriteCommonSettings;

        public List<Entry> spriteEntries = new List<Entry>();

        public List<ObjectOverride> objectOverrides = new List<ObjectOverride>();

        public bool IsTarget(Scene scene)
        {
            return scene.IsValid() && targetScenes.Contains(scene.name);
        }

        public bool IsSpriteTarget(Scene scene)
        {
            return spriteEnabled && scene.IsValid() && spriteScenes.Contains(scene.name);
        }

        /// <summary>3Dの対象シーンのどれかが読み込まれているか（2Dはそのときだけトゥーンにする）。</summary>
        public bool IsAnyTargetLoaded()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && IsTarget(scene))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>元 → トゥーンの対応表。オフの行は入れない。</summary>
        public static Dictionary<Material, Material> BuildMap(List<Entry> list)
        {
            var map = new Dictionary<Material, Material>();
            foreach (var entry in list)
            {
                if (entry != null && entry.enabled && entry.original != null && entry.toon != null)
                {
                    map[entry.original] = entry.toon;
                }
            }

            return map;
        }

        /// <summary>トゥーン → 元の対応表（オフの行・物ごとの材質も全部）。元に戻すときに使う。</summary>
        public Dictionary<Material, Material> BuildRevertMap()
        {
            var map = new Dictionary<Material, Material>();
            void Add(Material toon, Material original)
            {
                if (toon != null && original != null)
                {
                    map[toon] = original;
                }
            }

            foreach (var entry in entries)
            {
                if (entry != null) Add(entry.toon, entry.original);
            }

            foreach (var entry in spriteEntries)
            {
                if (entry != null) Add(entry.toon, entry.original);
            }

            foreach (var item in objectOverrides)
            {
                if (item != null) Add(item.toon, item.original);
            }

            return map;
        }

        /// <summary>このシーンの物ごとの材質を、道すじごとにまとめる（オンのものだけ）。</summary>
        public Dictionary<string, List<ObjectOverride>> BuildOverrides(string sceneName)
        {
            var result = new Dictionary<string, List<ObjectOverride>>();
            foreach (var item in objectOverrides)
            {
                if (item == null || !item.enabled || item.toon == null || item.scene != sceneName || string.IsNullOrEmpty(item.path))
                {
                    continue;
                }

                if (!result.TryGetValue(item.path, out var list))
                {
                    list = new List<ObjectOverride>();
                    result.Add(item.path, list);
                }

                list.Add(item);
            }

            return result;
        }

        public Entry FindByOriginal(List<Entry> list, Material original)
        {
            return list.Find(e => e != null && e.original == original);
        }

        public Entry FindByToon(Material toon)
        {
            return entries.Find(e => e != null && e.toon == toon) ?? spriteEntries.Find(e => e != null && e.toon == toon);
        }

        public ObjectOverride FindOverride(string sceneName, string path, int slot)
        {
            return objectOverrides.Find(o => o != null && o.scene == sceneName && o.path == path && o.slot == slot);
        }

        public bool IsOverrideMaterial(Material material)
        {
            return material != null && objectOverrides.Exists(o => o != null && o.toon == material);
        }
    }
}
