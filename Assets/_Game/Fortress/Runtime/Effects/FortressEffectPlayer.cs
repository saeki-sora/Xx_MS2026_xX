using DDrive.Foundation.Handle;
using DDrive.Runtime.Audio;
using DDrive.Runtime.Loop;
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
        /// <summary>D-Drive の準備（番号札の登録）が終わっているか。終わる前に鳴らすと「未登録のIDがPlaceholderになった」警告が出るので、その間は鳴らさない。</summary>
        private static bool IsReady => DDriveRuntimeBootstrap.Instance != null && DDriveRuntimeBootstrap.Instance.IsReady;

        /// <summary>その場に1回だけ出す（エフェクトは VFX 側の寿命の設定で消える）。</summary>
        public static void PlayOnce(FortressEffect effect, Vector3 position, Quaternion rotation, int playerIndex = -1)
        {
            if (effect == null)
            {
                return;
            }

            SpawnVfx(effect, position, rotation, playerIndex);
            PlaySound(effect, position, playerIndex);
        }

        /// <summary>エフェクトだけをその場に出し、Handle を返す（音は鳴らさない）。エフェクトが空なら Invalid。</summary>
        public static Handle<VfxMarker> SpawnVfx(FortressEffect effect, Vector3 position, Quaternion rotation, int playerIndex = -1)
        {
            if (effect == null || !effect.HasVfx || !IsReady)
            {
                return Handle<VfxMarker>.Invalid;
            }

            var handle = Vfx.Spawn(effect.vfx, position, rotation);
            Tint(effect, handle, playerIndex);
            return handle;
        }

#if UNITY_EDITOR
        /// <summary>
        /// エディタだけ: 効果音を実際に鳴らしたとき（このPCで鳴らすと決まって D-Drive に頼んだ後）に呼ばれる。
        /// サウンドスタジオが「鳴った行を光らせる」ために使う。ビルドには入らない。
        /// </summary>
        public static event System.Action<FortressEffect, int> Played;
#endif

        /// <summary>
        /// 効果音だけを鳴らす。<paramref name="playerIndex"/> は「誰の事か」（分からなければ -1）。
        /// 欄の「鳴らすPC」がその人のPCだけなら、自分の事でないときは鳴らさない（CPU の事は Host で鳴らす）。
        /// 鳴らしたら、この場所の倍率と全体音量を掛ける。
        /// </summary>
        public static void PlaySound(FortressEffect effect, Vector3 position, int playerIndex = -1)
        {
            if (effect == null || !effect.HasSe || !IsReady)
            {
                return;
            }

            var local = FortressAudioMix.LocalPlayerIndex;
            var isHost = local >= 0 && FortressAudioMix.IsHost;
            var ownerIsHuman = !isHost || FortressAudioMix.OwnerIsHuman(playerIndex);
            if (!FortressSoundRules.ShouldPlay(effect.audience, playerIndex, local, isHost, ownerIsHuman))
            {
                return;
            }

            // このゲームの効果音は位置で変わらない（SE の Spatial=None）ので、位置は渡さない。
            // 渡すと D-Drive が「位置の指定は無視されます」という警告を出し続ける。
            var handle = Audio.PlaySe(effect.se);
            FortressAudioMix.ApplySeVolume(handle, effect.se, effect.volumeScale, effect.isVoice);
#if UNITY_EDITOR
            Played?.Invoke(effect, playerIndex);
#endif
        }

        /// <summary>
        /// <paramref name="follow"/> の位置・向きでエフェクトを出し、以後その位置に追従させる（音は鳴らさない）。
        /// 向きも追従させたいときは、VFX の「出す位置（Anchor）」で Follow Rotation を ON にする。
        /// 止めるときは <see cref="Stop"/>。エフェクトが空なら Invalid を返す。
        /// </summary>
        public static Handle<VfxMarker> StartFollowing(FortressEffect effect, Transform follow, int playerIndex = -1)
        {
            if (effect == null || !effect.HasVfx || follow == null || !IsReady)
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
