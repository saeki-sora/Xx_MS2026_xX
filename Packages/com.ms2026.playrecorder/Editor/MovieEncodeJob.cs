using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MS2026.PlayRecorder.Editor
{
    /// <summary>
    /// 一時ファイル（JPEG の連番＋生の音）から、上限サイズに収まる動画を書き出す仕事。
    /// エディタを固めないよう <see cref="Step"/> で少しずつ進める。
    ///
    /// 20MB に収める工夫：
    ///  1. 録画の長さが分かってから、上限 ÷ 長さ でビットレートを決める（途中で切らない）。
    ///  2. そのビットレートで綺麗に見える解像度・fps を選ぶ（長いほど 720p → 360p … と下がる）。
    ///  3. 音は実測した大きさを先に差し引く。音が上限を圧迫するならモノラル → 音なしへ。
    ///  4. 書き出したら実際の大きさを確かめ、超えていたら縮めて書き直す（最大 5 回）。
    /// </summary>
    internal sealed class MovieEncodeJob : IDisposable
    {
        const int MaxAttempts = 5;

        public readonly string Dir;
        public string Status { get; private set; } = "準備中";
        public float Progress01 { get; private set; }
        public bool IsFinished { get; private set; }
        public bool Succeeded { get; private set; }
        public string OutputPath { get; private set; }
        public string Error { get; private set; }

        readonly SessionMeta _meta;
        readonly List<FrameEntry> _frames;
        readonly double _duration;
        readonly float _aspect;
        readonly long _limit;
        readonly bool _vp8;
        readonly string _ext;
        readonly List<QualityRung> _ladder;
        readonly int _audioRate;

        int _attempt;
        VideoPlan _plan;
        AudioMode _audio;
        double _audioBytesPerSec;
        long _targetBps;
        int _minRung;

        MediaEncoder _enc;
        string _tmpPath;
        FileStream _frameStream;
        AudioReader _audioReader;
        Texture2D _src, _out;
        RenderTexture _rt;
        int _k, _n, _cursor, _shownFrame = -1;
        long _audioFramesWritten;
        double _lastStepAt = -1, _workSeconds; // 残り時間の見込み用（今回の書き出しにかかった実時間）

        MovieEncodeJob(string dir, SessionMeta meta, List<FrameEntry> frames)
        {
            Dir = dir;
            _meta = meta;
            _frames = frames;
            double lastFrameEnd = frames[frames.Count - 1].Time + 0.05;
            _duration = Math.Max(meta.complete ? meta.duration : 0, lastFrameEnd);
            int sw = meta.sourceWidth > 0 ? meta.sourceWidth : frames[0].Width;
            int sh = meta.sourceHeight > 0 ? meta.sourceHeight : frames[0].Height;
            _aspect = (float)sw / Math.Max(1, sh);
            _limit = PlayRecorderSettings.LimitBytes;
            _vp8 = Application.platform == RuntimePlatform.LinuxEditor; // Linux の Unity は H.264 を書けない
            _ext = _vp8 ? ".webm" : ".mp4";
            _audioRate = meta.audioSampleRate > 0 ? meta.audioSampleRate : 48000;

            // 取り込んだ画像より大きくはしない
            int maxCaptured = 0;
            foreach (var f in frames) maxCaptured = Math.Max(maxCaptured, f.Height);
            _ladder = RecordingPlan.BuildLadder(PlayRecorderSettings.MaxHeight, PlayRecorderSettings.MaxFps, maxCaptured);
        }

        /// <summary>書き出せる中身が無ければ null（理由を <paramref name="reason"/> に返す）。</summary>
        public static MovieEncodeJob TryCreate(string dir, out string reason)
        {
            reason = null;
            var meta = SessionFiles.ReadMeta(dir);
            if (meta == null) { reason = "録画の情報（meta.json）が読めません"; return null; }
            var frames = SessionFiles.ReadIndex(dir);
            if (frames.Count == 0) { reason = "画像が 1 枚も録れていません（すぐ止めた／Game ビューが表示されていなかった）"; return null; }
            return new MovieEncodeJob(dir, meta, frames);
        }

        public double Duration => _duration;
        public VideoPlan Plan => _plan;
        public AudioMode Audio => _audio;

        /// <summary>今の書き出し（縮め直しなら今回分）が終わるまでの見込み秒数。まだ分からなければ -1。</summary>
        public double SecondsRemaining
        {
            get
            {
                float p = Progress01;
                if (p < 0.03f || _workSeconds < 1.0) return -1;
                return _workSeconds / p * (1.0 - p);
            }
        }

        /// <summary>最大 <paramref name="budgetMs"/> ミリ秒だけ進める。</summary>
        public void Step(double budgetMs)
        {
            if (IsFinished) return;
            try
            {
                var sw = Stopwatch.StartNew();
                double now = EditorApplication.timeSinceStartup;
                if (_lastStepAt >= 0 && now - _lastStepAt < 2.0) _workSeconds += now - _lastStepAt; // プレイ中など止まっていた間は数えない
                _lastStepAt = now;
                if (_enc == null) BeginAttempt();
                while (_k < _n && sw.Elapsed.TotalMilliseconds < budgetMs)
                {
                    EncodeFrame(_k);
                    _k++;
                }
                Progress01 = Mathf.Clamp01((float)_k / _n);
                Status = $"{_plan.Height}p {_plan.Fps}fps" + (_attempt > 0 ? $"（縮め直し {_attempt} 回目）" : "");
                if (_k >= _n) FinishAttempt();
            }
            catch (Exception e)
            {
                Error = e.Message;
                Debug.LogException(e);
                IsFinished = true;
                Succeeded = false;
                CloseEncoder(deleteTemp: true);
            }
        }

        void BeginAttempt()
        {
            if (_attempt == 0)
            {
                bool hasAudio = _meta.audioChannels > 0 && File.Exists(Path.Combine(Dir, SessionFiles.AudioFile));
                _audio = RecordingPlan.ChooseAudio(hasAudio, _meta.audioChannels, _duration, _limit,
                    m => AudioCalibration.BytesPerSecond(m, _audioRate, _ext, Dir));
                _audioBytesPerSec = AudioCalibration.BytesPerSecond(_audio, _audioRate, _ext, Dir);
                _targetBps = RecordingPlan.VideoBitsPerSecond(_limit, _duration, _audioBytesPerSec);
            }

            _plan = RecordingPlan.Choose(_ladder, _aspect, _targetBps, _minRung);
            _n = Math.Max(1, (int)Math.Ceiling(_duration * _plan.Fps));
            _k = 0;
            _workSeconds = 0;
            _cursor = 0;
            _shownFrame = -1;
            _audioFramesWritten = 0;

            _rt = GpuScale.Ensure(_rt, _plan.Width, _plan.Height, "PlayRecorder Encode");
            if (_out == null || _out.width != _plan.Width || _out.height != _plan.Height)
            {
                if (_out != null) UnityEngine.Object.DestroyImmediate(_out);
                _out = new Texture2D(_plan.Width, _plan.Height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            }
            if (_src == null)
                _src = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };

            _tmpPath = Path.Combine(Dir, $"try{_attempt}{_ext}");
            if (File.Exists(_tmpPath)) File.Delete(_tmpPath);

            var video = CreateVideoAttributes();
            if (_audio != AudioMode.None)
            {
                var audio = new AudioTrackAttributes
                {
                    sampleRate = new MediaRational(_audioRate),
                    channelCount = (ushort)(_audio == AudioMode.Stereo ? 2 : 1),
                    language = "",
                };
                _enc = new MediaEncoder(_tmpPath, video, audio);
                _audioReader = new AudioReader(Path.Combine(Dir, SessionFiles.AudioFile), _meta.audioChannels, audio.channelCount);
            }
            else
            {
                _enc = new MediaEncoder(_tmpPath, video);
            }
            _frameStream = new FileStream(Path.Combine(Dir, SessionFiles.FramesFile), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        VideoTrackEncoderAttributes CreateVideoAttributes()
        {
            VideoTrackEncoderAttributes v;
            if (_vp8)
            {
                v = new VideoTrackEncoderAttributes(new VP8EncoderAttributes { keyframeDistance = (uint)(_plan.Fps * 2) });
            }
            else
            {
                v = new VideoTrackEncoderAttributes(new H264EncoderAttributes
                {
                    gopSize = (uint)(_plan.Fps * 2),
                    numConsecutiveBFrames = 2,
                    profile = VideoEncodingProfile.H264High,
                });
            }
            v.frameRate = new MediaRational(_plan.Fps);
            v.width = (uint)_plan.Width;
            v.height = (uint)_plan.Height;
            v.includeAlpha = false;
            v.bitRateMode = VideoBitrateMode.Medium;
            v.targetBitRate = (uint)Math.Min(uint.MaxValue, _plan.BitsPerSecond);
            return v;
        }

        void EncodeFrame(int k)
        {
            // 出力の k 枚目の時刻に一番近い（それ以前の）取り込み画像を使う。足りないところは前の絵を繰り返す
            double t = k / (double)_plan.Fps;
            while (_cursor + 1 < _frames.Count && _frames[_cursor + 1].Time <= t) _cursor++;
            if (_cursor != _shownFrame)
            {
                DecodeInto(_frames[_cursor]);
                _shownFrame = _cursor;
            }
            _enc.AddFrame(_out);

            if (_audioReader != null)
            {
                long target = (long)Math.Round((k + 1) * (double)_audioRate / _plan.Fps);
                int need = (int)(target - _audioFramesWritten);
                if (need > 0)
                {
                    var samples = _audioReader.Read(need);
                    try { _enc.AddSamples(samples); }
                    finally { samples.Dispose(); }
                    _audioFramesWritten = target;
                }
            }
        }

        void DecodeInto(FrameEntry e)
        {
            var bytes = new byte[e.Length];
            _frameStream.Position = e.Offset;
            int read = 0;
            while (read < e.Length)
            {
                int r = _frameStream.Read(bytes, read, e.Length - read);
                if (r <= 0) break;
                read += r;
            }
            _src.LoadImage(bytes, false);
            GpuScale.Blit(_src, _rt, false);
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            _out.ReadPixels(new Rect(0, 0, _plan.Width, _plan.Height), 0, 0, false);
            _out.Apply(false);
            RenderTexture.active = prev;
        }

        void FinishAttempt()
        {
            CloseEncoder(deleteTemp: false);
            long size = new FileInfo(_tmpPath).Length;
            bool fits = size <= _limit;
            bool lastChance = _attempt + 1 >= MaxAttempts
                              || (_plan.RungIndex >= _ladder.Count - 1 && _plan.BitsPerSecond <= RecordingPlan.MinVideoBitsPerSecond && _audio == AudioMode.None);
            if (fits || lastChance)
            {
                Complete(size, fits);
                return;
            }

            // 超えた → 実際の大きさから縮める割合を出して書き直す
            double overhead = RecordingPlan.ContainerOverheadBytes;
            double audioBytes = _audioBytesPerSec * _duration;
            double videoBytes = Math.Max(1, size - audioBytes - overhead);
            double desiredVideo = _limit * RecordingPlan.SafetyFactor - audioBytes - overhead;
            if (desiredVideo < _limit * 0.3 && _audio != AudioMode.None)
            {
                _audio = _audio == AudioMode.Stereo ? AudioMode.Mono : AudioMode.None;
                _audioBytesPerSec = AudioCalibration.BytesPerSecond(_audio, _audioRate, _ext, Dir);
                audioBytes = _audioBytesPerSec * _duration;
                desiredVideo = _limit * RecordingPlan.SafetyFactor - audioBytes - overhead;
            }

            double ratio = Math.Max(0.05, desiredVideo / videoBytes);
            _targetBps = Math.Max(RecordingPlan.MinVideoBitsPerSecond, (long)(_plan.BitsPerSecond * ratio * 0.95));
            // エンコーダが指定ビットレートを大きく超えてくる（守っていない）なら解像度も 1 段下げる
            double overshoot = videoBytes * 8.0 / Math.Max(1.0, _plan.BitsPerSecond * _duration);
            _minRung = overshoot > 1.4 ? _plan.RungIndex + 1 : _plan.RungIndex;

            try { File.Delete(_tmpPath); } catch { /* ignore */ }
            _attempt++;
        }

        void Complete(long size, bool fits)
        {
            string folder = PlayRecorderSettings.OutputFolderAbsolute;
            Directory.CreateDirectory(folder);
            string baseName = $"{Sanitize(_meta.projectName)}_{_meta.createdAt}";
            string path = Path.Combine(folder, baseName + _ext);
            for (int n = 2; File.Exists(path); n++) path = Path.Combine(folder, $"{baseName}_{n}{_ext}");
            File.Move(_tmpPath, path);

            OutputPath = path;
            Succeeded = true;
            IsFinished = true;
            Progress01 = 1f;

            string audioText = _audio == AudioMode.Stereo ? "音あり" : _audio == AudioMode.Mono ? "音あり(モノラル)" : "音なし";
            string msg = $"[録画プレイ] 保存しました: {path}\n" +
                         $"{size / 1000_000.0:0.0}MB / 上限 {PlayRecorderSettings.LimitMB:0.#}MB ・ {FormatTime(_duration)} ・ " +
                         $"{_plan.Width}x{_plan.Height} {_plan.Fps}fps ・ {audioText}" +
                         (_meta.complete ? "" : " ・ ※前回 Unity が録画中に終了したため、残っていた分から復元しました");
            if (fits) Debug.Log(msg);
            else Debug.LogWarning(msg + "\n※できる限り縮めましたが上限を少し超えました。設定の「最大の解像度」を下げると収まりやすくなります。");
        }

        void CloseEncoder(bool deleteTemp)
        {
            _enc?.Dispose();
            _enc = null;
            _frameStream?.Dispose();
            _frameStream = null;
            _audioReader?.Dispose();
            _audioReader = null;
            if (deleteTemp && _tmpPath != null)
                try { if (File.Exists(_tmpPath)) File.Delete(_tmpPath); } catch { /* ignore */ }
        }

        public void Dispose()
        {
            CloseEncoder(deleteTemp: !Succeeded);
            GpuScale.Release(ref _rt);
            if (_src != null) UnityEngine.Object.DestroyImmediate(_src);
            if (_out != null) UnityEngine.Object.DestroyImmediate(_out);
            _src = _out = null;
        }

        public static string FormatTime(double sec)
        {
            int s = (int)Math.Round(sec);
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
        }

        static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Play";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }
    }
}
