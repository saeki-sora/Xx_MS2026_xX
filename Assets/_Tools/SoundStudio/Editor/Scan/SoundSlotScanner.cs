using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DDrive.Foundation.Identity;
using DDrive.Runtime.Audio;
using MS2026.Fortress;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.SoundStudio
{
    /// <summary>
    /// プロジェクトの音の欄を全部集める。設定ファイル（Assets の下の ScriptableObject）・Prefab・ビルドに入るシーンを調べる。
    /// 時間がかかるので、窓を開いたときと［調べ直す］のときだけ呼ぶ。
    /// 開いていないシーンは裏で開いて読み、読んだら閉じる（ユーザーが開いているシーンは閉じない）。Play 中は開いていないシーンは読まない。
    /// </summary>
    public static class SoundSlotScanner
    {
        /// <summary>最後の調べで、Play 中のため読めなかったシーンがあったか。</summary>
        public static bool LastScanSkippedScenes { get; private set; }

        public static List<SoundSlot> ScanAll()
        {
            var slots = new List<SoundSlot>();
            try
            {
                ScanAssets(slots);
                ScanPrefabs(slots);
                ScanScenes(slots);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            foreach (var candidate in SoundCatalog.Candidates)
            {
                slots.Add(new SoundSlot
                {
                    Kind = SoundSlotKind.Candidate,
                    Place = SoundSlotPlace.None,
                    Candidate = candidate,
                    Page = candidate.Page,
                    Group = candidate.Group,
                    Label = candidate.Label,
                    Personal = candidate.Personal,
                    Voice = candidate.Voice,
                });
            }

            return slots;
        }

        /// <summary>1つの物の中の欄を、行にして足す（シーンを開いたまま使う所からも呼ぶ）。</summary>
        public static void AddSlotsOf(Object owner, SoundSlotPlace place, string assetPath, List<SoundSlot> into)
        {
            if (owner == null || !SlotPathFinder.MayHoldSlots(owner.GetType()))
            {
                return;
            }

            var found = SlotPathFinder.FindSlots(owner);
            if (found.Count == 0)
            {
                return;
            }

            var globalId = place == SoundSlotPlace.Asset ? null : UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(owner).ToString();
            foreach (var f in found)
            {
                into.Add(MakeSlot(owner, place, assetPath, globalId, f));
            }
        }

        private static SoundSlot MakeSlot(Object owner, SoundSlotPlace place, string assetPath, string globalId, FoundSlot f)
        {
            var entry = SoundCatalog.Find(f.DeclaringType.FullName, f.Field.Name);
            var tooltip = f.Field.GetCustomAttribute<TooltipAttribute>()?.tooltip;
            var hasSound = f.IsBgm ? f.Value is AssetId<BgmMarker> bgm && bgm.IsValid : f.Value is FortressEffect e && e.HasSe;
            return new SoundSlot
            {
                Kind = f.IsBgm ? SoundSlotKind.Bgm : SoundSlotKind.Effect,
                Place = place,
                Entry = entry,
                Owner = owner,
                AssetPath = assetPath,
                GlobalObjectId = globalId,
                PropertyPath = f.Path,
                DeclaringType = f.DeclaringType,
                FieldName = f.Field.Name,
                OwnerName = OwnerName(owner, place),
                Tooltip = tooltip,
                Page = entry?.Page ?? SoundPageId.Other,
                Group = entry?.Group ?? ObjectNames.NicifyVariableName(f.DeclaringType.Name),
                Label = entry?.Label ?? FallbackLabel(tooltip, f.Field.Name),
                Personal = entry?.Personal ?? false,
                Voice = entry?.Voice ?? false,
                Value = f.Value,
                HasSound = hasSound,
            };
        }

        private static string OwnerName(Object owner, SoundSlotPlace place)
        {
            if (owner is Component component)
            {
                return place == SoundSlotPlace.Scene ? component.gameObject.scene.name + " › " + component.gameObject.name : component.gameObject.name;
            }

            return owner.name;
        }

        private static string FallbackLabel(string tooltip, string fieldName)
        {
            if (string.IsNullOrEmpty(tooltip))
            {
                return ObjectNames.NicifyVariableName(fieldName);
            }

            // 説明の最初の文だけを名前にする。
            var end = tooltip.IndexOfAny(new[] { '。', '（', '(' });
            return end > 0 ? tooltip.Substring(0, end) : tooltip;
        }

        private static void ScanAssets(List<SoundSlot> slots)
        {
            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (!SlotPathFinder.MayHoldSlots(type))
                {
                    continue;
                }

                if (i % 50 == 0)
                {
                    EditorUtility.DisplayProgressBar("サウンドスタジオ", "設定ファイルを調べています…", (float)i / guids.Length * 0.4f);
                }

                AddSlotsOf(AssetDatabase.LoadMainAssetAtPath(path), SoundSlotPlace.Asset, path, slots);
            }
        }

        private static void ScanPrefabs(List<SoundSlot> slots)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (i % 50 == 0)
                {
                    EditorUtility.DisplayProgressBar("サウンドスタジオ", "Prefab を調べています…", 0.4f + (float)i / guids.Length * 0.3f);
                }

                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                {
                    continue;
                }

                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    AddSlotsOf(behaviour, SoundSlotPlace.Prefab, path, slots);
                }
            }
        }

        private static void ScanScenes(List<SoundSlot> slots)
        {
            LastScanSkippedScenes = false;
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled && !string.IsNullOrEmpty(s.path)).Select(s => s.path).ToList();
            for (var i = 0; i < scenes.Count; i++)
            {
                var path = scenes[i];
                EditorUtility.DisplayProgressBar("サウンドスタジオ", "シーンを調べています: " + path, 0.7f + (float)i / scenes.Count * 0.3f);
                var scene = SceneManager.GetSceneByPath(path);
                var openedHere = false;
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        LastScanSkippedScenes = true;
                        continue;
                    }

                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    openedHere = true;
                }

                try
                {
                    AddSceneSlots(scene, path, slots);
                }
                finally
                {
                    if (openedHere)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
        }

        /// <summary>開いているシーンの中の欄を足す。</summary>
        public static void AddSceneSlots(Scene scene, string path, List<SoundSlot> slots)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    // Prefab から置いた部品で、欄が Prefab のまま（上書きなし）のものも、シーンの欄として出す（シーンで変えられるため）。
                    AddSlotsOf(behaviour, SoundSlotPlace.Scene, path, slots);
                }
            }
        }
    }
}
