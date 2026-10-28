using System.Collections.Generic;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ステージの仕掛け（歯車・動く障害物）の時計（2026-10-29）。仕掛けはこの秒数だけを見て形を決めるので、
    /// 時計がそろっていれば全員のPCで同じ形になる（通信するのは時計だけ。<see cref="GimmickClockRunner"/>）。
    /// ・試合ごとに 0 から。ウェーブの計画があるステージでは、発表・戦い・撤退・合間の間だけ進む（始まりの演出や結果の間は止まる）
    /// ・ウェーブの計画が無いときは、このPCの準備（幕・始まりの演出）ができたら進む
    /// ・Host（オフラインでは自分）が進め、Client には Host の値が届く
    /// エディタのプレビュー中は、仕掛けは <see cref="PreviewTime"/> の形になる（Play していないときだけ）。
    /// </summary>
    public static class GimmickClock
    {
        /// <summary>Host → 全員: 時計の値。</summary>
        public const string StateMessage = "MS2026.Fortress.Gimmicks";

        private static readonly List<GimmickBehaviour> Registry = new List<GimmickBehaviour>();

        /// <summary>今の秒数（試合の始めが 0）。</summary>
        public static float Time { get; internal set; }

        /// <summary>今、時計が進んでいるか。</summary>
        public static bool Running { get; internal set; }

        /// <summary>試しに速く進める倍率（要塞デザイナーの「仕掛け」タブ。Host とオフラインだけが変えられる。Client には届く）。</summary>
        public static float Speed { get; internal set; } = 1f;

        /// <summary>試しに止めている（要塞デザイナーの「仕掛け」タブ）。</summary>
        public static bool Paused { get; internal set; }

        /// <summary>エディタのプレビューの秒数（負ならプレビューしていない）。Play 中は使わない。</summary>
        public static float PreviewTime { get; set; } = -1f;

        public static bool IsPreviewing => PreviewTime >= 0f && !Application.isPlaying;

        /// <summary>仕掛けが今使う秒数（Play 中は時計、編集中はプレビューの秒数、どちらでもなければ 0）。</summary>
        public static float Now => Application.isPlaying ? Time : Mathf.Max(0f, PreviewTime);

        /// <summary>時計が飛んだ（途中参加で Host に合わせた・試しに先へ進めた）。演出を鳴らし直さないために使う。</summary>
        public static int JumpCount { get; internal set; }

        /// <summary>有効な仕掛け（歯車・動く障害物）。</summary>
        public static IReadOnlyList<GimmickBehaviour> All => Registry;

        /// <summary>時計を変えられるか（オフライン・Host）。</summary>
        public static bool CanControl => FortressNet.HasSimulationAuthority;

        /// <summary>試しに速さを変える（Host・オフラインだけ。0 で止める）。</summary>
        public static void SetDebugSpeed(float speed)
        {
            if (CanControl)
            {
                Speed = Mathf.Clamp(speed, 0f, 16f);
                GimmickClockRunner.MarkChanged();
            }
        }

        public static void SetDebugPaused(bool paused)
        {
            if (CanControl)
            {
                Paused = paused;
                GimmickClockRunner.MarkChanged();
            }
        }

        /// <summary>試しに時計を指定の秒数にする（Host・オフラインだけ）。</summary>
        public static void DebugJump(float time)
        {
            if (CanControl)
            {
                Time = Mathf.Max(0f, time);
                JumpCount++;
                GimmickClockRunner.MarkChanged();
            }
        }

        internal static void Register(GimmickBehaviour gimmick)
        {
            if (gimmick != null && !Registry.Contains(gimmick))
            {
                Registry.Add(gimmick);
            }
        }

        internal static void Unregister(GimmickBehaviour gimmick)
        {
            Registry.Remove(gimmick);
        }

        internal static void ResetForMatch()
        {
            Time = 0f;
            Running = false;
            Speed = 1f;
            Paused = false;
            JumpCount++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Registry.Clear();
            Time = 0f;
            Running = false;
            Speed = 1f;
            Paused = false;
            JumpCount = 0;
            PreviewTime = -1f;
        }

        /// <summary>時計の値が変わったときに全員の仕掛けへ形を当てはめ直す（エディタのプレビュー用）。</summary>
        public static void ApplyAll(float time)
        {
            for (var i = Registry.Count - 1; i >= 0; i--)
            {
                var gimmick = Registry[i];
                if (gimmick == null)
                {
                    Registry.RemoveAt(i);
                    continue;
                }

                gimmick.ApplyAt(time);
            }
        }

        /// <summary>シーンにある仕掛け（編集中も。無効な物は除く）。</summary>
        public static List<GimmickBehaviour> FindAllInScenes()
        {
            var list = new List<GimmickBehaviour>(UnityEngine.Object.FindObjectsByType<GimmickBehaviour>(FindObjectsSortMode.None));
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list;
        }

        /// <summary>仕掛けの時計が進む段階か（ウェーブの計画があるとき）。</summary>
        public static bool RunsInPhase(WavePhase phase)
        {
            switch (phase)
            {
                case WavePhase.Announce:
                case WavePhase.Combat:
                case WavePhase.Retreat:
                case WavePhase.Break:
                    return true;
                default:
                    return false;
            }
        }
    }
}
