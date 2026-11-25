using System;
using DDrive.Editor.Preview;
using DDrive.Foundation.Data;
using DDrive.Foundation.Handle;
using DDrive.Foundation.Identity;
using DDrive.Runtime.Audio;
using DDrive.Runtime.Loop;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.SoundStudio
{
    /// <summary>
    /// 試聴（▶ / ■）。Play していないときは D-Drive のエディタ用の試聴の仕組み（PreviewService）で鳴らす
    /// （D-Drive の Audio は Play 中しか動かないため）。Play 中はゲームと同じ道で鳴らす。
    /// どちらも「音そのものの音量 × この場所の倍率 × 全体音量」で鳴らす。いま鳴らしている行を覚えていて、行の ▶ が ■ に変わる。
    /// </summary>
    public sealed class SoundPreview : IDisposable
    {
        private PreviewService _service;
        private BgmData _bgmCopy;
        private BgmData _bgmSource;
        private string _playingKey;
        private SoundSlotKind _playingKind;
        private Handle<SeMarker> _seHandle = Handle<SeMarker>.Invalid;

        /// <summary>試聴で何か鳴っているか（上の帯の ■ を押せるか）。</summary>
        public bool AnyPlaying => _playingKey != null && IsStillPlaying();

        /// <summary>この行の音を試聴で鳴らしているか（行の ▶ を ■ にする）。</summary>
        public bool IsPlaying(SoundSlot slot)
        {
            return slot != null && _playingKey != null && slot.Key == _playingKey && IsStillPlaying();
        }

        /// <summary>鳴らせたら null、鳴らせなかったら理由。前の試聴は止めてから鳴らす。</summary>
        public string Play(SoundSlot slot, SoundLibrary library)
        {
            if (slot == null || slot.Kind == SoundSlotKind.Candidate)
            {
                return "まだ欄がないので鳴らせません。";
            }

            var id = SoundStudioContext.SoundIdOf(slot);
            if (id == 0)
            {
                return "音が入っていません。";
            }

            Stop();
            var problem = slot.Kind == SoundSlotKind.Bgm ? PlayBgm(id, library.Bgm(id)) : PlaySe(slot, library);
            if (problem == null)
            {
                _playingKey = slot.Key;
                _playingKind = slot.Kind;
            }

            return problem;
        }

        /// <summary>
        /// 素材そのもの（欄に入れる前の SE / BGM）を聞く。音を選ぶ窓の ▶ で使う。<paramref name="key"/> は鳴っているかを見分ける名前。
        /// 音そのものの音量 × 全体の音量で鳴らす（この場所の倍率はまだ無いので掛けない）。
        /// </summary>
        public string PlayData(AssetDataBase data, string key)
        {
            if (data == null)
            {
                return "音のデータが見つかりません。";
            }

            Stop();
            string problem;
            if (data is BgmData bgm)
            {
                problem = PlayBgm(bgm.Id, bgm);
                _playingKind = SoundSlotKind.Bgm;
            }
            else if (data is SeData se)
            {
                problem = PlaySeData(se);
                _playingKind = SoundSlotKind.Effect;
            }
            else
            {
                return "この素材は聞けません。";
            }

            if (problem == null)
            {
                _playingKey = key;
            }

            return problem;
        }

        /// <summary>この名前の音を試聴で鳴らしているか。</summary>
        public bool IsPlayingKey(string key)
        {
            return key != null && _playingKey == key && IsStillPlaying();
        }

        /// <summary>試聴の音を全部止める（Play 中は、鳴っている BGM も止まる）。</summary>
        public void Stop()
        {
            _service?.StopAll();
            if (EditorApplication.isPlaying)
            {
                var audio = DDriveRuntimeBootstrap.Instance != null ? DDriveRuntimeBootstrap.Instance.Audio : null;
                if (audio != null && _seHandle != Handle<SeMarker>.Invalid && audio.IsPlaying(_seHandle))
                {
                    audio.Stop(_seHandle);
                }

                if (_playingKind == SoundSlotKind.Bgm && _playingKey != null)
                {
                    Audio.StopBgm();
                }
            }

            _seHandle = Handle<SeMarker>.Invalid;
            _playingKey = null;
            _bgmSource = null;
        }

        /// <summary>
        /// 試聴で鳴らしている BGM の音量を、今の BGM の音量 × BGM 全体の音量に合わせる（バーを動かすと、聞きながら変わる）。
        /// D-Drive の BGM には後から音量を変える口が無いので、その曲を鳴らしている AudioSource を探して入れ直す。エディタだけ。
        /// </summary>
        public void ApplyBgmVolume(BgmData data)
        {
            if (data == null || _bgmSource != data || !IsStillPlaying())
            {
                return;
            }

            var volume = Mathf.Clamp01(data.Volume * FortressAudioMix.BgmMaster);
            if (_bgmCopy != null)
            {
                _bgmCopy.Volume = volume;
            }

            foreach (var source in Resources.FindObjectsOfTypeAll<AudioSource>())
            {
                if (source != null && source.isPlaying && source.clip != null && (source.clip == data.LoopBody || source.clip == data.Intro))
                {
                    source.volume = volume;
                }
            }
        }

        /// <summary>BGM 全体の音量を動かしたとき。</summary>
        public void ApplyBgmMaster()
        {
            ApplyBgmVolume(_bgmSource);
        }

        public void Dispose()
        {
            Stop();
            _service?.Dispose();
            _service = null;
            if (_bgmCopy != null)
            {
                UnityEngine.Object.DestroyImmediate(_bgmCopy);
                _bgmCopy = null;
            }
        }

        private bool IsStillPlaying()
        {
            if (_playingKind == SoundSlotKind.Bgm)
            {
                if (EditorApplication.isPlaying)
                {
                    var bgm = DDriveRuntimeBootstrap.Instance != null ? DDriveRuntimeBootstrap.Instance.Bgm : null;
                    return bgm != null && bgm.IsPlaying;
                }

                return _service?.BgmManager != null && _service.BgmManager.IsPlaying;
            }

            if (_seHandle == Handle<SeMarker>.Invalid)
            {
                return false;
            }

            if (EditorApplication.isPlaying)
            {
                var audio = DDriveRuntimeBootstrap.Instance != null ? DDriveRuntimeBootstrap.Instance.Audio : null;
                return audio != null && audio.IsPlaying(_seHandle);
            }

            return _service?.AudioManager != null && _service.AudioManager.IsPlaying(_seHandle);
        }

        private string PlaySe(SoundSlot slot, SoundLibrary library)
        {
            var effect = slot.Value as FortressEffect;
            if (effect == null)
            {
                return "欄が読めませんでした。［調べ直す］を押してください。";
            }

            if (EditorApplication.isPlaying)
            {
                // 試聴は「誰の事か」を気にせず、このPCで鳴らす。
                _seHandle = Audio.PlaySe(effect.se);
                FortressAudioMix.ApplySeVolume(_seHandle, effect.se, effect.volumeScale, effect.isVoice);
                return _seHandle == Handle<SeMarker>.Invalid ? "鳴りませんでした（連打の間引き中か、D-Drive の準備がまだです）。" : null;
            }

            var data = library.Se(effect.se.Value);
            if (data == null)
            {
                return "音のデータが見つかりません。D-Drive の AssetBrowser で確かめてください。";
            }

            var service = Service();
            if (service == null)
            {
                return "Play していないときの試聴が使えませんでした。Play 中に試してください。";
            }

            _seHandle = service.PlaySe(data);
            service.AudioManager?.SetVolume(_seHandle, FortressSoundRules.FinalVolume(data.Volume, effect.volumeScale, FortressAudioMix.Master(effect.isVoice)));
            return null;
        }

        private string PlayBgm(ulong id, BgmData data)
        {
            if (EditorApplication.isPlaying)
            {
                FortressAudioMix.PlayBgm(new AssetId<BgmMarker>(id, AssetType.Bgm));
                _bgmSource = data;
                return null;
            }

            if (data == null)
            {
                return "BGM のデータが見つかりません。";
            }

            var service = Service();
            if (service == null)
            {
                return "Play していないときの試聴が使えませんでした。Play 中に試してください。";
            }

            if (_bgmCopy != null)
            {
                UnityEngine.Object.DestroyImmediate(_bgmCopy);
            }

            // 全体音量を掛けた複製で鳴らす（元のデータは書き換えない）。
            _bgmCopy = UnityEngine.Object.Instantiate(data);
            _bgmCopy.hideFlags = HideFlags.HideAndDontSave;
            _bgmCopy.Volume = Mathf.Clamp01(data.Volume * FortressAudioMix.BgmMaster);
            service.PlayBgm(_bgmCopy);
            _bgmSource = data;
            return null;
        }

        private string PlaySeData(SeData data)
        {
            if (EditorApplication.isPlaying)
            {
                _seHandle = Audio.PlaySe(new AssetId<SeMarker>(data.Id, AssetType.Se));
                if (_seHandle != Handle<SeMarker>.Invalid && DDriveRuntimeBootstrap.Instance != null && DDriveRuntimeBootstrap.Instance.Audio != null)
                {
                    DDriveRuntimeBootstrap.Instance.Audio.SetVolume(_seHandle, FortressSoundRules.FinalVolume(data.Volume, 1f, FortressAudioMix.Master(false)));
                }

                return _seHandle == Handle<SeMarker>.Invalid ? "鳴りませんでした（連打の間引き中か、D-Drive の準備がまだです）。" : null;
            }

            var service = Service();
            if (service == null)
            {
                return "Play していないときの試聴が使えませんでした。Play 中に試してください。";
            }

            _seHandle = service.PlaySe(data);
            service.AudioManager?.SetVolume(_seHandle, FortressSoundRules.FinalVolume(data.Volume, 1f, FortressAudioMix.Master(false)));
            return null;
        }

        private PreviewService Service()
        {
            if (_service != null)
            {
                return _service;
            }

            try
            {
                _service = new PreviewService();
                _service.Initialize();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[サウンドスタジオ] 試聴の準備に失敗しました: " + e.Message);
                _service = null;
            }

            return _service;
        }
    }
}
