using UnityEngine;

namespace MS2026.Fortress
{
    public enum SwarmPreset
    {
        Standard,
        Viscous,
        Fluid,
        Packed
    }

    /// <summary>
    /// 群衆シミュレーション全体の設定。「流体らしさ」に関わる値はここに集約してあり、
    /// 要塞デザイナーの「群衆」タブからプリセット選択や数値調整ができる。
    /// </summary>
    [CreateAssetMenu(menuName = "Fortress/Swarm Settings", fileName = "New SwarmSettings")]
    public sealed class SwarmSettings : ScriptableObject
    {
        [Header("容量（Play開始時に確保。変更後は再Playが必要）")]
        [Tooltip("同時に存在できる敵の最大数。多いほどメモリを使う。")]
        [Range(256, 65536)]
        public int maxEnemies = 40000;

        [Tooltip("1フレームで進めるシミュレーション時間の上限(秒)。重いフレームでの暴走・すり抜けを防ぐ。")]
        [Range(0.005f, 0.1f)]
        public float maxFrameDelta = 0.033f;

        [Header("移動と密度")]
        [Tooltip("この半径内の敵の数を「混み具合」として数える。")]
        [Range(0.2f, 2f)]
        public float densityRadius = 0.6f;

        [Tooltip("混み具合の基準数。この数が周囲にいると「完全に密集」とみなす。")]
        [Range(1f, 30f)]
        public float densityReferenceCount = 6f;

        [Tooltip("密集したときの減速の強さ(0-1)。大きいほど詰まりやすく、細い通路で渋滞する。")]
        [Range(0f, 1f)]
        public float densitySlowdown = 0.55f;

        [Tooltip("密集しても最低限保つ速度の割合。")]
        [Range(0.05f, 1f)]
        public float minSpeedFactor = 0.2f;

        [Tooltip("押し合いで動かされた分を、そのまま速度として引き継ぐ割合。大きいほど押されて流れる（さらさら）、小さいほど粘る（どろどろ）。")]
        [Range(0f, 1f)]
        public float momentumTransfer = 0.6f;

        [Tooltip("向きが変わる鋭さ。大きいほど素早く向きを変える。")]
        [Range(1f, 30f)]
        public float turnSharpness = 10f;

        [Header("押し合い（重なりを押し戻す処理）")]
        [Tooltip("1フレームあたりの押し戻しの反復回数。多いほど重なりが解消され硬くなるが、重くなる。")]
        [Range(1, 8)]
        public int separationIterations = 3;

        [Tooltip("押し戻しの強さ(0-1)。1に近いほど重なりを許さない。")]
        [Range(0f, 1f)]
        public float separationStiffness = 0.85f;

        [Tooltip("敵同士が保つ距離の倍率。1で半径ちょうど、小さいほどぎゅうぎゅうに詰まる。")]
        [Range(0.5f, 1.5f)]
        public float personalSpace = 1f;

        [Tooltip("1回の押し戻しで動く最大量（半径に対する倍率）。大きいと弾け飛ぶように動く。")]
        [Range(0.1f, 2f)]
        public float maxCorrectionRatio = 0.6f;

        [Tooltip("壁（障害物）から押し出す強さ(0-1)。")]
        [Range(0f, 1f)]
        public float wallStiffness = 1f;

        [Tooltip("1体あたりが調べる周囲の敵の最大数。密集地帯で処理時間が爆発するのを防ぐ上限。0で無制限。" +
                 "小さいほど軽いが、超密集時の押し合いがやや甘くなる。")]
        [Range(0, 256)]
        public int maxNeighborsChecked = 48;

        [Header("コア到達")]
        [Tooltip("コアに着いた敵の扱い。")]
        public SwarmArrivalMode arrivalMode = SwarmArrivalMode.VanishAndDamage;

        [Tooltip("コア中心からこの距離（敵の半径を足す）に入ると「到達」。")]
        [Min(0.1f)]
        public float arrivalRadius = 0.6f;

        [Tooltip("張り付きモード時、敵1体がコアに与える秒間ダメージ。")]
        [Min(0f)]
        public float lingerDamagePerSecond = 1f;

        [Header("湧き")]
        [Tooltip("湧き位置からこの半径内にばらけて出現する。同じ点に重ならないようにするため。")]
        [Min(0f)]
        public float spawnJitter = 0.3f;

        [Header("描画")]
        [Tooltip("下（手前）にいる敵を上に描く。オフにすると少し軽くなるが、重なり方が不自然になる。")]
        public bool ySort = true;

        [Tooltip("敵の描画順（レンダーキュー）。他のスプライトより後ろに描くなら小さく、手前なら大きくする。")]
        [Range(1000, 4000)]
        public int renderQueue = 2990;

        [Tooltip("被弾時に白く光る長さ(秒)。")]
        [Min(0.01f)]
        public float hitFlashDuration = 0.1f;

        [Header("範囲")]
        [Tooltip("NavigationFieldが無い場合に使う、シミュレーション範囲の大きさ。")]
        [Min(8f)]
        public float fallbackAreaSize = 64f;

        public void ApplyPreset(SwarmPreset preset)
        {
            switch (preset)
            {
                case SwarmPreset.Viscous:
                    densitySlowdown = 0.8f;
                    momentumTransfer = 0.25f;
                    separationStiffness = 0.6f;
                    separationIterations = 3;
                    personalSpace = 1f;
                    break;

                case SwarmPreset.Fluid:
                    densitySlowdown = 0.3f;
                    momentumTransfer = 0.9f;
                    separationStiffness = 0.95f;
                    separationIterations = 4;
                    personalSpace = 1f;
                    break;

                case SwarmPreset.Packed:
                    densitySlowdown = 0.7f;
                    momentumTransfer = 0.5f;
                    separationStiffness = 0.7f;
                    separationIterations = 4;
                    personalSpace = 0.8f;
                    break;

                default:
                    densitySlowdown = 0.55f;
                    momentumTransfer = 0.6f;
                    separationStiffness = 0.85f;
                    separationIterations = 3;
                    personalSpace = 1f;
                    break;
            }
        }
    }
}
