using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 爆弾の敵の設定（<see cref="EnemyTypeDefinition.bomb"/>）。「倒すと爆発する」を ON にすると、
    /// 点滅しながら近づき、倒された場所で爆発して近くの敵・破壊可能物・コアを巻き込む。
    /// 巻き込まれた爆弾もまた爆発するので、群れの中で連鎖する。今は群衆(Swarm)モードの敵だけで動く。
    /// </summary>
    [Serializable]
    public sealed class EnemyBombSettings
    {
        [Tooltip("ON にすると、この敵は倒されたときに爆発する（爆弾の敵）。")]
        public bool explodeOnDeath;

        [Header("爆発")]
        [Tooltip("爆発が届く半径（ワールド単位）。")]
        [Min(0.1f)]
        public float explosionRadius = 1.6f;

        [Tooltip("爆発に巻き込まれた敵が受けるダメージ。巻き込まれた爆弾も、これで倒れれば爆発する（連鎖）。")]
        [Min(0f)]
        public float enemyDamage = 30f;

        [Tooltip("爆発に巻き込まれた破壊可能物（スマッシュボールも含む）が受けるダメージ。0 なら壊さない。")]
        [Min(0f)]
        public float obstacleDamage = 10f;

        [Tooltip("コアの近くで倒したとき、爆発がコア（味方）に与えるダメージ。0 ならコアは巻き込まない。")]
        [Min(0f)]
        public float coreDamage = 2f;

        [Tooltip("爆発してから、まわりにダメージが届くまでの秒数。0.1 前後にすると、ポン、ポン、ポンと順に連鎖して見える。0 なら一瞬で連鎖する。")]
        [Range(0f, 1f)]
        public float chainDelaySeconds = 0.12f;

        [Tooltip("ON なら、コアにたどり着いたときもその場で自爆する（コアには上の「コアに与えるダメージ」ではなく、敵の「コアに与えるダメージ」が入る）。" +
                 "まわりの敵や破壊可能物は巻き込む。コアに張り付くモードでも、爆弾は張り付かずに自爆する。")]
        public bool explodeAtCore = true;

        [Header("爆発の演出")]
        [Tooltip("爆発したときのエフェクト・効果音。各PCが自分で鳴らす（通信しない）。色付けは倒した人のプレイヤー色（Host・オフラインのみ）。")]
        public FortressEffect onExplode = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("爆発の届く範囲を、広がって消える輪で見せる（仮の見た目。本番のエフェクトができたら OFF にしてよい）。")]
        public bool showBlastRing = true;

        [Tooltip("広がる輪の色。")]
        public Color blastRingColor = new Color(1f, 0.55f, 0.15f, 0.9f);

        [Tooltip("爆発のまわりの床・背景の物が、輪の形に波打って広がる（衝撃波）。敵・砲台・コアの絵は歪まない。各PCが自分で描く（通信しない）。")]
        public bool shockwave = true;

        [Tooltip("衝撃波で床・背景をずらす量（メートル）。")]
        [Range(0f, 1f)]
        public float shockwaveStrength = 0.25f;

        [Tooltip("衝撃波が広がりきるまでの秒数。")]
        [Range(0.1f, 2f)]
        public float shockwaveSeconds = 0.45f;

        [Tooltip("爆発の瞬間に、丸い閃光をパッと光らせる。")]
        public bool flash = true;

        [Tooltip("閃光の色。")]
        [ColorUsage(false)]
        public Color flashColor = new Color(1f, 0.85f, 0.55f, 1f);

        [Tooltip("閃光の大きさ（爆発の半径に対する倍率）。")]
        [Range(0.1f, 3f)]
        public float flashSize = 1f;

        [Tooltip("閃光が消えるまでの秒数。")]
        [Range(0.05f, 1f)]
        public float flashSeconds = 0.22f;

        [Header("点滅（爆発しそうな見た目）")]
        [Tooltip("点滅するときの色。")]
        public Color blinkColor = new Color(1f, 0.15f, 0.1f, 1f);

        [Tooltip("点滅の強さ。0 なら点滅しない、1 なら点滅の瞬間は完全にこの色になる。")]
        [Range(0f, 1f)]
        public float blinkStrength = 0.85f;

        [Tooltip("止まっているとき・コアから遠いときの点滅の速さ（回/秒）。")]
        [Min(0f)]
        public float blinkPerSecond = 1.5f;

        [Tooltip("コアに近づくほど点滅が速くなる強さ。0 なら速さは変わらない。")]
        [Min(0f)]
        public float blinkSpeedUpNearCore = 4f;

        [Tooltip("コアからこの距離より近づくと、点滅が速くなり始める（ワールド単位）。")]
        [Min(0.5f)]
        public float blinkSpeedUpDistance = 14f;

        [Tooltip("点滅の瞬間に、体を少しふくらませる割合。0.15 なら 15% 大きくなる。")]
        [Range(0f, 0.5f)]
        public float blinkSwell = 0.15f;
    }
}
