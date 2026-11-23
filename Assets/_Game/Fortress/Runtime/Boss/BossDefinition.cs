using System;
using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using DDrive.Foundation.Identity;
using DDrive.Runtime.Audio;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>部位の種類。</summary>
    public enum BossPartKind
    {
        /// <summary>砲口: コアへ向けて弾を撃つ（弾はレーザーで消せる）。</summary>
        Cannon = 0,

        /// <summary>菌を出す口: 雑魚の菌をまとめて呼ぶ（いつもの群衆と同じくコアへ向かう）。</summary>
        Vent = 1
    }

    /// <summary>ボスの部位1つ（<see cref="BossDefinition.parts"/>）。場所・大きさはボスの体の半径を 1 とした値。</summary>
    [Serializable]
    public sealed class BossPartDefinition
    {
        [Tooltip("名前（調整画面とテストの表示用）。")]
        public string label = "砲口";

        [Tooltip("種類。砲口は弾を撃ち、菌を出す口は雑魚を呼ぶ。")]
        public BossPartKind kind = BossPartKind.Cannon;

        [Tooltip("体の中心からの場所（体の半径を 1 とした値。−X がコア側＝左、+Y が画面の上）。")]
        public Vector2 offset = new Vector2(-0.57f, 0.73f);

        [Tooltip("当たり判定の半径（体の半径を 1 とした値）。")]
        [Min(0.02f)]
        public float radius = 0.2f;

        [Tooltip("硬さ（HP）。普段のレーザー1本を最大の太さで当て続けると 1秒に約 20 減る。")]
        [Min(1f)]
        public float maxHealth = 80f;

        [Tooltip("戦いが始まってから最初に攻撃するまでの秒数。")]
        [Min(0f)]
        public float firstDelaySeconds = 1.5f;

        [Tooltip("攻撃の間隔（秒）の最小。この範囲でばらつく。")]
        [Min(0.1f)]
        public float intervalMin = 2.2f;

        [Tooltip("攻撃の間隔（秒）の最大。")]
        [Min(0.1f)]
        public float intervalMax = 3.0f;

        [Header("砲口")]
        [Tooltip("撃った弾がコアに届くまでの秒数（距離に関係なくこの秒数で届く速さになる）。")]
        [Min(0.5f)]
        public float bulletTravelSeconds = 6f;

        [Tooltip("弾がコアに当たったときに減らすコアの体力（最大の体力に対する割合。0.02 なら 2%）。")]
        [Range(0f, 0.5f)]
        public float bulletDamage01 = 0.02f;

        [Tooltip("弾の硬さ。小さいほど、レーザーがかすっただけで消える。")]
        [Min(0.01f)]
        public float bulletHealth = 0.6f;

        [Tooltip("弾の半径（メートル）。")]
        [Min(0.05f)]
        public float bulletRadius = 0.45f;

        [Header("菌を出す口")]
        [Tooltip("1回に呼ぶ菌の数。")]
        [Min(1)]
        public int spawnCount = 5;

        [Tooltip("呼んだ菌がばらける半径（メートル）。")]
        [Min(0f)]
        public float spawnSpread = 0.8f;

        public float RandomInterval() => UnityEngine.Random.Range(Mathf.Min(intervalMin, intervalMax), Mathf.Max(intervalMin, intervalMax));
    }

    /// <summary>
    /// ボス1体の決まり（2026-11-15 ユーザー決定）。<see cref="WaveDefinition.boss"/> に入れると、そのウェーブがボスウェーブになる。
    /// ・前のウェーブの後、歯みがきタイムも席替えもせず「FINAL WAVE」→ その場で並び替え（コアが左へ滑り、4人が跳んで縦一列。席替えと同じ跳び方）
    /// ・全員同じ引きの画面。砲台は右向きの扇形を往復する
    /// ・「WARNING」→ ボスが右から入ってきて、少しずつ近づく。部位を壊すと少し押し戻せる
    /// ・弾はコアだけを狙う（レーザーで消せる）。菌を出す口は雑魚を呼ぶ
    /// ・HP は部位を壊したときだけ減る（本体は無敵・生え直さない）。部位を全部壊したら最後の「全員で握れ！」（失敗しない）
    /// ・コアに届いたらコアの体力が減り（既定で最大の 45%）、コアが残ればクリア
    /// ・ボス戦の間は試合の制限時間が止まる
    /// </summary>
    [CreateAssetMenu(menuName = "Fortress/Boss", fileName = "New Boss")]
    public sealed class BossDefinition : ScriptableObject
    {
        [Header("名前・見た目")]
        [Tooltip("画面に出す名前。")]
        public string displayName = "むしば大王";

        [Tooltip("体の絵（空なら仮の絵をコードで作る）。絵の真ん中が体の中心。")]
        public Sprite bodySprite;

        [Tooltip("砲口の絵（空なら仮の絵）。右（+X）向きに描く。")]
        public Sprite cannonSprite;

        [Tooltip("菌を出す口の絵（空なら仮の絵）。")]
        public Sprite ventSprite;

        [Tooltip("壊れた部位の絵（空なら仮の絵）。")]
        public Sprite brokenPartSprite;

        [Tooltip("弾の絵（空なら仮の絵）。")]
        public Sprite bulletSprite;

        [Tooltip("体の半径（ボス戦の遊べる範囲の高さに対する割合）。0.17 なら高さの 17%。")]
        [Range(0.05f, 0.4f)]
        public float bodyRadius01 = 0.17f;

        [Tooltip("体の絵の大きさ（当たり判定の半径に対する倍率。トゲの分だけ大きく描く）。")]
        [Min(0.5f)]
        public float bodySpriteScale = 1.25f;

        [Header("部位")]
        [Tooltip("部位の並び。HP は部位を壊したときだけ減る（全部壊したら最後の「全員で握れ！」）。")]
        public List<BossPartDefinition> parts = DefaultParts();

        [Tooltip("ON: 普段のレーザーが部位に与えるダメージの倍率。")]
        [Min(0f)]
        public float laserDamageMultiplier = 1f;

        [Tooltip("スキル（4色）が部位に与えるダメージの倍率（2026-11-15 ユーザー決定: 部位にも効く。強すぎたらここで弱める）。本体には効かない。")]
        [Min(0f)]
        public float skillDamageMultiplier = 1f;

        [Header("動き")]
        [Tooltip("止まる場所からコアに届くまでの秒数（押し戻さなかったとき）。大きいほどゆっくり近づく。")]
        [Min(5f)]
        public float approachSeconds = 75f;

        [Tooltip("部位を1つ壊したときに押し戻す量（近づく秒数に換算した秒数）。")]
        [Min(0f)]
        public float pushBackSeconds = 9f;

        [Tooltip("上下にゆれる大きさ（遊べる範囲の高さの半分に対する割合。体がはみ出さない所まで）。")]
        [Range(0f, 1f)]
        public float bobAmount01 = 0.45f;

        [Tooltip("上下に1往復する秒数。")]
        [Min(1f)]
        public float bobPeriodSeconds = 9f;

        [Header("並び替え（FINAL WAVE）")]
        [Tooltip("並び替えの帯の文字。")]
        public string formationBanner = "FINAL WAVE";

        [Tooltip("並び替えの帯の小さい文字。")]
        public string formationSubText = "ボスが来る！ 配置が変わります";

        [Tooltip("並び替えの段階の長さ（秒）。4人が跳び終わるまでより長くする。")]
        [Min(1f)]
        public float formationSeconds = 3.6f;

        [Tooltip("コアが左へ滑る秒数。")]
        [Min(0.1f)]
        public float coreSlideSeconds = 1.2f;

        [Tooltip("障害物が動く秒数（残す物は行き先へ、残さない物は画面の端へ）。")]
        [Min(0.1f)]
        public float obstacleSlideSeconds = 1.4f;

        [Tooltip("カメラが全員同じ引きの画面へ移る秒数。")]
        [Min(0f)]
        public float cameraBlendSeconds = 1.2f;

        [Tooltip("配置の割合（ステージの広さから自動で作るときの決まり。ステージに目印があればそちらが優先）。")]
        public BossFormationShape formation = new BossFormationShape();

        [Tooltip("扇形の真ん中から端までの角度（度）。")]
        [Range(5f, 90f)]
        public float sweepHalfAngle = 30f;

        [Tooltip("扇形を往復する速さ（普段の回る速さに対する倍率）。")]
        [Min(0.05f)]
        public float sweepSpeedMultiplier = 1f;

        [Header("WARNING・登場")]
        [Tooltip("「WARNING」を見せる秒数（この後ボスが入ってくる）。")]
        [Min(0.5f)]
        public float warningSeconds = 2.4f;

        [Tooltip("ボスが右から入ってきて止まるまでの秒数。")]
        [Min(0.2f)]
        public float enterSeconds = 2f;

        [Header("終わり")]
        [Tooltip("最後の「全員で握れ！」の最長の秒数（失敗しない。握らなくても、この秒数で必ず終わる）。")]
        [Min(3f)]
        public float finaleMaxSeconds = 30f;

        [Tooltip("（仮・とどめの演出ができるまで）部位を全部壊してから爆発するまでの秒数。")]
        [Min(0.5f)]
        public float finalePlaceholderSeconds = 2.4f;

        [Tooltip("ボスがコアに届いたときに減らすコアの体力（最大の体力に対する割合。2026-11-15 ユーザー決定 45%）。")]
        [Range(0f, 1f)]
        public float reachCoreDamage01 = 0.45f;

        [Tooltip("ボスがコアに届いてから、クリアになるまでの秒数。")]
        [Min(0.5f)]
        public float reachedSeconds = 3f;

        [Header("そのほか")]
        [Tooltip("呼ぶ雑魚の種類（空なら、ウェーブの計画の湧きの行に出てくる最初の種類）。")]
        public EnemyTypeDefinition minionType;

        [Tooltip("ON: ボス戦の間も、このウェーブの湧きの行を流す（既定 OFF。雑魚はボスが呼ぶ）。")]
        public bool alsoPlayWaveSpawns;

        [Header("画面の揺れ")]
        public SimpleShakeSettings enterShake = new SimpleShakeSettings { amplitude = 0.5f, rollAmplitude = 0.8f, duration = 0.5f };
        public SimpleShakeSettings partBreakShake = new SimpleShakeSettings { amplitude = 0.4f, rollAmplitude = 0.6f, duration = 0.35f };
        public SimpleShakeSettings reachShake = new SimpleShakeSettings { amplitude = 0.8f, rollAmplitude = 1.5f, duration = 0.7f };
        public SimpleShakeSettings defeatShake = new SimpleShakeSettings { amplitude = 0.9f, rollAmplitude = 1.5f, duration = 0.9f };

        // 演出（D-Drive の番号札。各PCで鳴らす。空なら鳴らさない）。
        [Tooltip("「WARNING」が出たとき。")]
        public FortressEffect onWarning = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("ボス戦の BGM。「WARNING」が出たときに、試合の BGM から切り替える（空なら試合の BGM のまま。サウンドスタジオ N19）。")]
        public AssetId<BgmMarker> bossBgm;

        [Tooltip("ボスが入ってきて止まったとき。")]
        public FortressEffect onEnter = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("砲口が弾を撃ったとき（砲口の場所）。")]
        public FortressEffect onCannonFire = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("弾をレーザーで消したとき（弾の場所）。")]
        public FortressEffect onBulletBreak = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("弾がコアに当たったとき。")]
        public FortressEffect onBulletHitCore = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("菌を出す口が雑魚を呼んだとき。")]
        public FortressEffect onVentSpawn = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("部位を壊したとき（壊した人の色）。")]
        public FortressEffect onPartBreak = new FortressEffect();

        [Tooltip("ボスがコアに届いたとき。")]
        public FortressEffect onReachCore = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("ボスを倒したとき（大爆発）。")]
        public FortressEffect onDefeat = new FortressEffect { tintWithPlayerColor = false };

        /// <summary>ボスウェーブの「WARNING ＋ 登場」の段階の長さ。</summary>
        public float WarningSeconds => Mathf.Max(0.5f, warningSeconds) + Mathf.Max(0.2f, enterSeconds);

        /// <summary>並び替えの段階の長さ。</summary>
        public float FormationSeconds => Mathf.Max(1f, formationSeconds);

        public int PartCount => parts?.Count ?? 0;

        public static List<BossPartDefinition> DefaultParts() => new List<BossPartDefinition>
        {
            new BossPartDefinition { label = "砲口（上）", kind = BossPartKind.Cannon, offset = new Vector2(-0.57f, 0.73f), radius = 0.2f, maxHealth = 80f, firstDelaySeconds = 1.2f },
            new BossPartDefinition { label = "菌の口（上）", kind = BossPartKind.Vent, offset = new Vector2(-0.95f, 0.27f), radius = 0.18f, maxHealth = 70f, firstDelaySeconds = 2.4f, intervalMin = 3f, intervalMax = 3.8f },
            new BossPartDefinition { label = "菌の口（下）", kind = BossPartKind.Vent, offset = new Vector2(-0.95f, -0.27f), radius = 0.18f, maxHealth = 70f, firstDelaySeconds = 4f, intervalMin = 3f, intervalMax = 3.8f },
            new BossPartDefinition { label = "砲口（下）", kind = BossPartKind.Cannon, offset = new Vector2(-0.57f, -0.73f), radius = 0.2f, maxHealth = 80f, firstDelaySeconds = 2.8f },
        };

        private void OnValidate()
        {
            parts ??= DefaultParts();
        }
    }
}
