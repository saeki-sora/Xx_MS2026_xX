using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 合間のイベント「歯みがきタイム」（2026-10-24、ユーザー案）。
    /// 歯（コア）に汚れが付くので、全員で「にぎにぎ」（小刻みに握る）して磨く。きれいにした量だけコアが回復し、
    /// 満タンからあふれた分は次のウェーブだけ効く「コーティング」（ダメージを先に受けてくれる盾）になる。
    /// 全員がそろって握ると「シンクロ」でボーナス。100% になったら早めに終わる。
    /// 流れ: はじめ（説明・手のアニメ）→ みがく → 結果（HP +12 など）。砲台はこの間、撃てない・回らない。
    /// 数えるのは各PCで（自分のセンサーで握った回数を数えて Host に送る）。結果を決めるのは Host。
    /// </summary>
    [CreateAssetMenu(menuName = "Fortress/Wave Events/歯みがきタイム", fileName = "Brushing")]
    public sealed class BrushingEvent : WaveIntervalEvent
    {
        [Header("時間（秒）")]
        [Tooltip("はじめの説明（「歯みがきタイム！にぎにぎしてみがこう」と手のアニメ）。この間は数えない。")]
        [Min(0f)]
        public float introSeconds = 1.5f;

        [Tooltip("みがく時間。")]
        [Min(1f)]
        public float brushSeconds = 8f;

        [Tooltip("結果（HP +12 など）を見せる時間。")]
        [Min(0f)]
        public float resultSeconds = 2f;

        [Header("にぎにぎ")]
        [Tooltip("1人あたり、この回数にぎにぎすると 100%（全員分をまとめて数える。いる人数で必要な回数が変わる）。")]
        [Min(1)]
        public int pulsesPerPlayer = 18;

        [Tooltip("握りの値がこれを越えたら「1回」。")]
        [Range(0.05f, 1f)]
        public float pressThreshold = 0.55f;

        [Tooltip("次の1回を数えるには、握りの値がこれより下がる必要がある（値のゆれで数えすぎないように）。")]
        [Range(0f, 0.95f)]
        public float releaseThreshold = 0.35f;

        [Header("ごほうび（最大HPに対する割合）")]
        [Tooltip("100% きれいにしたときに増える量。まず HP の回復に使い、満タンからあふれた分はコーティングになる。")]
        [Range(0f, 1f)]
        public float healFraction = 0.25f;

        [Tooltip("コーティングの上限。次のウェーブの間だけ、ダメージを先に受けてくれる。")]
        [Range(0f, 1f)]
        public float coatingFraction = 0.2f;

        [Header("シンクロ（全員がそろって握る）")]
        [Tooltip("全員がこの秒数の中で1回ずつ握ったら「シンクロ」。")]
        [Min(0.05f)]
        public float syncWindowSeconds = 0.3f;

        [Tooltip("シンクロ1回で足されるボーナスの回数。")]
        [Min(0)]
        public int syncBonusPulses = 2;

        [Tooltip("次のシンクロまでの最短の秒数。")]
        [Min(0f)]
        public float syncCooldownSeconds = 0.5f;

        // 演出（各PCで鳴らす。位置はコア）。要塞デザイナーでは折りたたみの中に出す。
        [Tooltip("歯みがきタイムが始まったとき。")]
        public FortressEffect onStart = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("自分が1回にぎにぎしたとき（シャカッ）。自分のPCだけで鳴る。")]
        public FortressEffect onPulse = new FortressEffect();

        [Tooltip("シンクロしたとき。")]
        public FortressEffect onSync = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("結果が出たとき。")]
        public FortressEffect onResult = new FortressEffect { tintWithPlayerColor = false };

        public override float DurationSeconds => introSeconds + brushSeconds + resultSeconds;

        public override WaveIntervalRunner CreateRunner(GameObject host) => host.AddComponent<BrushingRunner>();

        private void Reset()
        {
            displayName = "歯みがきタイム";
        }
    }
}
