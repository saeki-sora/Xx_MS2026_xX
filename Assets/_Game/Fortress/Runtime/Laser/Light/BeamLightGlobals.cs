using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ビームの光をシェーダーへ渡すグローバル値（Shaders/FortressBeamLight.hlsl）。砲台（プレイヤー）ごとに1枠、4本ぶん。
    /// 群衆の敵（SwarmSprite）と、床・背景の物（ステージの MS2026/Stage/Prop）が読む。撃っていない枠は明るさ0。
    /// </summary>
    public static class BeamLightGlobals
    {
        public const int SlotCount = 4;

        private static readonly int SegmentsId = Shader.PropertyToID("_FortressBeamLightSegments");
        private static readonly int EnemyColorsId = Shader.PropertyToID("_FortressBeamLightColors");
        private static readonly int FloorColorsId = Shader.PropertyToID("_FortressBeamFloorColors");
        private static readonly int FloorParamsId = Shader.PropertyToID("_FortressBeamFloorParams");
        private static readonly int EnemyOverlayId = Shader.PropertyToID("_FortressBeamEnemyOverlay");

        private static readonly Vector4[] Segments = new Vector4[SlotCount];
        private static readonly Vector4[] EnemyColors = new Vector4[SlotCount];
        private static readonly Vector4[] FloorColors = new Vector4[SlotCount];
        private static readonly Vector4[] FloorParams = new Vector4[SlotCount];

        /// <summary>1本のビームの光。</summary>
        public struct Beam
        {
            /// <summary>ビームの始点・終点（ワールド）。z はビームの高さとして使う。</summary>
            public Vector3 start;
            public Vector3 end;

            /// <summary>敵を照らす色×明るさと、ビームから何メートルまで照らすか。</summary>
            public Color enemyColor;
            public float enemyRadius;

            /// <summary>床・背景の物を照らす色×明るさと、光の届く距離（メートル）。</summary>
            public Color floorColor;
            public float floorRange;

            /// <summary>光の線をビームからどれだけ浮かせるか（メートル。-Z が上）。</summary>
            public float floorHeight;

            /// <summary>当たっている場所（終点）に足す明るさ（0なら足さない）。</summary>
            public float impactExtra;

            /// <summary>照らされた色が床・背景の物の色にどれだけ左右されるか（0〜1）。</summary>
            public float surfaceColorInfluence;

            /// <summary>照らされた敵に光の色をそのまま乗せる量（0〜1。全ビーム共通の値として使う）。</summary>
            public float enemyColorOverlay;
        }

        public static void Set(int slot, in Beam beam)
        {
            if (slot < 0 || slot >= SlotCount)
            {
                return;
            }

            Segments[slot] = new Vector4(beam.start.x, beam.start.y, beam.end.x, beam.end.y);
            EnemyColors[slot] = new Vector4(beam.enemyColor.r, beam.enemyColor.g, beam.enemyColor.b, Mathf.Max(0.01f, beam.enemyRadius));
            FloorColors[slot] = new Vector4(beam.floorColor.r, beam.floorColor.g, beam.floorColor.b, Mathf.Max(0.01f, beam.floorRange));
            FloorParams[slot] = new Vector4(beam.start.z, beam.floorHeight, Mathf.Max(0f, beam.impactExtra), Mathf.Clamp01(beam.surfaceColorInfluence));
            Shader.SetGlobalFloat(EnemyOverlayId, Mathf.Clamp01(beam.enemyColorOverlay));
            Push();
        }

        public static void Clear(int slot)
        {
            if (slot < 0 || slot >= SlotCount)
            {
                return;
            }

            EnemyColors[slot] = Vector4.zero;
            FloorColors[slot] = Vector4.zero;
            FloorParams[slot] = Vector4.zero;
            Push();
        }

        private static void Push()
        {
            Shader.SetGlobalVectorArray(SegmentsId, Segments);
            Shader.SetGlobalVectorArray(EnemyColorsId, EnemyColors);
            Shader.SetGlobalVectorArray(FloorColorsId, FloorColors);
            Shader.SetGlobalVectorArray(FloorParamsId, FloorParams);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (var i = 0; i < SlotCount; i++)
            {
                Segments[i] = Vector4.zero;
                EnemyColors[i] = Vector4.zero;
                FloorColors[i] = Vector4.zero;
                FloorParams[i] = Vector4.zero;
            }

            Push();
        }
    }
}
