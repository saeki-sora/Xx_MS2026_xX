using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// レーザー1本分の挙動チューニング。全砲台で共有する想定だが、砲台ごとに別アセットを
    /// 割り当てれば個体差（キャラ違いなど）も表現できる。数値は全てインスペクターで調整可能。
    /// </summary>
    [CreateAssetMenu(menuName = "Fortress/Laser Tuning Config", fileName = "New LaserTuningConfig")]
    public sealed class LaserTuningConfig : ScriptableObject
    {
        [Header("チャージ発射（最大握力を一定時間キープしてから発射）")]
        [Tooltip("握力がchargeGripThreshold01以上の状態を、この秒数だけ継続キープすると発射が始まる。" +
                 "キープ中に握力がしきい値を下回るとチャージは0に戻る。")]
        [Min(0f)]
        public float chargeToFireSeconds = 1f;

        [Tooltip("チャージが進行するとみなす握力のしきい値(0-1)。1.0で完全な最大握力のみ、" +
                 "0.95などにするとほぼ最大でもチャージが進むようになる。")]
        [Range(0f, 1f)]
        public float chargeGripThreshold01 = 0.95f;

        [Header("太さ（握力%＝レーザーの太さ）")]
        [Tooltip("握力(0-1)からレーザーの太さ(0-1)への変換カーブ。左端が握力0、右端が握力1。")]
        public AnimationCurve gripToThicknessCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("レーザーの最小の太さ(ワールド単位)。握っていない間は太さ0になる。")]
        [Min(0f)]
        public float minThickness = 0.05f;

        [Tooltip("レーザーの最大の太さ(ワールド単位)。握力1.0のときの太さ。")]
        [Min(0f)]
        public float maxThickness = 0.6f;

        [Header("熱・オーバーヒート（熱＝握力²の時間積分）")]
        [Tooltip("熱の上昇係数。毎フレーム 握力^2 × この値 × 経過時間 を積分する。強く握るほど熱が跳ね上がる。")]
        [Min(0f)]
        public float heatGainPerSecond = 1f;

        [Tooltip("握っていない間、またはオーバーヒート沈黙中に熱が自然に下がる速度(熱量/秒)。")]
        [Min(0f)]
        public float heatCoolingPerSecond = 0.6f;

        [Tooltip("この熱量に達すると砲台が灼け落ちてオーバーヒートする。")]
        [Min(0.01f)]
        public float overheatThreshold = 10f;

        [Tooltip("オーバーヒート後、再び握って発射できるようになるまでの沈黙時間(秒)。")]
        [Min(0f)]
        public float overheatSilenceDuration = 3f;

        [Header("射程・ダメージ")]
        [Tooltip("レーザーが届く最大距離(ワールド単位)。")]
        [Min(0.1f)]
        public float range = 20f;

        [Tooltip("太さ最大時に敵へ与える秒間ダメージ。実際のダメージは現在の太さ(0-1)に比例してスケールする。")]
        [Min(0f)]
        public float maxDamagePerSecond = 20f;

        /// <summary>握力(0-1)から太さ(0-1、正規化値)を求める。</summary>
        public float EvaluateThickness01(float grip01)
        {
            return Mathf.Clamp01(gripToThicknessCurve.Evaluate(Mathf.Clamp01(grip01)));
        }

        /// <summary>握力(0-1)からワールド単位の太さを求める。</summary>
        public float EvaluateThicknessMeters(float grip01)
        {
            return Mathf.Lerp(minThickness, maxThickness, EvaluateThickness01(grip01));
        }
    }
}
