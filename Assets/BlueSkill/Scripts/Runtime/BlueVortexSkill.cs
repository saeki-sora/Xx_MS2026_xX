using System;
using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using UnityEngine;

namespace MS2026.Skills.Blue
{
    /// <summary>
    /// 青キャラのスキル「うがい薬の渦」の設定ファイル。値はすべてここで調整できる（Play 中に変えてもすぐ反映される）。
    /// 使える状態の間は砲台の上に青いうがい薬のボトルを持ち、次の攻撃（握った瞬間）でそれを向いている方向へ投げる。
    /// ボトルが落ちた場所で破裂し、水の渦（デバフエリア）を一定時間広げる。渦の中の敵はその場にとどまり（足止め）、
    /// 渦に沿ってぐるぐる回され、受けるダメージが増え、一定間隔で少しずつダメージを受ける。
    /// 実際に動かすのは <see cref="BlueVortexSkillRunner"/>（スキル欄が自動で付ける）と、落ちた場所に出る <see cref="BlueVortexArea"/>。
    /// スキル欄・配る係・キャラとの対応表は赤スキルと共通（Assets/RedSkill/Scripts/Runtime/Core）。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/スキル/ブルー：うがい薬の渦", fileName = "BlueVortexSkill")]
    public sealed class BlueVortexSkill : CharacterSkill
    {
        [Tooltip("振りかぶってから投げて、落ちるまでの流れ。")]
        [SkillLabel("タイミング（投げる）")]
        public BlueThrowTiming timing = new BlueThrowTiming();

        [Tooltip("ボトルが落ちる場所（＝渦を広げる場所）。")]
        [SkillLabel("落ちる場所（渦を広げる場所）")]
        public BlueThrowTarget target = new BlueThrowTarget();

        [Tooltip("ボトルを何本持つか・どう投げるか（1本だけ／まとめて一気に／1本ずつ順番に）。「1本だけ」にすると元の動きに戻る。")]
        [SkillLabel("投げ方（1本・まとめて・1本ずつ）")]
        public BlueMultiThrow multi = new BlueMultiThrow();

        [Tooltip("渦（デバフエリア）の広さと時間。")]
        [SkillLabel("渦の広さ・時間")]
        public BlueVortexAreaSettings area = new BlueVortexAreaSettings();

        [Tooltip("渦の中の敵にかかる効果（足止め・巻き込み・ダメージ）。")]
        [SkillLabel("敵への効果（デバフ）")]
        public BlueVortexDebuff debuff = new BlueVortexDebuff();

        [Tooltip("渦の色・模様・回り方。")]
        [SkillLabel("渦の見た目")]
        public BlueVortexLook look = new BlueVortexLook();

        [Tooltip("使える状態のときに持つ、うがい薬のボトル。")]
        [SkillLabel("うがい薬のボトル")]
        public BlueBottleSettings bottle = new BlueBottleSettings();

        [Tooltip("使える状態（溜め）の間の演出。")]
        [SkillLabel("溜め（使える状態）の演出")]
        public BlueChargeFx charge = new BlueChargeFx();

        [Tooltip("投げている間と、落ちて破裂した瞬間の演出。")]
        [SkillLabel("投げる・破裂の演出")]
        public BlueBurstFx burst = new BlueBurstFx();

        [Tooltip("投げた瞬間・破裂・渦が消えるときの画面の揺れ。")]
        [SkillLabel("カメラの揺れ")]
        public BlueCameraShake cameraShake = new BlueCameraShake();

        [Tooltip("D-Drive の効果音・追加エフェクト（空なら鳴らさない）。")]
        [SkillLabel("効果音・追加エフェクト（D-Drive）")]
        public BlueSkillSounds sounds = new BlueSkillSounds();

        [Tooltip("見た目に使うシェーダー（通常は触らない）。")]
        [SkillLabel("シェーダー（通常は触らない）")]
        public BlueSkillShaders shaders = new BlueSkillShaders();

        public BlueVortexSkill()
        {
            skillId = "blue_vortex";
            themeColor = new Color(0.2f, 0.55f, 1f, 1f);
        }

        public override CharacterSkillRunner CreateRunner(PlayerSkillSlot slot)
        {
            var runner = slot.gameObject.AddComponent<BlueVortexSkillRunner>();
            runner.Configure(this);
            return runner;
        }

        /// <summary>1回の攻撃で投げるボトルの数（「まとめて一気に」のときだけ 2 以上）。</summary>
        public int ThrowCount => multi != null ? multi.ThrowCount : 1;

        /// <summary>1回の溜め（スマッシュボール1回）で投げられる回数（「1本ずつ順番に」のときだけ 2 以上）。</summary>
        public int ThrowsPerCharge => multi != null ? multi.ThrowsPerCharge : 1;

        /// <summary>
        /// その砲台が今投げたときにボトルが落ちる場所（床の上。z は砲台の足元と同じ）を計算する。
        /// 「落ちる場所」の設定（距離・左右のずれ・壁で止めるか）をそのまま使う。目印の表示と実際に投げるときの両方で使う。
        /// まとめて投げるときは真ん中の1本の場所（全部の場所は <see cref="PredictLandings"/>）。
        /// </summary>
        public Vector3 PredictLanding(LaserTurret turret)
        {
            return PredictLanding(turret, 0f, 1f);
        }

        /// <summary>
        /// 1回の攻撃で投げる全部のボトルが落ちる場所を results に入れて、数を返す（左から右の順）。
        /// 「まとめて一気に」以外なら1つだけ（<see cref="PredictLanding(LaserTurret)"/> と同じ場所）。
        /// </summary>
        public int PredictLandings(LaserTurret turret, List<Vector3> results)
        {
            results.Clear();
            var count = ThrowCount;
            var center = (count - 1) * 0.5f;
            for (var i = 0; i < count; i++)
            {
                var offset = i - center;
                var outer = center > 0f ? Mathf.Abs(offset) / center : 0f;
                var distanceScale = Mathf.Lerp(1f, multi.outerDistanceScale, outer);
                results.Add(PredictLanding(turret, offset * multi.spreadAngle, distanceScale));
            }

            return count;
        }

        /// <summary>まとめて一気に投げるときの、1つの渦の大きさの倍率（それ以外は 1）。</summary>
        public float AreaRadiusScale => multi != null && multi.ThrowCount > 1 ? multi.radiusScale : 1f;

        private Vector3 PredictLanding(LaserTurret turret, float angleOffset, float distanceScale)
        {
            if (turret == null)
            {
                return Vector3.zero;
            }

            var aim = turret.AimDirection;
            if (aim.sqrMagnitude < 0.0001f)
            {
                aim = Vector2.up;
            }

            aim.Normalize();
            var right = new Vector2(aim.y, -aim.x);
            // 扇の角度は「＋で右」（左右のずれと同じ向き）。
            var dir = BlueFx.Rotate(aim, -angleOffset);
            var muzzle = turret.MuzzlePosition;
            Vector2 origin = target.measureFromMuzzle ? (Vector2)muzzle : (Vector2)turret.transform.position;
            var distance = Mathf.Max(0f, target.distance) * distanceScale;
            var goal = origin + dir * distance + right * target.sideOffset;

            if (target.stopAtWalls)
            {
                var start = (Vector2)muzzle;
                var delta = goal - start;
                var length = delta.magnitude;
                if (length > 0.01f)
                {
                    var filter = new ContactFilter2D { useTriggers = false };
                    filter.SetLayerMask(target.wallLayers);
                    var hits = WallHits;
                    var count = Physics2D.Linecast(start, goal, filter, hits);
                    var best = 1f;
                    for (var i = 0; i < count; i++)
                    {
                        var hit = hits[i];
                        if (hit.collider == null || hit.collider.transform.IsChildOf(turret.transform))
                        {
                            continue;
                        }

                        if (hit.fraction < best)
                        {
                            best = hit.fraction;
                        }
                    }

                    if (best < 1f)
                    {
                        var stop = Mathf.Max(0f, best * length - target.wallPadding);
                        goal = start + delta / length * stop;
                    }
                }
            }

            return new Vector3(goal.x, goal.y, turret.transform.position.z);
        }

        private static readonly RaycastHit2D[] WallHits = new RaycastHit2D[16];
    }

    /// <summary>ボトルの投げ方。</summary>
    public enum BlueThrowMode
    {
        /// <summary>1本だけ持って、次の攻撃で投げる（元の動き）。</summary>
        [InspectorName("1本だけ（元の動き）")]
        Single,

        /// <summary>何本か扇形に持って、次の攻撃で全部まとめて投げる。</summary>
        [InspectorName("まとめて一気に投げる")]
        AllAtOnce,

        /// <summary>何本か持って、攻撃のたびに1本ずつ投げる（持っている本数が減っていく）。</summary>
        [InspectorName("1本ずつ順番に投げる")]
        OneByOne
    }

    [Serializable]
    public sealed class BlueMultiThrow
    {
        public const int MaxCount = 7;

        [Tooltip("1本だけ（元の動き）／まとめて一気に投げる（扇形に全部）／1本ずつ順番に投げる（攻撃のたびに1本。一度離して握り直すと次の1本）。")]
        [SkillLabel("投げ方")]
        public BlueThrowMode mode = BlueThrowMode.OneByOne;

        [Tooltip("持つボトルの数。「まとめて一気に」なら一度に投げる数、「1本ずつ順番に」なら1回の溜めで投げられる回数。「1本だけ」では使わない。")]
        [SkillLabel("ボトルの数")]
        [Range(1, MaxCount)]
        public int count = 3;

        [Header("まとめて一気に投げるとき")]
        [Tooltip("となり同士のボトルの向きの差（度）。大きいほど横に広がる。")]
        [SkillLabel("広がる角度（度）")]
        [Range(0f, 90f)]
        public float spreadAngle = 32f;

        [Tooltip("いちばん外側のボトルの落ちる距離の倍率（真ん中が 1。間はなめらかにつながる）。1 より小さいと弓なり、大きいと V 字。")]
        [SkillLabel("外側の距離の倍率")]
        [Range(0.2f, 2f)]
        public float outerDistanceScale = 0.85f;

        [Tooltip("ボトルを1本ずつ手から離す間隔（秒）。0 なら全部同時。")]
        [SkillLabel("投げる間隔（秒）")]
        [Range(0f, 0.5f)]
        public float staggerSeconds = 0.06f;

        [Tooltip("まとめて投げたときの、1つ1つの渦の大きさの倍率（「渦の半径」に掛ける）。")]
        [SkillLabel("1つの渦の大きさの倍率")]
        [Range(0.2f, 1.5f)]
        public float radiusScale = 0.75f;

        [Tooltip("2つ目以降の渦の、画面の揺れの強さの倍率（揺れが重なりすぎないように）。")]
        [SkillLabel("2つ目以降の揺れの強さ")]
        [Range(0f, 1f)]
        public float extraShakeScale = 0.35f;

        private int Clamped => Mathf.Clamp(count, 1, MaxCount);

        /// <summary>1回の攻撃で投げる数。</summary>
        public int ThrowCount => mode == BlueThrowMode.AllAtOnce ? Clamped : 1;

        /// <summary>1回の溜めで投げられる回数。</summary>
        public int ThrowsPerCharge => mode == BlueThrowMode.OneByOne ? Clamped : 1;
    }

    [Serializable]
    public sealed class BlueThrowTiming
    {
        [Tooltip("握ってから投げるまでの「振りかぶり」の時間（秒）。0 ならすぐ投げる。")]
        [SkillLabel("振りかぶる時間（秒）")]
        [Range(0f, 2f)]
        public float windupSeconds = 0.35f;

        [Tooltip("ボトルが手を離れてから落ちるまでの時間（秒）。")]
        [SkillLabel("飛んでいる時間（秒）")]
        [Range(0.05f, 3f)]
        public float flightSeconds = 0.55f;

        [Tooltip("放物線の一番高いところ（m）。カメラの方向へ本当に持ち上がるので、遠近で大きく見える。")]
        [SkillLabel("放物線の高さ（m）")]
        [Range(0f, 8f)]
        public float arcHeight = 2.4f;

        [Tooltip("飛んでいる間にボトルが回る回数。")]
        [SkillLabel("飛んでいる間の回転（回）")]
        [Range(0f, 6f)]
        public float spinTurns = 1.75f;

        [Tooltip("振りかぶっている間の砲台の回る速さ（1=いつも通り、0=止める）。狙いを定めやすくする。")]
        [SkillLabel("振りかぶり中の砲台の回る速さ")]
        [Range(0f, 1f)]
        public float rotationSpeedScale = 0.3f;
    }

    [Serializable]
    public sealed class BlueThrowTarget
    {
        [Tooltip("ボトルが落ちる場所までの距離（m）。砲台の向いている方向へ測る。")]
        [SkillLabel("落ちる距離（m）")]
        [Range(0f, 30f)]
        public float distance = 7f;

        [Tooltip("落ちる場所を左右にずらす（m、＋で向いている方向の右）。")]
        [SkillLabel("左右のずれ（m）")]
        [Range(-10f, 10f)]
        public float sideOffset = 0f;

        [Tooltip("ON なら発射口から、OFF なら砲台の中心から距離を測る。")]
        [SkillLabel("発射口から測る")]
        public bool measureFromMuzzle = true;

        [Tooltip("ON なら、途中に壁・障害物があるとその手前で落ちる。OFF なら壁を飛び越えて決めた距離に落ちる。")]
        [SkillLabel("壁の手前で落ちる")]
        public bool stopAtWalls = false;

        [Tooltip("「壁の手前で落ちる」が ON のとき、壁から離す距離（m）。")]
        [SkillLabel("壁から離す距離（m）")]
        [Min(0f)]
        public float wallPadding = 0.5f;

        [Tooltip("「壁の手前で落ちる」で壁として扱うレイヤー。")]
        [SkillLabel("壁のレイヤー")]
        public LayerMask wallLayers = ~0;

        [Tooltip("ON なら、使える状態の間、落ちる場所に目印（輪と点線の軌道）を出す。")]
        [SkillLabel("落ちる場所の目印を出す")]
        public bool showMarker = true;

        [Tooltip("目印の色（HDR）。")]
        [SkillLabel("目印の色")]
        [ColorUsage(true, true)]
        public Color markerColor = new Color(0.35f, 0.85f, 1.6f, 0.55f);

        [Tooltip("目印の点線の点の数。")]
        [SkillLabel("点線の点の数")]
        [Range(0, 40)]
        public int markerDots = 14;
    }

    [Serializable]
    public sealed class BlueVortexAreaSettings
    {
        [Tooltip("渦の半径（m）。この中の敵に効果がかかる。")]
        [SkillLabel("渦の半径（m）")]
        [Range(0.5f, 12f)]
        public float radius = 3.2f;

        [Tooltip("渦が広がりきってから消え始めるまでの時間（秒）。")]
        [SkillLabel("渦が続く時間（秒）")]
        [Range(0.1f, 30f)]
        public float durationSeconds = 5f;

        [Tooltip("破裂してから渦が広がりきるまでの時間（秒）。")]
        [SkillLabel("広がる時間（秒）")]
        [Range(0.05f, 2f)]
        public float expandSeconds = 0.45f;

        [Tooltip("広がるときの大きさの変化（横=時間0〜1、縦=半径の倍率）。1を少し超えると「ぶわっ」と広がって戻る。")]
        [SkillLabel("広がり方のカーブ")]
        public AnimationCurve expandCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 4.5f),
            new Keyframe(0.6f, 1.12f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));

        [Tooltip("渦が吸い込まれるように縮んで消えるまでの時間（秒）。")]
        [SkillLabel("消える時間（秒）")]
        [Range(0.05f, 3f)]
        public float collapseSeconds = 0.6f;

        [Tooltip("消える前の何秒間、ふちを点滅させて「もうすぐ消える」と知らせるか（0で点滅しない）。")]
        [SkillLabel("消える前の点滅（秒）")]
        [Range(0f, 3f)]
        public float warnSeconds = 1f;

        [Tooltip("ON なら、効果（足止めなど）は広がっている途中から、今の大きさに合わせてかかる。OFF なら広がりきってからかかる。")]
        [SkillLabel("広がる途中から効く")]
        public bool affectWhileExpanding = true;
    }

    [Serializable]
    public sealed class BlueVortexDebuff
    {
        [Tooltip("足止めの強さ。1 = その場で止まる（コアへ進めない）、0.5 = 半分の速さ、0 = 足止めしない。")]
        [SkillLabel("足止めの強さ（1=止まる）")]
        [Range(0f, 1f)]
        public float holdStrength = 1f;

        [Tooltip("ふちの何割の幅で効果が弱まっていくか（0 = ふちでいきなり効く）。")]
        [SkillLabel("ふちのやわらかさ（割合）")]
        [Range(0f, 1f)]
        public float edgeSoftness = 0.2f;

        [Tooltip("渦に沿って敵をぐるぐる回す速さ（m/秒、0で回さない）。敵の歩く速さの1.6倍までに抑えられる。")]
        [SkillLabel("渦に巻き込む速さ（m/秒）")]
        [Range(0f, 6f)]
        public float swirlSpeed = 1.4f;

        [Tooltip("ON なら時計回り、OFF なら反時計回りに巻き込む（見た目の渦の回る向きも同じになる）。")]
        [SkillLabel("時計回り")]
        public bool clockwise = false;

        [Tooltip("中心へ引き寄せる速さ（m/秒、0で引き寄せない）。")]
        [SkillLabel("中心へ引き寄せる速さ（m/秒）")]
        [Range(0f, 6f)]
        public float pullSpeed = 0.7f;

        [Tooltip("中心から半径の何割より内側へは引き寄せない（敵が真ん中の1点に固まらないように）。")]
        [SkillLabel("引き寄せない内側（割合）")]
        [Range(0f, 1f)]
        public float pullInnerRadius = 0.3f;

        [Tooltip("渦の中の敵が受けるダメージの倍率（1 = いつも通り、1.5 = 1.5倍）。レーザー・爆発など、他から受けたダメージに上乗せする。群衆の敵だけ。")]
        [SkillLabel("受けるダメージの倍率")]
        [Range(1f, 5f)]
        public float damageTakenMultiplier = 1.5f;

        [Tooltip("一定間隔で渦の中の敵に与えるダメージ（1回ぶん、0でなし）。")]
        [SkillLabel("渦のダメージ（1回）")]
        [Min(0f)]
        public float tickDamage = 1.5f;

        [Tooltip("渦のダメージの間隔（秒）。")]
        [SkillLabel("渦のダメージの間隔（秒）")]
        [Range(0.1f, 5f)]
        public float tickInterval = 0.5f;

        [Tooltip("ON なら、渦のダメージのたびに中の敵を白く光らせる（ネット対戦の全員の画面で光る）。")]
        [SkillLabel("ダメージのたびに光らせる")]
        public bool tickFlash = true;

        [Tooltip("渦が消えたあとも足止めが少し残る時間（秒、0で残らない）。だんだん弱まる。")]
        [SkillLabel("消えたあとの足止めの残り（秒）")]
        [Range(0f, 3f)]
        public float lingerSeconds = 0.4f;

        [Tooltip("ON なら、群衆ではない敵（Actor モード）にも足止めと渦のダメージをかける。")]
        [SkillLabel("Actor の敵にもかける")]
        public bool affectActors = true;
    }

    [Serializable]
    public sealed class BlueVortexLook
    {
        [Tooltip("渦の色（中心→ふち、HDR）。左が中心の暗い目、右がふち。アルファは濃さ。")]
        [SkillLabel("渦の色（中心→ふち）")]
        [GradientUsage(true)]
        public Gradient radialGradient = DefaultRadialGradient();

        [Tooltip("渦の筋（泡・しぶき）の色（HDR）。")]
        [SkillLabel("渦の筋の色")]
        [ColorUsage(true, true)]
        public Color foamColor = new Color(0.75f, 1.25f, 1.9f, 1f);

        [Tooltip("ふちの光る輪の色（HDR）。")]
        [SkillLabel("ふちの輪の色")]
        [ColorUsage(true, true)]
        public Color rimColor = new Color(0.35f, 1.1f, 2.4f, 1f);

        [Tooltip("中心の目のまわりの光る輪の色（HDR）。")]
        [SkillLabel("中心の輪の色")]
        [ColorUsage(true, true)]
        public Color eyeRingColor = new Color(1.6f, 1.9f, 2.6f, 1f);

        [Tooltip("渦の腕（うず巻きの筋）の本数。")]
        [SkillLabel("うず巻きの本数")]
        [Range(1, 12)]
        public int arms = 4;

        [Tooltip("うず巻きのねじれの強さ。大きいほどぐるぐる巻く。")]
        [SkillLabel("ねじれの強さ")]
        [Range(0f, 12f)]
        public float twist = 4.5f;

        [Tooltip("渦が回る速さ（回/秒）。")]
        [SkillLabel("回る速さ（回/秒）")]
        [Range(0f, 4f)]
        public float spinSpeed = 0.55f;

        [Tooltip("筋のくっきり具合（大きいほど細くくっきり）。")]
        [SkillLabel("筋のくっきり具合")]
        [Range(0.5f, 8f)]
        public float armSharpness = 2.6f;

        [Tooltip("水っぽい揺らぎ（ノイズ）の強さ。")]
        [SkillLabel("水の揺らぎ")]
        [Range(0f, 1f)]
        public float turbulence = 0.45f;

        [Tooltip("中心の暗い目の大きさ（半径に対する割合）。")]
        [SkillLabel("中心の目の大きさ")]
        [Range(0f, 0.6f)]
        public float eyeSize = 0.17f;

        [Tooltip("全体の濃さ（0 = 光を足すだけ、1 = 床を青く塗る）。床が明るいときは高めに。")]
        [SkillLabel("濃さ（床を塗る強さ）")]
        [Range(0f, 1f)]
        public float opacity = 0.85f;

        [Tooltip("全体の明るさ。")]
        [SkillLabel("明るさ")]
        [Range(0f, 4f)]
        public float intensity = 1.2f;

        [Tooltip("内側にもう1枚、逆向きに回る渦を重ねる（奥行きが出る）。")]
        [SkillLabel("逆回りの渦を重ねる")]
        public bool counterLayer = true;

        [Tooltip("渦に吸い込まれていく水しぶきの量（個/秒）。")]
        [SkillLabel("吸い込まれるしぶき（個/秒）")]
        [Range(0f, 200f)]
        public float spiralDropletRate = 55f;

        [Tooltip("渦から浮かぶ泡の量（個/秒）。")]
        [SkillLabel("浮かぶ泡（個/秒）")]
        [Range(0f, 120f)]
        public float bubbleRate = 26f;

        [Tooltip("ふちのきらめきの量（個/秒）。")]
        [SkillLabel("ふちのきらめき（個/秒）")]
        [Range(0f, 200f)]
        public float rimSparkleRate = 40f;

        [Tooltip("波紋の輪が出る間隔（秒、0で出さない）。")]
        [SkillLabel("波紋の間隔（秒）")]
        [Range(0f, 3f)]
        public float rippleInterval = 0.55f;

        [Tooltip("つかまっている敵のところに出る泡の量（個/秒の目安、0で出さない）。")]
        [SkillLabel("つかまった敵の泡（個/秒）")]
        [Range(0f, 120f)]
        public float trappedBubbleRate = 30f;

        [Tooltip("しぶき・泡・きらめきの明るさ。")]
        [SkillLabel("粒の明るさ")]
        [Range(0f, 6f)]
        public float particleBrightness = 2.2f;

        [Tooltip("粒の色（HDRではない。明るさは上の値で）。")]
        [SkillLabel("粒の色")]
        public Color particleColor = new Color(0.55f, 0.85f, 1f, 1f);

        [Tooltip("渦の床からの浮かせ方（m）。床の模様にめり込むときに少し上げる。")]
        [SkillLabel("床から浮かせる（m）")]
        [Range(0f, 0.5f)]
        public float floorLift = 0.03f;

        [Tooltip("渦（床に塗る部分）の描く順番。敵（2990）より小さい値にすると敵が渦の上に見える。")]
        [SkillLabel("渦の描く順番（renderQueue）")]
        public int vortexRenderQueue = 2980;

        [Tooltip("しぶき・光など、敵の上に重ねる演出の sortingOrder。")]
        [SkillLabel("上に重ねる演出の順番（sortingOrder）")]
        public int overlaySortingOrder = 150;

        private static Gradient DefaultRadialGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.01f, 0.02f, 0.12f), 0f),
                    new GradientColorKey(new Color(0.08f, 0.12f, 0.55f), 0.22f),
                    new GradientColorKey(new Color(0.12f, 0.35f, 1.1f), 0.55f),
                    new GradientColorKey(new Color(0.25f, 0.75f, 1.6f), 0.85f),
                    new GradientColorKey(new Color(0.5f, 1.1f, 2f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.9f, 0.5f),
                    new GradientAlphaKey(0.75f, 0.85f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }
    }

    [Serializable]
    public sealed class BlueBottleSettings
    {
        [Tooltip("ボトルの絵（口が上向きの画像）。空なら、コードで描いた仮のボトル（青いうがい薬）を使う。")]
        [SkillLabel("ボトルの絵（空なら仮の絵）")]
        public Sprite sprite;

        [Tooltip("ボトルの色（絵にかける色）。")]
        [SkillLabel("ボトルの色")]
        public Color tint = Color.white;

        [Tooltip("ボトルの長さ（m）。")]
        [SkillLabel("ボトルの長さ（m）")]
        [Range(0.2f, 6f)]
        public float length = 1.6f;

        [Tooltip("ボトルの幅（m）。")]
        [SkillLabel("ボトルの幅（m）")]
        [Range(0.1f, 4f)]
        public float width = 0.95f;

        [Tooltip("持っている位置: 砲台の中心から向いている方向へのずれ（m）。")]
        [SkillLabel("持つ位置（前へ m）")]
        public float forwardOffset = 0.2f;

        [Tooltip("持っている位置: 左右のずれ（m、＋で右）。")]
        [SkillLabel("持つ位置（右へ m）")]
        public float sideOffset = 0.55f;

        [Tooltip("持っている高さ（m、カメラの方向へ浮かせる）。")]
        [SkillLabel("持つ高さ（m）")]
        [Range(0f, 3f)]
        public float holdHeight = 0.6f;

        [Tooltip("ボトルを砲台の向きに対して何度傾けて持つか（度）。")]
        [SkillLabel("持つときの傾き（度）")]
        [Range(-90f, 90f)]
        public float holdTilt = -20f;

        [Tooltip("ふわふわ浮き沈みする幅（m）と速さ（回/秒）。")]
        [SkillLabel("ふわふわの幅（m）")]
        [Range(0f, 0.5f)]
        public float bobAmount = 0.08f;

        [SkillLabel("ふわふわの速さ（回/秒）")]
        [Range(0f, 5f)]
        public float bobFrequency = 1.1f;

        [Tooltip("中の液体がちゃぷちゃぷ揺れて見えるように、ボトルを小さく左右に振る角度（度）。")]
        [SkillLabel("ちゃぷちゃぷ揺れ（度）")]
        [Range(0f, 20f)]
        public float sloshAngle = 5f;

        [Tooltip("振りかぶるときにボトルを後ろへ引く距離（m）と、震える強さ（m）。")]
        [SkillLabel("振りかぶりで後ろへ引く（m）")]
        [Range(0f, 2f)]
        public float windupPullBack = 0.45f;

        [SkillLabel("振りかぶりの震え（m）")]
        [Range(0f, 0.3f)]
        public float windupShake = 0.05f;

        [Tooltip("出てくる／消えるときの時間（秒）。")]
        [SkillLabel("出てくる時間（秒）")]
        [Range(0f, 1f)]
        public float appearSeconds = 0.3f;

        [Tooltip("ボトルの描く順番（sortingOrder）。")]
        [SkillLabel("ボトルの描く順番")]
        public int sortingOrder = 160;

        [Tooltip("飛んでいる間、床に落とす影の濃さ（0で影なし）。")]
        [SkillLabel("飛んでいる間の影の濃さ")]
        [Range(0f, 1f)]
        public float shadowOpacity = 0.4f;
    }

    [Serializable]
    public sealed class BlueChargeFx
    {
        [Tooltip("ボトルの後ろの光の色（HDR）。")]
        [SkillLabel("ボトルの後ろの光の色")]
        [ColorUsage(true, true)]
        public Color auraColor = new Color(0.25f, 0.8f, 2.2f, 1f);

        [Tooltip("ボトルの後ろの光の大きさ（ボトルの長さに対する倍率）。")]
        [SkillLabel("後ろの光の大きさ")]
        [Range(0f, 5f)]
        public float auraScale = 2.1f;

        [Tooltip("光が脈打つ速さ（回/秒）と強さ。")]
        [SkillLabel("脈打つ速さ（回/秒）")]
        [Range(0f, 6f)]
        public float pulseFrequency = 1.6f;

        [SkillLabel("脈打つ強さ")]
        [Range(0f, 1f)]
        public float pulseAmount = 0.3f;

        [Tooltip("砲台の足元に出す小さな渦の半径（m、0で出さない）。")]
        [SkillLabel("足元の小さな渦の半径（m）")]
        [Range(0f, 5f)]
        public float miniVortexRadius = 1.7f;

        [Tooltip("足元の小さな渦の濃さ。")]
        [SkillLabel("足元の小さな渦の濃さ")]
        [Range(0f, 1f)]
        public float miniVortexOpacity = 0.6f;

        [Tooltip("ボトルのまわりを回りながら吸い込まれる水の粒の量（個/秒）。")]
        [SkillLabel("まわりを回る水の粒（個/秒）")]
        [Range(0f, 150f)]
        public float orbitRate = 34f;

        [Tooltip("水の粒が回り始める距離（m）。")]
        [SkillLabel("水の粒が回り始める距離（m）")]
        [Range(0.2f, 6f)]
        public float orbitRadius = 2.2f;

        [Tooltip("浮かぶ泡の量（個/秒）。")]
        [SkillLabel("浮かぶ泡（個/秒）")]
        [Range(0f, 100f)]
        public float bubbleRate = 14f;

        [Tooltip("足元から広がる波紋の間隔（秒、0で出さない）。")]
        [SkillLabel("足元の波紋の間隔（秒）")]
        [Range(0f, 3f)]
        public float rippleInterval = 0.8f;

        [Tooltip("使えるようになった瞬間に広がる輪の大きさ（m、0で出さない）。")]
        [SkillLabel("使えるようになった瞬間の輪（m）")]
        [Range(0f, 12f)]
        public float readyBurstSize = 5f;

        [Tooltip("振りかぶっている間、粒・渦・光をどれだけ強めるか（倍率）。")]
        [SkillLabel("振りかぶり中の強まり（倍）")]
        [Range(1f, 6f)]
        public float windupMultiplier = 3f;

        [Tooltip("溜めの演出の粒の色（HDRではない）。")]
        [SkillLabel("粒の色")]
        public Color particleColor = new Color(0.5f, 0.85f, 1f, 1f);

        [Tooltip("粒の明るさ。")]
        [SkillLabel("粒の明るさ")]
        [Range(0f, 6f)]
        public float particleBrightness = 2.4f;
    }

    [Serializable]
    public sealed class BlueBurstFx
    {
        [Tooltip("投げた瞬間に手元で出る水しぶきの数。")]
        [SkillLabel("投げた瞬間のしぶき（個）")]
        [Range(0, 120)]
        public int throwSplashCount = 26;

        [Tooltip("飛んでいるボトルからこぼれる水の粒（個/秒）。")]
        [SkillLabel("飛んでいる間にこぼれる粒（個/秒）")]
        [Range(0f, 300f)]
        public float trailRate = 110f;

        [Tooltip("飛んでいるボトルのまわりの光の大きさ（m、0で出さない）。")]
        [SkillLabel("飛んでいるボトルの光（m）")]
        [Range(0f, 6f)]
        public float trailGlowSize = 1.8f;

        [Tooltip("破裂の瞬間の光の大きさ（渦の半径に対する倍率）と色（HDR）。")]
        [SkillLabel("破裂の光の大きさ（倍）")]
        [Range(0f, 4f)]
        public float flashScale = 1.5f;

        [SkillLabel("破裂の光の色")]
        [ColorUsage(true, true)]
        public Color flashColor = new Color(1.4f, 2.2f, 3.2f, 1f);

        [Tooltip("破裂の光のトゲ（水の王冠）の本数。")]
        [SkillLabel("水の王冠のトゲの数")]
        [Range(0, 32)]
        public int crownSpikes = 14;

        [Tooltip("破裂の瞬間に広がる衝撃の輪の数・大きさ（渦の半径に対する倍率）・色（HDR）。")]
        [SkillLabel("衝撃の輪の数")]
        [Range(0, 6)]
        public int shockwaveCount = 3;

        [SkillLabel("衝撃の輪の大きさ（倍）")]
        [Range(0.2f, 4f)]
        public float shockwaveScale = 1.7f;

        [SkillLabel("衝撃の輪の色")]
        [ColorUsage(true, true)]
        public Color shockwaveColor = new Color(0.5f, 1.4f, 3f, 1f);

        [Tooltip("破裂で飛び散る水しぶきの数と速さ（m/秒）。")]
        [SkillLabel("飛び散るしぶき（個）")]
        [Range(0, 400)]
        public int dropletCount = 150;

        [SkillLabel("しぶきの速さ（m/秒）")]
        [Range(0f, 40f)]
        public float dropletSpeed = 14f;

        [Tooltip("割れたボトルのかけらの数。")]
        [SkillLabel("ボトルのかけら（個）")]
        [Range(0, 60)]
        public int shardCount = 14;

        [Tooltip("破裂でわき上がる泡の数。")]
        [SkillLabel("わき上がる泡（個）")]
        [Range(0, 200)]
        public int bubbleCount = 40;

        [Tooltip("渦が消える瞬間のしぶきの数（0で出さない）。")]
        [SkillLabel("消える瞬間のしぶき（個）")]
        [Range(0, 300)]
        public int collapseSplashCount = 70;

        [Tooltip("しぶき・かけら・泡の色（HDRではない）。")]
        [SkillLabel("しぶきの色")]
        public Color dropletColor = new Color(0.6f, 0.9f, 1f, 1f);

        [SkillLabel("かけらの色")]
        public Color shardColor = new Color(0.85f, 0.95f, 1f, 1f);

        [Tooltip("粒の明るさ。")]
        [SkillLabel("粒の明るさ")]
        [Range(0f, 6f)]
        public float particleBrightness = 2.6f;
    }

    [Serializable]
    public sealed class BlueCameraShake
    {
        [Tooltip("ON なら、投げたプレイヤーの画面だけ揺らす（ネット対戦で他の人の画面は揺らさない）。")]
        [SkillLabel("投げた人の画面だけ揺らす")]
        public bool ownerViewOnly = false;

        [Tooltip("投げた瞬間の揺れ。")]
        [SkillLabel("投げた瞬間の揺れ")]
        public SimpleShakeSettings onThrow = new SimpleShakeSettings { amplitude = 0.12f, rollAmplitude = 0.3f, frequency = 20f, duration = 0.2f };

        [SkillLabel("投げた瞬間の揺れの強さ")]
        [Range(0f, 3f)]
        public float throwStrength = 1f;

        [Tooltip("破裂した瞬間の揺れ。")]
        [SkillLabel("破裂の揺れ")]
        public SimpleShakeSettings onBurst = new SimpleShakeSettings { amplitude = 0.45f, rollAmplitude = 1.1f, frequency = 20f, duration = 0.55f };

        [SkillLabel("破裂の揺れの強さ")]
        [Range(0f, 3f)]
        public float burstStrength = 1f;

        [Tooltip("渦が出ている間ずっと続く小さな揺れ。")]
        [SkillLabel("渦が出ている間の揺れ")]
        public SimpleShakeSettings whileActive = new SimpleShakeSettings { amplitude = 0.04f, rollAmplitude = 0.15f, frequency = 14f, duration = 0.3f };

        [SkillLabel("渦が出ている間の揺れの強さ（0で揺らさない）")]
        [Range(0f, 3f)]
        public float whileActiveStrength = 1f;

        [SkillLabel("渦が出ている間の揺れの間隔（秒）")]
        [Range(0.05f, 2f)]
        public float whileActiveInterval = 0.25f;

        [Tooltip("渦が消える瞬間の揺れ。")]
        [SkillLabel("消える瞬間の揺れ")]
        public SimpleShakeSettings onCollapse = new SimpleShakeSettings { amplitude = 0.2f, rollAmplitude = 0.5f, frequency = 22f, duration = 0.3f };

        [SkillLabel("消える瞬間の揺れの強さ")]
        [Range(0f, 3f)]
        public float collapseStrength = 1f;
    }

    [Serializable]
    public sealed class BlueSkillSounds
    {
        [Tooltip("使えるようになった瞬間（砲台の位置）。")]
        [SkillLabel("使えるようになった瞬間")]
        public FortressEffect onReady = new FortressEffect();

        [Tooltip("振りかぶり始め（ボトルの位置）。")]
        [SkillLabel("振りかぶり始め")]
        public FortressEffect onWindup = new FortressEffect();

        [Tooltip("投げた瞬間（ボトルの位置）。")]
        [SkillLabel("投げた瞬間")]
        public FortressEffect onThrow = new FortressEffect();

        [Tooltip("飛んでいる間ずっと（ボトルについていく。VFX は Loop にしておく）。")]
        [SkillLabel("飛んでいる間（ループ）")]
        public FortressEffect whileFlying = new FortressEffect();

        [Tooltip("落ちて破裂した瞬間（落ちた場所）。")]
        [SkillLabel("破裂した瞬間")]
        public FortressEffect onBurst = new FortressEffect();

        [Tooltip("渦が出ている間ずっと（渦の中心。VFX は Loop にしておく）。")]
        [SkillLabel("渦が出ている間（ループ）")]
        public FortressEffect whileVortex = new FortressEffect();

        [Tooltip("渦のダメージのたび（渦の中心）。")]
        [SkillLabel("渦のダメージのたび")]
        public FortressEffect onTick = new FortressEffect();

        [Tooltip("渦が消えた瞬間（渦の中心）。")]
        [SkillLabel("渦が消えた瞬間")]
        public FortressEffect onEnd = new FortressEffect();
    }

    [Serializable]
    public sealed class BlueSkillShaders
    {
        [Tooltip("渦の本体のシェーダー（MS2026/BlueSkill/Vortex）。")]
        [SkillLabel("渦")]
        public Shader vortex;

        [Tooltip("光・輪・トゲのシェーダー（MS2026/BlueSkill/Glow）。")]
        [SkillLabel("光・輪")]
        public Shader glow;

        [Tooltip("しぶき・泡・きらめきのシェーダー（MS2026/BlueSkill/Particle）。")]
        [SkillLabel("粒")]
        public Shader particle;
    }
}
