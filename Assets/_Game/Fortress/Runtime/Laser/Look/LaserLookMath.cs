using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary><see cref="LaserTurretLook"/> の計算部分（Unityの部品に触らない純粋な計算。テストあり）。</summary>
    public static class LaserLookMath
    {
        /// <summary>ビームの表面が流れる速さ（メートル/秒）。強く握るほど速い。</summary>
        public static float FlowSpeed(LaserLookSettings look, float thickness01)
        {
            return Mathf.Lerp(look.flowSpeedWeak, look.flowSpeedStrong, Mathf.Clamp01(thickness01));
        }
    }
}
