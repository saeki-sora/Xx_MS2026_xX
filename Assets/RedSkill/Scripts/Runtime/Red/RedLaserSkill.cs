using System;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.Serialization;

namespace MS2026.Skills.Red
{
    /// <summary>
    /// 赤キャラのスキル「極太レーザー」の設定ファイル。値はすべてここで調整できる（Play 中に変えてもすぐ反映される）。
    /// 砲台が向いている方向へ、根元から先へ広がっていく極太のレーザーを一定時間撃つ。
    /// 壁・障害物に当たるとそこで止まり、当たった場所に火花・光・衝撃の輪を出す。群衆の敵と破壊できる物にダメージを与える。
    /// 実際に動かすのは <see cref="RedLaserSkillRunner"/>（スキル欄が自動で付ける）。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/スキル/レッド：極太レーザー", fileName = "RedLaserSkill")]
    public sealed class RedLaserSkill : CharacterSkill
    {
        [Tooltip("撃つまでの流れと長さ。")]
        [SkillLabel("タイミング")]
        public RedLaserTiming timing = new RedLaserTiming();

        [Tooltip("レーザーの形（長さ・太さ・広がり方）。")]
        [SkillLabel("形・大きさ")]
        public RedLaserShape shape = new RedLaserShape();

        [Tooltip("ダメージと、何に当たると止まるか。")]
        [SkillLabel("ダメージ・当たり判定")]
        public RedLaserDamage damage = new RedLaserDamage();

        [Tooltip("レーザーの色・グラデーション・模様。")]
        [SkillLabel("レーザーの色・模様")]
        public RedLaserLook look = new RedLaserLook();

        [Tooltip("溜め（スキルを使える状態）の見た目。")]
        [SkillLabel("溜め（使える状態）の演出")]
        public RedLaserChargeFx charge = new RedLaserChargeFx();

        [Tooltip("ビリビリ（赤い稲妻）。溜め中・撃っている間・当たった場所に出る。")]
        [SkillLabel("ビリビリ（稲妻）")]
        public RedLaserLightningFx lightning = new RedLaserLightningFx();

        [Tooltip("スキルを使える間、砲台の上に乗る強力そうな歯磨き粉。スキルのレーザーはこの口から出る。")]
        [SkillLabel("溜めの歯磨き粉")]
        public RedLaserTubeFx tube = new RedLaserTubeFx();

        [Tooltip("発射口（根元）の見た目。")]
        [SkillLabel("発射口の演出")]
        public RedLaserMuzzleFx muzzle = new RedLaserMuzzleFx();

        [Tooltip("壁・障害物に当たった場所の見た目。")]
        [SkillLabel("壁に当たった場所の演出")]
        public RedLaserImpactFx impact = new RedLaserImpactFx();

        [Tooltip("カメラの揺れ。")]
        [SkillLabel("カメラの揺れ")]
        public RedLaserCameraShake cameraShake = new RedLaserCameraShake();

        [Tooltip("レーザーをもっと派手にする追加の演出（光の波・外側の光・レーザーを進む輪・発射口の二重の光）。")]
        [SkillLabel("派手さ（追加の演出）")]
        public RedLaserFlashFx flash = new RedLaserFlashFx();

        [Tooltip("効果音と、D-Drive の追加エフェクト（空なら何もしない）。")]
        [SkillLabel("効果音・追加エフェクト（D-Drive）")]
        public RedLaserSounds sounds = new RedLaserSounds();

        [Tooltip("見た目に使うシェーダー（通常は触らない）。")]
        [SkillLabel("シェーダー（通常は触らない）")]
        public RedLaserShaders shaders = new RedLaserShaders();

        public override CharacterSkillRunner CreateRunner(PlayerSkillSlot slot)
        {
            var runner = slot.gameObject.AddComponent<RedLaserSkillRunner>();
            runner.Configure(this);
            return runner;
        }

        private void Reset()
        {
            skillId = "red_laser";
            displayName = "極太レーザー";
            description = "砲台の向いている方向へ、広がっていく極太の赤いレーザーを撃つ。壁に当たるとそこで止まる。";
            themeColor = new Color(1f, 0.15f, 0.1f);
        }

        /// <summary>根元からの距離 distance での、当たり判定の太さ（メートル）。widthScale は開き・閉じの倍率。</summary>
        public float WidthAt(float distance, float widthScale)
        {
            var spread = shape.spreadDistance > 0.01f ? Mathf.Clamp01(distance / shape.spreadDistance) : 1f;
            var t = shape.spreadCurve != null && shape.spreadCurve.length > 0 ? shape.spreadCurve.Evaluate(spread) : spread;
            return Mathf.Max(0f, Mathf.LerpUnclamped(shape.rootWidth, shape.tipWidth, t) * widthScale);
        }
    }

    [Serializable]
    public sealed class RedLaserTiming
    {
        [Tooltip("撃ち始める前の「ため」の秒数（光が発射口に集まる）。0なら即発射。")]
        [SkillLabel("撃つ前のため（秒）")]
        [Min(0f)]
        public float windupSeconds = 0.3f;

        [Tooltip("レーザーを撃ち続ける秒数（細くなって消える時間は含まない）。")]
        [SkillLabel("撃ち続ける時間（秒）")]
        [Min(0.05f)]
        public float fireSeconds = 3f;

        [Tooltip("撃ち始めてから太さが最大になるまでの秒数。")]
        [SkillLabel("太くなるまで（秒）")]
        [Min(0.01f)]
        public float openSeconds = 0.35f;

        [Tooltip("撃ち終わりに細くなって消えるまでの秒数。")]
        [SkillLabel("消えるまで（秒）")]
        [Min(0.01f)]
        public float closeSeconds = 0.45f;

        [Tooltip("撃ち始めの太さの変化（横=経過0〜1、縦=太さの倍率）。1を少し超えると「ドンッ」と膨らんでから落ち着く。")]
        [SkillLabel("撃ち始めの太さの変化")]
        public AnimationCurve openCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 4f), new Keyframe(0.55f, 1.18f), new Keyframe(1f, 1f));

        [Tooltip("撃ち終わりの太さの変化（横=経過0〜1、縦=太さの倍率）。")]
        [SkillLabel("撃ち終わりの太さの変化")]
        public AnimationCurve closeCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.25f, 1.08f), new Keyframe(1f, 0f));

        [Tooltip("ONなら、撃っている途中で握るのをやめるとすぐ撃ち終わる。OFFなら最後まで撃つ。")]
        [SkillLabel("離したらすぐ終わる")]
        public bool endWhenGripReleased;

        [Tooltip("スキル中の砲台の回る速さの倍率（0で止まる、1で普段どおり）。")]
        [SkillLabel("スキル中の回転速度の倍率")]
        [Min(0f)]
        public float rotationSpeedScale = 0.3f;
    }

    [Serializable]
    public sealed class RedLaserShape
    {
        [Tooltip("レーザーが届く最大の距離（メートル）。普段のレーザーは 20。")]
        [SkillLabel("届く距離（m）")]
        [Min(1f)]
        public float range = 28f;

        [Tooltip("根元（発射口）の太さ（メートル）。普段のレーザーは最大 0.6。")]
        [SkillLabel("根元の太さ（m）")]
        [Min(0f)]
        public float rootWidth = 0.8f;

        [Tooltip("先の太さ（メートル）。根元からここまで広がっていく。")]
        [SkillLabel("先の太さ（m）")]
        [Min(0f)]
        public float tipWidth = 5.5f;

        [Tooltip("根元からこの距離（メートル）で「先の太さ」になる。それより先は同じ太さ。")]
        [SkillLabel("広がりきる距離（m）")]
        [Min(0.1f)]
        public float spreadDistance = 14f;

        [Tooltip("広がり方（横=根元0〜広がりきる距離1、縦=根元の太さ0〜先の太さ1）。")]
        [SkillLabel("広がり方")]
        public AnimationCurve spreadCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));

        [Tooltip("レーザーの先端が伸びる速さ（メートル/秒）。大きいほど一瞬で届く。")]
        [SkillLabel("先端が伸びる速さ（m/秒）")]
        [Min(1f)]
        public float extendSpeed = 90f;

        [Tooltip("撃っている間、太さが脈打つ大きさ（0〜1）。")]
        [SkillLabel("太さの脈動の大きさ")]
        [Range(0f, 0.5f)]
        public float pulseAmount = 0.06f;

        [Tooltip("太さが脈打つ速さ（回/秒）。")]
        [SkillLabel("太さの脈動の速さ（回/秒）")]
        [Min(0f)]
        public float pulseFrequency = 14f;

        [Tooltip("見た目の太さ ÷ 当たり判定の太さ。1より大きいと、まわりのにじむ光の分だけ見た目が太くなる。")]
        [SkillLabel("見た目の太さの倍率")]
        [Min(0.1f)]
        public float visualWidthScale = 1.35f;

        [Tooltip("見た目の線を長さ方向に分ける数。多いほど広がりがなめらか。")]
        [SkillLabel("見た目の細かさ")]
        [Range(4, 128)]
        public int visualSegments = 40;
    }

    [Serializable]
    public sealed class RedLaserDamage
    {
        [Tooltip("群衆の敵に与えるダメージ（毎秒）。普段のレーザーは最大 20。敵の体力は 10（ボスは 150）。")]
        [SkillLabel("群衆の敵へのダメージ（毎秒）")]
        [Min(0f)]
        public float swarmDamagePerSecond = 100f;

        [Tooltip("破壊できる物（壁の箱・スマッシュボール・Actorの敵など）に与えるダメージ（毎秒）。")]
        [SkillLabel("破壊できる物へのダメージ（毎秒）")]
        [Min(0f)]
        public float obstacleDamagePerSecond = 60f;

        [Tooltip("ONなら、破壊できる物へのダメージに砲台のチューニングの「障害物ダメージ倍率」も掛ける（普段のレーザーと同じ扱い）。")]
        [SkillLabel("砲台の障害物ダメージ倍率も掛ける")]
        public bool useTurretObstacleMultiplier = true;

        [Tooltip("当たり判定の太さの倍率（見た目の太さとは別）。1で見た目の芯と同じ太さ。")]
        [SkillLabel("当たり判定の太さの倍率")]
        [Min(0f)]
        public float hitWidthScale = 1f;

        [Tooltip("群衆の敵の当たり判定を、長さ方向に分ける数。多いほど広がる形に正確だが、継ぎ目で少し多めに当たる（群衆の判定が丸い端の棒の形のため）。")]
        [SkillLabel("群衆の当たり判定の分割数（長さ）")]
        [Range(1, 6)]
        public int hitSegments = 3;

        [Tooltip("群衆の敵の当たり判定を、横に分ける列の数。列ごとに壁で止まる長さが変わる（一部だけ壁に当たったとき、当たっていない列は先まで届く）。列×長さの数がレーザー1本ぶんの判定数になる（全員合計で1フレーム32本まで）。")]
        [SkillLabel("群衆の当たり判定の列の数（横）")]
        [Range(1, 6)]
        public int hitColumns = 3;

        [Tooltip("止まる判定のために、レーザーを横に分ける筋の数。筋ごとに壁で止まるので、レーザーの一部だけが壁に当たったら、その部分だけが止まる。多いほど壁のかどに沿ってきれいに切れるが、判定が重くなる（筋の数+1 本 × 4 回の判定）。")]
        [SkillLabel("横に分ける筋の数（止まる判定）")]
        [Range(4, 48)]
        public int laneCount = 20;

        [Tooltip("レーザーを止める物のレイヤー。通常は全部（トリガーは止めない）。")]
        [SkillLabel("止める物のレイヤー")]
        public LayerMask blockerLayers = ~0;


        [Tooltip("ONなら、ダメージを与える物（破壊できる物）も止まる対象にする。OFFなら破壊できる物は貫通して壊していく。Actorの敵（1体ずつの敵）では止まらず、貫通してダメージを与える。")]
        [SkillLabel("破壊できる物でも止まる")]
        public bool stopAtDestructibles = true;
    }

    [Serializable]
    public sealed class RedLaserLook
    {
        [Tooltip("斜めのしま模様の色（左→右が しま1周ぶん。右端の次は左端につながる）。キーを2つずつ近くに置くと、色の帯の間がなめらかなグラデーションになる。")]
        [SkillLabel("しま模様の色（1周ぶん）")]
        public Gradient stripeGradient = MakeGradient(
            new[]
            {
                new GradientColorKey(new Color(0.95f, 0.12f, 0.16f), 0f),
                new GradientColorKey(new Color(0.95f, 0.12f, 0.16f), 0.17f),
                new GradientColorKey(new Color(1f, 0.84f, 0.86f), 0.27f),
                new GradientColorKey(new Color(1f, 0.84f, 0.86f), 0.42f),
                new GradientColorKey(new Color(0.58f, 0.02f, 0.07f), 0.52f),
                new GradientColorKey(new Color(0.58f, 0.02f, 0.07f), 0.67f),
                new GradientColorKey(new Color(1f, 0.88f, 0.89f), 0.77f),
                new GradientColorKey(new Color(1f, 0.88f, 0.89f), 0.92f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

        [Tooltip("しまの色どうしをなじませる強さ（0〜1）。0 で色の帯がくっきり、大きいほど帯と帯の間が広いグラデーションになる。1 でほぼ全部がグラデーション。")]
        [SkillLabel("しまのグラデーションの広さ")]
        [Range(0f, 1f)]
        public float stripeBlend = 0.65f;

        [Tooltip("しま模様の濃さ（0でしまなし＝下の断面の色だけ、1でしまの色そのまま）。")]
        [SkillLabel("しま模様の濃さ")]
        [Range(0f, 1f)]
        public float stripeStrength = 1f;

        [Tooltip("しま模様1周ぶん（上の色を全部たどる）の長さ（メートル）。大きいほど太いしまになる。")]
        [SkillLabel("しま1周の長さ（m）")]
        [Min(0.05f)]
        public float stripePeriod = 3.2f;

        [Tooltip("しまの傾き（度）。0でレーザーに直角の輪切り、大きいほど斜めに寝る。")]
        [SkillLabel("しまの傾き（度）")]
        [Range(-80f, 80f)]
        public float stripeAngle = 40f;

        [Tooltip("しまが先へ流れる速さ（メートル/秒）。0で止まる、マイナスで根元へ戻る。")]
        [SkillLabel("しまが流れる速さ（m/秒）")]
        public float stripeScrollSpeed = 7f;

        [Tooltip("レーザーの真ん中に乗せる白いツヤの強さ（筒のような立体感）。0でなし。")]
        [SkillLabel("真ん中のツヤ")]
        [Range(0f, 1f)]
        public float coreShine = 0.25f;

        [Tooltip("壁に当たって止まっている部分の、壁ぎわを明るく光らせる強さ（ぶつかって押し返されている感じ）。0でなし。")]
        [SkillLabel("壁ぎわの光")]
        [Range(0f, 3f)]
        public float wallGlow = 1.2f;

        [Tooltip("レーザーの断面の色（左=中心、右=外側のふち）。しま模様の色に掛け合わされる。中心は白くしてしまの色を見せ、外へ行くほど少しずつ赤くして、ふちは濃い赤の光のにじみにする。アルファは濃さ。")]
        [SkillLabel("断面の色（中心→ふち）")]
        [GradientUsage(true)]
        public Gradient crossGradient = MakeGradient(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                new GradientColorKey(new Color(1f, 0.9f, 0.9f), 0.3f),
                new GradientColorKey(new Color(1f, 0.68f, 0.66f), 0.55f),
                new GradientColorKey(new Color(1f, 0.38f, 0.34f), 0.72f),
                new GradientColorKey(new Color(0.95f, 0.08f, 0.1f), 0.88f),
                new GradientColorKey(new Color(0.55f, 0f, 0.04f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(0.55f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            });

        [Tooltip("レーザーの長さ方向の色（左=根元、右=先端）。断面の色に掛け合わされる。")]
        [SkillLabel("長さ方向の色（根元→先端）")]
        [GradientUsage(true)]
        public Gradient lengthGradient = MakeGradient(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                new GradientColorKey(new Color(1f, 0.88f, 0.86f), 0.45f),
                new GradientColorKey(new Color(1f, 0.66f, 0.62f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

        [Tooltip("全体の明るさ。ポストエフェクトの光のにじみ（Bloom）がある場合は 1 より大きいほど強く光る。")]
        [SkillLabel("明るさ")]
        [Min(0f)]
        public float intensity = 1.15f;

        [Tooltip("明るい背景でも見えるように、レーザーの濃い部分で後ろを隠す強さ（0=光を足すだけ、1=絵の具のように塗る）。")]
        [SkillLabel("塗りの強さ（明るい背景用）")]
        [Range(0f, 1f)]
        public float bodyOpacity = 0.85f;

        [Tooltip("火花・光の粒の明るさ（1 より大きいと強く光る）。")]
        [SkillLabel("火花・粒の明るさ")]
        [Min(0f)]
        public float particleBrightness = 2.5f;

        [Tooltip("流れるスジ模様の濃さ（0でスジなし）。")]
        [SkillLabel("スジの濃さ")]
        [Range(0f, 1f)]
        public float streakStrength = 0.2f;

        [Tooltip("スジの本数（断面あたり）。")]
        [SkillLabel("スジの本数")]
        [Min(0f)]
        public float streakDensity = 10f;

        [Tooltip("スジが先へ流れる速さ。")]
        [SkillLabel("スジが流れる速さ")]
        public float streakSpeed = 16f;

        [Tooltip("ふちのギザギザ（エネルギーが荒れる感じ）の大きさ（0〜1）。")]
        [SkillLabel("ふちのギザギザ")]
        [Range(0f, 1f)]
        public float edgeJag = 0.2f;

        [Tooltip("ふちのギザギザの細かさ。")]
        [SkillLabel("ギザギザの細かさ")]
        [Min(0f)]
        public float edgeJagFrequency = 1.4f;

        [Tooltip("ふちのギザギザが動く速さ。")]
        [SkillLabel("ギザギザの速さ")]
        public float edgeJagSpeed = 10f;

        [Tooltip("明るさのちらつき（0〜1）。")]
        [SkillLabel("ちらつき")]
        [Range(0f, 1f)]
        public float flicker = 0.12f;

        [Tooltip("何にも当たらないとき、先端をぼかして消す長さ（メートル）。")]
        [SkillLabel("先端のぼかし（m）")]
        [Min(0f)]
        public float tipFadeLength = 4f;

        [Tooltip("描く順番（大きいほど手前）。敵や背景の絵より大きくしておく。")]
        [SkillLabel("描く順番")]
        public int sortingOrder = 150;

        [Tooltip("断面・長さのグラデーションを画像に焼き直す細かさ。")]
        [SkillLabel("グラデーションの細かさ")]
        [Range(16, 512)]
        public int gradientResolution = 128;

        private static Gradient MakeGradient(GradientColorKey[] colors, GradientAlphaKey[] alphas)
        {
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            return gradient;
        }
    }

    [Serializable]
    public sealed class RedLaserChargeFx
    {
        [Tooltip("溜めの光（発射口の光・使えるようになった瞬間の輪）の色（HDR: 1 より大きいと強く光る）。")]
        [SkillLabel("溜めの光の色")]
        [ColorUsage(true, true)]
        [FormerlySerializedAs("circleColor")]
        public Color glowColor = new Color(1.6f, 0.18f, 0.14f, 1f);

        [Tooltip("明るさが脈打つ速さ（回/秒）。")]
        [SkillLabel("明るさの脈の速さ（回/秒）")]
        [Min(0f)]
        public float pulseFrequency = 1.6f;

        [Tooltip("明るさが脈打つ大きさ（0〜1）。")]
        [SkillLabel("明るさの脈の大きさ")]
        [Range(0f, 1f)]
        public float pulseAmount = 0.35f;

        [Tooltip("発射口に灯る光の大きさ（メートル）。")]
        [SkillLabel("発射口の光の大きさ（m）")]
        [Min(0f)]
        public float muzzleGlowSize = 1.8f;

        [Tooltip("発射口へ吸い込まれていく光の粒の数（毎秒）。")]
        [SkillLabel("吸い込まれる粒の数（毎秒）")]
        [Min(0f)]
        public float emberRate = 40f;

        [Tooltip("吸い込まれる光の粒の色。")]
        [SkillLabel("吸い込まれる粒の色")]
        public Color emberColor = new Color(1f, 0.3f, 0.24f, 1f);

        [Tooltip("光の粒が湧く距離（発射口からの半径、メートル）。")]
        [SkillLabel("粒が湧く距離（m）")]
        [Min(0f)]
        public float emberRadius = 2.6f;

        [Tooltip("使えるようになった瞬間に広がる輪の大きさ（メートル）。0で出さない。")]
        [SkillLabel("使えるようになった瞬間の輪（m）")]
        [Min(0f)]
        public float readyBurstSize = 6f;

        [Tooltip("撃つ前の「ため」で光の粒が増える倍率。")]
        [SkillLabel("ため中の粒の倍率")]
        [Min(0f)]
        public float windupEmberMultiplier = 5f;
    }

    [Serializable]
    public sealed class RedLaserTubeFx
    {
        [Tooltip("ONなら、スキルを使える間・撃っている間、砲台の上に歯磨き粉を出し、レーザーをその口から出す。OFFなら砲台の発射口から出す。")]
        [SkillLabel("歯磨き粉を出す")]
        public bool enabled = true;

        [Tooltip("歯磨き粉の絵（口が上を向いた絵）。空ならコードで描いた仮の絵（赤いチューブ）を使う。差し替えるときは絵を入れるだけでよい。")]
        [SkillLabel("歯磨き粉の絵（口が上向き）")]
        public Sprite sprite;

        [Tooltip("絵に掛ける色（白でそのまま）。")]
        [SkillLabel("絵の色")]
        public Color tint = Color.white;

        [Tooltip("歯磨き粉の長さ（メートル）。")]
        [SkillLabel("長さ（m）")]
        [Min(0.1f)]
        public float length = 3.4f;

        [Tooltip("歯磨き粉の横幅（メートル）。")]
        [SkillLabel("横幅（m）")]
        [Min(0.05f)]
        public float width = 1f;

        [Tooltip("砲台の中心から、砲台の向きへどれだけずらして置くか（メートル）。大きいほど口が前に出る。")]
        [SkillLabel("前へのずらし（m）")]
        public float forwardOffset = 0f;

        [Tooltip("砲台の向きに対して横へずらす量（メートル。プラスで右）。")]
        [SkillLabel("横へのずらし（m）")]
        public float sideOffset = 0f;

        [Tooltip("描く順番。砲台の絵（このシーンでは 2）より大きく、レーザー（150）より小さくする。")]
        [SkillLabel("描く順番")]
        public int sortingOrder = 140;

        [Tooltip("使えるようになったとき「ポンッ」と出てくる時間（秒）。")]
        [SkillLabel("出てくる時間（秒）")]
        [Min(0.01f)]
        public float appearSeconds = 0.3f;

        [Tooltip("撃ち終わって消えるまでの時間（秒）。")]
        [SkillLabel("消える時間（秒）")]
        [Min(0.01f)]
        public float fadeOutSeconds = 0.35f;

        [Tooltip("ブルブル震える大きさ（メートル）。左右に行ったり来たりする幅。")]
        [SkillLabel("震えの大きさ（m）")]
        [Min(0f)]
        public float shakeAmount = 0.04f;

        [Tooltip("ブルブル震える角度（度）。")]
        [SkillLabel("震えの角度（度）")]
        [Min(0f)]
        public float shakeAngle = 1.5f;

        [Tooltip("1秒に左右へ行ったり来たりする回数。画面の更新（60回/秒など）より大きいと、毎フレーム反対へ動く一番速い震えになる。")]
        [SkillLabel("震えの速さ（回/秒）")]
        [Min(0f)]
        public float shakeFrequency = 40f;

        [Tooltip("踏ん張って押しつぶされている強さ（0〜0.3）。いつも少し短く太くなって、力んでいるように見える。")]
        [SkillLabel("踏ん張りの強さ")]
        [Range(0f, 0.3f)]
        public float strainAmount = 0.06f;

        [Tooltip("「ぐっ」と強く力む波の回数（回/秒）。力むたびに、もっと押しつぶされて震えも強くなる。0で波なし。")]
        [SkillLabel("力みの波（回/秒）")]
        [Min(0f)]
        public float strainSurgeFrequency = 1.3f;

        [Tooltip("ため中・撃っている間に、震えが強くなる倍率。")]
        [SkillLabel("ため・発射中の震えの倍率")]
        [Min(1f)]
        public float windupShakeMultiplier = 2.5f;

        [Tooltip("中身がパンパンで、ふくらんだりしぼんだりする大きさ（0〜0.3）。")]
        [SkillLabel("ふくらみの大きさ")]
        [Range(0f, 0.3f)]
        public float pulseAmount = 0.06f;

        [Tooltip("ふくらんだりしぼんだりする速さ（回/秒）。")]
        [SkillLabel("ふくらみの速さ（回/秒）")]
        [Min(0f)]
        public float pulseFrequency = 2.5f;

        [Header("しぼり方（タイトルの冒頭と同じ。ためで握り始め、撃っている間にくびれて中身が出ていく）")]
        [Tooltip("真ん中のくびれの深さ（0〜0.9）。撃っている間に徐々に深くなり、撃ち終わりでここまでくびれる。")]
        [SkillLabel("くびれの深さ")]
        [Range(0f, 0.9f)]
        public float pinchAmount = 0.55f;

        [Tooltip("くびれる高さ（0 おしり 〜 1 口）。")]
        [SkillLabel("くびれる高さ")]
        [Range(0f, 1f)]
        public float pinchHeight = 0.45f;

        [Tooltip("くびれの幅（縦の長さに対する割合）。小さいほど細くキュッとくびれる。")]
        [SkillLabel("くびれの幅")]
        [Range(0.02f, 0.5f)]
        public float pinchWidth = 0.09f;

        [Tooltip("握られて、くびれ以外がふくらむ大きさ（横幅に対する割合）。")]
        [SkillLabel("ふくらむ大きさ")]
        [Range(0f, 1f)]
        public float swellAmount = 0.35f;

        [Tooltip("撃ち終わるころに、中身が出てやせる大きさ（横幅に対する割合）。")]
        [SkillLabel("やせる大きさ")]
        [Range(0f, 0.6f)]
        public float deflateAmount = 0.2f;

        [Tooltip("撃っている間に、チューブ全体が左右から細くなる割合（0〜0.8。撃ち終わりでここまで細くなる）。おしりの帯・キャップはこの3割だけ細くなる。")]
        [SkillLabel("撃つ間に横から縮む割合")]
        [Range(0f, 0.8f)]
        public float sideShrinkWhileFiring = 0.4f;

        [Tooltip("ため（撃つ前）の間に握る強さ（撃っている間の最大を1として）。")]
        [SkillLabel("ためで握る強さ")]
        [Range(0f, 1f)]
        public float windupGrip = 0.15f;

        [Tooltip("ふくらむ範囲（0 おしり 〜 1 口）。おしりの帯とキャップはふくらまないようにする。")]
        [SkillLabel("ふくらむ範囲（下〜上）")]
        public Vector2 swellRange = new Vector2(0.07f, 0.84f);

        [Tooltip("絵を横に細長く切る帯の数。多いほどなめらかにくびれる。")]
        [SkillLabel("しぼりの帯の数")]
        [Range(4, 96)]
        public int sliceCount = 40;

        [Tooltip("撃ち始めの反動で後ろへ下がる距離（メートル）。")]
        [SkillLabel("撃った反動（m）")]
        [Min(0f)]
        public float recoilDistance = 0.35f;

        [Tooltip("使える間、口からにじみ出る歯磨き粉の粒の数（毎秒）。")]
        [SkillLabel("口からにじむ粒（毎秒）")]
        [Min(0f)]
        public float dripRate = 10f;

        [Tooltip("口からにじむ粒の色。")]
        [SkillLabel("にじむ粒の色")]
        public Color dripColor = new Color(1f, 0.82f, 0.86f, 1f);

        [Tooltip("使える間、チューブの表面を走る稲妻の数（毎秒）。0で出さない（色・太さは「ビリビリ（稲妻）」の設定）。")]
        [SkillLabel("チューブを走る稲妻（毎秒）")]
        [Min(0f)]
        public float lightningRate = 12f;

        [Tooltip("使える間、まわりから歯磨き粉に落ちてくる太い雷の間隔（秒、最短）。0で出さない。")]
        [SkillLabel("雷の間隔（最短・秒）")]
        [Min(0f)]
        public float thunderIntervalMin = 0.35f;

        [Tooltip("雷の間隔（秒、最長）。最短〜最長の間でランダム。ため中は3倍の速さで落ちる。")]
        [SkillLabel("雷の間隔（最長・秒）")]
        [Min(0f)]
        public float thunderIntervalMax = 0.9f;

        [Tooltip("雷がどれだけ遠くから落ちてくるか（メートル）。")]
        [SkillLabel("雷の長さ（m）")]
        [Min(0.5f)]
        public float thunderLength = 4.5f;

        [Tooltip("雷の太さ（「ビリビリ（稲妻）」の太さに対する倍率）。")]
        [SkillLabel("雷の太さ（倍）")]
        [Min(0.1f)]
        public float thunderWidthScale = 3f;

        [Tooltip("チューブの後ろに出る光の大きさ（チューブの長さに対する倍率）。0で出さない。")]
        [SkillLabel("後ろの光の大きさ（倍）")]
        [Min(0f)]
        public float auraScale = 1.5f;

        [Tooltip("チューブの後ろに出る光の色（HDR）。")]
        [SkillLabel("後ろの光の色")]
        [ColorUsage(true, true)]
        public Color auraColor = new Color(1.4f, 0.12f, 0.1f, 1f);
    }

    [Serializable]
    public sealed class RedLaserLightningFx
    {
        [Tooltip("稲妻の色（明るさは下の「稲妻の明るさ」で決まる）。")]
        [SkillLabel("稲妻の色")]
        public Color color = new Color(1f, 0.16f, 0.12f, 1f);

        [Tooltip("稲妻の明るさ（1 より大きいと強く光る）。")]
        [SkillLabel("稲妻の明るさ")]
        [Min(0f)]
        public float brightness = 3.5f;

        [Tooltip("稲妻の線の太さ（メートル）。")]
        [SkillLabel("稲妻の太さ（m）")]
        [Min(0.005f)]
        public float width = 0.08f;

        [Tooltip("稲妻のギザギザの大きさ（稲妻の長さに対する割合）。")]
        [SkillLabel("ギザギザの大きさ")]
        [Range(0f, 0.6f)]
        public float jitter = 0.22f;

        [Tooltip("稲妻1本が出ている時間（秒）。短いほどパチパチ光る。")]
        [SkillLabel("1本が出ている時間（秒）")]
        [Min(0.02f)]
        public float boltLife = 0.09f;

        [Tooltip("稲妻が途中で枝分かれする確率（0〜1）。")]
        [SkillLabel("枝分かれする確率")]
        [Range(0f, 1f)]
        public float branchChance = 0.35f;

        [Header("溜め（使える状態）")]
        [Tooltip("使える状態の間、発射口のまわりに出る稲妻の数（毎秒）。0で出さない。")]
        [SkillLabel("溜め中の稲妻の数（毎秒）")]
        [Min(0f)]
        public float chargeRate = 22f;

        [Tooltip("溜め中の稲妻が届く範囲（発射口・砲台からの半径、メートル）。")]
        [SkillLabel("溜め中の稲妻の範囲（m）")]
        [Min(0.1f)]
        public float chargeRadius = 2.4f;

        [Tooltip("撃つ前の「ため」で稲妻が増える倍率。ためでは稲妻がまわりから発射口へ吸い込まれる。")]
        [SkillLabel("ため中の稲妻の倍率")]
        [Min(0f)]
        public float windupMultiplier = 3f;

        [Header("撃っている間")]
        [Tooltip("撃っている間、レーザーにまとわりつく稲妻の数（毎秒）。0で出さない。")]
        [SkillLabel("レーザーにまとわりつく稲妻（毎秒）")]
        [Min(0f)]
        public float beamRate = 30f;

        [Tooltip("レーザーにまとわりつく稲妻1本の長さ（メートル）。")]
        [SkillLabel("まとわりつく稲妻の長さ（m）")]
        [Min(0.1f)]
        public float beamBoltLength = 4f;

        [Tooltip("レーザーのふちから外へ飛び出す稲妻の数（毎秒）。0で出さない。")]
        [SkillLabel("ふちから飛び出す稲妻（毎秒）")]
        [Min(0f)]
        public float edgeRate = 24f;

        [Tooltip("壁に当たった場所から飛び散る稲妻の数（毎秒）。0で出さない。")]
        [SkillLabel("当たった場所の稲妻（毎秒）")]
        [Min(0f)]
        public float impactRate = 26f;
    }

    [Serializable]
    public sealed class RedLaserMuzzleFx
    {
        [Tooltip("発射口の光の色（HDR）。")]
        [SkillLabel("発射口の光の色")]
        [ColorUsage(true, true)]
        public Color flareColor = new Color(2.2f, 0.6f, 0.5f, 1f);

        [Tooltip("発射口の光の大きさ（メートル、太さが最大のとき）。")]
        [SkillLabel("発射口の光の大きさ（m）")]
        [Min(0f)]
        public float flareSize = 3.6f;

        [Tooltip("発射口の光のトゲの本数。")]
        [SkillLabel("光のトゲの本数")]
        [Range(0, 32)]
        public int flareRays = 12;

        [Tooltip("撃ち始めの瞬間に広がる衝撃の輪の大きさ（メートル）。0で出さない。")]
        [SkillLabel("撃ち始めの衝撃の輪（m）")]
        [Min(0f)]
        public float fireBurstSize = 9f;

        [Tooltip("発射口から飛び散る火花の数（毎秒）。")]
        [SkillLabel("火花の数（毎秒）")]
        [Min(0f)]
        public float sparkRate = 120f;

        [Tooltip("火花の速さ（メートル/秒）。")]
        [SkillLabel("火花の速さ（m/秒）")]
        [Min(0f)]
        public float sparkSpeed = 16f;

        [Tooltip("火花が飛ぶ範囲の角度（レーザーの向きから左右に何度まで）。")]
        [SkillLabel("火花が飛ぶ角度（度）")]
        [Range(0f, 180f)]
        public float sparkSpreadAngle = 70f;

        [Tooltip("レーザーのまわりから外へ吹き出すトゲ状の光の数（毎秒）。エネルギーが荒れ狂う感じを出す。")]
        [SkillLabel("トゲ状の光の数（毎秒）")]
        [Min(0f)]
        public float shardRate = 70f;

        [Tooltip("トゲ状の光が出る範囲（根元からの距離、メートル）。")]
        [SkillLabel("トゲ状の光が出る範囲（m）")]
        [Min(0f)]
        public float shardReach = 10f;

        [Tooltip("レーザーに沿って舞う光の粒の数（毎秒）。")]
        [SkillLabel("レーザーに沿って舞う粒（毎秒）")]
        [Min(0f)]
        public float emberRate = 60f;

        [Tooltip("レーザーのすぐ外側を、レーザーと同じ向きに走る細い光の線の数（毎秒）。スピード感が出る。0で出さない。")]
        [SkillLabel("レーザーに沿って走る光の線（毎秒）")]
        [Min(0f)]
        public float speedLineRate = 70f;

        [Tooltip("レーザーに沿って走る光の線の速さ（メートル/秒）。速いほど長い線になる。")]
        [SkillLabel("走る光の線の速さ（m/秒）")]
        [Min(0f)]
        public float speedLineSpeed = 45f;

        [Tooltip("火花・光の粒の色（明るさは「レーザーの色」の「火花・粒の明るさ」で決まる）。")]
        [SkillLabel("火花・粒の色")]
        public Color particleColor = new Color(1f, 0.48f, 0.42f, 1f);

        [Tooltip("トゲ状の光の色。")]
        [SkillLabel("トゲ状の光の色")]
        public Color shardColor = new Color(1f, 0.14f, 0.12f, 1f);
    }

    [Serializable]
    public sealed class RedLaserImpactFx
    {
        [Tooltip("当たった場所の光の色（HDR）。")]
        [SkillLabel("当たった場所の光の色")]
        [ColorUsage(true, true)]
        public Color flareColor = new Color(2.6f, 0.95f, 0.8f, 1f);

        [Tooltip("当たった場所の光の大きさ（レーザーの太さに対する倍率）。")]
        [SkillLabel("当たった場所の光の大きさ（倍）")]
        [Min(0f)]
        public float flareScale = 1.3f;

        [Tooltip("壁に沿って横に広がる光の長さ（レーザーの太さに対する倍率）。ぶつかって押し広がる感じを出す。")]
        [SkillLabel("壁に沿って広がる光の長さ（倍）")]
        [Min(0f)]
        public float splashScale = 1.7f;

        [Tooltip("当たった場所から飛び散る火花の数（毎秒）。")]
        [SkillLabel("火花の数（毎秒）")]
        [Min(0f)]
        public float sparkRate = 220f;

        [Tooltip("当たった場所の火花の速さ（メートル/秒）。")]
        [SkillLabel("火花の速さ（m/秒）")]
        [Min(0f)]
        public float sparkSpeed = 18f;

        [Tooltip("当たった場所から出る衝撃の輪の間隔（秒）。0で出さない。")]
        [SkillLabel("衝撃の輪の間隔（秒）")]
        [Min(0f)]
        public float shockwaveInterval = 0.2f;

        [Tooltip("衝撃の輪の大きさ（レーザーの太さに対する倍率）。")]
        [SkillLabel("衝撃の輪の大きさ（倍）")]
        [Min(0f)]
        public float shockwaveScale = 1.6f;

        [Tooltip("当たった場所の煙っぽい光の粒の数（毎秒）。")]
        [SkillLabel("煙っぽい粒の数（毎秒）")]
        [Min(0f)]
        public float emberRate = 50f;

        [Tooltip("当たった場所の火花の色。")]
        [SkillLabel("火花の色")]
        public Color sparkColor = new Color(1f, 0.72f, 0.66f, 1f);
    }

    [Serializable]
    public sealed class RedLaserFlashFx
    {
        [Tooltip("ONなら、レーザーまわりの演出（外側の光・進む輪・壁に沿って広がる光・トゲ状の光・光の粒・ふちの稲妻・走る光の線）を、"
                 + "レーザーと同じ太さの中に収める（横にはみ出さない）。OFFなら、それぞれの「太さ（倍）」の設定どおり外へ広がる。")]
        [SkillLabel("演出をレーザーと同じ太さに収める")]
        public bool keepInsideLaser = true;

        [Header("光の波（レーザーの中を先へ走る明るい帯）")]
        [Tooltip("光の波の明るさ（0で出さない）。")]
        [SkillLabel("光の波の明るさ")]
        [Min(0f)]
        public float pulseStrength = 0.9f;

        [Tooltip("光の波と波の間隔（メートル）。")]
        [SkillLabel("光の波の間隔（m）")]
        [Min(0.2f)]
        public float pulseSpacing = 5f;

        [Tooltip("光の波が先へ走る速さ（メートル/秒）。")]
        [SkillLabel("光の波の速さ（m/秒）")]
        public float pulseSpeed = 45f;

        [Tooltip("光の波の太さ（間隔に対する割合）。")]
        [SkillLabel("光の波の太さ")]
        [Range(0.02f, 1f)]
        public float pulseWidth = 0.25f;

        [Header("外側の光（レーザーのまわりの大きなにじみ）")]
        [Tooltip("ONなら、レーザーのまわりに大きくにじむ光を出す（壁で止まった所では一緒に止まる）。")]
        [SkillLabel("外側の光を出す")]
        public bool haloEnabled = true;

        [Tooltip("ONなら、外側の光をレーザーの上に重ねて描く（レーザー全体が光に包まれる）。OFFならレーザーの後ろに描く（横にはみ出した所だけ見える）。")]
        [SkillLabel("外側の光をレーザーの上に重ねる")]
        public bool haloOnTop = true;

        [Tooltip("外側の光の太さ（レーザーの見た目の太さに対する倍率）。")]
        [SkillLabel("外側の光の太さ（倍）")]
        [Min(1f)]
        public float haloWidthScale = 2f;

        [Tooltip("外側の光の色。")]
        [SkillLabel("外側の光の色")]
        public Color haloColor = new Color(1f, 0.22f, 0.28f, 1f);

        [Tooltip("外側の光の明るさ。")]
        [SkillLabel("外側の光の明るさ")]
        [Min(0f)]
        public float haloIntensity = 1.5f;

        [Header("レーザーを進む輪（発射口から先へ飛んでいく光の輪）")]
        [Tooltip("輪を出す間隔（秒）。0で出さない。")]
        [SkillLabel("輪を出す間隔（秒）")]
        [Min(0f)]
        public float beamRingInterval = 0.16f;

        [Tooltip("輪が先へ進む速さ（メートル/秒）。")]
        [SkillLabel("輪の速さ（m/秒）")]
        [Min(1f)]
        public float beamRingSpeed = 40f;

        [Tooltip("輪の厚み（レーザーの向きの長さ、メートル）。")]
        [SkillLabel("輪の厚み（m）")]
        [Min(0.05f)]
        public float beamRingThickness = 0.9f;

        [Tooltip("輪の色（HDR）。")]
        [SkillLabel("輪の色")]
        [ColorUsage(true, true)]
        public Color beamRingColor = new Color(2.2f, 0.6f, 0.7f, 1f);

        [Header("発射口の二重の光")]
        [Tooltip("発射口の光の後ろに、逆向きに回る大きな光を重ねる（発射口の光に対する大きさの倍率）。0で出さない。")]
        [SkillLabel("二重の光の大きさ（倍）")]
        [Min(0f)]
        public float muzzleFlare2Scale = 1.9f;

        [Tooltip("二重の光のトゲの本数。")]
        [SkillLabel("二重の光のトゲの本数")]
        [Range(0, 32)]
        public int muzzleFlare2Rays = 6;
    }

    [Serializable]
    public sealed class RedLaserCameraShake
    {
        [Tooltip("ONなら、撃った本人の画面だけ揺らす（OFFなら全員の画面が揺れる）。")]
        [SkillLabel("撃った本人の画面だけ揺らす")]
        public bool ownerViewOnly;

        [Tooltip("撃ち始めの揺れ。")]
        [SkillLabel("撃ち始めの揺れ")]
        public SimpleShakeSettings fire = new SimpleShakeSettings { amplitude = 0.35f, rollAmplitude = 0.8f, frequency = 22f, duration = 0.5f };

        [Tooltip("撃ち始めの揺れの強さ（0で揺らさない）。")]
        [SkillLabel("撃ち始めの揺れの強さ")]
        [Min(0f)]
        public float fireStrength = 1f;

        [Tooltip("壁に当たっている間、間隔をあけて揺らす弱い揺れ。")]
        [SkillLabel("壁に当たっている間の揺れ")]
        public SimpleShakeSettings impact = new SimpleShakeSettings { amplitude = 0.1f, rollAmplitude = 0.15f, frequency = 26f, duration = 0.18f };

        [Tooltip("壁に当たっている間の揺れの強さ（0で揺らさない）。")]
        [SkillLabel("当たっている間の揺れの強さ")]
        [Min(0f)]
        public float impactStrength = 1f;

        [Tooltip("撃っている間、ずっと画面を揺らす揺れ（ゴゴゴ…）。揺れ幅・回転の揺れ幅を大きくするほど激しく揺れる。")]
        [SkillLabel("撃っている間の揺れ")]
        public SimpleShakeSettings whileFiring = new SimpleShakeSettings { amplitude = 0.22f, rollAmplitude = 0.7f, frequency = 32f, duration = 0.16f };

        [Tooltip("撃っている間の揺れの強さ（0で揺らさない）。")]
        [SkillLabel("撃っている間の揺れの強さ")]
        [Min(0f)]
        public float whileFiringStrength = 1f;

        [Tooltip("撃っている間の揺れの間隔（秒）。")]
        [SkillLabel("撃っている間の揺れの間隔（秒）")]
        [Min(0.02f)]
        public float whileFiringInterval = 0.1f;

        [Tooltip("壁に当たっている間の揺れの間隔（秒）。")]
        [SkillLabel("当たっている間の揺れの間隔（秒）")]
        [Min(0.02f)]
        public float impactInterval = 0.15f;
    }

    [Serializable]
    public sealed class RedLaserSounds
    {
        [Tooltip("使えるようになった瞬間（砲台の位置）。")]
        [SkillLabel("使えるようになった瞬間")]
        public FortressEffect onReady = new FortressEffect();

        [Tooltip("撃つ前の「ため」が始まった瞬間（発射口）。")]
        [SkillLabel("ため開始")]
        public FortressEffect onWindup = new FortressEffect();

        [Tooltip("撃ち始めた瞬間（発射口）。")]
        [SkillLabel("撃ち始め")]
        public FortressEffect onFire = new FortressEffect();

        [Tooltip("撃っている間ずっと（発射口についていく。VFX は Loop にしておく）。")]
        [SkillLabel("撃っている間（ループ）")]
        public FortressEffect whileFiring = new FortressEffect();

        [Tooltip("壁・障害物に当たり始めた瞬間（当たった場所）。")]
        [SkillLabel("壁に当たり始め")]
        public FortressEffect onImpact = new FortressEffect();

        [Tooltip("撃ち終わった瞬間（発射口）。")]
        [SkillLabel("撃ち終わり")]
        public FortressEffect onEnd = new FortressEffect();
    }

    [Serializable]
    public sealed class RedLaserShaders
    {
        [Tooltip("レーザー本体のシェーダー（MS2026/RedSkill/Beam）。")]
        [SkillLabel("レーザー本体")]
        public Shader beam;

        [Tooltip("光・衝撃の輪のシェーダー（MS2026/RedSkill/Radial）。")]
        [SkillLabel("光・輪")]
        public Shader radial;

        [Tooltip("火花・光の粒のシェーダー（MS2026/RedSkill/Particle）。")]
        [SkillLabel("火花・粒")]
        public Shader particle;
    }
}
