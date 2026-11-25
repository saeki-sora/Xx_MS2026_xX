using System.Collections.Generic;
using System.Linq;
using DDrive.Foundation.Data;
using DDrive.Runtime.Audio;
using UnityEngine;

namespace MS2026.SoundStudio
{
    /// <summary>素材1つの大きさ（音そのものの音量を掛けた dB）。</summary>
    public sealed class LoudnessInfo
    {
        public AssetDataBase Data;
        public bool IsBgm;

        /// <summary>測れたか（ストリーミングの音などは測れない）。</summary>
        public bool Measured;

        /// <summary>平均の強さ（dB）。音そのものの音量を掛けた値。</summary>
        public float Db;

        /// <summary>一番大きい所（dB）。音そのものの音量を掛けた値。0 dB に近いと割れて聞こえることがある。</summary>
        public float PeakDb;

        public LoudnessMark Mark;
    }

    /// <summary>
    /// 素材（SE / BGM）の大きさを測る（エディタだけ）。素材の波形を読み、平均の強さと一番大きい所を dB にする。
    /// 同じ素材は1回だけ測る（窓を開いている間は覚えておく）。真ん中の大きさから 6 dB 以上ずれた音に印を付ける。
    /// </summary>
    public sealed class SoundLoudness
    {
        public const float Tolerance = 6f;

        private readonly Dictionary<AudioClip, (float rms, float peak, bool ok)> _clipCache = new Dictionary<AudioClip, (float, float, bool)>();

        public List<LoudnessInfo> Se { get; private set; } = new List<LoudnessInfo>();
        public List<LoudnessInfo> Bgm { get; private set; } = new List<LoudnessInfo>();
        public float SeMedian { get; private set; }
        public float BgmMedian { get; private set; }
        public bool HasMeasured { get; private set; }

        /// <summary>今の素材を全部測り直す（音量を変えたときも呼ぶ。波形は覚えているので速い）。</summary>
        public void Measure(SoundLibrary library)
        {
            Se = library.AllSe.Select(d => Info(d, false, ClipsOf(d))).ToList();
            Bgm = library.AllBgm.Select(d => Info(d, true, new[] { d.Intro, d.LoopBody })).ToList();
            SeMedian = Mark(Se);
            BgmMedian = Mark(Bgm);
            HasMeasured = true;
        }

        /// <summary>この素材の測った結果（まだ測っていなければ null）。</summary>
        public LoudnessInfo Find(AssetDataBase data)
        {
            return data == null ? null : Se.Concat(Bgm).FirstOrDefault(i => i.Data == data);
        }

        private static float Mark(List<LoudnessInfo> infos)
        {
            var measured = infos.Where(i => i.Measured).ToList();
            var median = LoudnessMath.Median(measured.Select(i => i.Db).ToList());
            foreach (var info in measured)
            {
                info.Mark = measured.Count >= 3 ? LoudnessMath.Classify(info.Db, median, Tolerance) : LoudnessMark.Normal;
            }

            return median;
        }

        private static IEnumerable<AudioClip> ClipsOf(SeData data)
        {
            var clips = new List<AudioClip>();
            if (data.Clips != null)
            {
                clips.AddRange(data.Clips);
            }

            if (data.Sources != null)
            {
                clips.AddRange(data.Sources.Select(s => s.Source));
            }

            return clips;
        }

        private LoudnessInfo Info(AssetDataBase data, bool isBgm, IEnumerable<AudioClip> clips)
        {
            var volume = data is SeData se ? se.Volume : data is BgmData bgm ? bgm.Volume : 1f;
            var info = new LoudnessInfo { Data = data, IsBgm = isBgm };

            // 何本かの音がある SE は、一番大きい本を代表にする。
            var best = -1f;
            var bestPeak = 0f;
            foreach (var clip in clips.Where(c => c != null).Distinct())
            {
                var (rms, peak, ok) = MeasureClip(clip);
                if (ok && rms > best)
                {
                    best = rms;
                    bestPeak = peak;
                }
            }

            if (best < 0f)
            {
                return info;
            }

            info.Measured = true;
            info.Db = LoudnessMath.WithVolume(LoudnessMath.ToDb(best), volume);
            info.PeakDb = LoudnessMath.WithVolume(LoudnessMath.ToDb(bestPeak), volume);
            return info;
        }

        private (float rms, float peak, bool ok) MeasureClip(AudioClip clip)
        {
            if (_clipCache.TryGetValue(clip, out var cached))
            {
                return cached;
            }

            var result = (0f, 0f, false);
            try
            {
                if (clip.loadType != AudioClipLoadType.Streaming && clip.samples > 0)
                {
                    if (clip.loadState != AudioDataLoadState.Loaded)
                    {
                        clip.LoadAudioData();
                    }

                    var count = clip.samples * clip.channels;
                    var samples = new float[count];
                    if (clip.GetData(samples, 0))
                    {
                        result = (LoudnessMath.Rms(samples, count), LoudnessMath.Peak(samples, count), true);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[サウンドスタジオ] 音の大きさを測れませんでした: " + clip.name + "（" + e.Message + "）");
            }

            _clipCache[clip] = result;
            return result;
        }
    }
}
