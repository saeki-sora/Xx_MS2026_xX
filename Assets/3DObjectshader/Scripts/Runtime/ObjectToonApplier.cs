using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.ObjectToon
{
    /// <summary>
    /// 対象のシーン（ObjectToonProfile.targetScenes / spriteScenes）が読み込まれたら、その中の物の材質をトゥーンに差し替える。
    /// どの物にも部品を付けず、シーン・Prefab も変えない（他の人の作業とぶつからないように）。
    /// 見た目だけの処理なので通信はしない（各PCが自分で差し替える）。
    /// </summary>
    public static class ObjectToonApplier
    {
        private static ObjectToonProfile profile;
        private static readonly List<Renderer> RendererBuffer = new List<Renderer>();
        private static readonly List<GameObject> RootBuffer = new List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            profile = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            profile = Resources.Load<ObjectToonProfile>(ObjectToonProfile.ResourcePath);
            if (profile == null || !profile.enabledInPlay)
            {
                profile = null;
                return;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        // エディタで複数のシーンを開いたまま再生したときは、最初から読み込まれているシーンにも当てる（2回当てても変わらない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyToLoadedScenes()
        {
            if (profile != null)
            {
                ApplyAllLoaded(profile);
            }
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (profile == null)
            {
                return;
            }

            // 3Dのステージが来たら、先に読み込まれていた Game の2Dの絵もここで差し替える。
            if (profile.IsTarget(scene))
            {
                ApplyAllLoaded(profile);
            }
            else
            {
                Apply(scene, profile);
            }
        }

        /// <summary>
        /// 後から出した物（実行中に Instantiate した背景など）もトゥーンにしたいときに呼ぶ。
        /// 材質ごとの表だけを使う（物ごとの個別の材質は、シーンに置いてある物だけ）。差し替えた材質の数を返す。
        /// </summary>
        public static int ApplyTo(GameObject root)
        {
            if (profile == null || root == null)
            {
                return 0;
            }

            return Swap(root, ObjectToonProfile.BuildMap(profile.entries), ObjectToonProfile.BuildMap(profile.spriteEntries), null);
        }

        public static int ApplyAllLoaded(ObjectToonProfile source)
        {
            int count = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    count += Apply(scene, source);
                }
            }

            return count;
        }

        /// <summary>シーンの物をトゥーンにする（対象でないシーンは何もしない）。差し替えた材質の数を返す。</summary>
        public static int Apply(Scene scene, ObjectToonProfile source)
        {
            if (source == null || !scene.IsValid() || !scene.isLoaded)
            {
                return 0;
            }

            // 物ごとの材質も、対象のシーンのときだけ使う（ほかのステージで Game の絵だけトゥーンになったりしないように）。
            bool props = source.IsTarget(scene);
            bool sprites = source.IsSpriteTarget(scene) && source.IsAnyTargetLoaded();
            if (!props && !sprites)
            {
                return 0;
            }

            var overrides = source.BuildOverrides(scene.name);

            var meshMap = props ? ObjectToonProfile.BuildMap(source.entries) : null;
            var spriteMap = sprites ? ObjectToonProfile.BuildMap(source.spriteEntries) : null;
            int count = 0;
            scene.GetRootGameObjects(RootBuffer);
            foreach (var root in RootBuffer)
            {
                count += Swap(root, meshMap, spriteMap, overrides);
            }

            RootBuffer.Clear();
            return count;
        }

        /// <summary>シーンの物を元の材質に戻す（オフにした行・物ごとの材質も戻す）。</summary>
        public static int Revert(Scene scene, ObjectToonProfile source)
        {
            if (source == null || !scene.IsValid() || !scene.isLoaded)
            {
                return 0;
            }

            var map = source.BuildRevertMap();
            int count = 0;
            scene.GetRootGameObjects(RootBuffer);
            foreach (var root in RootBuffer)
            {
                count += Swap(root, map, map, null);
            }

            RootBuffer.Clear();
            return count;
        }

        /// <summary>
        /// root の下の物の材質を差し替える。3Dの物（MeshRenderer / SkinnedMeshRenderer）は meshMap、2Dの絵（SpriteRenderer）は spriteMap。
        /// overrides があれば、道すじが合う物の枠はそちらを優先する。
        /// </summary>
        public static int Swap(GameObject root, Dictionary<Material, Material> meshMap, Dictionary<Material, Material> spriteMap,
            Dictionary<string, List<ObjectToonProfile.ObjectOverride>> overrides)
        {
            int count = 0;
            root.GetComponentsInChildren(true, RendererBuffer);
            foreach (var renderer in RendererBuffer)
            {
                Dictionary<Material, Material> map = null;
                if (renderer is SpriteRenderer)
                {
                    map = spriteMap;
                }
                else if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                {
                    map = meshMap;
                }
                else
                {
                    continue;
                }

                List<ObjectToonProfile.ObjectOverride> own = null;
                if (overrides != null && overrides.Count > 0)
                {
                    overrides.TryGetValue(GetPath(renderer.transform), out own);
                }

                if ((map == null || map.Count == 0) && own == null)
                {
                    continue;
                }

                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var current = materials[i];
                    if (current == null)
                    {
                        continue;
                    }

                    var replacement = FindOverride(own, i, current, map);
                    if (replacement == null && map != null)
                    {
                        map.TryGetValue(current, out replacement);
                    }

                    if (replacement != null && replacement != current)
                    {
                        materials[i] = replacement;
                        changed = true;
                        count++;
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }

            RendererBuffer.Clear();
            return count;
        }

        // 物ごとの材質は、その枠が「元の材質」か「その材質のトゥーン」のときだけ使う（別の材質に付け替えられた物には当てない）。
        private static Material FindOverride(List<ObjectToonProfile.ObjectOverride> own, int slot, Material current,
            Dictionary<Material, Material> map)
        {
            if (own == null)
            {
                return null;
            }

            foreach (var item in own)
            {
                if (item.slot != slot)
                {
                    continue;
                }

                bool matches = current == item.original || current == item.toon ||
                               (map != null && item.original != null && map.TryGetValue(item.original, out var mapped) && mapped == current);
                return matches ? item.toon : null;
            }

            return null;
        }

        /// <summary>シーンの一番上からの名前の道すじ。同じ名前の兄弟が先にあれば「名前#2」のように番号を付ける。</summary>
        public static string GetPath(Transform target)
        {
            var parts = new List<string>();
            for (var t = target; t != null; t = t.parent)
            {
                parts.Add(Segment(t));
            }

            var builder = new StringBuilder();
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                builder.Append(parts[i]);
                if (i > 0)
                {
                    builder.Append('/');
                }
            }

            return builder.ToString();
        }

        private static string Segment(Transform t)
        {
            int index = 0;
            if (t.parent != null)
            {
                int sibling = t.GetSiblingIndex();
                for (int i = 0; i < sibling; i++)
                {
                    if (t.parent.GetChild(i).name == t.name)
                    {
                        index++;
                    }
                }
            }
            else
            {
                foreach (var root in t.gameObject.scene.GetRootGameObjects())
                {
                    if (root.transform == t)
                    {
                        break;
                    }

                    if (root.name == t.name)
                    {
                        index++;
                    }
                }
            }

            return index == 0 ? t.name : t.name + "#" + (index + 1);
        }
    }
}
