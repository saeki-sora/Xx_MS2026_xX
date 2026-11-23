using System.Collections;
using System.Collections.Generic;
using DDrive.Foundation.Handle;
using DDrive.Foundation.Identity;
using DDrive.Runtime.Audio;
using DDrive.Runtime.Loop;
using MS2026.Fortress.Net;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 鳴らした音に「この場所の倍率」と全体音量（<see cref="FortressSoundBalance"/>）を掛ける（サウンドスタジオ、2026-11-22）。
    /// D-Drive は改造しないので、SE は鳴らした後の Handle に音量を入れ直し、BGM は音量を掛けた複製のデータを鳴らす。
    /// D-Drive の準備ができていないときは何もしない（今までと同じ）。
    /// </summary>
    public static class FortressAudioMix
    {
        private static readonly Dictionary<ulong, (BgmData source, float volume, BgmData clone)> BgmClones =
            new Dictionary<ulong, (BgmData, float, BgmData)>();

        /// <summary>SE 全体（声なら声全体）の音量。音のバランスが無ければ 1。</summary>
        public static float Master(bool isVoice)
        {
            var balance = FortressSoundBalance.Current;
            if (balance == null)
            {
                return 1f;
            }

            return isVoice ? balance.voiceVolume : balance.seVolume;
        }

        /// <summary>BGM 全体の音量。音のバランスが無ければ 1。</summary>
        public static float BgmMaster => FortressSoundBalance.Current != null ? FortressSoundBalance.Current.bgmVolume : 1f;

        /// <summary>このPCが Host（またはサーバー）か。</summary>
        public static bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        /// <summary>このPCのプレイヤー番号（ネットにつながっていなければ -1）。</summary>
        public static int LocalPlayerIndex => FortressNetworkBootstrap.Instance != null ? FortressNetworkBootstrap.Instance.LocalPlayerIndex : -1;

        /// <summary>その席に人（接続しているPC）がいるか。Host のときだけ正しく分かる（CPU の音を Host で鳴らすために使う）。</summary>
        public static bool OwnerIsHuman(int ownerIndex)
        {
            var bootstrap = FortressNetworkBootstrap.Instance;
            if (bootstrap == null || ownerIndex < 0)
            {
                return true;
            }

            foreach (var pair in bootstrap.ClientIdToPlayerIndex)
            {
                if (pair.Value == ownerIndex)
                {
                    return true;
                }
            }

            return ownerIndex == bootstrap.LocalPlayerIndex;
        }

        /// <summary>
        /// 鳴らした SE に、音そのものの音量 × この場所の倍率 × 全体音量を入れる。倍率も全体音量も 1 なら何もしない（D-Drive が入れた音量のまま）。
        /// Handle が無効（連打の間引きで鳴らなかった）・D-Drive が無い・音のデータが読めないときも何もしない。
        /// </summary>
        public static void ApplySeVolume(Handle<SeMarker> handle, AssetId<SeMarker> id, float slotScale, bool isVoice)
        {
            var master = Master(isVoice);
            if (handle == Handle<SeMarker>.Invalid || (slotScale >= 1f && master >= 1f))
            {
                return;
            }

            var bootstrap = DDriveRuntimeBootstrap.Instance;
            if (bootstrap == null || bootstrap.Registry == null || bootstrap.Audio == null)
            {
                return;
            }

            if (!bootstrap.Registry.TryResolveSync<SeData>(id.Value, out var data) || data == null)
            {
                return;
            }

            bootstrap.Audio.SetVolume(handle, FortressSoundRules.FinalVolume(data.Volume, slotScale, master));
        }

        /// <summary>
        /// BGM を鳴らす（<c>Audio.PlayBgm</c> の代わり）。BGM 全体の音量が 1 なら今までと同じ。
        /// 1 より小さいときは、音量を掛けた複製のデータを鳴らす（曲ごとに1つ作って使い回す）。
        /// </summary>
        public static void PlayBgm(AssetId<BgmMarker> id, float fadeIn = -1f)
        {
            var master = BgmMaster;
            var bootstrap = DDriveRuntimeBootstrap.Instance;
            if (master >= 1f || bootstrap == null || bootstrap.Registry == null || bootstrap.Bgm == null)
            {
                Audio.PlayBgm(id, fadeIn);
                return;
            }

            if (!bootstrap.Registry.TryResolveSync<BgmData>(id.Value, out var data) || data == null)
            {
                Audio.PlayBgm(id, fadeIn);
                return;
            }

            bootstrap.Bgm.PlayBgmData(ScaledClone(id.Value, data, master), fadeIn);
        }

        /// <summary>
        /// D-Drive の準備ができるのを待ってから BGM を鳴らす（シーンの始めに使う。StartCoroutine に渡す）。
        /// <paramref name="timeoutSeconds"/> 秒たっても準備ができなければ鳴らさない（D-Drive の起動役が無いシーンだけで遊んだとき）。
        /// </summary>
        public static IEnumerator PlayBgmWhenReady(AssetId<BgmMarker> id, float timeoutSeconds = 10f)
        {
            if (!id.IsValid)
            {
                yield break;
            }

            var until = Time.unscaledTime + timeoutSeconds;
            while (DDriveRuntimeBootstrap.Instance == null || !DDriveRuntimeBootstrap.Instance.IsReady)
            {
                if (Time.unscaledTime > until)
                {
                    yield break;
                }

                yield return null;
            }

            PlayBgm(id);
        }

        private static BgmData ScaledClone(ulong id, BgmData source, float master)
        {
            var volume = Mathf.Clamp01(source.Volume * master);
            if (BgmClones.TryGetValue(id, out var cached) && cached.clone != null && cached.source == source)
            {
                cached.clone.Volume = volume;
                return cached.clone;
            }

            // データ（ScriptableObject）の複製。プレハブを出すわけではない。
            var clone = Object.Instantiate(source);
            clone.hideFlags = HideFlags.HideAndDontSave;
            clone.Volume = volume;
            BgmClones[id] = (source, volume, clone);
            return clone;
        }
    }
}
