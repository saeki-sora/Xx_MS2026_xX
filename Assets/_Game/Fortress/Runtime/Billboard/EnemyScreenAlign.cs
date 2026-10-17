using UnityEngine;

namespace MS2026.Fortress.Billboards
{
    /// <summary>
    /// 敵(群衆)の絵を「画面の向き」に合わせるための共有値。画面を回した視点(画面の回転が0°以外)でも、
    /// 絵の上が画面の上を向き、左右の向き・前後の重なり順も画面基準になる。
    /// 視点プリセットの enemiesFaceScreen を見て、カメラリグ(FortressCameraRig)が毎フレーム書く。リグが無ければ以前と同じワールド基準。
    /// 各PCが自分のカメラで決めるだけなので、通信やゲームの判定には一切影響しない。
    /// </summary>
    public static class EnemyScreenAlign
    {
        /// <summary>シェーダー(SwarmSpriteCommon.hlsl)が読むグローバル値。1で画面基準、0でワールドの+Yが絵の上。</summary>
        public static readonly int ShaderId = Shader.PropertyToID("_FortressEnemyScreenAlign");

        /// <summary>画面基準にしているか。</summary>
        public static bool Enabled { get; private set; }

        /// <summary>地面(XY平面)上での「画面の上」方向(単位ベクトル)。群衆の重なり順(奥から描く)に使う。OFFなら+Y。</summary>
        public static Vector2 ScreenUp { get; private set; } = Vector2.up;

        /// <summary>カメラの向きから値を決める。camera が無い・OFFならワールド基準に戻す。</summary>
        public static void Apply(bool enabled, Camera camera)
        {
            var up = Vector2.up;
            if (enabled && camera != null)
            {
                var cameraUp = camera.transform.up;
                var ground = new Vector2(cameraUp.x, cameraUp.y);
                if (ground.sqrMagnitude > 1e-8f)
                {
                    up = ground.normalized;
                }
            }

            Enabled = enabled && camera != null;
            ScreenUp = up;
            Shader.SetGlobalFloat(ShaderId, Enabled ? 1f : 0f);
        }

        public static void Reset() => Apply(false, null);

        /// <summary>ドメインリロード無効時にPlay Modeをまたいで値が残らないようにする。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Reset();
    }
}
