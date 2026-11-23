using System;
using DDrive.Editor.Preview;
using DDrive.Foundation.Handle;
using DDrive.Foundation.Identity;
using DDrive.Runtime.Audio;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.SoundStudio
{
    /// <summary>
    /// 試聴（▶）。Play していないときは D-Drive のエディタ用の試聴の仕組み（PreviewService）で鳴らす
    /// （D-Drive の Audio は Play 中しか動かないため）。Play 中はゲームと同じ道（FortressEffectPlayer）で鳴らす。
    /// どちらも「音そのものの音量 × この場所の倍率 × 全体音量」で鳴らす。
    /// </summary>
    public sealed class SoundPreview : IDisposable
    {
        private PreviewService _service;
        private BgmData _bgmCopy;

        /// <summary>鳴らせたら null、鳴らせなかったら理由。</summary>
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

            return slot.Kind == SoundSlotKind.Bgm ? PlayBgm(id, library) : PlaySe(slot, library);
        }

        public void Stop()
        {
            _service?.StopAll();
            if (EditorApplication.isPlaying)
            {
                Audio.StopBgm();
            }
        }

        public void Dispose()
        {
            _service?.Dispose();
            _service = null;
            if (_bgmCopy != null)
            {
                UnityEngine.Object.DestroyImmediate(_bgmCopy);
                _bgmCopy = null;
            }
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
                var handle = Audio.PlaySe(effect.se);
                FortressAudioMix.ApplySeVolume(handle, effect.se, effect.volumeScale, effect.isVoice);
                return handle == Handle<SeMarker>.Invalid ? "鳴りませんでした（連打の間引き中か、D-Drive の準備がまだです）。" : null;
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

            var previewHandle = service.PlaySe(data);
            service.AudioManager?.SetVolume(previewHandle, FortressSoundRules.FinalVolume(data.Volume, effect.volumeScale, FortressAudioMix.Master(effect.isVoice)));
            return null;
        }

        private string PlayBgm(ulong id, SoundLibrary library)
        {
            if (EditorApplication.isPlaying)
            {
                FortressAudioMix.PlayBgm(new AssetId<BgmMarker>(id, AssetType.Bgm));
                return null;
            }

            var data = library.Bgm(id);
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
