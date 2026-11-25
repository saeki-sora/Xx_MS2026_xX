using System;
using System.Collections.Generic;
using System.Linq;

namespace MS2026.SoundStudio
{
    public enum LoudnessMark
    {
        Normal,
        Loud,
        Quiet,
    }

    /// <summary>音の大きさの計算（dB・平均の強さ・ばらつきの印）。Unity に頼らないのでテストできる。</summary>
    public static class LoudnessMath
    {
        /// <summary>無音のときの dB（これより小さい値は出さない）。</summary>
        public const float Silence = -120f;

        public static float ToDb(float linear)
        {
            return linear <= 1e-6f ? Silence : Math.Max(Silence, (float)(20.0 * Math.Log10(linear)));
        }

        /// <summary>平均の強さ（二乗平均の平方根）。<paramref name="count"/> 個目までを使う。</summary>
        public static float Rms(float[] samples, int count)
        {
            if (samples == null || count <= 0)
            {
                return 0f;
            }

            double sum = 0;
            for (var i = 0; i < count; i++)
            {
                sum += samples[i] * samples[i];
            }

            return (float)Math.Sqrt(sum / count);
        }

        public static float Peak(float[] samples, int count)
        {
            var peak = 0f;
            for (var i = 0; samples != null && i < count; i++)
            {
                peak = Math.Max(peak, Math.Abs(samples[i]));
            }

            return peak;
        }

        public static float Median(IList<float> values)
        {
            if (values == null || values.Count == 0)
            {
                return 0f;
            }

            var sorted = values.OrderBy(v => v).ToList();
            var mid = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) * 0.5f;
        }

        /// <summary>素材の大きさ（dB）に、音そのものの音量（0〜1）を掛けた dB。</summary>
        public static float WithVolume(float db, float volume)
        {
            return volume <= 1e-6f ? Silence : Math.Max(Silence, db + ToDb(volume));
        }

        /// <summary>真ん中の大きさから <paramref name="tolerance"/> dB 以上離れていたら、大きい／小さいの印を付ける。</summary>
        public static LoudnessMark Classify(float db, float median, float tolerance)
        {
            if (db >= median + tolerance)
            {
                return LoudnessMark.Loud;
            }

            return db <= median - tolerance ? LoudnessMark.Quiet : LoudnessMark.Normal;
        }
    }
}
