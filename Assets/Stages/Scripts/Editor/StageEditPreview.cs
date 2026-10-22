using System;
using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MS2026.Stages.EditorTools
{
    /// <summary>
    /// 編集中（Play していないとき）も、Game のコア・砲台 4 台・カメラの視点を、開いているステージの「配置の目印」に合わせて見せる。
    /// コア・砲台の本体は共通の Game にあるので、何もしないと編集画面では Stage1 の位置のまま見える（Play で初めて目印へ動く）。
    /// <b>見せるだけで、Game のシーンファイルは変えない</b>：Game を保存する直前に元の位置・視点へ戻して保存し、保存の後で並べ直す。
    /// ステージのシーンを閉じたときも元に戻す。元の値は SessionState に入れるので、スクリプトの再読み込みをまたいでも戻せる。
    /// </summary>
    [InitializeOnLoad]
    internal static class StageEditPreview
    {
        private const string OriginalsKey = "MS2026.Stages.EditPreview.Originals";

        [Serializable]
        private sealed class Original
        {
            public string id;
            public Vector3 position;
            public Quaternion rotation;
            public string presetGuid;
            public bool isRig;
        }

        [Serializable]
        private sealed class OriginalList
        {
            public List<Original> items = new List<Original>();
        }

        static StageEditPreview()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            // 開いたのがステージでも Game でも、両方そろっていれば並べる（シーンの読み込みが終わってから）。
            EditorApplication.delayCall += PlaceLoaded;
        }

        private static void OnSceneClosing(Scene scene, bool removingScene)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && HasLayout(scene))
            {
                RestoreAll();
            }
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Restore(scene);
            }
        }

        private static void OnSceneSaved(Scene scene)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += PlaceLoaded;
            }
        }

        private static void PlaceLoaded()
        {
            var layout = FindLoadedLayout();
            if (layout != null)
            {
                Place(layout, false);
            }
        }

        private static bool HasLayout(Scene scene)
        {
            return scene.isLoaded && scene.GetRootGameObjects().Any(r => r.GetComponentInChildren<StageLayout>(true) != null);
        }

        /// <summary>開いているステージの配置の目印（ステージのシーンが開いていなければ null）。</summary>
        private static StageLayout FindLoadedLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return null;
            }

            var layouts = Object.FindObjectsByType<StageLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (layouts.Length == 0)
            {
                return null;
            }

            // 2 つ以上開いているときは、アクティブなシーンの物を優先する。
            var active = SceneManager.GetActiveScene();
            return layouts.FirstOrDefault(l => l.gameObject.scene == active) ?? layouts[0];
        }

        /// <summary>
        /// Game のコア・砲台を目印の位置・向きへ、カメラを目印の視点へ動かして見せる（Game のファイルには保存されない）。
        /// 動かせたら true。Game が開いていない（コア・砲台が無い）ときは何もしない。
        /// </summary>
        public static bool Place(StageLayout layout, bool log)
        {
            var core = Object.FindObjectsByType<CoreCrystalController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.gameObject.scene != layout.gameObject.scene);
            var turrets = Object.FindObjectsByType<LaserTurret>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.gameObject.scene != layout.gameObject.scene).ToArray();
            if (core == null && turrets.Length == 0)
            {
                return false;
            }

            var originals = LoadOriginals();
            var moved = 0;
            if (core != null && layout.coreAnchor != null && !SamePlace(core.transform, layout.coreAnchor, false))
            {
                Remember(originals, core.transform);
                var p = layout.coreAnchor.position;
                core.transform.position = new Vector3(p.x, p.y, core.transform.position.z);
                moved++;
            }

            foreach (var turret in turrets)
            {
                var anchor = layout.AnchorFor(turret.playerIndex);
                if (anchor == null || SamePlace(turret.transform, anchor, true))
                {
                    continue;
                }

                Remember(originals, turret.transform);
                var p = anchor.position;
                turret.transform.SetPositionAndRotation(
                    new Vector3(p.x, p.y, turret.transform.position.z),
                    Quaternion.Euler(0f, 0f, anchor.eulerAngles.z));
                moved++;
            }

            var rig = Object.FindObjectsByType<FortressCameraRig>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (rig != null && layout.cameraPreset != null && rig.preset != layout.cameraPreset)
            {
                RememberRig(originals, rig);
                rig.preset = layout.cameraPreset;
                moved++;
            }

            SaveOriginals(originals);
            if (moved > 0)
            {
                SceneView.RepaintAll();
            }

            if (moved > 0 || log)
            {
                Debug.Log($"[Stage] 編集画面のコア・砲台・カメラを「{layout.gameObject.scene.name}」の配置の目印に合わせて見せています（Game のファイルは変わりません）。");
            }

            return true;
        }

        /// <summary>動かした物を全部元に戻す。</summary>
        public static void RestoreAll()
        {
            var originals = LoadOriginals();
            foreach (var original in originals.items)
            {
                Apply(original);
            }

            originals.items.Clear();
            SaveOriginals(originals);
            SceneView.RepaintAll();
        }

        /// <summary>そのシーンの中で動かした物だけ元に戻す（保存の直前）。保存の後に並べ直す。</summary>
        private static void Restore(Scene scene)
        {
            var originals = LoadOriginals();
            var remaining = new List<Original>();
            foreach (var original in originals.items)
            {
                var target = Resolve(original.id);
                if (target is Component component && component.gameObject.scene == scene)
                {
                    Apply(original);
                }
                else
                {
                    remaining.Add(original);
                }
            }

            if (remaining.Count != originals.items.Count)
            {
                originals.items = remaining;
                SaveOriginals(originals);
            }
        }

        private static void Apply(Original original)
        {
            var target = Resolve(original.id);
            if (original.isRig && target is FortressCameraRig rig)
            {
                rig.preset = AssetDatabase.LoadAssetAtPath<CameraViewPreset>(AssetDatabase.GUIDToAssetPath(original.presetGuid));
            }
            else if (target is Transform transform)
            {
                transform.SetPositionAndRotation(original.position, original.rotation);
            }
        }

        private static void Remember(OriginalList originals, Transform transform)
        {
            var id = GlobalObjectId.GetGlobalObjectIdSlow(transform).ToString();
            if (originals.items.All(o => o.id != id))
            {
                originals.items.Add(new Original { id = id, position = transform.position, rotation = transform.rotation });
            }
        }

        private static void RememberRig(OriginalList originals, FortressCameraRig rig)
        {
            var id = GlobalObjectId.GetGlobalObjectIdSlow(rig).ToString();
            if (originals.items.All(o => o.id != id))
            {
                var guid = rig.preset != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(rig.preset)) : "";
                originals.items.Add(new Original { id = id, isRig = true, presetGuid = guid });
            }
        }

        private static Object Resolve(string id)
        {
            return GlobalObjectId.TryParse(id, out var globalId) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) : null;
        }

        private static OriginalList LoadOriginals()
        {
            var json = SessionState.GetString(OriginalsKey, "");
            return string.IsNullOrEmpty(json) ? new OriginalList() : JsonUtility.FromJson<OriginalList>(json) ?? new OriginalList();
        }

        private static void SaveOriginals(OriginalList originals)
        {
            SessionState.SetString(OriginalsKey, JsonUtility.ToJson(originals));
        }

        private static bool SamePlace(Transform piece, Transform anchor, bool rotation)
        {
            var a = piece.position;
            var b = anchor.position;
            if (new Vector2(a.x - b.x, a.y - b.y).sqrMagnitude > 1e-6f)
            {
                return false;
            }

            return !rotation || Mathf.Abs(Mathf.DeltaAngle(piece.eulerAngles.z, anchor.eulerAngles.z)) < 0.01f;
        }
    }
}
