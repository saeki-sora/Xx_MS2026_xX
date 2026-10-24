using System.Collections.Generic;
using UnityEngine;

namespace MS2026.UI.Game
{
    // 歯みがきタイム（brush.*）と、コアのコーティング（core.coating*）の値。書くのは WaveUiPublisher。
    public static partial class FortressUiKeys
    {
        public const string BrushActive = "brush.active";
        public const string BrushStage = "brush.stage";
        public const string BrushIntro = "brush.intro";
        public const string BrushBrushing = "brush.brushing";
        public const string BrushResult = "brush.result";
        public const string BrushClean01 = "brush.clean01";
        public const string BrushTimeLeft = "brush.timeLeft";
        public const string BrushPulses = "brush.pulses";
        public const string BrushSyncCount = "brush.syncCount";
        public const string BrushSyncShowing = "brush.syncShowing";
        public const string BrushResultText = "brush.resultText";
        public const string BrushResultTitle = "brush.resultTitle";
        public const string BrushRemaining = "brush.remaining";
        public const string BrushTime01 = "brush.time01";
        public const string BrushPreviewText = "brush.previewText";
        public const string BrushFullText = "brush.fullText";

        public const string CoreCoating = "core.coating";
        public const string CoreCoating01 = "core.coating01";
        public const string CoreHasCoating = "core.hasCoating";

        public static string BrushShare(int player) => $"brush.player.{player + 1}.share01";
        public static string BrushIn(int player) => $"brush.player.{player + 1}.in";

        private static IEnumerable<(string, string, string, UiValueKind, string, Vector2, float)> DescribeBrushing()
        {
            const string c = "歯みがき";
            var list = new List<(string, string, string, UiValueKind, string, Vector2, float)>
            {
                (BrushActive, "歯みがきタイム中", "ウェーブの合間の歯みがきタイムの間ON。", UiValueKind.Bool, c, Vector2.up, 1),
                (BrushStage, "歯みがきの段階", "0=はじめの説明、1=みがいている、2=結果。", UiValueKind.Number, c, new Vector2(0, 2), 1),
                (BrushIntro, "はじめの説明中", "「歯みがきタイム！」と手のアニメを出している間ON。", UiValueKind.Bool, c, Vector2.up, 0),
                (BrushBrushing, "みがいている", "にぎにぎを数えている間ON。", UiValueKind.Bool, c, Vector2.up, 1),
                (BrushResult, "結果を出している", "HP +12 などを出している間ON。", UiValueKind.Bool, c, Vector2.up, 0),
                (BrushClean01, "きれい度（割合）", "全員のにぎにぎで 0→1。", UiValueKind.Number, c, Vector2.up, 0.62f),
                (BrushTimeLeft, "みがく残り秒", "切り上げた秒数。", UiValueKind.Number, c, new Vector2(0, 10), 5),
                (BrushPulses, "自分のにぎにぎの回数", "この合間に自分が握った回数（1回ごとにはずむ動き用）。", UiValueKind.Number, c, new Vector2(0, 40), 11),
                (BrushSyncCount, "シンクロの回数", "全員がそろって握れた回数。", UiValueKind.Number, c, new Vector2(0, 10), 2),
                (BrushSyncShowing, "シンクロ！を出している", "シンクロした直後の少しの間ON。", UiValueKind.Bool, c, Vector2.up, 0),
                (BrushResultText, "歯みがきの結果の文字", "例: HP +12　コーティング +5", UiValueKind.Text, c, Vector2.up, 0),
                (BrushResultTitle, "歯みがきの結果の見出し", "ピカピカ！ / きれいになった！ / もう少し…（きれい度で変わる）。", UiValueKind.Text, c, Vector2.up, 0),
                (BrushRemaining, "100% まであと何回", "全員分の「にぎにぎ」の残りの回数。", UiValueKind.Number, c, new Vector2(0, 72), 12),
                (BrushTime01, "みがく残り時間（割合）", "1=始まったばかり、0=時間切れ（残り時間の輪）。", UiValueKind.Number, c, Vector2.up, 0.6f),
                (BrushPreviewText, "今もらえるごほうび", "例: いま HP +120（みがいている間、きれい度で増える）。", UiValueKind.Text, c, Vector2.up, 0),
                (BrushFullText, "100% のごほうび", "例: 100% で +250", UiValueKind.Text, c, Vector2.up, 0),
                (CoreCoating, "コアのコーティング", "ダメージを先に受けてくれる盾の量（歯みがきのごほうび。次の1ウェーブだけ）。", UiValueKind.Number, "コア", new Vector2(0, 30), 8),
                (CoreCoating01, "コアのコーティング（割合）", "コーティング ÷ コアの最大HP。", UiValueKind.Number, "コア", Vector2.up, 0.08f),
                (CoreHasCoating, "コーティングがある", "コーティングが少しでも残っているときON。", UiValueKind.Bool, "コア", Vector2.up, 1)
            };

            for (var p = 0; p < 4; p++)
            {
                list.Add((BrushShare(p), $"P{p + 1}の歯みがきの頑張り（割合）", "1人分の目標に対して、何割にぎにぎしたか。", UiValueKind.Number, c, Vector2.up, 0.3f + 0.2f * p));
                list.Add((BrushIn(p), $"P{p + 1}が歯みがきに参加", "その人が試合にいるときON（オフラインでは握った人）。", UiValueKind.Bool, c, Vector2.up, p < 3 ? 1 : 0));
            }

            return list;
        }
    }
}
