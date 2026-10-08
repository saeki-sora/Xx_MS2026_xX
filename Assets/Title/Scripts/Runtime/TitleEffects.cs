using DDrive.Runtime.Audio;
using DDrive.Runtime.Vfx;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Title
{
    /// <summary>タイトル画面の演出欄（<see cref="FortressEffect"/>）を鳴らす。</summary>
    internal static class TitleEffects
    {
        public static void Play(FortressEffect effect, Vector3 position)
        {
            if (effect == null || effect.IsEmpty) return;
            // D-Drive の起動役（DDriveRuntimeBootstrap）がこのシーンに無いと鳴らせない。警告を出し続けないよう、そのときは何もしない。
            if (effect.HasVfx && Vfx.IsBound) FortressEffectPlayer.SpawnVfx(effect, position, Quaternion.identity);
            if (effect.HasSe && Audio.IsBound) FortressEffectPlayer.PlaySound(effect, position);
        }
    }
}
