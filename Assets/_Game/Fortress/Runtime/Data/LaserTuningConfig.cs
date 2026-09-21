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

        [Header("回転（砲台は自動で360度回り続ける）")]
        [Tooltip("通常時の回転速度(度/秒)。360なら1秒で1周。")]
        [Min(0f)]
        public float rotationSpeedDegPerSec = 45f;

        [Tooltip("レーザー射出中の回転速度の倍率。横軸=レーザーの強さ(0-1、太さ)、縦軸=倍率。" +
                 "既定は強く握るほど遅くなり、最大の強さで20%まで落ちる。")]
        public AnimationCurve firingRotationMultiplierCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.2f);

        [Tooltip("チャージ中（最大握力をキープして発射を待っている間）の回転速度の倍率。")]
        [Range(0f, 1f)]
        public float chargingRotationMultiplier = 1f;

        [Tooltip("オーバーヒートで沈黙している間の回転速度の倍率。0で止まる。")]
        [Range(0f, 1f)]
        public float overheatedRotationMultiplier = 1f;

        [Tooltip("回転速度が切り替わるときの追従の速さ。大きいほど即座に切り替わり、小さいほど滑らかに加減速する。0で即時。")]
        [Min(0f)]
        public float rotationResponse = 8f;

        [Header("照準表示（砲台の向きを示す線。射出していない間だけ表示）")]
        [Tooltip("砲台の向きを示す線を表示するか。")]
        public bool showAimIndicator = true;

        [Tooltip("線の長さ(ワールド単位)。射程より長くはならない。")]
        [Min(0.1f)]
        public float aimIndicatorLength = 6f;

        [Tooltip("線の太さ(ワールド単位)。")]
        [Min(0.005f)]
        public float aimIndicatorWidth = 0.06f;

        [Tooltip("線の濃さ(0-1)。")]
        [Range(0f, 1f)]
        public float aimIndicatorAlpha = 0.35f;

        [Header("射程・ダメージ")]
        [Tooltip("レーザーが届く最大距離(ワールド単位)。")]
        [Min(0.1f)]
        public float range = 20f;

        [Tooltip("太さ最大時に敵へ与える秒間ダメージ。実際のダメージは現在の太さ(0-1)に比例してスケールする。")]
        [Min(0f)]
        public float maxDamagePerSecond = 20f;

        [Header("貫通（群衆の敵に対して）")]
        [Tooltip("ONならレーザーが群衆の敵を貫通して、ビーム上の敵全員にダメージを与える。OFFなら最初の1体だけ。")]
        public bool pierceEnemies = true;

        [Tooltip("貫通できる敵の最大数（砲台に近い順）。0なら無制限。")]
        [Min(0)]
        public int maxPierceCount;

        [Tooltip("破壊可能な障害物へのダメージ倍率。1で敵と同じ、2なら障害物は倍の速さで壊れる。")]
        [Min(0f)]
        public float obstacleDamageMultiplier = 1f;

        /// <summary>レーザーの強さ(太さ0-1)に応じた、射出中の回転速度の倍率。</summary>
        public float EvaluateFiringRotationMultiplier(float strength01)
        {
            return Mathf.Max(0f, firingRotationMultiplierCurve.Evaluate(Mathf.Clamp01(strength01)));
        }

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
