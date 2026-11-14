using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>【実験】WASD で動ける範囲の決め方。</summary>
    public enum TurretMoveArea
    {
        /// <summary>ステージの床の上ならどこでも。</summary>
        WholeFloor,

        /// <summary>自分の席のまわりの円の中だけ。</summary>
        AroundSeat
    }

    /// <summary>
    /// 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】WASD で自分の砲台を動かす設定。
    /// Resources/TurretMoveSettings に置くと読み込まれる（無ければ既定値で動く）。Play 中に値を変えるとすぐ効く。
    /// </summary>
    [CreateAssetMenu(menuName = "Fortress/Turret Move Settings (実験)", fileName = "TurretMoveSettings")]
    public sealed class TurretMoveSettings : ScriptableObject
    {
        public const string ResourcePath = "TurretMoveSettings";

        [Tooltip("OFF にすると WASD で動かなくなる（今までと同じ、その場から動かない砲台に戻る）。")]
        public bool movementEnabled = true;

        [Header("動き")]
        [Tooltip("いちばん速いときの速さ（ワールド単位/秒）。")]
        [Min(0f)]
        public float moveSpeed = 5.5f;

        [Tooltip("押し始めてから最高速になるまでの速さ（大きいほどキビキビ動き出す）。")]
        [Min(0.1f)]
        public float acceleration = 38f;

        [Tooltip("離してから止まるまでの速さ（大きいほどピタッと止まる）。")]
        [Min(0.1f)]
        public float deceleration = 30f;

        [Tooltip("ON ならキーの上下左右は「画面の」上下左右（画面を回した視点でも W が画面の上）。OFF ならワールドの上下左右。")]
        public bool screenRelative = true;

        [Header("当たり判定")]
        [Tooltip("砲台の体の半径。障害物・コア・敵とはこの丸で当たる。")]
        [Min(0.05f)]
        public float bodyRadius = 0.55f;

        [Tooltip("障害物として当たるレイヤー。トリガー（通行コスト地帯など）は当たらない。砲台どうしは当たらない。")]
        public LayerMask obstacleLayers = ~0;

        [Tooltip("コア（歯）の当たり半径の代わりの値。歯のモデルが見えているときは、その大きさ（下の「歯の当たりの大きさ」）を使う。")]
        [Min(0f)]
        public float coreBlockRadius = 1.6f;

        [Header("歯（コア）がビームを止める")]
        [Tooltip("ON なら、ビームは壁と同じように歯で止まる（歯は傷つかない）。歯の向こう側の敵には届かない。\n" +
                 "歯に張り付くと、回るビームの半分が歯に当たってしまうので、少し離れて立つ方が得になる。")]
        public bool coreBlocksLasers = true;

        [Tooltip("歯の当たりの大きさ。歯のモデルの見た目の半径にこの倍率を掛ける（1 で見た目ぴったり、小さくするとビームが歯のふちをかすめる）。")]
        [Range(0.3f, 1.5f)]
        public float coreLaserBlockScale = 0.85f;

        [Header("スマッシュボール（枠の外側を漂う）")]
        [Tooltip("ON なら、スマッシュボールは枠（上の「枠の名前」）の外側の帯の中だけを漂う。枠が無いステージでは今まで通り。")]
        public bool smashBallOutsideFrame = true;

        [Tooltip("枠の外側のふちと、ボールのふちの間を最低これだけ空ける。")]
        [Min(0f)]
        public float smashBallGap = 0.4f;

        [Tooltip("枠の外側のふちから、ボールの中心がこの距離まで離れてよい。\n" +
                 "枠のふちに張り付いたプレイヤーの画面から出ない幅にする（ステージ1の今のカメラだと、枠の外側のふちから画面の上下の端までは約8。ボールの大きさと余裕を見て 4.5）。")]
        [Min(0.5f)]
        public float smashBallBandWidth = 4.5f;

        [Tooltip("漂う向きのうち、枠に沿って回る向きをどれだけ強くするか。0 = ばらばら、1 = ほぼ枠に沿って回る。")]
        [Range(0f, 1f)]
        public float smashBallAlongFrame = 0.7f;

        [Header("行ける範囲")]
        [Tooltip("床全域 = ステージの枠（下の「枠の名前」）の内側。枠が無ければ「床」の上ならどこでも（体が床からはみ出さない所まで）。床も無いステージでは敵の通り道の範囲（NavigationField）。\n" +
                 "席のまわり = 自分の席から「席からの距離」の円の中だけ。")]
        public TurretMoveArea area = TurretMoveArea.WholeFloor;

        [Tooltip("「床全域」のとき、役割が「床」の物の中にこの名前の物があれば、その枠の内側だけを動ける範囲にする（ほかの床は使わない）。\n" +
                 "枠の内側は、枠のモデルの形から自動で求める。空にすると、床を全部合わせた範囲。")]
        public string areaFrameName = "stage_frame";

        [Tooltip("枠の内側から、さらにこの幅だけ内側までにする（枠のふちに乗って見えるときに増やす）。")]
        [Min(0f)]
        public float frameInset = 0.1f;

        [Tooltip("エディタだけ: 障害物に当たったとき、当たった物の名前を Console に出す（何に止められているか確かめる用）。")]
        public bool logBlockingObstacles = true;

        [Tooltip("「席のまわり」のとき、自分の席（最初に置かれた場所）からこの距離まで動ける。")]
        [Min(0f)]
        public float rangeRadius = 6f;

        [Tooltip("範囲の端のこの幅に入ると、外向きの動きがだんだん弱まる（壁にゴツンと当たる感じを和らげる）。")]
        [Min(0f)]
        public float rangeSoftEdge = 1.2f;

        [Tooltip("「席のまわり」のとき、範囲の端に近づくと地面に薄い輪（自分の色）を出す。床全域のときは床の端が見えるので出さない。")]
        public bool showRangeRing = true;

        [Tooltip("輪を出し始める、端までの距離。")]
        [Min(0.1f)]
        public float ringShowDistance = 2f;

        [Header("敵に押される")]
        [Tooltip("OFF にすると敵に押されない（すり抜ける）。")]
        public bool pushedByEnemies = true;

        [Tooltip("敵と重なったときに押し出される強さ（重なり1体ぶんあたりの速さ）。")]
        [Min(0f)]
        public float enemyPushStrength = 9f;

        [Tooltip("押される速さの上限（何千体に囲まれても、これ以上は速く流されない）。")]
        [Min(0f)]
        public float maxEnemyPushSpeed = 4f;

        [Tooltip("押される速さの変化をなめらかにする速さ（大きいほどすぐ反応する）。")]
        [Min(0.1f)]
        public float enemyPushResponse = 12f;

        [Header("菌にやられる・復活する")]
        [Tooltip("ON なら、菌（群衆の敵）に触れている間、砲台に汚れが溜まり（体力が減り）、溜まりきると倒れる（撃てない・回らない・動けない・スキルも使えない）。\n" +
                 "倒れている間は、菌は素通りする（押さない・汚さない）。味方のビームを当ててもらうか、握る連打で抜け出すと復活する。\n" +
                 "時間では回復しない。味方のビームを当ててもらうと回復する。")]
        public bool downEnabled = true;

        [Tooltip("菌1体ぶんがぴったり重なっているとき、1秒に溜まる汚れ（1 で倒れる）。重なっている菌が多いほど速く溜まる。")]
        [Min(0f)]
        public float dirtPerOverlap = 0.45f;

        [Tooltip("どんなに囲まれても、1秒に溜まる汚れはこれまで（倒れるまでの最短の秒数 = 1 ÷ この値）。")]
        [Min(0.01f)]
        public float dirtMaxPerSecond = 0.9f;

        [Tooltip("時間では回復しない。倒れていない人に味方のビームが当たっている間、1秒にこれだけ汚れが落ちる（体力が戻る。2人に当ててもらえば2倍）。")]
        [Min(0f)]
        public float allyBeamHealPerSecond = 0.5f;

        [Tooltip("自分の画面の右上に、自分の体力バーを出す（仮の見た目）。")]
        public bool showHealthBar = true;

        [Tooltip("倒れた人の砲台を、ほかの人の画面で色あせて見せる強さ（0 = そのまま、1 = 灰色）。")]
        [Range(0f, 1f)]
        public float downedDesaturate = 1f;

        [Tooltip("倒れた人の砲台の、ほかの人の画面での明るさ（1 = そのまま、小さいほど暗い）。")]
        [Range(0.05f, 1f)]
        public float downedDarken = 0.4f;

        [Tooltip("倒れた人の砲台の、ほかの人の画面での濃さ（1 = そのまま、小さいほど薄い）。")]
        [Range(0.1f, 1f)]
        public float downedAlpha = 0.85f;

        [Tooltip("倒れた砲台から煙を出す（全員の画面。菌の色のくすんだ煙が、画面の上へ立ちのぼる）。")]
        public bool downedSmoke = true;

        [Tooltip("倒れた砲台に、菌のべったりした汚れをかぶせる（全員の画面）。")]
        public bool downedGoo = true;

        [Tooltip("倒れている間、自分で抜け出すのに要る「握る」回数（キーボードは連打、専用コントローラーは にぎにぎ）。長押しは1回と数える。")]
        [Min(1)]
        public int strugglePresses = 18;

        [Tooltip("味方1人のビームを当て続けてもらって復活するまでの秒数（2人なら半分）。")]
        [Min(0.05f)]
        public float reviveBeamSeconds = 1.2f;

        [Tooltip("復活した直後、この秒数だけ菌にやられない（汚れが溜まらない）。")]
        [Min(0f)]
        public float protectSeconds = 2.5f;

        [Tooltip("倒れたときの演出（D-Drive の VFX・SE。プレイヤー色）。各PCが自分で鳴らす。")]
        public FortressEffect onDowned = new FortressEffect();

        [Tooltip("復活したときの演出（D-Drive の VFX・SE。プレイヤー色）。各PCが自分で鳴らす。")]
        public FortressEffect onRevived = new FortressEffect();

        [Header("カメラ")]
        [Tooltip("砲台が席から離れた分の、どれだけカメラが付いてくるか。1で完全に付いてくる、0で付いてこない。")]
        [Range(0f, 1f)]
        public float cameraFollow = 0.85f;

        [Tooltip("カメラが追いつくまでの時間（秒）。小さいほどぴったり付いてくる。")]
        [Min(0.01f)]
        public float cameraSmoothTime = 0.22f;

        [Tooltip("動いている方向の先を少し見せる時間（秒）。速さ×この秒数だけ先を映す。")]
        [Min(0f)]
        public float cameraLookAheadSeconds = 0.3f;

        [Tooltip("先を見せる距離の上限。")]
        [Min(0f)]
        public float cameraMaxLookAhead = 1.4f;

        [Tooltip("先読みが変わるときのなめらかさ（秒）。カメラ本体より少しゆっくりにすると、向きを変えたときにふわっと流れる。")]
        [Min(0.01f)]
        public float cameraLookAheadSmoothTime = 0.45f;

        [Tooltip("全速で動いている間だけ、少し引いて広く映す割合（0.05 = 5% 広く）。止まると戻る。")]
        [Range(0f, 0.3f)]
        public float cameraZoomOutWhileMoving = 0.05f;

        [Tooltip("引き・戻しのなめらかさ（秒）。")]
        [Min(0.01f)]
        public float cameraZoomSmoothTime = 0.6f;

        [Header("通信（LAN）")]
        [Tooltip("自分の位置を送る回数（回/秒）。")]
        [Min(1f)]
        public float sendRate = 30f;

        [Tooltip("他の人の砲台が、届いた位置へ追いつく速さ。")]
        [Min(0.1f)]
        public float remoteSharpness = 14f;

        [Tooltip("届いた位置とこれ以上離れていたら、なめらかにせずその場へ置く。")]
        [Min(0.1f)]
        public float remoteSnapDistance = 3f;

        private static TurretMoveSettings _fallback;
        private static TurretMoveSettings _loaded;

        /// <summary>Resources の設定。無ければ既定値の設定を作って返す。毎フレーム呼んでもよい（一度読んだら覚えておく）。</summary>
        public static TurretMoveSettings Load()
        {
            if (_loaded != null)
            {
                return _loaded;
            }

            _loaded = Resources.Load<TurretMoveSettings>(ResourcePath);
            if (_loaded != null)
            {
                return _loaded;
            }

            if (_fallback == null)
            {
                _fallback = CreateInstance<TurretMoveSettings>();
                _fallback.hideFlags = HideFlags.HideAndDontSave;
            }

            return _fallback;
        }
    }
}
