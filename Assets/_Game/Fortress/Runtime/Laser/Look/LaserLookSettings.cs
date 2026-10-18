using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 歯磨き粉ビームの「見た目だけ」の設定（表面が先へ流れる速さ）。
    /// <see cref="LaserTuningConfig.look"/> が持ち、<see cref="LaserTurretLook"/> が毎フレーム使う。
    /// 当たり判定・ダメージ・熱の計算には一切影響しない。ネット対戦でも通信せず、各PCが自分で描く。
    /// ビームの色・つや・こぶ・泡などの見た目は、ビームのマテリアル（MS2026/Fortress/ToothpasteBeam）の方で調整する。
    /// </summary>
    [Serializable]
    public sealed class LaserLookSettings
    {
        [Tooltip("OFFにすると、ビームの表面が流れなくなる（つや・こぶ・泡は止まったまま描かれる）。")]
        public bool enabled = true;

        [Min(0f), Tooltip("弱く握っているときに、ビームの表面（こぶ・つや・泡・すじ）が先へ流れる速さ（メートル/秒）。")]
        public float flowSpeedWeak = 1.5f;

        [Min(0f), Tooltip("強く握っているときに、ビームの表面が先へ流れる速さ（メートル/秒）。")]
        public float flowSpeedStrong = 7f;
    }
}
