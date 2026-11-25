using System.Linq;
using DDrive.Foundation.Identity;
using DDrive.Runtime.Audio;
using UnityEditor;

namespace MS2026.SoundStudio
{
    /// <summary>
    /// 欄に音を入れる。同じ欄がほかの設定にもあるとき（ステージごとのウェーブの設定など）は、「全部に入れる／ここだけ」を聞く。
    /// </summary>
    public static class SoundBulk
    {
        private const int ShownOwners = 6;

        /// <summary>欄に音（番号）を入れる。同じ欄がほかにもあれば、まとめて入れるか聞く。</summary>
        public static void SetSound(SoundStudioContext context, SoundSlot slot, ulong soundId)
        {
            var result = Write(slot, soundId);
            var same = SoundStudioContext.SameSlots(slot, context.Slots);
            if (result == SoundWriteResult.Saved || result == SoundWriteResult.SaveSceneNeeded)
            {
                // すでに同じ音が入っている物は聞かない。
                same = same.Where(s => SoundStudioContext.SoundIdOf(s) != soundId).ToList();
            }
            else
            {
                same.Clear();
            }

            if (same.Count > 0)
            {
                var names = string.Join("\n", same.Take(ShownOwners).Select(s => "・" + (string.IsNullOrEmpty(s.OwnerName) ? s.Label : s.OwnerName)));
                if (same.Count > ShownOwners)
                {
                    names += "\n…ほか " + (same.Count - ShownOwners) + " 個";
                }

                var all = EditorUtility.DisplayDialog("サウンドスタジオ",
                    "「" + slot.Label + "」と同じ欄が、ほかに " + same.Count + " 個あります。\n" + names + "\n\n全部に同じ音を入れますか？",
                    "全部に入れる", "ここだけ");
                if (all)
                {
                    foreach (var other in same)
                    {
                        var r = Write(other, soundId);
                        if (r != SoundWriteResult.Saved)
                        {
                            result = r;
                        }
                    }
                }
            }

            context.Report(result);
        }

        private static SoundWriteResult Write(SoundSlot slot, ulong soundId)
        {
            return slot.Kind == SoundSlotKind.Bgm
                ? SoundSlotWriter.SetBgm(slot, new AssetId<BgmMarker>(soundId, AssetType.Bgm))
                : SoundSlotWriter.SetSe(slot, new AssetId<SeMarker>(soundId, AssetType.Se));
        }
    }
}
