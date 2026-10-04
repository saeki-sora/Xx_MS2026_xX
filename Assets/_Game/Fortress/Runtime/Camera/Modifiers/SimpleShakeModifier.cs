using System;
using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>D-Driveの揺れアセットを用意しなくても使える、組み込みの簡易揺れの設定。</summary>
    [Serializable]
    public sealed class SimpleShakeSettings
    {
        [Tooltip("位置の揺れ幅(ワールド単位)。")]
        [Min(0f)]
        public float amplitude = 0.25f;

        [Tooltip("画面の回転の揺れ幅(度)。")]
        [Min(0f)]
        public float rollAmplitude = 0.5f;

        [Tooltip("揺れの細かさ(Hz)。")]
        [Min(0.1f)]
        public float frequency = 18f;

        [Tooltip("揺れが収まるまでの秒数。")]
        [Min(0.01f)]
        public float duration = 0.3f;
    }

    /// <summary>Perlinノイズで視点の中心と回転を揺らす簡易揺れ。時間とともに二乗で減衰する。</summary>
    public sealed class SimpleShakeModifier : ICameraViewModifier
    {
        private readonly SimpleShakeSettings _settings;
        private readonly float _strength;
        private readonly float _seed;
        private float _elapsed;

        public SimpleShakeModifier(SimpleShakeSettings settings, float strength)
        {
            _settings = settings;
            _strength = Mathf.Max(0f, strength);
            _seed = UnityEngine.Random.value * 1000f;
        }

        public bool Apply(ref CameraViewSettings view, float deltaTime)
        {
            if (_settings == null)
            {
                return false;
            }

            _elapsed += deltaTime;
            var remaining = 1f - Mathf.Clamp01(_elapsed / _settings.duration);
            var weight = remaining * remaining * _strength;
            var time = _elapsed * _settings.frequency;

            var offset = new Vector2(Noise(_seed, time), Noise(_seed + 37.1f, time)) * (_settings.amplitude * weight);
            view.center += offset;
            view.rollDegrees += Noise(_seed + 91.7f, time) * _settings.rollAmplitude * weight;

            return _elapsed < _settings.duration;
        }

        private static float Noise(float seed, float time) => Mathf.PerlinNoise(seed, time) * 2f - 1f;
    }
}
