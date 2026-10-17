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

        /// <summary>
        /// Unity は「音だけ」のファイルを作れない（no audio encoder factory registered）ので、
        /// 「小さな真っ黒の映像＋音」と「小さな真っ黒の映像だけ」を作り、その差を音の大きさとする。
        /// </summary>
        static double Measure(int channels, int sampleRate, string extension, string workDir)
        {
            string withAudio = Path.Combine(workDir, $"audio_calibration_{channels}ch{extension}");
            string videoOnly = Path.Combine(workDir, $"audio_calibration_none{extension}");
            Texture2D frame = null;
            try
            {
                frame = new Texture2D(VideoSize, VideoSize, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                frame.SetPixels32(new Color32[VideoSize * VideoSize]);
                frame.Apply(false);

                long both = Encode(withAudio, frame, channels, sampleRate, extension);
                long video = Encode(videoOnly, frame, 0, sampleRate, extension);
                return Math.Max(1000.0, (both - video) / (double)Seconds);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[録画プレイ] 音の大きさを測れませんでした（見込み値を使います）: " + e.Message);
                return -1;
            }
            finally
            {
                if (frame != null) UnityEngine.Object.DestroyImmediate(frame);
                try { if (File.Exists(withAudio)) File.Delete(withAudio); } catch { /* ignore */ }
                try { if (File.Exists(videoOnly)) File.Delete(videoOnly); } catch { /* ignore */ }
            }
        }

        const int VideoSize = 64;

        static long Encode(string path, Texture2D frame, int channels, int sampleRate, string extension)
        {
            VideoTrackEncoderAttributes video = extension == ".webm"
                ? new VideoTrackEncoderAttributes(new VP8EncoderAttributes { keyframeDistance = 1 })
                : new VideoTrackEncoderAttributes(new H264EncoderAttributes { gopSize = 1, numConsecutiveBFrames = 0, profile = VideoEncodingProfile.H264Baseline });
            video.frameRate = new MediaRational(1);
            video.width = VideoSize;
            video.height = VideoSize;
            video.includeAlpha = false;
            video.bitRateMode = VideoBitrateMode.Low;
            video.targetBitRate = 20_000;

            if (channels <= 0)
            {
                using (var enc = new MediaEncoder(path, video))
                    for (int s = 0; s < Seconds; s++) enc.AddFrame(frame);
                return new FileInfo(path).Length;
            }

            var audio = new AudioTrackAttributes
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
                using (var enc = new MediaEncoder(path, video, audio))
                {
                    for (int s = 0; s < Seconds; s++)
                    {
                        enc.AddFrame(frame);
                        enc.AddSamples(b);
                    }
                }
            }
            return new FileInfo(path).Length;
        }
    }
}
