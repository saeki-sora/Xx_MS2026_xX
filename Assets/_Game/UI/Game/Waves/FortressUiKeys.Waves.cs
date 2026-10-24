using System.Collections.Generic;
using UnityEngine;

namespace MS2026.UI.Game
{
    // ウェーブ（WAVE n / 目標撃破数 / 制限時間 / 発表 / クリア）の値。書くのは WaveUiPublisher。
    public static partial class FortressUiKeys
    {
        public const string WaveActive = "wave.active";
        public const string WaveNumber = "wave.number";
        public const string WaveCount = "wave.count";
        public const string WaveNumberText = "wave.numberText";
        public const string WaveIsFinal = "wave.isFinal";
        public const string WavePhase = "wave.phase";
        public const string WaveKills = "wave.kills";
        public const string WaveTarget = "wave.target";
        public const string WaveRemaining = "wave.remaining";
        public const string WaveProgress01 = "wave.progress01";
        public const string WaveNearGoal = "wave.nearGoal";
        public const string WaveHasTimeLimit = "wave.hasTimeLimit";
        public const string WaveTimeLeft = "wave.timeLeft";
        public const string WaveTimeText = "wave.timeText";
        public const string WaveTimeWarning = "wave.timeWarning";
        public const string WaveAnnouncing = "wave.announcing";
        public const string WaveAnnounceTitle = "wave.announceTitle";
        public const string WaveAnnounceGoal = "wave.announceGoal";
        public const string WaveAnnounceText = "wave.announceText";
        public const string WaveCountdown = "wave.countdown";
        public const string WaveGo = "wave.go";
        public const string WaveClearShowing = "wave.clearShowing";
        public const string WaveClearText = "wave.clearText";
        public const string WaveBreak = "wave.break";
        public const string WaveTimeUp = "wave.timeUp";
        public const string WaveFirstHint = "wave.firstHint";
        public const string WaveShowBanner = "wave.showBanner";
        public const string WaveShowNewEnemies = "wave.showNewEnemies";
        public const string WaveShowRouteHint = "wave.showRouteHint";

        public const string ResultReason = "result.reason";
        public const string ResultReachedWave = "result.reachedWave";
        public const string ResultSubtitle = "result.subtitle";

        private static IEnumerable<(string, string, string, UiValueKind, string, Vector2, float)> DescribeWaves()
        {
            const string c = "ウェーブ";
            var list = new List<(string, string, string, UiValueKind, string, Vector2, float)>
            {
                (WaveActive, "ウェーブで遊んでいる", "ステージにウェーブの計画があり、試合中のときON（無いステージでは WAVE の札を隠す）。", UiValueKind.Bool, c, Vector2.up, 1),
                (WaveNumber, "今のウェーブ", "1 から。", UiValueKind.Number, c, new Vector2(1, 5), 2),
                (WaveCount, "ウェーブの数", "最後のウェーブの番号。", UiValueKind.Number, c, new Vector2(1, 9), 5),
                (WaveNumberText, "ウェーブ（2/5 の形）", "例: 2/5。", UiValueKind.Text, c, Vector2.up, 0),
                (WaveIsFinal, "最終ウェーブ", "最後のウェーブのときON。", UiValueKind.Bool, c, Vector2.up, 0),
                (WavePhase, "ウェーブの段階", "0=なし、1=準備待ち、2=発表、3=戦い、4=クリア（撤退）、5=合間、6=終わり。", UiValueKind.Number, c, new Vector2(0, 6), 3),
                (WaveKills, "このウェーブで倒した数", "目標で止まる。", UiValueKind.Number, c, new Vector2(0, 200), 12),
                (WaveTarget, "このウェーブの目標撃破数", "この数を倒したら次のウェーブ。", UiValueKind.Number, c, new Vector2(0, 200), 35),
                (WaveRemaining, "目標まであと何体", "目標 − 倒した数。", UiValueKind.Number, c, new Vector2(0, 200), 23),
                (WaveProgress01, "目標までの進み具合（割合）", "0=始まったばかり、1=目標に届いた。", UiValueKind.Number, c, Vector2.up, 0.34f),
                (WaveNearGoal, "目標まであと少し", "戦っている間、あと5体以下になったらON（「あと5体！」の盛り上げ）。", UiValueKind.Bool, c, Vector2.up, 0),
                (WaveHasTimeLimit, "制限時間がある", "計画に制限時間があるときON。", UiValueKind.Bool, c, Vector2.up, 1),
                (WaveTimeLeft, "試合の残り時間（秒）", "制限時間が無ければ −1。", UiValueKind.Number, c, new Vector2(-1, 600), 168),
                (WaveTimeText, "試合の残り時間（02:48 の形）", "制限時間が無ければ「--:--」。", UiValueKind.Text, c, Vector2.up, 0),
                (WaveTimeWarning, "残り時間が少ない", "時計が動いていて、残り10秒以下のときON。", UiValueKind.Bool, c, Vector2.up, 0),
                (WaveAnnouncing, "発表中", "「WAVE n！目標撃破数 m！」を出している間ON。", UiValueKind.Bool, c, Vector2.up, 1),
                (WaveAnnounceTitle, "発表の大きな文字", "例: WAVE 2 / FINAL WAVE。", UiValueKind.Text, c, Vector2.up, 0),
                (WaveAnnounceGoal, "発表の目標の文字", "例: 目標撃破数 60！", UiValueKind.Text, c, Vector2.up, 0),
                (WaveAnnounceText, "発表の一言", "計画に書いた一言（無ければ空）。", UiValueKind.Text, c, Vector2.up, 0),
                (WaveCountdown, "3・2・1", "発表の最後の 3・2・1。出していないときは 0。", UiValueKind.Number, c, new Vector2(0, 3), 2),
                (WaveGo, "GO!", "ウェーブが始まってから少しの間ON。", UiValueKind.Bool, c, Vector2.up, 0),
                (WaveClearShowing, "WAVE CLEAR! 中", "目標を倒して、敵が逃げている間ON。", UiValueKind.Bool, c, Vector2.up, 0),
                (WaveClearText, "クリアの文字", "例: WAVE 2 CLEAR!", UiValueKind.Text, c, Vector2.up, 0),
                (WaveBreak, "合間中", "ウェーブの合間（歯みがきタイムなど）の間ON。", UiValueKind.Bool, c, Vector2.up, 0),
                (WaveTimeUp, "時間切れ", "試合の制限時間が来たときON。", UiValueKind.Bool, c, Vector2.up, 0),
                (WaveFirstHint, "握り方のヒントを出す", "試合の始まり（全員を待っている間と、WAVE 1 の発表の前のヒントの段階）ON。", UiValueKind.Bool, c, Vector2.up, 0),
                (WaveShowBanner, "「WAVE n／目標」を出す", "発表の中で、表示の設定の秒数がたった後 ON。", UiValueKind.Bool, c, Vector2.up, 1),
                (WaveShowNewEnemies, "NEW ENEMY を出す", "発表の中で、表示の設定の秒数がたった後 ON（初めて出てくる敵がいれば、カードが出る）。", UiValueKind.Bool, c, Vector2.up, 1),
                (WaveShowRouteHint, "「赤い線＝敵の通り道」を出す", "発表の間、表示の設定で ON のときだけ ON。", UiValueKind.Bool, c, Vector2.up, 0),
                (ResultReason, "試合が終わった理由", "0=なし、1=撃破数、2=全部のウェーブをクリア、3=コアが壊れた、4=時間切れ。", UiValueKind.Number, "リザルト", new Vector2(0, 4), 4),
                (ResultReachedWave, "行けたウェーブ", "どのウェーブまで行けたか（1 から。計画が無ければ 0）。", UiValueKind.Number, "リザルト", new Vector2(0, 5), 3),
                (ResultSubtitle, "リザルトの一言", "例: 時間切れ… WAVE 3 まで行けた！ / コアが壊されてしまった…", UiValueKind.Text, "リザルト", Vector2.up, 0)
            };
            list.AddRange(DescribeBrushing());
            return list;
        }
    }
}
