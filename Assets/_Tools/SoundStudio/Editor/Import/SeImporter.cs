using System.IO;
using System.Linq;
using DDrive.Editor.Import;
using DDrive.Foundation.Data;
using DDrive.Foundation.Identity;
using DDrive.Runtime.Audio;
using UnityEditor;
using UnityEngine;

namespace MS2026.SoundStudio
{
    /// <summary>
    /// wav（mp3/ogg/aiff も可）を D-Drive の素材フォルダへ入れ、D-Drive が SE（BGM）として登録するのを待って、欄に入れる。
    /// D-Drive の登録は取り込みの少し後に動くので、最大 <see cref="WaitSeconds"/> 秒待つ。
    /// </summary>
    public static class SeImporter
    {
        private const double WaitSeconds = 10;

        /// <summary>
        /// 取り込む。<paramref name="slot"/> が null なら（「はじめに」に落としたとき）登録だけして欄には入れない。
        /// </summary>
        public static void Import(SoundStudioContext context, string sourcePath, SoundSlot slot)
        {
            if (!SeImportNaming.IsSoundFile(sourcePath) || !File.Exists(sourcePath))
            {
                context.Notify("音のファイル（wav / mp3 / ogg / aiff）を落としてください。");
                return;
            }

            var isBgm = slot != null && slot.Kind == SoundSlotKind.Bgm;
            var page = slot?.Page ?? SoundPageId.Other;
            var suggested = SeImportNaming.SuggestName(slot?.Label, Path.GetFileName(sourcePath));
            SeNameDialog.Ask(suggested, SoundPageIds.SeFolder(page), name =>
            {
                var root = ImportRuleService.ResolveSourceRoot(ImportRuleService.DefaultSourceRoot);
                var target = SeImportNaming.MakeFileName(root, page, name, Path.GetExtension(sourcePath), isBgm);
                if (File.Exists(target) && !EditorUtility.DisplayDialog("サウンドスタジオ",
                        "同じ名前の音がもうあります。\n" + target + "\n\n上書きしますか？（その音を使っている所すべてが新しい音になります）", "上書きする", "やめる"))
                {
                    return;
                }

                Copy(sourcePath, target);
                WaitForData(context, target, isBgm, slot, EditorApplication.timeSinceStartup + WaitSeconds);
            });
        }

        private static void Copy(string sourcePath, string target)
        {
            var folder = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.Copy(sourcePath, target, true);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
        }

        private static void WaitForData(SoundStudioContext context, string target, bool isBgm, SoundSlot slot, double until)
        {
            void Check()
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(target);
                var data = clip != null ? FindData(clip, isBgm) : null;
                if (data == null)
                {
                    if (EditorApplication.timeSinceStartup < until)
                    {
                        return;
                    }

                    EditorApplication.update -= Check;
                    context.Notify("D-Drive の登録が終わりませんでした。D-Drive の AssetBrowser で確かめてください。");
                    return;
                }

                EditorApplication.update -= Check;
                context.Library.Reload();
                if (slot != null)
                {
                    SoundBulk.SetSound(context, slot, data.Id);
                }
                else
                {
                    context.Touch();
                }

                context.Notify("登録しました: " + SoundLibrary.NameOf(data));
            }

            EditorApplication.update += Check;
        }

        private static AssetDataBase FindData(AudioClip clip, bool isBgm)
        {
            if (isBgm)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(BgmData)))
                {
                    var bgm = AssetDatabase.LoadAssetAtPath<BgmData>(AssetDatabase.GUIDToAssetPath(guid));
                    if (bgm != null && (bgm.LoopBody == clip || bgm.Intro == clip))
                    {
                        return bgm;
                    }
                }

                return null;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(SeData)))
            {
                var se = AssetDatabase.LoadAssetAtPath<SeData>(AssetDatabase.GUIDToAssetPath(guid));
                if (se == null)
                {
                    continue;
                }

                if ((se.Clips != null && se.Clips.Contains(clip)) || (se.Sources != null && se.Sources.Any(s => s.Source == clip)))
                {
                    return se;
                }
            }

            return null;
        }
    }
}
