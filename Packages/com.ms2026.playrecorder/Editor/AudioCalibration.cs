using System;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;

namespace MS2026.PlayRecorder.Editor
{
    /// <summary>
    /// 音が 1 秒あたり何バイトになるかを実際に測る。
    /// Unity の音声エンコーダ（AAC など）はビットレートを指定できず、OS によって変わるため、
    /// 数秒のノイズを試しに書き出してファイルの大きさを見る。結果はこの PC に覚えておく。
    /// </summary>
    internal static class AudioCalibration
    {
        const int Seconds = 6;

        public static double BytesPerSecond(AudioMode mode, int sampleRate, string extension, string workDir)
        {
            if (mode == AudioMode.None) return 0;
            int ch = mode == AudioMode.Stereo ? 2 : 1;
            string key = $"MS2026.PlayRecorder.AudioBps.{Application.unityVersion}.{extension}.{sampleRate}.{ch}";
            float cached = EditorPrefs.GetFloat(key, -1f);
            if (cached > 0) return cached;

            double measured = Measure(ch, sampleRate, extension, workDir);
            if (measured > 0) EditorPrefs.SetFloat(key, (float)measured);
            else measured = ch == 2 ? 24000 : 12000; // 測れなかったときの安全側の見込み（192kbps / 96kbps）
            return measured;
        }

        static double Measure(int channels, int sampleRate, string extension, string workDir)
        {
            string path = Path.Combine(workDir, $"audio_calibration_{channels}ch{extension}");
            try
            {
                var attrs = new AudioTrackAttributes
                {
                    sampleRate = new MediaRational(sampleRate),
                    channelCount = (ushort)channels,
                    language = "",
                };
                var rnd = new System.Random(1234);
                using (var buffer = new NativeArray<float>(sampleRate * channels, Allocator.Temp))
                {
                    var b = buffer;
                    for (int i = 0; i < b.Length; i++) b[i] = (float)(rnd.NextDouble() * 0.4 - 0.2);
                    using (var enc = new MediaEncoder(path, attrs))
                        for (int s = 0; s < Seconds; s++) enc.AddSamples(b);
                }
                long size = new FileInfo(path).Length;
                return size / (double)Seconds;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[録画プレイ] 音の大きさを測れませんでした（見込み値を使います）: " + e.Message);
                return -1;
            }
            finally
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
            }
        }
    }
}
