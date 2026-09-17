using UnityEngine;

namespace MS2026.GripInputBridge.Transports
{
    /// <summary>
    /// シミュレータ入力（キー押下時に上昇、離すと減衰）の純粋な計算部分。
    /// Unity API（Keyboard.current / Time.deltaTime）に依存させず、ここだけを切り出すことで
    /// Edit Mode テストから直接検証できるようにしている。
    /// </summary>
    public static class GripSimulationMath
    {
        /// <summary>
        /// 現在値・保持状態・経過時間から次フレームの値を計算する。0-1にクランプする。
        /// </summary>
        public static float StepValue(
            float currentValue,
            bool isHeld,
            float deltaTime,
            float riseSpeedPerSecond,
            float decaySpeedPerSecond)
        {
            float speed = isHeld ? riseSpeedPerSecond : -decaySpeedPerSecond;
            return Mathf.Clamp01(currentValue + speed * deltaTime);
        }
    }
}
