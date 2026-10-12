using DDrive.Runtime.Audio;
using DDrive.Runtime.Vfx;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.CharacterSelect
{
    /// <summary>キャラ選択の演出欄（<see cref="FortressEffect"/>）を鳴らす。</summary>
    internal static class CharacterSelectEffects
    {
        public static void Play(FortressEffect effect, Vector3 position, int playerIndex)
        {
            if (effect == null || effect.IsEmpty)
            {
                return;
            }

            // D-Drive の起動役はロビーのシーンにある。このシーンだけで再生したときは無いので、警告を出し続けないよう何もしない。
            if (effect.HasVfx && Vfx.IsBound)
            {
                FortressEffectPlayer.SpawnVfx(effect, position, Quaternion.identity, playerIndex);
            }

            if (effect.HasSe && Audio.IsBound)
            {
                FortressEffectPlayer.PlaySound(effect, position);
            }
        }
    }
}
