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

        [Header("押し合いの方式")]
        [Tooltip("1体ずつ押し合う（従来）: 1体ごとに近くの敵を調べて押し戻す。上の「押し戻しの反復回数」だけくり返す。重い。\n" +
                 "流体の圧力でまとめて押す: マスごとの混み具合を出し、混んでいる所から空いている所へまとめて押す（液体のような動き）。" +
                 "近くの重なりの仕上げだけ1体ずつ行う（下の「仕上げの押し合い回数」）。群衆の計算がかなり軽くなる。" +
                 "押し合いを計算するのは Host（とオフライン）だけなので、通信や参加側には関係しない。")]
        public SwarmCrowdSolver crowdSolver = SwarmCrowdSolver.Fluid;

        [Tooltip("【流体】混んでいる所から押し出す強さ。大きいほど早く広がる（さらさら）、小さいほど詰まったまま溜まる（どろどろ）。")]
        [Range(0f, 2f)]
        public float fluidPressure = 0.35f;

        [Tooltip("【流体】体でこの割合までマスが埋まると押し返し始める（0〜1。1でマスいっぱい）。大きいほどぎゅうぎゅうに詰まる。")]
        [Range(0.1f, 1.2f)]
        public float fluidRestOccupancy = 0.6f;

        [Tooltip("【流体】まとめて押した後に、1体ずつの押し合い（近くの重なりの仕上げ）を何回行うか。0なら行わない（一番軽いが、重なりが少し残る）。")]
        [Range(0, 4)]
        public int fluidRefineIterations = 1;

        [Header("障害物とのぶつかり（ぶつかってから避ける挙動）")]
        [Tooltip("障害物を事前に見て避ける度合い。0=障害物を気にせずコアへ一直線に進み、ぶつかってから壁沿いに流れて回り込む。" +
                 "1=最初から経路どおりに迂回する（ぶつからない）。")]
        [Range(0f, 1f)]
        public float obstacleAwareness = 0.1f;

        [Tooltip("壁にこの距離（ワールド単位）まで近づくと、経路（回り込む向き）に従って壁沿いに進み始める。" +
                 "大きいほど壁の手前から回り込み、小さいほど壁にぎりぎりまで突っ込む。")]
        [Min(0.05f)]
        public float wallContactRange = 0.6f;

        [Tooltip("壁に向かう速度を消して壁沿いに滑らせる強さ(0-1)。0だと壁に押し付けたまま止まり、1だとするっと滑る。")]
        [Range(0f, 1f)]
        public float wallSlide = 0.9f;

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

        [Tooltip("1フレームに実際に追加する敵の上限。一斉投入でも数フレームに分けて追加し、その瞬間のカクつきを抑える。" +
                 "超えた分は次のフレームに回る(数万体でも0.1〜0.2秒ほどで全員が出る)。0なら無制限(一度に全員出す)。")]
        [Min(0)]
        public int maxSpawnsPerFrame = 3000;

        [Header("描画")]
        [Tooltip("下（手前）にいる敵を上に描く。オフにすると少し軽くなるが、重なり方が不自然になる。")]
        public bool ySort = true;

        [Tooltip("敵の描画順（レンダーキュー）。他のスプライトより後ろに描くなら小さく、手前なら大きくする。")]
        [Range(1000, 4000)]
        public int renderQueue = 2990;

        [Tooltip("被弾時に白く光る長さ(秒)。")]
        [Min(0.01f)]
        public float hitFlashDuration = 0.1f;

        [Tooltip("敵1体ずつの足元に、ぼんやりした丸い影を落とす。地面に立っている感じが出る（全ての敵を1回の描画でまとめて描くので軽い）。")]
        public bool footShadow = true;

        [Tooltip("足元の影の横幅（敵の絵の大きさに対する割合）。")]
        [Range(0.1f, 2f)]
        public float footShadowSize = 0.75f;

        [Tooltip("足元の影の濃さ（0〜1）。")]
        [Range(0f, 1f)]
        public float footShadowDarkness = 0.4f;

        [Header("群れのうねり（混んだ所に白いつやの波が流れる。見た目だけ）")]
        [Tooltip("混んだ所の敵の上を、白いつやの帯が波のようにゆっくり流れる。液体のように見える。")]
        public bool crowdSheen = true;

        [Tooltip("つやの強さ（0で光らない）。")]
        [Range(0f, 2f)]
        public float crowdSheenStrength = 0.45f;

        [Tooltip("つやの色。")]
        [ColorUsage(false)]
        public Color crowdSheenColor = Color.white;

        [Tooltip("混んだ所を少し暗くする量（0〜1）。立体感が出る。")]
        [Range(0f, 1f)]
        public float crowdDarken = 0.18f;

        [Tooltip("つやの波が流れる速さ。")]
        [Min(0f)]
        public float crowdWaveSpeed = 1.6f;

        [Tooltip("つやの波の細かさ（大きいほど波の間隔が狭い）。")]
        [Range(0.05f, 5f)]
        public float crowdWaveScale = 0.9f;

        [Tooltip("まわりにこの数の敵がいると「すっかり混んでいる」とみなす（混み具合の基準。小さいほど少しの群れでも光る）。")]
        [Min(1f)]
        public float crowdFullCount = 24f;

        [Header("倒したときの光の粒（見た目だけ）")]
        [Tooltip("倒した敵が、倒したプレイヤーの色の光の粒になってパッと散り、その人の砲台へ吸い込まれる。だれが倒したか分からないときは、その場で消える。")]
        public bool killSparks = true;

        [Tooltip("1体倒すごとに出す光の粒の数。")]
        [Range(0, 8)]
        public int sparksPerKill = 3;

        [Tooltip("画面に同時に出せる光の粒の上限。超えた分は出さない（大群を一度に倒しても重くならないように）。")]
        [Range(100, 10000)]
        public int maxSparks = 3000;

        [Tooltip("1フレームに新しく出せる光の粒の上限。")]
        [Range(10, 2000)]
        public int maxNewSparksPerFrame = 300;

        [Tooltip("光の粒の大きさ（メートル）。")]
        [Range(0.02f, 1f)]
        public float sparkSize = 0.16f;

        [Tooltip("倒した瞬間に散る速さ（メートル/秒）。")]
        [Min(0f)]
        public float sparkBurstSpeed = 3.5f;

        [Tooltip("散ってから砲台へ向かい始めるまでの秒数。")]
        [Min(0f)]
        public float sparkHomingDelay = 0.18f;

        [Tooltip("砲台へ向かう速さ（メートル/秒）。")]
        [Min(0.1f)]
        public float sparkHomingSpeed = 16f;

        [Tooltip("倒した瞬間の、燃え尽きるような光の大きさ（敵の絵の大きさに対する倍率）。0なら出さない。")]
        [Range(0f, 3f)]
        public float deathFlashSize = 0.9f;

        [Tooltip("光の粒の明るさ。")]
        [Range(0.1f, 4f)]
        public float sparkBrightness = 1.4f;

        [Header("演出")]
        [Tooltip("レーザーが群衆の敵に当たった瞬間のエフェクト（砲台配置タブ「群衆の敵に当たった瞬間」）を、" +
                 "全レーザー合計で1秒に出してよい最大数。0で無制限（非推奨。数千体に当たると一瞬で大量に出る。それでも1フレーム256個まで）。" +
                 "「当たった瞬間」は、被弾フラッシュが消えていた敵（被弾フラッシュの長さ以上当たっていなかった敵）に触れた瞬間。")]
        [Min(0)]
        public int hitEffectsPerSecond = 60;

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
                    fluidPressure = 0.22f;
                    fluidRestOccupancy = 0.65f;
                    break;

                case SwarmPreset.Fluid:
                    densitySlowdown = 0.3f;
                    momentumTransfer = 0.9f;
                    separationStiffness = 0.95f;
                    separationIterations = 4;
                    personalSpace = 1f;
                    fluidPressure = 0.55f;
                    fluidRestOccupancy = 0.5f;
                    break;

                case SwarmPreset.Packed:
                    densitySlowdown = 0.7f;
                    momentumTransfer = 0.5f;
                    separationStiffness = 0.7f;
                    separationIterations = 4;
                    personalSpace = 0.8f;
                    fluidPressure = 0.25f;
                    fluidRestOccupancy = 0.8f;
                    break;

                default:
                    densitySlowdown = 0.55f;
                    momentumTransfer = 0.6f;
                    separationStiffness = 0.85f;
                    separationIterations = 3;
                    personalSpace = 1f;
                    fluidPressure = 0.35f;
                    fluidRestOccupancy = 0.6f;
                    break;
            }
        }
    }
}
