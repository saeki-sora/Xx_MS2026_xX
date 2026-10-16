using System;
using System.IO;
using Unity.Collections;

namespace MS2026.PlayRecorder.Editor
{
    /// <summary>audio.raw（float の生音）を読みながら、ステレオ／モノラルにまとめ直す。足りない分は無音。</summary>
    internal sealed class AudioReader : IDisposable
    {
        readonly FileStream _fs;
        readonly int _srcCh;
        readonly int _dstCh;
        byte[] _bytes = new byte[0];
        float[] _src = new float[0];
        float[] _dst = new float[0];

        public AudioReader(string path, int sourceChannels, int destChannels)
        {
            _fs = File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite) : null;
            _srcCh = Math.Max(1, sourceChannels);
            _dstCh = destChannels;
        }

        /// <summary><paramref name="frames"/> サンプルフレーム分を読んで、エンコーダ用の配列にする（呼び出し側で Dispose）。</summary>
        public NativeArray<float> Read(int frames)
        {
            int srcCount = frames * _srcCh;
            int bytes = srcCount * 4;
            if (_bytes.Length < bytes) _bytes = new byte[bytes];
            if (_src.Length < srcCount) _src = new float[srcCount];

            int got = 0;
            if (_fs != null)
            {
                while (got < bytes)
                {
                    int r = _fs.Read(_bytes, got, bytes - got);
                    if (r <= 0) break;
                    got += r;
                }
            }
            got -= got % 4;
            Buffer.BlockCopy(_bytes, 0, _src, 0, got);
            Array.Clear(_src, got / 4, srcCount - got / 4);

            int dstCount = frames * _dstCh;
            if (_dst.Length != dstCount) _dst = new float[dstCount];
            for (int f = 0; f < frames; f++)
            {
                int s = f * _srcCh;
                float l = _src[s];
                float r = _srcCh >= 2 ? _src[s + 1] : l;
                if (_srcCh >= 3)
                {
                    // 5.1ch などはセンターを左右に混ぜる
                    float c = _src[s + 2] * 0.707f;
                    l = Clamp(l + c);
                    r = Clamp(r + c);
                }
                if (_dstCh == 2)
                {
                    _dst[f * 2] = l;
                    _dst[f * 2 + 1] = r;
                }
                else
                {
                    _dst[f] = (l + r) * 0.5f;
                }
            }
            return new NativeArray<float>(_dst, Allocator.Temp);
        }

        static float Clamp(float v) => v < -1f ? -1f : v > 1f ? 1f : v;

        public void Dispose() => _fs?.Dispose();
    }
}
