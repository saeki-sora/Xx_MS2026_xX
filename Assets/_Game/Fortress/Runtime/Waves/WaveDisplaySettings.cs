using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ウェーブの表示のタイミングと、出すか出さないか（2026-10-24、ユーザー依頼「出る順番や秒数を調整したい」）。
    /// 数だけの入れ物なので、テストや「設定が無いとき」は <see cref="Default"/> を使う。
    /// </summary>
    [Serializable]
    public sealed class WaveDisplayTiming
    {
        /// <summary>設定ファイルが無いときの値（今までの値と同じ）。</summary>
        public static readonly WaveDisplayTiming Default = new WaveDisplayTiming();

        [Header("ウェーブの始まり")]
        [Tooltip("ウェーブが始まったとき「GO!」を出す秒数（0 なら出さない）。")]
        [Min(0f)]
        public float goSeconds = 0.9f;

        [Header("戦っている間")]
        [Tooltip("目標まで残りこの数以下になったら「あと少し！」を出す（0 なら出さない）。")]
        [Min(0)]
        public int nearGoalKills = 5;

        [Tooltip("試合の残り時間がこの秒数以下になったら、TIME を赤く点滅させ、警告の音を1秒ごとに鳴らす（0 なら出さない）。")]
        [Min(0f)]
        public float timeWarningSeconds = 10f;

        [Header("敵のルート（赤い線）")]
        [Tooltip("発表が始まってから、赤い線がはっきり出るまでの秒数。")]
        [Min(0.01f)]
        public float routeShowSeconds = 0.3f;

        [Tooltip("ウェーブが始まってから、赤い線が消えきるまでの秒数（0 ならすぐ消える）。")]
        [Min(0f)]
        public float routeFadeSeconds = 2f;

        [Tooltip("線の上を流れる白い矢印の間隔（m）。")]
        [Min(0.3f)]
        public float routeArrowSpacing = 1.6f;

        [Tooltip("白い矢印が流れる速さ（m/秒）。")]
        [Min(0f)]
        public float routeArrowSpeed = 3.2f;

        [Tooltip("発表の間、道を引き直す間隔（秒）。橋が崩れてコアが落ちたときなど、道が変わっても線が合うように。")]
        [Min(0.2f)]
        public float routeRebuildSeconds = 1f;

        [Tooltip("自分の画面の外から来る敵を、画面のふちの矢印で知らせる（赤い線と同じ間）。")]
        public bool showEdgeMarkers = true;

        [Header("初めての人向け")]
        [Tooltip("「YOU」の矢印と握り方のヒントを、最初の何ウェーブの発表まで出すか（0 なら出さない。1 なら WAVE 1 だけ）。試合の始まりの準備待ちの間も出る。")]
        [Min(0)]
        public int firstHintWaves = 1;

        [Tooltip("「YOU」の矢印（自分の砲台の上）を出す。")]
        public bool showYouMarker = true;

        [Tooltip("握り方のヒント（画面の下）を出す。")]
        public bool showGripHint = true;

        [Tooltip("発表のとき、初めて出てくる敵の「NEW ENEMY」カードを出す。")]
        public bool showNewEnemyCards = true;

        [Header("歯みがきタイム")]
        [Tooltip("「シンクロ！ ボーナス！」を出しておく秒数。")]
        [Min(0.1f)]
        public float syncShowSeconds = 0.9f;

        /// <summary>今の試合で使う値（ステージの計画の設定。無ければ <see cref="Default"/>）。</summary>
        public static WaveDisplayTiming Current
        {
            get
            {
                var plan = FortressWaves.Plan;
                return plan != null && plan.display != null && plan.display.timing != null ? plan.display.timing : Default;
            }
        }

        /// <summary>wave（0 から）の発表・準備待ちのときに、初めての人向けの表示を出すか。</summary>
        public bool ShowFirstHint(WavePhase phase, int wave) =>
            wave < firstHintWaves && (phase == WavePhase.WaitingForPlayers || phase == WavePhase.Announce);
    }

    /// <summary>
    /// ウェーブの表示の設定ファイル（GO! の秒数・赤い線の出方・初めての人向けの表示など）。
    /// ウェーブの計画（<see cref="WavePlan.display"/>）から使う。全部のステージで同じファイルを使ってよい（既定は Presets/Waves/WaveDisplay_Default）。
    /// HUD を作り直しても消えない（HUD の部品ではなく、ここを読んで動く）。
    /// </summary>
    [CreateAssetMenu(menuName = "Fortress/Wave Display Settings", fileName = "WaveDisplay")]
    public sealed class WaveDisplaySettings : ScriptableObject
    {
        public WaveDisplayTiming timing = new WaveDisplayTiming();
    }
}
