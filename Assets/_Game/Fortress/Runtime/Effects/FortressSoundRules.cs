using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 効果音の「鳴らすか」「どの音量で鳴らすか」の決まり（サウンドスタジオ、2026-11-22）。
    /// Unity や通信に頼らない計算だけなので、テストで全部の組み合わせを確かめられる。
    /// </summary>
    public static class FortressSoundRules
    {
        /// <summary>
        /// このPCで鳴らすか。ネットにつながっていない（<paramref name="localIndex"/> &lt; 0）・持ち主が分からない（<paramref name="ownerIndex"/> &lt; 0）ときは、今までどおり鳴らす。
        /// 持ち主が人でない席（CPU）の音は Host のPCで鳴らす。
        /// </summary>
        public static bool ShouldPlay(SoundAudience audience, int ownerIndex, int localIndex, bool isHost, bool ownerIsHuman)
        {
            if (audience == SoundAudience.Everyone || ownerIndex < 0 || localIndex < 0)
            {
                return true;
            }

            // Auto でここに来た = 持ち主の番号が渡された → その人のPCだけ。
            if (ownerIndex == localIndex)
            {
                return true;
            }

            return isHost && !ownerIsHuman;
        }

        /// <summary>音そのものの音量 × この場所の倍率 × 全体音量を、0〜1 に収めて返す（Unity の音量は1が上限）。</summary>
        public static float FinalVolume(float soundVolume, float slotScale, float master)
        {
            return Mathf.Clamp01(soundVolume * slotScale * master);
        }
    }
}
