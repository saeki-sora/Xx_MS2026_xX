using System;
using DDrive.Foundation.Manager;
using DDrive.Runtime.Loop;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 終了するときの D-Drive の音のエラーを防ぐ（2026-10-13）。
    /// D-Drive（パッケージ、改造しない）は、終了時（エディタの Play を止めたとき・アプリを閉じたとき・D-Drive を消したとき）に
    /// 鳴っている SE・BGM を止めようとするが、そのときには音の部品（AudioSource）が先に消えていることがあり、
    /// MissingReferenceException（AudioManager.StopAll / BgmManager.StopBgm）になる。
    /// そこで、部品がまだ残っているうちに（Application.quitting、タイトルへ戻る前）こちらから全部止めておく。
    /// 止めた後は <see cref="IsShuttingDown"/> が true になり、BGM 係（<see cref="FortressBgmPlayer"/>）は止める命令を出さない。
    /// </summary>
    public static class FortressAudioShutdown
    {
        /// <summary>終了の途中（音はもう止めた）。この間は D-Drive の音を止める命令を出さない。</summary>
        public static bool IsShuttingDown { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            IsShuttingDown = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting()
        {
            StopAllNow();
            IsShuttingDown = true;
        }

        /// <summary>
        /// 鳴っている SE と BGM を今すぐ全部止める（D-Drive を消す直前に呼ぶ）。D-Drive が無ければ何もしない。
        /// </summary>
        public static void StopAllNow()
        {
            var bootstrap = DDriveRuntimeBootstrap.Instance;
            if (bootstrap == null)
            {
                return;
            }

            try
            {
                bootstrap.Audio?.StopAll(StopReason.SceneUnload);
                bootstrap.Bgm?.StopBgm(0f);
            }
            catch (Exception e)
            {
                // すでに部品が消え始めていた。止められなかった分は D-Drive の後始末に任せる。
                Debug.LogWarning($"[FortressAudio] 終了前に音を止められませんでした: {e.Message}");
            }
        }
    }
}
