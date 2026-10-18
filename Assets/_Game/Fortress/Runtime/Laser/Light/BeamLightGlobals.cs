using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 群衆の敵のシェーダー（SwarmSpriteCommon.hlsl）へ、4本のビームの光（線分・色・届く距離）を渡すグローバル値。
    /// 砲台（プレイヤー）ごとに1枠。撃っていない枠は明るさ0。
    /// </summary>
    public static class BeamLightGlobals
    {
        public const int SlotCount = 4;

        private static readonly int SegmentsId = Shader.PropertyToID("_FortressBeamLightSegments");
        private static readonly int ColorsId = Shader.PropertyToID("_FortressBeamLightColors");

        private static readonly Vector4[] Segments = new Vector4[SlotCount];
        private static readonly Vector4[] Colors = new Vector4[SlotCount];

        /// <param name="color">光の色×明るさ（rgb）。</param>
        /// <param name="radius">ビームから何メートルまで照らすか。</param>
        public static void Set(int slot, Vector2 start, Vector2 end, Color color, float radius)
        {
            if (slot < 0 || slot >= SlotCount)
            {
                return;
            }

            Segments[slot] = new Vector4(start.x, start.y, end.x, end.y);
            Colors[slot] = new Vector4(color.r, color.g, color.b, Mathf.Max(0.01f, radius));
            Push();
        }

        public static void Clear(int slot)
        {
            if (slot < 0 || slot >= SlotCount)
            {
                return;
            }

            Colors[slot] = Vector4.zero;
            Push();
        }

        private static void Push()
        {
            Shader.SetGlobalVectorArray(SegmentsId, Segments);
            Shader.SetGlobalVectorArray(ColorsId, Colors);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (var i = 0; i < SlotCount; i++)
            {
                Segments[i] = Vector4.zero;
                Colors[i] = Vector4.zero;
            }

            Push();
        }
    }
}
