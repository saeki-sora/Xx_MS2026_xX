using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 盾持ちの敵（コの字の大きな盾で自分とお供を守る。2026-10-29、ユーザーが比較ページで「短めのB（にらみ盾）」を選んだ）。
    /// ・盾はビームを止める壁（当たり判定）。普通のビーム・赤の極太レーザーは止まり、黄色の矢は跳ね返る。緑・青のスキルと爆発は範囲なので中まで届く
    /// ・盾は「最近いちばん当ててきている人」の方へゆっくり向きを変える（誰も当てていなければ一番近い砲台）。向きは Host が決めて配る
    /// ・湧くときにお供（<see cref="escortType"/>）も一緒に湧き、お供は盾持ちのまわりの決まった場所にまとまって歩く
    /// ・盾持ちを倒すと盾はすぐ消え、お供は普通の敵としてコアへ向かう
    /// 今は群衆モードのみ。
    /// </summary>
    [Serializable]
    public sealed class EnemyShieldSettings
    {
        [Tooltip("ON にすると、この種類はコの字の盾を持つ（群衆モードのみ）。")]
        public bool hasShield;

        [Header("盾の形（m。盾持ちの中心から見て、前＝にらんでいる向き）")]
        [Tooltip("中心から正面の板までの距離。")]
        [Min(0.2f)] public float frontDistance = 1.85f;

        [Tooltip("盾の幅の半分（中心から横の板まで）。")]
        [Min(0.2f)] public float halfWidth = 1.75f;

        [Tooltip("横の板の長さ（正面の板から後ろへ）。短いほど、後ろ側のお供を横から撃てる。")]
        [Min(0f)] public float sideLength = 1.85f;

        [Tooltip("板の厚み（当たり判定と見た目）。")]
        [Min(0.05f)] public float thickness = 0.25f;

        [Header("にらむ（向きを変える）")]
        [Tooltip("向きを変える速さ（度/秒）。速いほど裏に回りにくい。")]
        [Min(1f)] public float turnDegreesPerSecond = 63f;

        [Tooltip("当てられた記憶が薄れる速さ（秒）。長いほど、前に撃ってきた人をにらみ続ける。")]
        [Min(0.1f)] public float threatMemorySeconds = 2f;

        [Tooltip("にらむ相手を変えるには、新しい人の「当てた量」が今の相手の何倍必要か（小さいとすぐ向きを変える）。")]
        [Min(1f)] public float switchRatio = 1.3f;

        [Tooltip("ビーム（スキル含む）が盾持ちの中心からこの距離以内を通ったら「当ててきた」と数える（m）。")]
        [Min(0.5f)] public float threatRadius = 2.4f;

        [Header("お供")]
        [Tooltip("一緒に湧くお供の種類。空ならお供なし。お供は普通の敵として作り、盾持ちの近くにいる間だけ盾の中にまとまって歩く。")]
        public EnemyTypeDefinition escortType;

        [Tooltip("一緒に湧くお供の数。")]
        [Range(0, 30)] public int escortCount = 12;

        [Tooltip("お供が並ぶ輪の間隔（m）。1つ目の輪に6体、2つ目に12体…と外へ並ぶ。")]
        [Min(0.2f)] public float escortSpacing = 0.46f;

        [Tooltip("盾持ちからこれより離れたお供は、ついて行くのをやめて普通の敵に戻る（m）。")]
        [Min(1f)] public float escortLeashDistance = 5f;

        [Header("見た目")]
        [Tooltip("盾の色（仮の見た目）。")]
        public Color color = new Color(0.62f, 0.68f, 0.74f, 1f);

        [Tooltip("盾の高さ（仮の見た目。m）。")]
        [Min(0f)] public float wallHeight = 0.6f;

        [Tooltip("差し替え用の見た目。入れると仮の板の代わりにこれを置く（+X が正面、盾持ちの中心が原点。当たり判定は上の形のまま）。")]
        public GameObject visualPrefab;

        [Tooltip("にらんでいる人の色の印を、正面の板の前に出す。")]
        public bool showFocusMarker = true;

        [Header("演出")]
        [Tooltip("盾持ちを倒して盾が割れたとき（倒した人の色）。空なら仮の破片を飛ばす。")]
        public FortressEffect onBreak = new FortressEffect();

        [Tooltip("にらむ相手が変わったとき（新しく、にらまれた人の色）。効果音向け。")]
        public FortressEffect onFocusChanged = new FortressEffect();
    }
}
