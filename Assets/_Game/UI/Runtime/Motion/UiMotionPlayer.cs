using DDrive.Foundation.Handle;
using DDrive.Runtime.Audio;
using DDrive.Runtime.Ui;
using UnityEngine;

namespace MS2026.UI
{
    /// <summary>動きを実際に再生する係。ゲーム中は D-Drive（UiFx）、編集中のプレビューではツールが差し替える。</summary>
    public interface IUiMotionPlayer
    {
        /// <summary>今再生できるか（D-Drive の準備ができているか）。false なら動きは飛ばしてすぐ終わった扱いにする。</summary>
        bool CanPlay { get; }

        Handle<UiTweenMarker> Play(UiMotion motion, RectTransform target);

        bool IsPlaying(Handle<UiTweenMarker> handle);

        void Stop(Handle<UiTweenMarker> handle, bool complete);
    }

    /// <summary>今使う再生係。既定は D-Drive。UIスタジオのプレビュー中だけ差し替わる。</summary>
    public static class UiMotionPlayers
    {
        private static readonly IUiMotionPlayer DDrive = new DDriveMotionPlayer();

        /// <summary>差し替え（null で D-Drive に戻る）。</summary>
        public static IUiMotionPlayer Override { get; set; }

        public static IUiMotionPlayer Current => Override ?? DDrive;

        private sealed class DDriveMotionPlayer : IUiMotionPlayer
        {
            public bool CanPlay => UiFx.IsBound;

            public Handle<UiTweenMarker> Play(UiMotion motion, RectTransform target)
            {
                if (motion.se.IsValid)
                {
                    Audio.PlaySe(motion.se);
                }

                return motion.source == UiMotionSource.Preset
                    ? UiFx.Play(motion.preset.Preset, target, motion.preset)
                    : UiFx.Play(motion.tween, target);
            }

            public bool IsPlaying(Handle<UiTweenMarker> handle) => UiFx.IsPlaying(handle);

            public void Stop(Handle<UiTweenMarker> handle, bool complete)
            {
                if (UiFx.IsPlaying(handle))
                {
                    UiFx.Stop(handle, complete);
                }
            }
        }
    }
}
