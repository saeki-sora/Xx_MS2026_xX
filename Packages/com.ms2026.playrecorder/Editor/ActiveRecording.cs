using System;
using System.Collections.Generic;
using MS2026.PlayRecorder.Hooks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MS2026.PlayRecorder.Editor
{
    /// <summary>
    /// 録画中の 1 回分。ゲーム画面を毎フレーム取り込み（GPU で縮小 → 非同期で読み戻し）、
    /// 音は AudioListener から横取りして、<see cref="FrameStoreWriter"/> に渡す。
    /// エディタの一時停止中は時計を止める（動画に止まった時間が入らない）。
    /// </summary>
    internal sealed class ActiveRecording
    {
        const int MaxPendingFrames = 6;
        const double CheckInterval = 0.5;

        public readonly string Dir;
        readonly SessionMeta _meta;
        readonly FrameStoreWriter _writer;
        readonly GameObject _host;
        readonly PlayRecorderCapture _capture;
        readonly long _limitBytes;
        readonly bool _recordAudio;
        readonly int _audioRate;
        readonly bool _flip;
        List<QualityRung> _ladder;
        float _aspect;

        PlayRecorderAudioTap _tap;
        readonly Action<float[], int> _audioSink;
        RenderTexture _full, _small;
        QualityRung _rung;
        double _startTime, _pausedTotal, _pauseBegan = -1;
        double _lastCaptureTime = -1, _nextCheck;
        int _dropped;
        bool _stopped;

        public QualityRung CurrentRung => _rung;
        public int CapturedFrames => _writer.FramesWritten;
        public int DroppedFrames => _dropped;
        public bool IsPaused => _pauseBegan >= 0;

        /// <summary>録画の経過秒（一時停止していた時間は含まない）。</summary>
        public double Elapsed
        {
            get
            {
                double now = Time.realtimeSinceStartupAsDouble;
                double paused = _pausedTotal + (_pauseBegan >= 0 ? now - _pauseBegan : 0);
                return System.Math.Max(0, now - _startTime - paused);
            }
        }

        ActiveRecording(string dir, SessionMeta meta)
        {
            Dir = dir;
            _meta = meta;
            _limitBytes = PlayRecorderSettings.LimitBytes;
            _recordAudio = PlayRecorderSettings.RecordAudio;
            _flip = PlayRecorderSettings.ShouldFlip();
            _audioRate = AudioSettings.outputSampleRate;
            _writer = new FrameStoreWriter(dir);
            var writer = _writer;
            _audioSink = (data, channels) => writer.EnqueueAudio(data, data.Length, channels);
            if (_recordAudio) _writer.SetAudioChannels(SpeakerChannels(AudioSettings.speakerMode));

            RebuildLadder(Screen.width, Screen.height);
            _rung = _ladder[0];
            _startTime = Time.realtimeSinceStartupAsDouble;

            _host = new GameObject("PlayRecorder (録画中)") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _capture = _host.AddComponent<PlayRecorderCapture>();
            if (_capture == null)
            {
                UnityEngine.Object.DestroyImmediate(_host);
                _writer.Dispose();
                throw new InvalidOperationException("録画用の部品を付けられませんでした（Console の警告を確認してください）");
            }
            _capture.EndOfFrame = OnEndOfFrame;
            _capture.Tick = OnUpdate;
            OnUpdate();
        }

        public static ActiveRecording Start()
        {
            string dir = SessionFiles.CreateSessionDir(out string createdAt);
            var meta = new SessionMeta
            {
                createdAt = createdAt,
                projectName = System.IO.Path.GetFileName(PlayRecorderSettings.ProjectRoot),
                sceneName = SceneManager.GetActiveScene().name,
                sourceWidth = Screen.width,
                sourceHeight = Screen.height,
                audioSampleRate = AudioSettings.outputSampleRate,
                audioChannels = PlayRecorderSettings.RecordAudio ? SpeakerChannels(AudioSettings.speakerMode) : 0,
                complete = false,
            };
            SessionFiles.WriteMeta(dir, meta); // 落ちても後で救えるよう、最初に書いておく
            return new ActiveRecording(dir, meta);
        }

        void RebuildLadder(int width, int height)
        {
            _aspect = height > 0 ? (float)width / height : 16f / 9f;
            _ladder = RecordingPlan.BuildLadder(PlayRecorderSettings.MaxHeight, PlayRecorderSettings.MaxFps, height);
        }

        public void SetPaused(bool paused)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (paused && _pauseBegan < 0) _pauseBegan = now;
            else if (!paused && _pauseBegan >= 0)
            {
                _pausedTotal += now - _pauseBegan;
                _pauseBegan = -1;
            }
            if (_tap != null) _tap.Paused = paused;
        }

        /// <summary>毎フレーム（Update）。取り込み画質の見直しと、AudioListener の付け替え。</summary>
        public void OnUpdate()
        {
            if (_stopped) return;
            double t = Elapsed;
            if (t < _nextCheck) return;
            _nextCheck = t + CheckInterval;

            _rung = RecordingPlan.CaptureRung(_ladder, _aspect, _limitBytes, t);
            if (_recordAudio) KeepAudioTap(t);
        }

        void KeepAudioTap(double t)
        {
            var listener = FindActiveListener();
            if (_tap != null && (listener == null || _tap.gameObject != listener.gameObject || !_tap.isActiveAndEnabled))
            {
                _tap.Sink = null;
                UnityEngine.Object.Destroy(_tap);
                _tap = null;
            }

            // 音が来なかった間（AudioListener が無い・入れ替え中）を無音で埋めて、音ズレを防ぐ
            long expected = (long)(t * _audioRate);
            long missing = expected - _writer.AudioFramesQueued;
            if (missing > _audioRate / 5) _writer.EnqueueSilence(missing);

            if (_tap == null && listener != null)
            {
                _tap = listener.gameObject.AddComponent<PlayRecorderAudioTap>();
                _tap.hideFlags = HideFlags.HideAndDontSave;
                if (_tap != null)
                {
                    _tap.Paused = IsPaused;
                    _tap.Sink = _audioSink;
                }
            }
        }

        static AudioListener FindActiveListener()
        {
#if UNITY_2023_1_OR_NEWER
            var all = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
#else
            var all = UnityEngine.Object.FindObjectsOfType<AudioListener>();
#endif
            foreach (var l in all)
                if (l.isActiveAndEnabled) return l;
            return null;
        }

        /// <summary>描画が終わった直後に呼ばれる。ここで画面を取り込む。</summary>
        public void OnEndOfFrame()
        {
            if (_stopped || IsPaused) return;
            double t = Elapsed;
            double interval = 1.0 / _rung.Fps;
            if (_lastCaptureTime >= 0 && t - _lastCaptureTime < interval * 0.9) return;
            if (_writer.PendingFrames > MaxPendingFrames)
            {
                _dropped++; // 書き込みが追いつかない → 間引く（書き出し時に前の絵で埋める）
                return;
            }
            _lastCaptureTime = t;

            int sw = Screen.width, sh = Screen.height;
            if (sw < 16 || sh < 16) return;
            if (_full == null) RebuildLadder(sw, sh);

            _full = GpuScale.Ensure(_full, sw, sh, "PlayRecorder Screen");
            ScreenCapture.CaptureScreenshotIntoRenderTexture(_full);

            int h = RecordingPlan.EvenHeight(Mathf.Min(_rung.Height, sh));
            int w = RecordingPlan.EvenWidth(h, (float)sw / sh);
            _small = GpuScale.Ensure(_small, w, h, "PlayRecorder Small");
            GpuScale.Blit(_full, _small, _flip);

            var writer = _writer;
            double time = t;
            AsyncGPUReadback.Request(_small, 0, TextureFormat.RGBA32, req =>
            {
                if (req.hasError) return;
                var data = req.GetData<byte>();
                var buf = writer.RentBytes(data.Length);
                data.CopyTo(buf);
                writer.EnqueueFrame(buf, w, h, time);
            });
        }

        /// <summary>録画を止めて、一時ファイルを書き切る。</summary>
        public void Stop()
        {
            if (_stopped) return;
            double duration = Elapsed;
            _stopped = true;

            if (_capture != null)
            {
                _capture.EndOfFrame = null;
                _capture.Tick = null;
            }
            if (_tap != null)
            {
                _tap.Sink = null;
                UnityEngine.Object.DestroyImmediate(_tap);
                _tap = null;
            }
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);

            AsyncGPUReadback.WaitAllRequests();
            _writer.Dispose();
            GpuScale.Release(ref _full);
            GpuScale.Release(ref _small);

            if (_writer.Error != null)
                Debug.LogWarning("[録画プレイ] 一時ファイルの書き込みでエラーが出ました: " + _writer.Error);

            _meta.duration = duration;
            _meta.complete = true;
            _meta.capturedFrames = _writer.FramesWritten;
            _meta.droppedFrames = _dropped;
            _meta.audioChannels = _recordAudio ? _writer.AudioChannels : 0;
            SessionFiles.WriteMeta(Dir, _meta);
        }

        static int SpeakerChannels(AudioSpeakerMode mode)
        {
            switch (mode)
            {
                case AudioSpeakerMode.Mono: return 1;
                case AudioSpeakerMode.Quad: return 4;
                case AudioSpeakerMode.Surround: return 5;
                case AudioSpeakerMode.Mode5point1: return 6;
                case AudioSpeakerMode.Mode7point1: return 8;
                default: return 2;
            }
        }
    }
}
