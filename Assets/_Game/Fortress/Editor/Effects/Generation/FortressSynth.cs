using System;
using System.IO;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 仮の効果音・BGMをコードで作る小さなシンセ（モノラル16bit WAV）。本番の素材に差し替えるまでの仮置き用。
    /// 世界観: 口内ケア×サイバー。泡のポップ、きらめき、ミント色のレーザー、電子的なビート。
    /// </summary>
    public static class FortressSynth
    {
        public const int Rate = 32000;

        public enum Wave { Sine, Square, Saw, Tri }

        // ───────── 基本部品 ─────────

        public static float[] Buffer(float seconds) => new float[Mathf.Max(1, Mathf.CeilToInt(seconds * Rate))];

        public static float Midi(float note) => 440f * Mathf.Pow(2f, (note - 69f) / 12f);

        private static float Osc(Wave wave, float phase01)
        {
            switch (wave)
            {
                case Wave.Square: return phase01 < 0.5f ? 1f : -1f;
                case Wave.Saw: return phase01 * 2f - 1f;
                case Wave.Tri: return 4f * Mathf.Abs(phase01 - 0.5f) - 1f;
                default: return Mathf.Sin(phase01 * Mathf.PI * 2f);
            }
        }

        /// <summary>音程が freq0→freq1 に動く音を足す。attack秒で立ち上がり、decayPowerが大きいほど早く減衰する。</summary>
        public static void Tone(float[] buf, float start, float dur, float freq0, float freq1, float amp,
            Wave wave = Wave.Sine, float attack = 0.003f, float decayPower = 2f)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            int n = Mathf.RoundToInt(dur * Rate);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx < 0 || idx >= buf.Length)
                {
                    if (idx >= buf.Length) break;
                    continue;
                }

                float t = i / (float)n;
                float f = Mathf.Lerp(freq0, freq1, t);
                phase += f / Rate;
                phase -= Math.Floor(phase);
                float env = Mathf.Pow(1f - t, decayPower);
                float atk = attack > 0f ? Mathf.Clamp01(i / (attack * Rate)) : 1f;
                buf[idx] += Osc(wave, (float)phase) * amp * env * atk;
            }
        }

        /// <summary>ノイズ（lowpass: 0..1 小さいほどこもる / highpass: 低音を削る）を足す。</summary>
        public static void Noise(float[] buf, float start, float dur, float amp, float decayPower = 2f,
            float lowpass = 1f, float highpass = 0f, int seed = 1)
        {
            var rng = new System.Random(seed);
            int s0 = Mathf.RoundToInt(start * Rate);
            int n = Mathf.RoundToInt(dur * Rate);
            float lp = 0f, hpState = 0f;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= buf.Length) break;
                if (idx < 0) continue;

                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (x - lp) * Mathf.Clamp01(lowpass);
                float y = lp;
                if (highpass > 0f)
                {
                    hpState += (y - hpState) * Mathf.Clamp01(highpass);
                    y -= hpState;
                }

                float t = i / (float)n;
                buf[idx] += y * amp * Mathf.Pow(1f - t, decayPower);
            }
        }

        /// <summary>きらめき: 高い音を短く重ねる。</summary>
        public static void Sparkle(float[] buf, float start, float amp, int count, float baseNote, float spacing, int seed = 3)
        {
            var rng = new System.Random(seed);
            int[] scale = { 0, 2, 4, 7, 9, 12 };
            for (int i = 0; i < count; i++)
            {
                float note = baseNote + scale[rng.Next(scale.Length)] + (rng.Next(2) == 0 ? 0 : 12);
                Tone(buf, start + i * spacing, 0.12f, Midi(note), Midi(note), amp, Wave.Sine, 0.001f, 3f);
            }
        }

        /// <summary>音量を揃え、先頭と末尾のクリックを防ぐ。</summary>
        public static void Finish(float[] buf, float peak = 0.8f, float fadeSeconds = 0.004f)
        {
            float max = 0.0001f;
            for (int i = 0; i < buf.Length; i++)
            {
                max = Mathf.Max(max, Mathf.Abs(buf[i]));
            }

            float g = peak / max;
            int fade = Mathf.Min(buf.Length / 2, Mathf.RoundToInt(fadeSeconds * Rate));
            for (int i = 0; i < buf.Length; i++)
            {
                float f = 1f;
                if (i < fade) f = i / (float)fade;
                else if (i >= buf.Length - fade) f = (buf.Length - 1 - i) / (float)fade;
                buf[i] = Mathf.Clamp(buf[i] * g * f, -1f, 1f);
            }
        }

        /// <summary>BGMループ用: 先頭の無音化を避けて音量だけ揃える（端のフェードなし）。</summary>
        public static void NormalizeOnly(float[] buf, float peak = 0.7f)
        {
            float max = 0.0001f;
            for (int i = 0; i < buf.Length; i++)
            {
                max = Mathf.Max(max, Mathf.Abs(buf[i]));
            }

            float g = peak / max;
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] = Mathf.Clamp(buf[i] * g, -1f, 1f);
            }
        }

        public static void SaveWav(string path, float[] buf)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs))
            {
                int dataBytes = buf.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(Rate);
                w.Write(Rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                for (int i = 0; i < buf.Length; i++)
                {
                    w.Write((short)Mathf.RoundToInt(Mathf.Clamp(buf[i], -1f, 1f) * 32767f));
                }
            }
        }

        // ───────── 効果音 ─────────

        /// <summary>レーザー発射: ミント色の「ぴゅいん」＋泡のきらめき。</summary>
        public static float[] LaserMuzzle()
        {
            var b = Buffer(0.45f);
            Tone(b, 0f, 0.35f, 380f, 1500f, 0.5f, Wave.Saw, 0.004f, 1.6f);
            Tone(b, 0f, 0.35f, 760f, 3000f, 0.25f, Wave.Sine, 0.004f, 1.6f);
            Tone(b, 0f, 0.4f, 90f, 60f, 0.5f, Wave.Sine, 0.002f, 2f);
            Noise(b, 0f, 0.2f, 0.18f, 2f, 0.6f, 0.2f, 11);
            Sparkle(b, 0.1f, 0.18f, 4, 84f, 0.05f, 5);
            Finish(b);
            return b;
        }

        /// <summary>レーザー着弾: 水しぶきのぷしゅっ。</summary>
        public static float[] LaserImpact()
        {
            var b = Buffer(0.22f);
            Noise(b, 0f, 0.2f, 0.5f, 2.2f, 0.45f, 0.05f, 21);
            Tone(b, 0f, 0.12f, 900f, 300f, 0.35f, Wave.Sine, 0.001f, 2.5f);
            Sparkle(b, 0.02f, 0.12f, 2, 88f, 0.04f, 7);
            Finish(b);
            return b;
        }

        /// <summary>群衆ヒット: 菌がはじけるプチッ。短く、連打されても耳に痛くない。</summary>
        public static float[] SwarmHit()
        {
            var b = Buffer(0.12f);
            Tone(b, 0f, 0.07f, 1300f, 380f, 0.5f, Wave.Sine, 0.001f, 3f);
            Noise(b, 0f, 0.03f, 0.35f, 2f, 0.9f, 0.3f, 31);
            Finish(b);
            return b;
        }

        /// <summary>破壊物ヒット: 石けんをコツンと叩く音。</summary>
        public static float[] DestructHit()
        {
            var b = Buffer(0.16f);
            Tone(b, 0f, 0.14f, 320f, 140f, 0.6f, Wave.Sine, 0.001f, 3f);
            Tone(b, 0f, 0.08f, 1100f, 700f, 0.2f, Wave.Tri, 0.001f, 3f);
            Noise(b, 0f, 0.04f, 0.25f, 2f, 0.5f, 0.1f, 41);
            Finish(b);
            return b;
        }

        /// <summary>段階変化: ピシッとひびが入る。</summary>
        public static float[] DestructStage()
        {
            var b = Buffer(0.3f);
            Noise(b, 0f, 0.05f, 0.6f, 1.5f, 0.9f, 0.4f, 51);
            Noise(b, 0.06f, 0.04f, 0.45f, 1.5f, 0.9f, 0.4f, 52);
            Noise(b, 0.11f, 0.06f, 0.4f, 1.5f, 0.9f, 0.4f, 53);
            Tone(b, 0f, 0.25f, 220f, 110f, 0.35f, Wave.Sine, 0.001f, 2.5f);
            Sparkle(b, 0.12f, 0.1f, 3, 91f, 0.04f, 9);
            Finish(b);
            return b;
        }

        /// <summary>破壊: 砕ける＋泡がはじける＋きらきら。</summary>
        public static float[] DestructBreak()
        {
            var b = Buffer(0.7f);
            Tone(b, 0f, 0.4f, 180f, 45f, 0.7f, Wave.Sine, 0.001f, 2f);
            Noise(b, 0f, 0.45f, 0.6f, 1.8f, 0.55f, 0.04f, 61);
            Noise(b, 0.02f, 0.15f, 0.35f, 2f, 0.95f, 0.5f, 62);
            Sparkle(b, 0.08f, 0.2f, 8, 84f, 0.045f, 13);
            Finish(b);
            return b;
        }

        /// <summary>再生: ふわっと上がるチャイム（復活）。</summary>
        public static float[] DestructRegen()
        {
            var b = Buffer(0.6f);
            float[] notes = { 72f, 76f, 79f, 84f };
            for (int i = 0; i < notes.Length; i++)
            {
                Tone(b, i * 0.07f, 0.35f, Midi(notes[i]), Midi(notes[i]), 0.35f, Wave.Sine, 0.004f, 2.2f);
                Tone(b, i * 0.07f, 0.3f, Midi(notes[i] + 12f), Midi(notes[i] + 12f), 0.12f, Wave.Sine, 0.004f, 2.5f);
            }

            Noise(b, 0f, 0.25f, 0.08f, 2f, 0.3f, 0.1f, 71);
            Finish(b);
            return b;
        }

        /// <summary>スマッシュボールが割れる: 派手なガラス割れ＋上昇アルペジオ＋ドン。</summary>
        public static float[] SmashBreak()
        {
            var b = Buffer(1.3f);
            Tone(b, 0f, 0.6f, 150f, 35f, 0.9f, Wave.Sine, 0.001f, 1.8f);
            Noise(b, 0f, 0.7f, 0.55f, 1.6f, 0.6f, 0.03f, 81);
            Noise(b, 0.01f, 0.35f, 0.4f, 2f, 0.97f, 0.6f, 82);
            float[] notes = { 67f, 71f, 74f, 79f, 83f, 86f, 91f };
            for (int i = 0; i < notes.Length; i++)
            {
                Tone(b, 0.08f + i * 0.06f, 0.5f, Midi(notes[i]), Midi(notes[i]), 0.3f, Wave.Square, 0.002f, 2.2f);
                Tone(b, 0.08f + i * 0.06f, 0.5f, Midi(notes[i] + 12f), Midi(notes[i] + 12f), 0.1f, Wave.Sine, 0.002f, 2.2f);
            }

            Sparkle(b, 0.2f, 0.18f, 14, 88f, 0.045f, 17);
            Finish(b);
            return b;
        }

        /// <summary>コアクリスタルが割れる: 鋭いガラスの砕け音（パリーン）＋高い破片のきらめきが散る。</summary>
        public static float[] CoreShatter()
        {
            var b = Buffer(0.9f);
            Noise(b, 0f, 0.06f, 0.9f, 1.2f, 1f, 0.7f, 91);
            Noise(b, 0f, 0.4f, 0.4f, 2.2f, 0.95f, 0.5f, 92);
            Tone(b, 0f, 0.15f, 2400f, 900f, 0.4f, Wave.Sine, 0.0005f, 3f);
            Tone(b, 0f, 0.3f, 140f, 60f, 0.3f, Wave.Sine, 0.001f, 2.5f);
            // 破片が床に散る高い音（音程をばらして不規則に）
            var rng = new System.Random(93);
            for (int i = 0; i < 18; i++)
            {
                float t = 0.03f + i * 0.035f + (float)rng.NextDouble() * 0.02f;
                float f = 2200f + (float)rng.NextDouble() * 3800f;
                Tone(b, t, 0.1f + (float)rng.NextDouble() * 0.12f, f, f * 0.92f, 0.16f * (1f - i / 24f), Wave.Sine, 0.0005f, 3f);
            }

            Sparkle(b, 0.12f, 0.12f, 6, 96f, 0.05f, 19);
            Finish(b);
            return b;
        }

        /// <summary>オーバーヒート: 「シュゥゥ」と蒸気が抜ける音。</summary>
        public static float[] Overheat()
        {
            var b = Buffer(1.0f);
            Noise(b, 0f, 0.9f, 0.5f, 1.2f, 0.6f, 0.15f, 101);
            Noise(b, 0f, 0.5f, 0.3f, 2f, 0.95f, 0.5f, 102);
            Tone(b, 0f, 0.5f, 520f, 150f, 0.25f, Wave.Sine, 0.01f, 2f);
            Finish(b);
            return b;
        }

        // ───────── BGM ─────────

        public const float BgmBpm = 140f;
        public const int BgmBars = 8;

        /// <summary>疾走感のあるサイバー・エレクトロ（Am-F-C-G、8小節ループ）。仮置き用の簡易版。</summary>
        public static float[] BgmCyberRun()
        {
            float step = 60f / BgmBpm / 4f; // 16分音符
            int steps = BgmBars * 16;
            var b = Buffer(steps * step);
            // 2小節ずつ Am / F / C / G
            int[] roots = { 45, 41, 48, 43 };
            int[][] chords =
            {
                new[] { 0, 3, 7, 12 }, // Am: A C E A
                new[] { 0, 4, 7, 12 }, // F : F A C F
                new[] { 0, 4, 7, 12 }, // C : C E G C
                new[] { 0, 4, 7, 11 }, // G : G B D F#→Gmaj7風
            };
            int[] arpPattern = { 0, 1, 2, 3, 2, 1, 2, 3, 0, 1, 2, 3, 2, 3, 2, 1 };

            for (int s = 0; s < steps; s++)
            {
                float t = s * step;
                int bar = s / 16;
                int chordIdx = (bar / 2) % 4;
                int stepInBar = s % 16;
                int root = roots[chordIdx];

                // キック（4つ打ち）
                if (stepInBar % 4 == 0)
                {
                    Tone(b, t, 0.18f, 130f, 42f, 0.9f, Wave.Sine, 0.001f, 2.5f);
                    Noise(b, t, 0.01f, 0.2f, 1f, 0.9f, 0f, s + 100);
                }

                // スネア（2・4拍）
                if (stepInBar == 4 || stepInBar == 12)
                {
                    Noise(b, t, 0.16f, 0.35f, 1.6f, 0.8f, 0.2f, s + 200);
                    Tone(b, t, 0.1f, 220f, 160f, 0.25f, Wave.Tri, 0.001f, 2.5f);
                }

                // ハイハット（16分、裏拍を強調）
                float hat = stepInBar % 4 == 2 ? 0.2f : 0.08f;
                Noise(b, t, 0.04f, hat, 2f, 1f, 0.5f, s + 300);

                // ベース（8分でうねる）
                if (stepInBar % 2 == 0)
                {
                    float note = root + ((stepInBar % 8 == 6) ? 12 : 0);
                    Tone(b, t, step * 1.8f, Midi(note), Midi(note), 0.32f, Wave.Saw, 0.002f, 1.2f);
                    Tone(b, t, step * 1.8f, Midi(note - 12), Midi(note - 12), 0.3f, Wave.Sine, 0.002f, 1f);
                }

                // リード（16分アルペジオ、ピッキング感）
                int tone = chords[chordIdx][arpPattern[stepInBar] % 4];
                float lead = root + 24 + tone;
                Tone(b, t, step * 2.2f, Midi(lead), Midi(lead), 0.14f, Wave.Square, 0.002f, 2.2f);

                // パッド（小節頭だけ長く）
                if (stepInBar == 0 && bar % 2 == 0)
                {
                    foreach (int c in chords[chordIdx])
                    {
                        Tone(b, t, step * 32f, Midi(root + 12 + c), Midi(root + 12 + c), 0.06f, Wave.Tri, 0.2f, 0.4f);
                    }
                }

                // 小節後半のきらめき
                if (stepInBar == 14)
                {
                    Sparkle(b, t, 0.07f, 2, 96f, 0.05f, s);
                }
            }

            NormalizeOnly(b, 0.7f);
            return b;
        }
    }
}
