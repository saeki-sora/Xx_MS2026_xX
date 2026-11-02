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
    /// 演出（2026-11-02 ユーザー決定。見本帳の 1〜6・8〜14・17・19〜24。着地の画面の揺れと残像はやめた）:
    /// 跳ぶ前（溜め・行き先の点線・回る矢印の輪・「席替え！」の帯・画面の端を暗く）、
    /// 跳んでいる間（影・光の尾・1回転・頂点でふわっと・カメラを引きながら回す・流れる線）、
    /// 着地（順番に着地・衝撃波の輪・ヒットストップ・床の光の波・キラッ）、締め（YOU の光の柱・4色の光がコアへ・OK マーク）、音。
    /// </summary>
    [Serializable]
    public sealed class WaveSeatRotation
    {
        [Tooltip("ON: ウェーブが1つ終わるたび（合間の後、次の発表の前）に、全員の砲台が1つ隣の席へ跳んで移る。")]
        public bool enabled = true;

        [Tooltip("回る向き（画面で見た向き）。")]
        public SeatRotationDirection direction = SeatRotationDirection.Clockwise;

        [Header("ジャンプ")]
        [Tooltip("前の席から次の席まで跳ぶ秒数（「頂点でふわっと」の分は別に足す）。")]
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
        public float landSquash = 0.4f;

        [Tooltip("つぶれてから元に戻るまでの秒数。")]
        [Min(0f)]
        public float landSquashSeconds = 0.3f;

        [Header("跳ぶ前（予告）")]
        [Tooltip("ON: 跳ぶ前にしゃがんで震え（溜め）、跳ぶ瞬間に縦へ伸びる。")]
        public bool anticipation = true;

        [Tooltip("溜めの秒数（この間に点線・矢印の輪・帯も出る）。")]
        [Min(0f)]
        public float anticipationSeconds = 0.6f;

        [Tooltip("溜めでしゃがむ量（0.24 = 横に 24% 広がって縦に縮む）。")]
        [Min(0f)]
        public float crouchAmount = 0.24f;

        [Tooltip("溜めで震える幅（メートル）。")]
        [Min(0f)]
        public float crouchShake = 0.06f;

        [Tooltip("跳ぶ瞬間に縦へ伸びる量。")]
        [Min(0f)]
        public float launchStretch = 0.2f;

        [Tooltip("ON: 自分の色の点線で、どの席へ跳ぶかを先に見せる。")]
        public bool showPathPreview = true;

        [Tooltip("ON: コアのまわりを、回る向きに矢印の輪が回る。")]
        public bool showDirectionRing = true;

        [Tooltip("ON: 画面を横切る「席替え！」の帯を出す。")]
        public bool showBanner = true;

        [Tooltip("帯の大きい文字。")]
        public string bannerText = "席替え！";

        [Tooltip("帯の小さい文字（空なら回る向きから自動）。")]
        public string bannerSubText = "";

        [Tooltip("帯の文字のフォント（空なら Unity の標準フォント）。")]
        public Font bannerFont;

        [Tooltip("ON: 跳んでいる間、画面の端を暗くする（砲台に目が行くように）。")]
        public bool darkenEdges = true;

        [Tooltip("画面の端の暗さ（0〜1）。")]
        [Range(0f, 1f)]
        public float darkenAmount = 0.6f;

        [Header("跳んでいる間")]
        [Tooltip("ON: 地面に影を出す（浮くほど小さく薄くなる）。")]
        public bool showShadow = true;

        [Tooltip("ON: 自分の色に光る尾を引いて跳ぶ。")]
        public bool showTrail = true;

        [Tooltip("ON: 空中で回る（回る向きと同じ向き）。")]
        public bool spinInAir = true;

        [Tooltip("空中で回る回数。")]
        [Min(0)]
        public int spinTurns = 1;

        [Tooltip("ON: ビュッと上がって、いちばん上で一瞬ふわっと浮き、ストンと落ちる。")]
        public bool hangAtApex = true;

        [Tooltip("ふわっと浮く分、跳ぶ秒数に足す秒数。")]
        [Min(0f)]
        public float hangSeconds = 0.25f;

        [Tooltip("ふわっと止まる強さ（1 = 普通の山なり。大きいほど頂点で長く止まる）。")]
        [Range(1f, 4f)]
        public float hangPower = 2.1f;

        [Tooltip("ON: 回っている間だけカメラを少し引いて全体を見せ、着地に合わせて戻す。")]
        public bool cameraPullBack = true;

        [Tooltip("カメラを引く量（0.16 = 16% 広く映す）。")]
        [Range(0f, 0.6f)]
        public float cameraPullAmount = 0.16f;

        [Tooltip("ON: カメラが回っている間、画面の端に勢いの線が流れる。")]
        public bool speedLines = true;

        [Header("カメラ")]
        [Tooltip("跳び始めてから、カメラが次の席の視点へ動き始めるまでの秒数（少し遅れて付いていく）。")]
        [Min(0f)]
        public float cameraDelaySeconds = 0.1f;

        [Tooltip("カメラが次の席の視点へ移る秒数。")]
        [Min(0f)]
        public float cameraBlendSeconds = 1.4f;

        [Tooltip("ON: 着地で自分の画面を揺らす（各PCが自分のカメラだけ揺らす）。")]
        public bool shakeOnLand;

        [Tooltip("着地の揺れ。")]
        public SimpleShakeSettings landShake = new SimpleShakeSettings { amplitude = 0.18f, rollAmplitude = 0.4f, frequency = 18f, duration = 0.3f };

        [Header("着地")]
        [Tooltip("ON: 4基が順番に着地する（自分から回る向きに、少しずつずれて「ドドドドン」）。")]
        public bool staggerLanding = true;

        [Tooltip("1基ごとにずらす秒数。")]
        [Min(0f)]
        public float staggerSeconds = 0.12f;

        [Tooltip("ON: 着地点から自分の色の輪（衝撃波）が広がる。")]
        public bool shockwave = true;

        [Tooltip("衝撃波の輪の大きさ（半径・メートル）。")]
        [Min(0.5f)]
        public float shockwaveRadius = 4.4f;

        [Tooltip("ON: 着地の瞬間に、砲台の動きを一瞬止める（ヒットストップ。各PCの砲台の動きだけで、敵や通信は止めない）。")]
        public bool hitStop = true;

        [Tooltip("ヒットストップの秒数（着地1回ごと）。")]
        [Min(0f)]
        public float hitStopSeconds = 0.06f;

        [Tooltip("ON: 着地点から床のタイルが自分の色で光りながら波打つ。")]
        public bool floorWave = true;

        [Tooltip("床の光るタイルの大きさ（メートル）。")]
        [Min(0.3f)]
        public float floorTileSize = 1.6f;

        [Tooltip("ON: 着地した砲台に「キラッ」。")]
        public bool sparkle = true;

        [Header("移った後（締め）")]
        [Tooltip("ON: 着地してから、自分の砲台の上に「YOU」の矢印を出す。")]
        public bool showYouMarker = true;

        [Tooltip("YOU の矢印を出す秒数（この間に光の柱・光がコアへ・OK マークも出る）。")]
        [Min(0f)]
        public float youMarkerSeconds = 2f;

        [Tooltip("ON: 自分の砲台に光の柱が立ち、YOU の矢印が上から弾んで落ちてくる。")]
        public bool youPillar = true;

        [Tooltip("ON: 全員の砲台から自分の色の光がコアへ飛び、コアがピカッと光る。")]
        public bool gatherToCore = true;

        [Tooltip("ON: 着地した人から順に、頭の上に自分の色の ✓（OK マーク）がポンと出る。")]
        public bool okMarks = true;

        // 演出（D-Drive の番号札。各PCで鳴らす。空なら鳴らさない）。VFX は4基それぞれ、SE は自分の砲台で1回だけ。
        [Tooltip("溜め始め（砲台ごと。プレイヤーの色）。")]
        public FortressEffect onAnticipate = new FortressEffect();

        [Tooltip("跳び始めたとき（砲台ごと。プレイヤーの色）。")]
        public FortressEffect onJump = new FortressEffect();

        [Tooltip("着地したとき（砲台ごと。プレイヤーの色）。")]
        public FortressEffect onLand = new FortressEffect();

        [Tooltip("キラッ（砲台ごと）。")]
        public FortressEffect onSparkle = new FortressEffect();

        [Tooltip("4色の光がコアに届いたとき（コアで1回）。")]
        public FortressEffect onGather = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("OK マークが出たとき（砲台ごと）。")]
        public FortressEffect onOk = new FortressEffect();

        public bool Clockwise => direction == SeatRotationDirection.Clockwise;

        // ───────── 時間割（全員のPCで同じ。Host はこれで「席替え」の段階の長さを決める） ─────────

        /// <summary>溜めの秒数（溜めが OFF なら 0）。</summary>
        public float AnticipationTime => anticipation ? anticipationSeconds : 0f;

        /// <summary>1基が跳んでいる秒数（ふわっとの分を含む）。</summary>
        public float FlightSeconds => jumpSeconds + (hangAtApex ? hangSeconds : 0f);

        /// <summary>1基ごとに着地をずらす秒数（OFF なら 0）。</summary>
        public float StaggerTime => staggerLanding ? staggerSeconds : 0f;

        /// <summary>ヒットストップで止まる秒数の合計（ずらして着地するなら4回、そろえて着地するなら1回）。</summary>
        public float HitStopTotal => hitStop ? hitStopSeconds * (StaggerTime > 0f ? FortressSeats.SeatCount : 1) : 0f;

        /// <summary>カメラが次の席へ動き始める時刻（席替えの始めから）。</summary>
        public float CameraStartTime => AnticipationTime + cameraDelaySeconds;

        /// <summary>溜めの始めから、最後の砲台のつぶれが戻るまでの秒数（ヒットストップを含む）。</summary>
        public float TotalSeconds =>
            AnticipationTime + StaggerTime * (FortressSeats.SeatCount - 1) + FlightSeconds + landSquashSeconds + HitStopTotal;

        /// <summary>着地の後の締め（YOU の矢印・光の柱・光がコアへ・OK マーク）の秒数。</summary>
        public float FinaleSeconds => showYouMarker || youPillar || gatherToCore || okMarks ? youMarkerSeconds : 0f;

        /// <summary>
        /// ウェーブの「席替え」の段階の秒数（OFF なら 0）。跳ぶ動きとカメラが落ち着いて、締めが終わってから発表が始まる。
        /// </summary>
        public float WaveSeconds =>
            enabled ? Mathf.Max(TotalSeconds, CameraStartTime + cameraBlendSeconds) + FinaleSeconds : 0f;
    }
}
