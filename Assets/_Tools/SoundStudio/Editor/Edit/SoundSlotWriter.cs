using System;
using DDrive.Foundation.Identity;
using DDrive.Runtime.Audio;
using MS2026.Fortress;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.SoundStudio
{
    public enum SoundWriteResult
    {
        /// <summary>変えて保存した。</summary>
        Saved,

        /// <summary>開いているシーンの中で変えた。シーンの保存はユーザーがする。</summary>
        SaveSceneNeeded,

        /// <summary>Play 中なので、シーンの欄は変えなかった（止めると消えるため）。</summary>
        Locked,

        /// <summary>欄が見つからなかった（ファイルが消えた・名前が変わったなど）。</summary>
        Failed,
    }

    /// <summary>
    /// 欄を書き換えて保存する係。設定ファイル・Prefab はその場で保存する（Undo で戻せる）。
    /// シーンの欄は、そのシーンが開いていればその場で変えて「保存してください」を返し、開いていなければ裏で開いて変え、保存して閉じる。
    /// ユーザーが開いているシーンは、勝手に保存も閉じもしない。
    /// </summary>
    public static class SoundSlotWriter
    {
        public static SoundWriteResult SetSe(SoundSlot slot, AssetId<SeMarker> id)
        {
            return Edit(slot, "音を入れる", p => WriteId(p.FindPropertyRelative("se"), id.Value, AssetType.Se));
        }

        public static SoundWriteResult SetBgm(SoundSlot slot, AssetId<BgmMarker> id)
        {
            return Edit(slot, "BGM を入れる", p => WriteId(p, id.Value, AssetType.Bgm));
        }

        public static SoundWriteResult SetVolumeScale(SoundSlot slot, float scale)
        {
            return Edit(slot, "この場所の音量", p => p.FindPropertyRelative(nameof(FortressEffect.volumeScale)).floatValue = Mathf.Clamp01(scale));
        }

        public static SoundWriteResult SetAudience(SoundSlot slot, SoundAudience audience)
        {
            return Edit(slot, "鳴らすPC", p => p.FindPropertyRelative(nameof(FortressEffect.audience)).intValue = (int)audience);
        }

        /// <summary>この場所の設定（倍率・鳴らすPC）を初期値に戻す。音そのものは外さない。</summary>
        public static SoundWriteResult ResetPlace(SoundSlot slot)
        {
            return Edit(slot, "初期値に戻す", p =>
            {
                p.FindPropertyRelative(nameof(FortressEffect.volumeScale)).floatValue = 1f;
                p.FindPropertyRelative(nameof(FortressEffect.audience)).intValue = (int)SoundAudience.Auto;
            });
        }

        /// <summary>「声かどうか」だけを名前の表に合わせる（違うときだけ書く）。</summary>
        public static SoundWriteResult SyncVoice(SoundSlot slot)
        {
            return Edit(slot, "声の印", _ => { });
        }

        /// <summary>この場所の設定が初期値から変わっているか（印を付ける）。</summary>
        public static bool IsChanged(SoundSlot slot)
        {
            return slot != null && slot.Value is FortressEffect effect
                                && (!Mathf.Approximately(effect.volumeScale, 1f) || effect.audience != SoundAudience.Auto);
        }

        private static void WriteId(SerializedProperty idProperty, ulong value, AssetType type)
        {
            idProperty.FindPropertyRelative("value").ulongValue = value;
            idProperty.FindPropertyRelative("type").intValue = value == 0 ? 0 : (int)type;
        }

        private static SoundWriteResult Edit(SoundSlot slot, string undoName, Action<SerializedProperty> change)
        {
            if (slot == null || slot.Kind == SoundSlotKind.Candidate)
            {
                return SoundWriteResult.Failed;
            }

            if (slot.Place != SoundSlotPlace.Scene)
            {
                var target = slot.Owner != null ? slot.Owner : ResolveAsset(slot);
                if (target == null || !Apply(slot, target, undoName, change))
                {
                    return SoundWriteResult.Failed;
                }

                if (!string.IsNullOrEmpty(slot.AssetPath))
                {
                    AssetDatabase.SaveAssetIfDirty(target);
                }

                return SoundWriteResult.Saved;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return SoundWriteResult.Locked;
            }

            var scene = SceneManager.GetSceneByPath(slot.AssetPath);
            if (scene.IsValid() && scene.isLoaded)
            {
                var target = ResolveSceneObject(slot);
                if (target == null || !Apply(slot, target, undoName, change))
                {
                    return SoundWriteResult.Failed;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                return SoundWriteResult.SaveSceneNeeded;
            }

            // このツールが開いたシーンだけを、保存して閉じる。
            scene = EditorSceneManager.OpenScene(slot.AssetPath, OpenSceneMode.Additive);
            try
            {
                var target = ResolveSceneObject(slot);
                if (target == null || !Apply(slot, target, undoName, change))
                {
                    return SoundWriteResult.Failed;
                }

                EditorSceneManager.SaveScene(scene);
                return SoundWriteResult.Saved;
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                slot.Owner = null;
            }
        }

        private static bool Apply(SoundSlot slot, UnityEngine.Object target, string undoName, Action<SerializedProperty> change)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(slot.PropertyPath);
            if (property == null)
            {
                return false;
            }

            Undo.RecordObject(target, "サウンドスタジオ: " + undoName);
            change(property);
            if (slot.Kind == SoundSlotKind.Effect)
            {
                var voice = property.FindPropertyRelative(nameof(FortressEffect.isVoice));
                if (voice != null && voice.boolValue != slot.Voice)
                {
                    voice.boolValue = slot.Voice;
                }
            }

            if (so.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(target);
            }

            Refresh(slot, target);
            return true;
        }

        /// <summary>書いた後の中身を読み直す（Unity が書き戻すときに中身の物を作り直すことがあるため）。</summary>
        private static void Refresh(SoundSlot slot, UnityEngine.Object target)
        {
            foreach (var found in SlotPathFinder.FindSlots(target))
            {
                if (found.Path != slot.PropertyPath)
                {
                    continue;
                }

                slot.Value = found.Value;
                slot.HasSound = found.IsBgm ? found.Value is AssetId<BgmMarker> bgm && bgm.IsValid : found.Value is FortressEffect e && e.HasSe;
                return;
            }
        }

        private static UnityEngine.Object ResolveAsset(SoundSlot slot)
        {
            if (!string.IsNullOrEmpty(slot.GlobalObjectId) && UnityEditor.GlobalObjectId.TryParse(slot.GlobalObjectId, out var id))
            {
                var obj = UnityEditor.GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
                if (obj != null)
                {
                    slot.Owner = obj;
                    return obj;
                }
            }

            var asset = string.IsNullOrEmpty(slot.AssetPath) ? null : AssetDatabase.LoadMainAssetAtPath(slot.AssetPath);
            slot.Owner = asset;
            return asset;
        }

        private static UnityEngine.Object ResolveSceneObject(SoundSlot slot)
        {
            if (string.IsNullOrEmpty(slot.GlobalObjectId) || !UnityEditor.GlobalObjectId.TryParse(slot.GlobalObjectId, out var id))
            {
                return null;
            }

            var obj = UnityEditor.GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
            slot.Owner = obj;
            return obj;
        }
    }
}
