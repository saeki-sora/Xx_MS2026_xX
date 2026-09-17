using UnityEngine;

namespace MS2026.GripInputBridge.Transports
{
    /// <summary>
    /// 波形プリセットの値を、経過時間から計算する純粋な関数群。
    /// Unity実行時のTime/Keyboardに依存しないため、Edit Modeテストで直接検証できる。
    /// </summary>
    public static class GripWaveformPresetMath
    {
        // リズムテスト用パルスの1拍の長さ(秒)。設計書8.3の譜面イメージに合わせた簡易実装。
        private const float PulseBeatSeconds = 0.4f;
        private const float PulseHighValue = 0.8f;
        private const float PulseLowValue = 0f;

        // 一定値を保つプリセットも、完全に静止した値だと不自然なので、
        // 人が握力を維持しようとしたときの微小なゆらぎに見立てた緩やかな正弦波を重ねる。
        private const float OscillationHz = 0.5f;

        public static float Evaluate(GripWaveformPreset preset, float elapsedSeconds)
        {
            return preset switch
            {
                GripWaveformPreset.Soft => Oscillate(elapsedSeconds, 0.2f, 0.3f),
                GripWaveformPreset.Firm => Oscillate(elapsedSeconds, 0.5f, 0.6f),
                GripWaveformPreset.Full => Oscillate(elapsedSeconds, 0.9f, 1.0f),
                GripWaveformPreset.RhythmTestPulse => RhythmPulse(elapsedSeconds),
                _ => 0f
            };
        }

        private static float Oscillate(float elapsedSeconds, float min, float max)
        {
            var mid = (min + max) * 0.5f;
            var amplitude = (max - min) * 0.5f;
            var value = mid + amplitude * Mathf.Sin(elapsedSeconds * 2f * Mathf.PI * OscillationHz);
            return Mathf.Clamp01(value);
        }

        private static float RhythmPulse(float elapsedSeconds)
        {
            var beatIndex = (int)(elapsedSeconds / PulseBeatSeconds);
            var isGripBeat = beatIndex % 2 == 0;
            return isGripBeat ? PulseHighValue : PulseLowValue;
        }
    }
}
