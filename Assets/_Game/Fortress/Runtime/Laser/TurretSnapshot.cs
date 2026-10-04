using Unity.Netcode;

namespace MS2026.Fortress
{
    /// <summary>
    /// 砲台の見た目に必要な状態一式。ネット対戦ではHostが計算した値をこの形でClientへ配る。
    /// 中身は値型だけなので、そのままバイト列として送れる(INetworkSerializeByMemcpy)。
    /// </summary>
    public struct TurretSnapshot : INetworkSerializeByMemcpy
    {
        public byte State;
        public float Heat;
        public float Thickness01;
        public float ThicknessMeters;
        public float ChargeProgress01;

        /// <summary>砲台の向き(transform.eulerAngles.z)。</summary>
        public float AngleDegrees;

        /// <summary>回転速度(度/秒)。反時計回りが正。Client側で次の値が届くまでの回転を補うのに使う。</summary>
        public float SignedRotationSpeed;
    }
}
