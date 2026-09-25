using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// DestructibleEffect（Prefab＋効果音）を実際に鳴らす共通処理。DestructibleFeedbackとSmashBallModuleの両方が使う。
    /// </summary>
    public static class DestructibleEffectPlayer
    {
        public static void Play(DestructibleEffect effect, Vector3 position, float effectLifetimeSeconds)
        {
            if (effect == null)
            {
                return;
            }

            if (effect.prefab != null)
            {
                var instance = Object.Instantiate(effect.prefab, position, Quaternion.identity);
                Object.Destroy(instance, effectLifetimeSeconds);
            }

            if (effect.sound != null)
            {
                PlaySound(effect, position);
            }
        }

        private static void PlaySound(DestructibleEffect effect, Vector3 position)
        {
            var pitch = Mathf.Max(0.1f, effect.pitch + Random.Range(-effect.pitchVariance, effect.pitchVariance));

            var go = new GameObject("DestructibleSound");
            go.transform.position = position;

            var source = go.AddComponent<AudioSource>();
            source.clip = effect.sound;
            source.volume = effect.volume;
            source.pitch = pitch;
            source.spatialBlend = 0f;
            source.Play();

            Object.Destroy(go, effect.sound.length / pitch + 0.1f);
        }
    }
}
