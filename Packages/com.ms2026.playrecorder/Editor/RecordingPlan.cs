using System;
using System.Collections.Generic;

namespace MS2026.PlayRecorder.Editor
{
    /// <summary>画質の段（縦の解像度と fps）。</summary>
    internal readonly struct QualityRung : IEquatable<QualityRung>
    {
        public readonly int Height;
        public readonly int Fps;

        public QualityRung(int height, int fps)
        {
            Height = height;
            Fps = fps;
        }

        public bool Equals(QualityRung other) => Height == other.Height && Fps == other.Fps;
        public override bool Equals(object obj) => obj is QualityRung r && Equals(r);
        public override int GetHashCode() => Height * 397 ^ Fps;
        public override string ToString() => $"{Height}p {Fps}fps";
    }

    /// <summary>書き出しに使う映像の設定。</summary>
    internal readonly struct VideoPlan
    {
        public readonly int RungIndex;
        public readonly int Width;
        public readonly int Height;
        public readonly int Fps;
        public readonly long BitsPerSecond;

        public VideoPlan(int rungIndex, int width, int height, int fps, long bitsPerSecond)
        {
            RungIndex = rungIndex;
            Width = width;
            Height = height;
            Fps = fps;
            BitsPerSecond = bitsPerSecond;
        }

        public override string ToString() => $"{Width}x{Height} {Fps}fps {BitsPerSecond / 1000}kbps";
    }

    internal enum AudioMode
    {
        None = 0,
        Mono = 1,
        Stereo = 2,
    }

    /// <summary>
    /// 「上限サイズに収める」ための計算（Unity に依存しない純粋な計算だけ）。
    ///
    /// 考え方：上限サイズ ÷ 録画の長さ = 使えるビットレート。
    /// ビットレートが足りないのに解像度を保つと画面がブロックノイズだらけになるので、
    /// 1 画素あたりのビット数（bpp）が <see cref="MinBitsPerPixel"/> を下回らない範囲で一番良い段を選ぶ。
    /// 長く録るほど段が下がる（720p30 → 540p30 → … → 144p10）が、録画を途中で切ることはない。
    /// </summary>
    internal static class RecordingPlan
    {
        /// <summary>段の一覧（上から良い順）。高さ・fps とも下に行くほど増えないこと（録画中の取り込み画質の前提）。</summary>
        static readonly QualityRung[] BaseLadder =
        {
            new QualityRung(1080, 60),
            new QualityRung(1080, 30),
            new QualityRung(720, 30),
            new QualityRung(540, 30),
            new QualityRung(480, 30),
            new QualityRung(360, 30),
            new QualityRung(360, 24),
            new QualityRung(288, 20),
            new QualityRung(240, 15),
            new QualityRung(180, 12),
            new QualityRung(144, 10),
        };

        /// <summary>H.264 でゲーム画面が見られる画質になる 1 画素あたりのビット数の目安。</summary>
        public const double MinBitsPerPixel = 0.07;

        /// <summary>これ以上ビットを使っても見た目がほぼ変わらない上限（短い録画を無駄に大きくしない）。</summary>
        public const double MaxBitsPerPixel = 0.2;

        /// <summary>エンコーダのブレ用に上限から引いておく割合。</summary>
        public const double SafetyFactor = 0.92;

        /// <summary>MP4 の入れ物そのものの大きさの見込み。</summary>
        public const long ContainerOverheadBytes = 64 * 1024;

        public const long MinVideoBitsPerSecond = 40_000;

        /// <summary>設定の上限と元画面の高さで段を切りそろえる（重複は 1 つにまとめる）。</summary>
        public static List<QualityRung> BuildLadder(int maxHeight, int maxFps, int sourceHeight)
        {
            int capH = Math.Max(144, Math.Min(maxHeight, sourceHeight > 0 ? sourceHeight : maxHeight));
            var result = new List<QualityRung>();
            foreach (var r in BaseLadder)
            {
                var c = new QualityRung(Math.Min(r.Height, capH), Math.Min(r.Fps, maxFps));
                if (result.Count == 0 || !result[result.Count - 1].Equals(c))
                    result.Add(c);
            }
            return result;
        }

        /// <summary>縦横比を保った偶数の横幅（H.264 は偶数サイズが必要）。</summary>
        public static int EvenWidth(int height, float aspect)
        {
            int w = (int)Math.Round(height * (double)aspect);
            w = Math.Max(16, w);
            return w + (w & 1);
        }

        public static int EvenHeight(int height) => Math.Max(16, height + (height & 1));

        /// <summary>映像に使えるビットレート（bit/秒）。</summary>
        public static long VideoBitsPerSecond(long limitBytes, double durationSec, double audioBytesPerSec)
        {
            double d = Math.Max(durationSec, 1.0);
            double usableBytes = limitBytes * SafetyFactor - ContainerOverheadBytes - audioBytesPerSec * d;
            double bps = usableBytes * 8.0 / d;
            return Math.Max(MinVideoBitsPerSecond, (long)bps);
        }

        /// <summary>ビットレートに見合う一番良い段を選ぶ（<paramref name="minRungIndex"/> より上の段は使わない）。</summary>
        public static VideoPlan Choose(IReadOnlyList<QualityRung> ladder, float aspect, long videoBitsPerSecond, int minRungIndex = 0)
        {
            int start = Math.Max(0, Math.Min(minRungIndex, ladder.Count - 1));
            for (int i = start; i < ladder.Count; i++)
            {
                var r = ladder[i];
                int h = EvenHeight(r.Height);
                int w = EvenWidth(h, aspect);
                double bpp = videoBitsPerSecond / ((double)w * h * r.Fps);
                if (bpp >= MinBitsPerPixel || i == ladder.Count - 1)
                {
                    long cap = (long)((double)w * h * r.Fps * MaxBitsPerPixel);
                    return new VideoPlan(i, w, h, r.Fps, Math.Min(videoBitsPerSecond, cap));
                }
            }
            throw new InvalidOperationException("ladder is empty");
        }

        /// <summary>
        /// 録画中の取り込み画質。今の経過時間で止めたときの段＝これ以降これより良い段にはならないので、
        /// それより細かく取り込んでも無駄になる（一時ファイルが増えすぎないための工夫）。
        /// </summary>
        public static QualityRung CaptureRung(IReadOnlyList<QualityRung> ladder, float aspect, long limitBytes, double elapsedSec)
        {
            var plan = Choose(ladder, aspect, VideoBitsPerSecond(limitBytes, elapsedSec, 0), 0);
            return ladder[plan.RungIndex];
        }

        /// <summary>音の入れ方を決める。音だけで上限を食いつぶさないよう、ステレオ → モノラル → 無しの順に落とす。</summary>
        public static AudioMode ChooseAudio(bool enabled, int sourceChannels, double durationSec, long limitBytes,
            Func<AudioMode, double> bytesPerSecond)
        {
            if (!enabled || sourceChannels <= 0) return AudioMode.None;
            double budget = limitBytes * SafetyFactor;
            double d = Math.Max(durationSec, 1.0);
            if (sourceChannels >= 2 && bytesPerSecond(AudioMode.Stereo) * d <= budget * 0.25) return AudioMode.Stereo;
            if (bytesPerSecond(AudioMode.Mono) * d <= budget * 0.4) return AudioMode.Mono;
            return AudioMode.None;
        }

        /// <summary>設定画面の「目安」表用：長さごとの段。</summary>
        public static VideoPlan Preview(int maxHeight, int maxFps, long limitBytes, double durationSec, double audioBytesPerSec, float aspect = 16f / 9f)
        {
            var ladder = BuildLadder(maxHeight, maxFps, 0);
            return Choose(ladder, aspect, VideoBitsPerSecond(limitBytes, durationSec, audioBytesPerSec));
        }
    }
}
