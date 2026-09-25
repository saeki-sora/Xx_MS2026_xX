using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>破壊後の再生のしかた。</summary>
    public enum ObstacleRegenMode
    {
        /// <summary>再生待ち時間が終わった瞬間、満タンで復活する。</summary>
        Instant,

        /// <summary>再生待ち時間の後、再生時間をかけてHPが少しずつ回復する（回復中も攻撃を受けられる）。</summary>
        Gradual
    }

    /// <summary>障害物の移動方式。</summary>
    public enum ObstacleMovementMode
    {
        /// <summary>動かない。配置した位置に固定。</summary>
        Stationary,

        /// <summary>プレイヤー側の基準点とコア側の基準点を結ぶ線分上を、ランダムに行ったり来たりする。</summary>
        Wander,

        /// <summary>コア(中心)を軸に、一定半径の円を描いて周回する。オブジェクト自体の自転ではなく公転。</summary>
        Orbit
    }

    /// <summary>
    /// 地形障害物1種類分のチューニング。耐久・再生・見た目・移動を全てインスペクターで調整できる。
    /// 障害物ごとに別アセットを割り当てれば、砲台ごとに硬さや動き方の違う地形も表現できる。
    /// </summary>
    [CreateAssetMenu(menuName = "Fortress/Destructible Obstacle Tuning", fileName = "New DestructibleObstacleTuning")]
    public sealed class DestructibleObstacleTuning : ScriptableObject
    {
        [Header("耐久")]
        [Tooltip("障害物の最大HP。")]
        [Min(1f)]
        public float maxHealth = 30f;

        [Header("レーザーの強さ→壊れる速さ")]
        [Tooltip("オフにすると、レーザーの太さに関係なく一定の速さで壊れる(太さ=1として計算)。")]
        public bool scaleBreakSpeedByLaserStrength = true;

        [Tooltip(
            "横軸=レーザーの太さ(0-1、握力に対応)、縦軸=ダメージの倍率。" +
            "例えば右肩上がりにすると、強く握るほど加速度的に速く壊れるようになる。")]
        public AnimationCurve laserStrengthToBreakSpeedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("再生（時間経過で戻る）")]
        [Tooltip("破壊されてから再生を始めるまでの待機時間(秒)。")]
        [Min(0f)]
        public float regenDelay = 4f;

        [Tooltip("Instant=待機後に瞬時に満タン復活。Gradual=待機後、regenDurationをかけて少しずつ回復。")]
        public ObstacleRegenMode regenMode = ObstacleRegenMode.Gradual;

        [Tooltip("Gradualのとき、0から満タンまで回復するのにかかる時間(秒)。")]
        [Min(0.01f)]
        public float regenDuration = 2.5f;

        [Header("ひび割れ演出")]
        [Tooltip("オフにすると、ダメージを受けてもひびは表示されない(色の変化のみ)。")]
        public bool enableCrackVisual = true;

        [Tooltip("横軸=残りHP(0-1)、縦軸=ひびの濃さ(0-1)。満タンで0、HPが減るほど濃くなるのが基本形。")]
        public AnimationCurve crackAlphaCurve = AnimationCurve.Linear(1f, 0f, 0f, 1f);

        [Header("破壊演出（ひびが限界に達して崩れ落ちる）")]
        [Tooltip("崩れるときに飛び散る破片の数。")]
        [Range(1, 12)]
        public int breakFragmentCount = 5;

        [Tooltip("破片が消えるまでの時間(秒)。")]
        [Min(0.05f)]
        public float breakEffectDuration = 0.6f;

        [Tooltip("破片が飛び散る速さ(ワールド単位/秒)。")]
        [Min(0f)]
        public float breakEffectLaunchSpeed = 3f;

        [Tooltip("破片の回転速度(度/秒)。")]
        public float breakEffectSpinSpeedDegrees = 320f;

        [Tooltip("破片が落ちる重力加速度(ワールド単位/秒^2)。0にすると重力なしで飛び散るだけになる。")]
        [Min(0f)]
        public float breakEffectGravity = 6f;

        [Header("移動")]
        [Tooltip("Stationary=動かない / Wander=プレイヤーとコアの間をランダムに漂う / Orbit=コアを中心に円形に周回する。")]
        public ObstacleMovementMode movementMode = ObstacleMovementMode.Wander;

        [Header("移動：ランダムに漂う（Wander）")]
        [Tooltip("移動する速さ(ワールド単位/秒)。")]
        [Min(0f)]
        public float wanderSpeed = 1.5f;

        [Tooltip(
            "移動できる範囲(x=下限, y=上限。ともに0-1)。0=プレイヤー側の端、1=コア側の端。" +
            "デフォルトはその中間帯のみを漂う。")]
        public Vector2 wanderRange01 = new Vector2(0.2f, 0.7f);

        [Tooltip("次の目的地へ向かう前に、その場で立ち止まる時間(秒)の範囲。")]
        public Vector2 wanderPauseSecondsRange = new Vector2(0.5f, 2f);

        [Header("移動：円形に周回する（Orbit）")]
        [Tooltip("コアからの周回半径(ワールド単位)。")]
        [Min(0.1f)]
        public float orbitRadius = 3f;

        [Tooltip("周回速度(度/秒)。負の値にすると回転方向が逆になる。")]
        public float orbitSpeedDegreesPerSecond = 40f;

        [Header("見た目（プレースホルダー。本番アセットに差し替え可能）")]
        [Tooltip("HPが満タンのときの色。")]
        public Color healthyColor = new Color(0.55f, 0.57f, 0.62f);

        [Tooltip("HPが0に近いときの色。healthyColorからこの色へ補間される。")]
        public Color damagedColor = new Color(0.85f, 0.32f, 0.18f);
    }
}
