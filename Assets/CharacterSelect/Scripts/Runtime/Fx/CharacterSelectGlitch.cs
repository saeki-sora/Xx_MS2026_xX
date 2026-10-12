using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 文字や絵に「ザザッ」と電脳ノイズ（マゼンタと水色の色ずれ）を走らせる演出。
    /// 同じ物に付けた Shadow 2つ（マゼンタ・水色）を、走らせている間だけ左右にずらして見せる（ふだんは透明）。
    /// <see cref="Play"/> で走らせる。<see cref="idleInterval"/> を入れると、ときどき勝手に走る。
    /// </summary>
    [AddComponentMenu("キャラ選択/演出/色ずれノイズ (CharacterSelectGlitch)")]
    public sealed class CharacterSelectGlitch : MonoBehaviour
    {
        [Tooltip("マゼンタ側のずれ（同じ物の Shadow）。")]
        public Shadow magenta;

        [Tooltip("水色側のずれ（同じ物の Shadow）。")]
        public Shadow cyan;

        [Tooltip("いちばん強いときのずれ（ピクセル）。")]
        public float amplitude = 8f;

        [Tooltip("1回のノイズの長さ（秒）。")]
        [Min(0.01f)]
        public float duration = 0.3f;

        [Tooltip("ときどき勝手に走る間隔（秒。0 で走らない）。毎回少しばらつく。")]
        [Min(0f)]
        public float idleInterval;

        [Tooltip("勝手に走るときの強さ（0〜1）。")]
        [Range(0f, 1f)]
        public float idleStrength = 0.4f;

        private float _left;
        private float _strength;
        private float _nextIdle;

        private static readonly Color Magenta = new Color(1f, 0.2f, 0.75f);
        private static readonly Color Cyan = new Color(0.2f, 0.95f, 1f);

        /// <summary>ノイズを走らせる（strength 0〜1）。</summary>
        public void Play(float strength = 1f)
        {
            _strength = Mathf.Max(_left > 0f ? _strength : 0f, Mathf.Clamp01(strength));
            _left = duration;
        }

        private void OnEnable()
        {
            _nextIdle = Time.unscaledTime + NextIdleDelay();
            Apply(0f);
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            if (idleInterval > 0f && now >= _nextIdle)
            {
                _nextIdle = now + NextIdleDelay();
                Play(idleStrength);
            }

            if (_left <= 0f)
            {
                return;
            }

            _left = Mathf.Max(0f, _left - Time.unscaledDeltaTime);
            Apply(_left / duration * _strength);
        }

        private float NextIdleDelay() => idleInterval > 0f ? idleInterval * Random.Range(0.7f, 1.3f) : float.MaxValue;

        private void Apply(float k)
        {
            // 毎フレーム少しずつ違う方向にずらすと「ザザッ」と見える。
            var x = amplitude * k * (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.5f, 1f);
            var y = amplitude * k * Random.Range(-0.25f, 0.25f);
            if (magenta != null)
            {
                magenta.effectDistance = new Vector2(x, y);
                magenta.effectColor = new Color(Magenta.r, Magenta.g, Magenta.b, k > 0f ? 0.85f : 0f);
            }

            if (cyan != null)
            {
                cyan.effectDistance = new Vector2(-x, -y);
                cyan.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, k > 0f ? 0.85f : 0f);
            }
        }
    }
}
