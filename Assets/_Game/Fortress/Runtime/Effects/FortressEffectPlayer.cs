using DDrive.Foundation.Handle;
using DDrive.Runtime.Audio;
using DDrive.Runtime.Vfx;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// <see cref="FortressEffect"/> を D-Drive（Vfx / Audio）経由で鳴らす共通処理。
    /// ネット対戦では、各PCが自分のところで起きた出来事（状態の変化・当たり判定）を見て自分で鳴らす。
    /// そのためVFXの Flags › Net は Local のままにすること（Cosmetic にすると Host の分が二重に届く）。
    /// </summary>
    public static class FortressEffectPlayer
    {
        /// <summary>その場に1回だけ出す（エフェクトは VFX 側の寿命の設定で消える）。</summary>
        public static void PlayOnce(FortressEffect effect, Vector3 position, Quaternion rotation, int playerIndex = -1)
        {
            if (effect == null)
            {
                return;
            }

            SpawnVfx(effect, position, rotation, playerIndex);
            PlaySound(effect, position);
        }

        /// <summary>エフェクトだけをその場に出し、Handle を返す（音は鳴らさない）。エフェクトが空なら Invalid。</summary>
        public static Handle<VfxMarker> SpawnVfx(FortressEffect effect, Vector3 position, Quaternion rotation, int playerIndex = -1)
        {
            if (effect == null || !effect.HasVfx)
            {
                return Handle<VfxMarker>.Invalid;
            }

            var handle = Vfx.Spawn(effect.vfx, position, rotation);
            Tint(effect, handle, playerIndex);
            return handle;
        }

        /// <summary>効果音だけを鳴らす。</summary>
        public static void PlaySound(FortressEffect effect, Vector3 position)
        {
            if (effect != null && effect.HasSe)
            {
                Audio.PlaySe(effect.se, position);
            }
        }

        /// <summary>
        /// <paramref name="follow"/> の位置・向きでエフェクトを出し、以後その位置に追従させる（音は鳴らさない）。
        /// 向きも追従させたいときは、VFX の「出す位置（Anchor）」で Follow Rotation を ON にする。
        /// 止めるときは <see cref="Stop"/>。エフェクトが空なら Invalid を返す。
        /// </summary>
        public static Handle<VfxMarker> StartFollowing(FortressEffect effect, Transform follow, int playerIndex = -1)
        {
            if (effect == null || !effect.HasVfx || follow == null)
            {
                return Handle<VfxMarker>.Invalid;
            }

            var handle = Vfx.Spawn(effect.vfx, follow.position, follow.rotation);
            Vfx.Attach(handle, follow);
            Tint(effect, handle, playerIndex);
            return handle;
        }

        /// <summary>出し続けているエフェクトを止める（自然に消える）。既に終わっていても安全。</summary>
        public static void Stop(ref Handle<VfxMarker> handle)
        {
            // 終わった Handle に Stop を呼ぶと開発ビルドで警告が出るので、再生中か（警告の出ない問い合わせ）を先に見る。
            if (handle != Handle<VfxMarker>.Invalid && Vfx.IsPlaying(handle))
            {
                Vfx.Stop(handle);
            }

            handle = Handle<VfxMarker>.Invalid;
        }

        /// <summary>XY平面のゲームで、Yの正方向(transform.up)を <paramref name="up"/> に向ける回転。</summary>
        public static Quaternion RotationFacing(Vector2 up)
        {
            return up.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(Vector3.forward, up) : Quaternion.identity;
        }

        private static void Tint(FortressEffect effect, Handle<VfxMarker> handle, int playerIndex)
        {
            if (!effect.tintWithPlayerColor || playerIndex < 0 || string.IsNullOrEmpty(effect.colorParam)
                || handle == Handle<VfxMarker>.Invalid)
            {
                return;
            }

            Vfx.SetParam(handle, effect.colorParam, FortressColors.PlayerColor(playerIndex));
        }
    }
}
