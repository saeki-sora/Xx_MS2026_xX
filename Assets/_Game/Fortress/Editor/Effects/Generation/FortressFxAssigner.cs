using System;
using System.Collections.Generic;
using System.Reflection;
using DDrive.Foundation.Identity;
using DDrive.Runtime.Audio;
using DDrive.Runtime.Vfx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 生成した素材（D-Drive が自動登録した VFX / SE / BGM）の設定を整え、ゲームの演出欄へ割り当てる。
    /// 空いている欄だけ入れる（入力済みは上書きしない。ただし発射音が初期の和太鼓のままなら置き換える）。
    /// D-Drive のデータ（.asset）はコードで新規作成せず、登録済みのものを探して設定だけ変える。
    /// </summary>
    public static class FortressFxAssigner
    {
        private const string GameDataRoot = "Assets/_Game/DDrive/GameData";
        private static readonly List<string> Problems = new List<string>();

        private sealed class Ids
        {
            public readonly Dictionary<string, ulong> vfx = new Dictionary<string, ulong>();
            public readonly Dictionary<string, ulong> se = new Dictionary<string, ulong>();
            public ulong bgm;
            public ulong oldDrumSe;
        }

        [MenuItem("Tools/要塞/演出素材/2 演出欄へ割り当て", priority = 101)]
        public static void AssignAll()
        {
            Problems.Clear();
            var ids = FindIds(out var missing);
            if (missing.Count > 0)
            {
                EditorUtility.DisplayDialog("演出素材", "まだ D-Drive に登録されていない素材があります。\n" +
                    "1「素材を生成」のあと、Unity のインポートと D-Drive の自動登録が終わるのを待ってから、もう一度実行してください。\n\n" +
                    "見つからない物:\n" + string.Join("\n", missing.GetRange(0, Mathf.Min(10, missing.Count))), "OK");
                return;
            }

            ConfigureData(ids);
            int slots = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:LaserTuningConfig"))
            {
                var cfg = AssetDatabase.LoadAssetAtPath<LaserTuningConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (cfg == null) continue;
                Undo.RecordObject(cfg, "Assign laser effects");
                slots += FillLaser(cfg.effects, ids);
                EditorUtility.SetDirty(cfg);
            }

            foreach (var guid in AssetDatabase.FindAssets("t:DestructiblePreset"))
            {
                var preset = AssetDatabase.LoadAssetAtPath<DestructiblePreset>(AssetDatabase.GUIDToAssetPath(guid));
                if (preset == null) continue;
                Undo.RecordObject(preset, "Assign destructible effects");
                slots += FillDestructible(preset.settings.feedback, ids);
                EditorUtility.SetDirty(preset);
            }

            // プレハブ（破壊可能物・スマッシュボール）
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;
                int n = FillComponents(root.GetComponentsInChildren<DestructibleObstacle>(true),
                    root.GetComponentsInChildren<SmashBallModule>(true), ids, isPrefabAsset: true);
                if (n > 0)
                {
                    slots += n;
                    EditorUtility.SetDirty(root);
                }
            }

            // シーン（ビルド設定のシーン。BGM用オブジェクトも置く）
            int scenes = 0;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                var opened = new List<string>();
                foreach (var s in EditorBuildSettings.scenes)
                {
                    if (!s.enabled || string.IsNullOrEmpty(s.path)) continue;
                    var scene = SceneManager.GetSceneByPath(s.path);
                    bool wasLoaded = scene.isLoaded;
                    if (!wasLoaded)
                    {
                        scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Additive);
                        opened.Add(s.path);
                    }

                    slots += ProcessScene(scene, ids, s.path.EndsWith("Game.unity"));
                    EditorSceneManager.SaveScene(scene);
                    scenes++;
                    if (!wasLoaded)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[FortressFx] 演出欄 " + slots + " か所に割り当てました（シーン " + scenes + " 個を処理）。");
            if (Problems.Count > 0)
            {
                Debug.LogWarning("[FortressFx] 設定できなかった項目があります。D-Drive の VFX/SE Editor で手動設定してください:\n" +
                                 string.Join("\n", Problems));
            }
        }

        // ───────── 登録済みデータの検索 ─────────

        private static Ids FindIds(out List<string> missing)
        {
            var ids = new Ids();
            missing = new List<string>();
            foreach (var n in FortressFxGenerator.VfxNames)
            {
                if (TryGetId(FindData("VFX_", n), out var id)) ids.vfx[n] = id; else missing.Add("VFX " + n);
            }

            foreach (var n in FortressFxGenerator.SeNames)
            {
                if (TryGetId(FindData("SE_", n), out var id)) ids.se[n] = id; else missing.Add("SE " + n);
            }

            if (TryGetId(FindData("BGM_", FortressFxGenerator.BgmName), out var bgm)) ids.bgm = bgm; else missing.Add("BGM " + FortressFxGenerator.BgmName);

            // 初期の和太鼓SE（ファイル名が日本語のため「SE_Se」）
            var drum = AssetDatabase.FindAssets("SE_Se t:ScriptableObject", new[] { GameDataRoot });
            foreach (var g in drum)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == "SE_Se"
                    && TryGetId(AssetDatabase.LoadAssetAtPath<ScriptableObject>(p), out var did))
                {
                    ids.oldDrumSe = did;
                }
            }

            return ids;
        }

        /// <summary>GameData から「PREFIX_(カテゴリ_)名前」のデータを探す。カテゴリ（Fortress）は付いていても付いていなくてもよい。</summary>
        private static ScriptableObject FindData(string prefix, string identifier)
        {
            foreach (var g in AssetDatabase.FindAssets(identifier + " t:ScriptableObject", new[] { GameDataRoot }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var file = System.IO.Path.GetFileNameWithoutExtension(p);
                if (file.StartsWith(prefix, StringComparison.Ordinal) && file.EndsWith("_" + identifier, StringComparison.Ordinal))
                {
                    return AssetDatabase.LoadAssetAtPath<ScriptableObject>(p);
                }
            }

            return null;
        }

        private static bool TryGetId(UnityEngine.Object data, out ulong id)
        {
            id = 0;
            if (data == null) return false;
            var t = data.GetType();
            for (; t != null && t != typeof(object); t = t.BaseType)
            {
                var prop = t.GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null && prop.PropertyType == typeof(ulong))
                {
                    id = (ulong)prop.GetValue(data);
                    return id != 0;
                }

                foreach (var name in new[] { "Id", "id", "_id", "<Id>k__BackingField" })
                {
                    var field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (field != null && field.FieldType == typeof(ulong))
                    {
                        id = (ulong)field.GetValue(data);
                        return id != 0;
                    }
                }
            }

            return false;
        }

        // ───────── D-Drive データの設定 ─────────

        private static void ConfigureData(Ids ids)
        {
            foreach (var n in FortressFxGenerator.VfxNames)
            {
                var data = FindData("VFX_", n);
                if (data == null) continue;
                var so = new SerializedObject(data);
                bool loop = n == "LaserMuzzle" || n == "LaserImpact";
                SetEnum(so, "LifeMode", loop ? "Loop" : "OneShot", n);
                if (!loop) SetFloatIfPresent(so, "FadeOutSec", 0f);
                SetEnum(so, "Flags.Pool.Kind", "Pooled", n);
                SetInt(so, "Flags.Pool.MaxCount", loop ? 8 : (n == "SwarmHit" ? 32 : 12), n);
                SetEnum(so, "Flags.Net", "Local", n);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }

            foreach (var n in FortressFxGenerator.SeNames)
            {
                var data = FindData("SE_", n);
                if (data == null) continue;
                var so = new SerializedObject(data);
                SetFloatIfPresent(so, "Volume", n == "SwarmHit" ? 0.5f : (n == "SmashBreak" ? 1f : 0.8f));
                if (n == "SwarmHit") SetFloatIfPresent(so, "CooldownSec", 0.05f);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }

            var bgm = FindData("BGM_", FortressFxGenerator.BgmName);
            if (bgm != null)
            {
                var so = new SerializedObject(bgm);
                SetFloatIfPresent(so, "Volume", 0.6f);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bgm);
            }
        }

        private static SerializedProperty Find(SerializedObject so, string path)
        {
            var parts = path.Split('.');
            SerializedProperty cur = null;
            foreach (var part in parts)
            {
                SerializedProperty next = null;
                foreach (var cand in Variants(part))
                {
                    next = cur == null ? so.FindProperty(cand) : cur.FindPropertyRelative(cand);
                    if (next != null) break;
                }

                if (next == null) return null;
                cur = next;
            }

            return cur;
        }

        private static IEnumerable<string> Variants(string name)
        {
            var lower = char.ToLowerInvariant(name[0]) + name.Substring(1);
            yield return name;
            yield return lower;
            yield return "_" + lower;
            yield return "m_" + name;
        }

        private static void SetEnum(SerializedObject so, string path, string value, string owner)
        {
            var p = Find(so, path);
            if (p == null || p.propertyType != SerializedPropertyType.Enum)
            {
                Problems.Add(owner + ": " + path + " が見つかりません（" + value + " にしてください）");
                return;
            }

            for (int i = 0; i < p.enumNames.Length; i++)
            {
                if (string.Equals(p.enumNames[i].Replace(" ", ""), value, StringComparison.OrdinalIgnoreCase))
                {
                    p.enumValueIndex = i;
                    return;
                }
            }

            Problems.Add(owner + ": " + path + " に選択肢 " + value + " がありません");
        }

        private static void SetInt(SerializedObject so, string path, int value, string owner)
        {
            var p = Find(so, path);
            if (p == null || p.propertyType != SerializedPropertyType.Integer)
            {
                Problems.Add(owner + ": " + path + " が見つかりません（" + value + " にしてください）");
                return;
            }

            p.intValue = value;
        }

        private static void SetFloatIfPresent(SerializedObject so, string path, float value)
        {
            var p = Find(so, path);
            if (p != null && p.propertyType == SerializedPropertyType.Float)
            {
                p.floatValue = value;
            }
        }

        // ───────── 演出欄への割り当て ─────────

        private static T MakeId<T>(ulong value, string typeName) where T : struct
        {
            object boxed = default(T);
            foreach (var f in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (f.FieldType == typeof(ulong))
                {
                    f.SetValue(boxed, value);
                }
                else if (f.FieldType.IsEnum && f.FieldType.Name == "AssetType")
                {
                    f.SetValue(boxed, Enum.Parse(f.FieldType, typeName));
                }
            }

            return (T)boxed;
        }

        private static bool SetVfx(FortressEffect e, Ids ids, string name)
        {
            if (e == null || e.HasVfx || !ids.vfx.TryGetValue(name, out var id)) return false;
            e.vfx = MakeId<AssetId<VfxMarker>>(id, "Vfx");
            return true;
        }

        private static bool SetSe(FortressEffect e, Ids ids, string name, bool replaceOldDrum = false)
        {
            if (e == null || !ids.se.TryGetValue(name, out var id)) return false;
            if (e.HasSe && !(replaceOldDrum && ids.oldDrumSe != 0 && e.se.Value == ids.oldDrumSe)) return false;
            e.se = MakeId<AssetId<SeMarker>>(id, "Se");
            return true;
        }

        private static int Both(FortressEffect e, Ids ids, string name, bool replaceOldDrum = false)
        {
            if (e == null) return 0;
            int n = 0;
            if (SetVfx(e, ids, name)) n++;
            if (SetSe(e, ids, name, replaceOldDrum)) n++;
            return n;
        }

        private static int FillLaser(LaserEffectSettings s, Ids ids)
        {
            if (s == null) return 0;
            return Both(s.muzzle, ids, "LaserMuzzle", replaceOldDrum: true)
                   + Both(s.impact, ids, "LaserImpact")
                   + Both(s.swarmHit, ids, "SwarmHit");
        }

        private static int FillDestructible(DestructibleFeedbackSettings f, Ids ids, bool includeDestroyed = true)
        {
            if (f == null) return 0;
            return Both(f.onHit, ids, "DestructHit")
                   + Both(f.onStageChanged, ids, "DestructStage")
                   + (includeDestroyed ? Both(f.onDestroyed, ids, "DestructBreak") : 0)
                   + Both(f.onRegenerated, ids, "DestructRegen");
        }

        private static int FillComponents(DestructibleObstacle[] obstacles, SmashBallModule[] smashBalls, Ids ids, bool isPrefabAsset)
        {
            int n = 0;
            foreach (var o in obstacles)
            {
                if (o == null) continue;
                // スマッシュボール本体の「壊れた」演出は onBroken に任せる（二重に鳴らさない）
                bool isSmash = o.GetComponent<SmashBallModule>() != null;
                if (!isPrefabAsset) Undo.RecordObject(o, "Assign destructible effects");
                int added = FillDestructible(o.settings.feedback, ids, includeDestroyed: !isSmash);
                if (added > 0)
                {
                    n += added;
                    EditorUtility.SetDirty(o);
                    if (!isPrefabAsset) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
                }
            }

            foreach (var b in smashBalls)
            {
                if (b == null) continue;
                if (!isPrefabAsset) Undo.RecordObject(b, "Assign smash ball effects");
                int added = Both(b.settings.onBroken, ids, "SmashBreak");
                if (added > 0)
                {
                    n += added;
                    EditorUtility.SetDirty(b);
                    if (!isPrefabAsset) PrefabUtility.RecordPrefabInstancePropertyModifications(b);
                }
            }

            return n;
        }

        private static int ProcessScene(Scene scene, Ids ids, bool placeBgm)
        {
            int n = 0;
            var obstacles = new List<DestructibleObstacle>();
            var balls = new List<SmashBallModule>();
            foreach (var root in scene.GetRootGameObjects())
            {
                obstacles.AddRange(root.GetComponentsInChildren<DestructibleObstacle>(true));
                balls.AddRange(root.GetComponentsInChildren<SmashBallModule>(true));
            }

            n += FillComponents(obstacles.ToArray(), balls.ToArray(), ids, isPrefabAsset: false);

            if (placeBgm && ids.bgm != 0)
            {
                FortressBgmPlayer player = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    player = root.GetComponentInChildren<FortressBgmPlayer>(true);
                    if (player != null) break;
                }

                if (player == null)
                {
                    var go = new GameObject("[Fortress] Bgm");
                    SceneManager.MoveGameObjectToScene(go, scene);
                    player = go.AddComponent<FortressBgmPlayer>();
                    Undo.RegisterCreatedObjectUndo(go, "Create BGM player");
                }

                if (!player.bgm.IsValid)
                {
                    Undo.RecordObject(player, "Assign BGM");
                    player.bgm = MakeId<AssetId<BgmMarker>>(ids.bgm, "Bgm");
                    EditorUtility.SetDirty(player);
                    n++;
                }
            }

            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            return n;
        }
    }
}
