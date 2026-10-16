using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace MS2026.PlayRecorder.Editor
{
    /// <summary>
    /// 録画中の画像と音を、別スレッドで JPEG にしてディスクへ書く係。
    /// ゲームのフレームを止めないよう、重い処理（JPEG 圧縮・書き込み）はすべてここで行う。
    /// 画像は GPU から非同期で受け取った RGBA の生データ、音は OnAudioFilterRead の float 配列。
    /// </summary>
    internal sealed class FrameStoreWriter : IDisposable
    {
        const int JpegQuality = 88;

        sealed class Item
        {
            public bool IsAudio;
            public byte[] Rgba;
            public int Width, Height;
            public double Time;
            public float[] Samples;
            public int SampleCount;
        }

        readonly ConcurrentQueue<Item> _queue = new ConcurrentQueue<Item>();
        readonly ConcurrentBag<byte[]> _bytePool = new ConcurrentBag<byte[]>();
        readonly ConcurrentBag<float[]> _floatPool = new ConcurrentBag<float[]>();
        readonly AutoResetEvent _signal = new AutoResetEvent(false);
        readonly Thread _thread;
        readonly FileStream _frames;
        readonly BinaryWriter _index;
        readonly FileStream _audio;
        readonly object _audioLock = new object();
        volatile bool _stopping;
        int _pendingFrames;
        long _audioFramesQueued;
        int _audioChannels;
        int _framesWritten;
        byte[] _audioBytes = new byte[0];
        string _error;

        public FrameStoreWriter(string dir)
        {
            _frames = new FileStream(Path.Combine(dir, SessionFiles.FramesFile), FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);
            _index = new BinaryWriter(new FileStream(Path.Combine(dir, SessionFiles.IndexFile), FileMode.Create, FileAccess.Write, FileShare.Read));
            _audio = new FileStream(Path.Combine(dir, SessionFiles.AudioFile), FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);
            _thread = new Thread(Run) { Name = "PlayRecorder Writer", IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal };
            _thread.Start();
        }

        /// <summary>まだ JPEG になっていない画像の数（多すぎたら取り込みを間引く目安）。</summary>
        public int PendingFrames => Volatile.Read(ref _pendingFrames);
        public int FramesWritten => Volatile.Read(ref _framesWritten);
        /// <summary>受け取った音の長さ（サンプルフレーム数。チャンネル数で割った数）。</summary>
        public long AudioFramesQueued => Interlocked.Read(ref _audioFramesQueued);
        public int AudioChannels => Volatile.Read(ref _audioChannels);
        public string Error => _error;

        public byte[] RentBytes(int length)
        {
            while (_bytePool.TryTake(out var b))
                if (b.Length == length) return b;
            return new byte[length];
        }

        /// <summary>画像を 1 枚渡す（rgba はこのクラスが引き取る）。メインスレッドから呼ぶ。</summary>
        public void EnqueueFrame(byte[] rgba, int width, int height, double time)
        {
            if (_stopping) return;
            Interlocked.Increment(ref _pendingFrames);
            _queue.Enqueue(new Item { Rgba = rgba, Width = width, Height = height, Time = time });
            if (!_stopping) _signal.Set();
        }

        /// <summary>音を渡す。オーディオスレッドから呼ばれる。最初に来たチャンネル数にそろえて保存する。</summary>
        public void EnqueueAudio(float[] data, int length, int channels)
        {
            if (_stopping || channels <= 0 || length <= 0) return;
            lock (_audioLock)
            {
                if (_audioChannels == 0) Volatile.Write(ref _audioChannels, channels);
                int dstCh = _audioChannels;
                int frames = length / channels;
                var buf = RentFloats(frames * dstCh);
                if (dstCh == channels)
                {
                    Array.Copy(data, buf, frames * dstCh);
                }
                else
                {
                    for (int f = 0; f < frames; f++)
                        for (int c = 0; c < dstCh; c++)
                            buf[f * dstCh + c] = data[f * channels + Math.Min(c, channels - 1)];
                }
                Interlocked.Add(ref _audioFramesQueued, frames);
                _queue.Enqueue(new Item { IsAudio = true, Samples = buf, SampleCount = frames * dstCh });
            }
            if (!_stopping) _signal.Set();
        }

        /// <summary>無音を足す（AudioListener が無かった間のすき間埋め。音ズレ防止）。</summary>
        public void EnqueueSilence(long frames)
        {
            if (_stopping || frames <= 0) return;
            lock (_audioLock)
            {
                int ch = _audioChannels;
                if (ch == 0) return; // まだ一度も音が来ていない＝先頭のすき間は書き出し時に時刻で合わせる
                const int chunk = 4096;
                while (frames > 0)
                {
                    int n = (int)Math.Min(chunk, frames);
                    var buf = RentFloats(n * ch);
                    Array.Clear(buf, 0, n * ch);
                    _queue.Enqueue(new Item { IsAudio = true, Samples = buf, SampleCount = n * ch });
                    Interlocked.Add(ref _audioFramesQueued, n);
                    frames -= n;
                }
            }
            _signal.Set();
        }

        /// <summary>音のチャンネル数を先に決めておく（先頭のすき間を無音で埋められるように）。</summary>
        public void SetAudioChannels(int channels)
        {
            lock (_audioLock)
                if (_audioChannels == 0 && channels > 0) Volatile.Write(ref _audioChannels, channels);
        }

        float[] RentFloats(int length)
        {
            if (_floatPool.TryTake(out var f) && f.Length >= length) return f;
            return new float[Math.Max(length, 2048)];
        }

        void Run()
        {
            var lastFlush = DateTime.UtcNow;
            while (true)
            {
                while (_queue.TryDequeue(out var it))
                {
                    try { Process(it); }
                    catch (Exception e) { _error = e.Message; }
                }
                if ((DateTime.UtcNow - lastFlush).TotalSeconds > 1.0)
                {
                    // こまめに書き切っておけば、Unity が落ちても途中までは救える
                    try { _frames.Flush(); _index.Flush(); _audio.Flush(); } catch { /* ignore */ }
                    lastFlush = DateTime.UtcNow;
                }
                if (_stopping && _queue.IsEmpty) break;
                _signal.WaitOne(30);
            }
        }

        void Process(Item it)
        {
            if (it.IsAudio)
            {
                int bytes = it.SampleCount * 4;
                if (_audioBytes.Length < bytes) _audioBytes = new byte[bytes];
                Buffer.BlockCopy(it.Samples, 0, _audioBytes, 0, bytes);
                _audio.Write(_audioBytes, 0, bytes);
                _floatPool.Add(it.Samples);
                return;
            }

            try
            {
                // ImageConversion.EncodeArrayToJPG はスレッドセーフ
                byte[] jpg = ImageConversion.EncodeArrayToJPG(it.Rgba, GraphicsFormat.R8G8B8A8_UNorm,
                    (uint)it.Width, (uint)it.Height, 0, JpegQuality);
                long offset = _frames.Position;
                _frames.Write(jpg, 0, jpg.Length);
                _index.Write(offset);
                _index.Write(jpg.Length);
                _index.Write(it.Time);
                _index.Write(it.Width);
                _index.Write(it.Height);
                Interlocked.Increment(ref _framesWritten);
            }
            finally
            {
                _bytePool.Add(it.Rgba);
                Interlocked.Decrement(ref _pendingFrames);
            }
        }

        /// <summary>残りを全部書き切ってからファイルを閉じる。</summary>
        public void Dispose()
        {
            if (_stopping) return;
            _stopping = true;
            _signal.Set();
            _thread.Join(TimeSpan.FromSeconds(30));
            try { _frames.Dispose(); } catch { /* ignore */ }
            try { _index.Dispose(); } catch { /* ignore */ }
            try { _audio.Dispose(); } catch { /* ignore */ }
            // _signal は Dispose しない（オーディオスレッドが最後に Set しても落ちないように）
        }
    }
}
