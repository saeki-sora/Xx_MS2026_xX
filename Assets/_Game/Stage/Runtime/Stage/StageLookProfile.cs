using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 背景の反応（透け・敵の影・熱・揺れ・壊れ方）の全体設定。ステージ（StageSet）ごとに選べる。
    /// オブジェクトごとのON/OFFや強さは各背景オブジェクトの「見た目の反応」で決め、ここは共通の強さ・色を決める。
    /// </summary>
    [CreateAssetMenu(menuName = "Stage/Stage Look Profile", fileName = "StageLookProfile")]
    public sealed class StageLookProfile : ScriptableObject
    {
        // ── 透け（手前の物が砲台・コアを隠したとき） ──（見出しは StageLookProfileEditor が日本語で出す）
        [Tooltip("砲台のまわりを透かす丸の半径（ワールド単位）。")]
        [Min(0f)] public float turretHoleRadius = 1.6f;

        [Tooltip("コア（中央の水晶）のまわりを透かす丸の半径（ワールド単位）。")]
        [Min(0f)] public float coreHoleRadius = 2.4f;

        [Tooltip("自分の砲台だけ、透かす丸をこの倍率で大きくする（ネット対戦で自分のPCの画面だけ）。")]
        [Min(1f)] public float localTurretHoleScale = 1.3f;

        [Tooltip("丸のふちのぼかし具合（0=くっきり、1=ふんわり）。")]
        [Range(0f, 1f)] public float holeSoftness = 0.4f;

        [Tooltip("透かす目印を、床からどれだけ上に置くか（砲台・コアの見た目の中心の高さ）。")]
        public float focusHeight = 0.4f;

        [Tooltip("「全体を薄く」の切り替わりの速さ。")]
        [Min(0.1f)] public float wholeFadeSpeed = 6f;

        // ── 隠れた敵の影（シルエット） ──（見出しは StageLookProfileEditor が日本語で出す）
        [Tooltip("ONなら、背景オブジェクトの裏に隠れた敵を単色の影で透かして見せる。")]
        public bool showHiddenEnemies = true;

        [Tooltip("影の色（アルファ＝濃さ）。")]
        public Color hiddenEnemyColor = new Color(0.35f, 0.9f, 1f, 0.55f);

        // ── 熱（レーザーが当たった所） ──（見出しは StageLookProfileEditor が日本語で出す）
        [Tooltip("当て続けたときに光る速さ（1秒あたり。1で1秒で真っ赤）。")]
        [Min(0f)] public float glowPerSecond = 2.5f;

        [Tooltip("当てるのをやめてから冷める速さ（1秒あたり）。")]
        [Min(0f)] public float coolPerSecond = 0.7f;

        [Tooltip("光っている間に焦げが溜まる速さ（1秒あたり。焦げは消えない）。")]
        [Min(0f)] public float scorchPerSecond = 0.3f;

        [Tooltip("光る範囲の半径（レーザーが一番細いとき）。")]
        [Min(0.01f)] public float heatRadius = 0.35f;

        [Tooltip("レーザーが太いほど光る範囲を広げる量（太さ×この値）。")]
        [Min(0f)] public float heatRadiusPerThickness = 0.8f;

        [Tooltip("光る点を、レーザーの線（砲台の発射口の高さ）からさらに上へずらす量。モデルの低い所だけが光るときに増やす。")]
        public float heatHeightOffset = 0.15f;

        [Tooltip("ほんのり熱いときの色。")]
        [ColorUsage(false, true)] public Color glowColor = new Color(2.2f, 0.45f, 0.08f);

        [Tooltip("一番熱いときの色。")]
        [ColorUsage(false, true)] public Color hotColor = new Color(4f, 3.2f, 1.6f);

        [Tooltip("焦げ跡の色。")]
        public Color scorchColor = new Color(0.12f, 0.07f, 0.05f);

        // ── 揺れ（敵の群れに押されたとき） ──（見出しは StageLookProfileEditor が日本語で出す）
        [Tooltip("この人数の敵が周りに押し寄せると、一番大きく傾く。")]
        [Min(1f)] public float pressureForFullTilt = 25f;

        [Tooltip("一番大きく傾いたときの角度（度）。")]
        [Range(0f, 30f)] public float maxTiltDegrees = 5f;

        [Tooltip("ぷるっと縦に縮む量（割合）。")]
        [Range(0f, 0.3f)] public float squash = 0.04f;

        [Tooltip("ばねの硬さ。大きいほど細かく速く震える。")]
        [Min(1f)] public float stiffness = 90f;

        [Tooltip("揺れの止まりやすさ。大きいほどすぐ止まる。")]
        [Min(0f)] public float damping = 7f;

        [Tooltip("押し寄せる敵を数える範囲（オブジェクトの輪郭から外側へ、ワールド単位）。")]
        [Min(0.1f)] public float pressureRange = 0.8f;

        // ── 壊せる壁 ──（見出しは StageLookProfileEditor が日本語で出す）
        [Tooltip("被弾した瞬間に光る色。")]
        [ColorUsage(false, true)] public Color hitFlashColor = new Color(1.6f, 1.6f, 1.6f);

        [Tooltip("被弾した瞬間に光る長さ（秒）。")]
        [Min(0f)] public float hitFlashSeconds = 0.08f;

        [Tooltip("耐久が0に近いほどこの割合まで暗くなる。")]
        [Range(0f, 1f)] public float darkenAtZeroHealth = 0.45f;

        [Tooltip("壊れて溶けるように消える時間（秒）。再生するときも同じ時間で現れる。")]
        [Min(0.01f)] public float dissolveSeconds = 0.7f;

        [Tooltip("溶けるときのふちの色。")]
        [ColorUsage(false, true)] public Color dissolveEdgeColor = new Color(3f, 1.2f, 0.25f);

        private static StageLookProfile _fallback;

        /// <summary>今のステージの設定。StageRoot が無いシーンでは既定値のコピー。</summary>
        public static StageLookProfile Current
        {
            get
            {
                var root = StageRoot.Active;
                if (root != null && root.LookProfile != null)
                {
                    return root.LookProfile;
                }

                if (_fallback == null)
                {
                    _fallback = CreateInstance<StageLookProfile>();
                    _fallback.hideFlags = HideFlags.HideAndDontSave;
                    _fallback.name = "StageLookProfile (既定)";
                }

                return _fallback;
            }
        }

        /// <summary>全体（シェーダーのグローバル値）に関わる色・強さを反映する。</summary>
        public void ApplyGlobals()
        {
            Shader.SetGlobalFloat(StageShaderIds.HoleSoftness, holeSoftness);
            Shader.SetGlobalColor(StageShaderIds.HeatGlowColor, glowColor);
            Shader.SetGlobalColor(StageShaderIds.HeatHotColor, hotColor);
            Shader.SetGlobalColor(StageShaderIds.ScorchColor, scorchColor);
            Shader.SetGlobalColor(StageShaderIds.SilhouetteColor, hiddenEnemyColor);
            Shader.SetGlobalFloat(StageShaderIds.SilhouetteEnabled, showHiddenEnemies ? 1f : 0f);
        }
    }
}
