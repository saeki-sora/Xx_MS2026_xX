using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ウェーブの表示の順番・タイミングと、出すか出さないか（2026-10-24、ユーザー依頼「出る順番や秒数を調整したい」）。
    /// 試合の始まりは「準備待ち → 握り方のヒント（YOU の矢印も）→ WAVE 1 の発表」の順。
    /// 発表の中は「WAVE n・目標 → 赤い線 → NEW ENEMY → 最後に 3・2・1」で、それぞれ発表が始まってから何秒後に出すかを決める。
    /// 数だけの入れ物なので、テストや「設定が無いとき」は <see cref="Default"/> を使う。
    /// </summary>
    [Serializable]
    public sealed class WaveDisplayTiming
    {
        /// <summary>設定ファイルが無いときの値。</summary>
        public static readonly WaveDisplayTiming Default = new WaveDisplayTiming();

        [Header("試合の始まり（初めての人向け）")]
        [Tooltip("全員の準備ができた後、WAVE 1 の発表の前に「握り方のヒント」と「YOU」の矢印だけを出す秒数（0 なら出さずにすぐ発表）。敵は来ず、時間も減らない。準備を待っている間も出る。")]
        [Min(0f)]
        public float hintSeconds = 4f;

        [Tooltip("「YOU」の矢印（自分の砲台の上）を出す。")]
        public bool showYouMarker = true;

        [Tooltip("握り方のヒント（画面の下）を出す。")]
        public bool showGripHint = true;

        [Header("発表の中の出る順番（発表が始まってから何秒後か）")]
        [Tooltip("「WAVE n／目標撃破数」を出すまでの秒数。")]
        [Min(0f)]
        public float bannerDelay;

        [Tooltip("敵のルート（赤い線・湧く場所の「！」・画面のふちの矢印）を出すまでの秒数。")]
        [Min(0f)]
        public float routeDelay = 0.4f;

        [Tooltip("「NEW ENEMY」カードを出すまでの秒数。")]
        [Min(0f)]
        public float newEnemyDelay = 1f;

        [Tooltip("「NEW ENEMY」カード（その試合で初めて出てくる敵の紹介）を出す。")]
        public bool showNewEnemyCards = true;

        [Tooltip("計画に書いた「発表の一言」（「ボス出現！」など）を出す。")]
        public bool showAnnounceText;

        [Tooltip("発表の下に「赤い線 ＝ 敵の通り道」と説明を出す。")]
        public bool showRouteHint;

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
        [Tooltip("赤い線が出てくる（はっきりするまでの）秒数。")]
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

        [Tooltip("画面のふちの矢印の横に、その場所から来る数（×40 など）を出す。")]
        public bool showEdgeMarkerCounts;

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

        /// <summary>握り方のヒント・YOU の矢印を出す段階か（準備待ちと、ヒントの段階）。</summary>
        public static bool IsHintPhase(WavePhase phase) => phase == WavePhase.WaitingForPlayers || phase == WavePhase.Hint;

        /// <summary>発表の中で、delay 秒後に出す物を、今出すか。</summary>
        public static bool ShowInAnnounce(WavePhase phase, float phaseElapsed, float delay) => phase == WavePhase.Announce && phaseElapsed >= delay;
    }

    /// <summary>
    /// ウェーブの表示の設定ファイル（出る順番・GO! の秒数・赤い線の出方・初めての人向けの表示など）。
    /// ウェーブの計画（<see cref="WavePlan.display"/>）から使う。全部のステージで同じファイルを使ってよい（既定は Presets/Waves/WaveDisplay_Default）。
    /// HUD を作り直しても消えない（HUD の部品ではなく、ここを読んで動く）。
    /// </summary>
    [CreateAssetMenu(menuName = "Fortress/Wave Display Settings", fileName = "WaveDisplay")]
    public sealed class WaveDisplaySettings : ScriptableObject
    {
        public WaveDisplayTiming timing = new WaveDisplayTiming();
    }
}
