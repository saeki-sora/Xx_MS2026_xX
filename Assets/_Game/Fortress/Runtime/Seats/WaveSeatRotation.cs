using System;
using MS2026.Fortress.Cameras;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>席替えで回る向き（画面で見た向き。ステージの席の場所から自動で並べる）。</summary>
    public enum SeatRotationDirection
    {
        /// <summary>時計回り（ステージ1: 上→右→下→左、ステージ2: 左上→右上→右下→左下）。</summary>
        Clockwise = 0,

        /// <summary>反時計回り。</summary>
        CounterClockwise = 1
    }

    /// <summary>
    /// ウェーブごとの席替えの決まり（<see cref="WavePlan.seatRotation"/>。2026-10-31 ユーザー決定:
    /// 時計回り・カメラは自分に付いていく・歯みがきの後、次の発表の前に移る・動きは「ピョンとジャンプ」・移った後に YOU 矢印）。
    /// </summary>
    [Serializable]
    public sealed class WaveSeatRotation
    {
        [Tooltip("ON: ウェーブが1つ終わるたび（合間の後、次の発表の前）に、全員の砲台が1つ隣の席へ跳んで移る。")]
        public bool enabled = true;

        [Tooltip("回る向き（画面で見た向き）。")]
        public SeatRotationDirection direction = SeatRotationDirection.Clockwise;

        [Header("ジャンプ")]
        [Tooltip("前の席から次の席まで跳ぶ秒数。")]
        [Min(0.1f)]
        public float jumpSeconds = 0.9f;

        [Tooltip("いちばん高い所で、画面の上へどれだけ浮くか（メートル）。")]
        [Min(0f)]
        public float jumpHeight = 2.5f;

        [Tooltip("いちばん高い所で、どれだけ大きく見せるか（0.15 = 15% 大きく）。")]
        [Min(0f)]
        public float jumpScale = 0.15f;

        [Tooltip("着地でつぶれる量（0 = つぶれない。0.25 = 横に 25% 広がって縦に縮む）。")]
        [Min(0f)]
        public float landSquash = 0.25f;

        [Tooltip("つぶれてから元に戻るまでの秒数。")]
        [Min(0f)]
        public float landSquashSeconds = 0.3f;

        [Header("カメラ")]
        [Tooltip("跳び始めてから、カメラが次の席の視点へ動き始めるまでの秒数（少し遅れて付いていく）。")]
        [Min(0f)]
        public float cameraDelaySeconds = 0.1f;

        [Tooltip("カメラが次の席の視点へ移る秒数。")]
        [Min(0f)]
        public float cameraBlendSeconds = 1f;

        [Tooltip("ON: 着地で自分の画面を揺らす（各PCが自分のカメラだけ揺らす）。")]
        public bool shakeOnLand = true;

        [Tooltip("着地の揺れ。")]
        public SimpleShakeSettings landShake = new SimpleShakeSettings { amplitude = 0.18f, rollAmplitude = 0.4f, frequency = 18f, duration = 0.3f };

        [Header("移った後")]
        [Tooltip("ON: 着地してから、自分の砲台の上に「YOU」の矢印を出す。")]
        public bool showYouMarker = true;

        [Tooltip("YOU の矢印を出す秒数。")]
        [Min(0f)]
        public float youMarkerSeconds = 2f;

        // 演出（D-Drive の番号札。各PCで鳴らす。空なら鳴らさない）。VFX は4基それぞれ、SE は自分の砲台で1回だけ。
        [Tooltip("跳び始めたとき（砲台ごと。プレイヤーの色）。")]
        public FortressEffect onJump = new FortressEffect();

        [Tooltip("着地したとき（砲台ごと。プレイヤーの色）。")]
        public FortressEffect onLand = new FortressEffect();

        public bool Clockwise => direction == SeatRotationDirection.Clockwise;

        /// <summary>跳び始めから、つぶれが戻るまでの秒数。</summary>
        public float TotalSeconds => jumpSeconds + landSquashSeconds;

        /// <summary>
        /// ウェーブの「席替え」の段階の秒数（OFF なら 0）。跳ぶ動きとカメラが落ち着いて、YOU の矢印が消えてから発表が始まる。
        /// </summary>
        public float WaveSeconds =>
            enabled ? Mathf.Max(TotalSeconds, cameraDelaySeconds + cameraBlendSeconds) + (showYouMarker ? youMarkerSeconds : 0f) : 0f;
    }
}
