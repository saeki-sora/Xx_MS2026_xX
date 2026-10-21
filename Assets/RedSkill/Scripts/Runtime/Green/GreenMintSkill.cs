using System;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using UnityEngine;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// 緑キャラのスキル「ミント・サテライト」の設定ファイル。値はすべてここで調整できる（Play 中に変えてもすぐ反映される。絵の色だけは次の Play から）。
    /// 使える間は砲台の上でミントタブレットのケースがカシャカシャ震え、次に握ると蓋が開いてタブレットがコアへ飛ぶ。
    /// コアに着いた瞬間に「ドン」と回復（画面のふちがミント色に光る）、その後は一定時間、コアの周りをタブレットが衛星のように回り、
    /// 近づいた敵へ錠剤の弾を撃ち続け、コアもじわじわ回復する。緑の砲台はその間、緑のジェルでコアへ力を送る（握ると太く・弾が増える）。
    /// 実際に動かすのは <see cref="GreenMintSkillRunner"/>（スキル欄が自動で付ける）。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/スキル/グリーン：ミント・サテライト", fileName = "GreenMintSkill")]
    public sealed class GreenMintSkill : CharacterSkill
    {
        [Tooltip("発動からおしまいまでの流れと長さ。")]
        [SkillLabel("タイミング")]
        public GreenMintTiming timing = new GreenMintTiming();

        [Tooltip("コアの回復量。")]
        [SkillLabel("コアの回復")]
        public GreenMintHeal heal = new GreenMintHeal();

        [Tooltip("衛星から撃つ錠剤の弾（狙い方・強さ・速さ）。")]
        [SkillLabel("コアの攻撃（錠剤の弾）")]
        public GreenMintAttack attack = new GreenMintAttack();

        [Tooltip("スキル中、緑のプレイヤーが握ったときのおまけ（ジェルが太く・弾が増える）。")]
        [SkillLabel("握ったときの強化")]
        public GreenMintBoost boost = new GreenMintBoost();

        [Tooltip("全体の色。絵（タブレット・ケース・葉っぱ）もこの色から作る。")]
        [SkillLabel("色（ミントの配色）")]
        public GreenMintPalette palette = new GreenMintPalette();

        [Tooltip("使える間、砲台の上で震えるミントタブレットのケース。")]
        [SkillLabel("溜めのタブレットケース")]
        public GreenMintCaseFx tabletCase = new GreenMintCaseFx();

        [Tooltip("コアの周りを回るタブレット（衛星）と、コアを包む光の膜。")]
        [SkillLabel("衛星とコアの膜")]
        public GreenMintSatelliteFx satellites = new GreenMintSatelliteFx();

        [Tooltip("緑の砲台からコアへ流れる緑のジェル。")]
        [SkillLabel("コアへ送るジェル")]
        public GreenMintLinkFx link = new GreenMintLinkFx();

        [Tooltip("錠剤の弾の見た目と、当たったときの粉・葉っぱ。")]
        [SkillLabel("弾と当たった場所の演出")]
        public GreenMintShotFx shots = new GreenMintShotFx();

        [Tooltip("コアに着いて「ドン」と回復する瞬間の演出（全員の画面に出る）。")]
        [SkillLabel("回復の瞬間の演出")]
        public GreenMintBurstFx burst = new GreenMintBurstFx();

        [Tooltip("カメラの揺れ。")]
        [SkillLabel("カメラの揺れ")]
        public GreenMintCameraShake cameraShake = new GreenMintCameraShake();

        [Tooltip("効果音と、D-Drive の追加エフェクト（空なら何もしない）。")]
        [SkillLabel("効果音・追加エフェクト（D-Drive）")]
        public GreenMintSounds sounds = new GreenMintSounds();

        [Tooltip("見た目に使うシェーダー（通常は触らない）。")]
        [SkillLabel("シェーダー（通常は触らない）")]
        public GreenMintShaders shaders = new GreenMintShaders();

        public override CharacterSkillRunner CreateRunner(PlayerSkillSlot slot)
        {
            var runner = slot.gameObject.AddComponent<GreenMintSkillRunner>();
            runner.Configure(this);
            return runner;
        }

        private void Reset()
        {
            skillId = "green_mint_satellite";
            displayName = "ミント・サテライト";
            description = "コアの周りをミントタブレットの衛星が回り、近づいた敵を錠剤の弾で撃ち落とす。発動の瞬間にコアを大きく回復し、その後もじわじわ回復する。";
            themeColor = new Color(0.25f, 0.8f, 0.5f);
        }

        /// <summary>発動してから終わるまでの合計秒数。</summary>
        public float TotalSeconds => timing.launchSeconds + timing.activeSeconds + timing.endSeconds;
    }

    [Serializable]
    public sealed class GreenMintTiming
    {
        [Tooltip("握ってから、タブレットがコアに着いて「ドン」と回復するまでの秒数（ケースの蓋が開いてタブレットが飛ぶ間）。")]
        [SkillLabel("コアに着くまで（秒）")]
        [Min(0.05f)]
        public float launchSeconds = 0.6f;

        [Tooltip("衛星がコアを守って撃ち続ける秒数（コアに着いてから）。")]
        [SkillLabel("続く時間（秒）")]
        [Min(0.1f)]
        public float activeSeconds = 8f;

        [Tooltip("最後に衛星がコアへ吸い込まれて消えるまでの秒数。")]
        [SkillLabel("消えるまで（秒）")]
        [Min(0.05f)]
        public float endSeconds = 0.7f;
    }

    [Serializable]
    public sealed class GreenMintHeal
    {
        [Tooltip("コアに着いた瞬間に回復する量（コアの最大体力に対する %）。")]
        [SkillLabel("ドンと回復（%）")]
        [Range(0f, 100f)]
        public float burstPercent = 20f;

        [Tooltip("続く時間の間に、合わせてじわじわ回復する量（コアの最大体力に対する %）。")]
        [SkillLabel("じわじわ回復（合計 %）")]
        [Range(0f, 100f)]
        public float regenPercent = 15f;

        [Tooltip("ONなら、試合が終わった後（クリア・ゲームオーバー）は回復しない。")]
        [SkillLabel("試合が終わったら回復しない")]
        public bool stopWhenMatchEnded = true;
    }

    [Serializable]
    public sealed class GreenMintAttack
    {
        [Tooltip("コアからこの距離（メートル）より内側の敵を狙う。画面の縦は約28メートル、砲台はコアから6メートル。")]
        [SkillLabel("守る範囲の半径（m）")]
        [Min(1f)]
        public float range = 9f;

        [Tooltip("1秒に撃つ弾の数（衛星全部の合計、握っていないとき）。")]
        [SkillLabel("1秒に撃つ数")]
        [Min(0.5f)]
        public float shotsPerSecond = 10f;

        [Tooltip("錠剤1発のダメージ（ふつうの敵の体力は 10。ボスは多い）。")]
        [SkillLabel("1発のダメージ")]
        [Min(0f)]
        public float damagePerShot = 14f;

        [Tooltip("当たった場所の、まわりの敵も巻き込む半径（m）。")]
        [SkillLabel("巻き込む半径（m）")]
        [Min(0.05f)]
        public float splashRadius = 0.55f;

        [Tooltip("1発で倒せる（当たる）敵の最大数。0 なら巻き込む範囲の全員。")]
        [SkillLabel("1発で当たる最大数")]
        [Min(0)]
        public int maxHitsPerShot = 3;

        [Tooltip("ONなら、Actor の敵（1体ずつ動く敵）も狙う。")]
        [SkillLabel("Actor の敵も狙う")]
        public bool targetActors = true;

        [Tooltip("爆弾の敵を、この距離（m）ぶんコアに近いものとして先に狙う（コアのすぐそばで爆発させないため）。")]
        [SkillLabel("爆弾の敵を先に狙う（m）")]
        [Min(0f)]
        public float bombPriorityMeters = 3f;

        [Tooltip("弾の速さ（m/秒）。")]
        [SkillLabel("弾の速さ（m/秒）")]
        [Min(1f)]
        public float bulletSpeed = 20f;

        [Tooltip("弾が曲がる速さ（度/秒）。大きいほど、動く敵にしっかり追いつく。")]
        [SkillLabel("弾が曲がる速さ（度/秒）")]
        [Min(0f)]
        public float bulletTurnRate = 900f;

        [Tooltip("弾がこの秒数で敵に届かなければ、その場ではじける。")]
        [SkillLabel("弾の寿命（秒）")]
        [Min(0.1f)]
        public float bulletLifetime = 1.4f;

        [Tooltip("1フレームに当てられる弾の数の上限（群衆の当たり判定の枠は全員で1フレーム32本まで）。")]
        [SkillLabel("1フレームに当てる最大数")]
        [Range(1, 12)]
        public int maxImpactsPerFrame = 6;
    }

    [Serializable]
    public sealed class GreenMintBoost
    {
        [Tooltip("握りきったときの、1秒に撃つ数（握る強さで「1秒に撃つ数」からここまで増える）。")]
        [SkillLabel("握りきったときの1秒に撃つ数")]
        [Min(0.5f)]
        public float boostedShotsPerSecond = 22f;

        [Tooltip("握りきったときのジェルの太さの倍率。")]
        [SkillLabel("握りきったときのジェルの太さ（倍）")]
        [Min(1f)]
        public float boostedWidthScale = 2.1f;

        [Tooltip("握りきったときのジェルの流れの速さの倍率。")]
        [SkillLabel("握りきったときの流れの速さ（倍）")]
        [Min(1f)]
        public float boostedFlowScale = 2.4f;

        [Tooltip("握る強さの変化をなめらかにする速さ（大きいほどすぐ反応）。")]
        [SkillLabel("反応の速さ")]
        [Min(0.5f)]
        public float response = 8f;
    }

    [Serializable]
    public sealed class GreenMintPalette
    {
        [Tooltip("一番明るいミント色（ジェルの芯・光）。")]
        [SkillLabel("明るいミント")]
        public Color mint = new Color(0.55f, 1f, 0.76f);

        [Tooltip("ふつうの緑（ケース・ジェルのふち）。")]
        [SkillLabel("ミントグリーン")]
        public Color green = new Color(0.2f, 0.82f, 0.5f);

        [Tooltip("濃い緑（影・ふちの線）。")]
        [SkillLabel("濃い緑")]
        public Color deep = new Color(0.04f, 0.42f, 0.27f);

        [Tooltip("輪郭の線の色。")]
        [SkillLabel("輪郭の線")]
        public Color outline = new Color(0.03f, 0.2f, 0.13f);

        [Tooltip("タブレットの白。")]
        [SkillLabel("タブレットの白")]
        public Color tablet = new Color(0.97f, 1f, 0.98f);

        [Tooltip("葉っぱの色。")]
        [SkillLabel("葉っぱ")]
        public Color leaf = new Color(0.22f, 0.72f, 0.36f);

        [Tooltip("光るところの色（HDR。加算で重ねる光・キラキラ）。")]
        [SkillLabel("光（HDR）")]
        [ColorUsage(false, true)]
        public Color glow = new Color(0.55f, 1.6f, 0.95f);
    }

    [Serializable]
    public sealed class GreenMintCaseFx
    {
        [Tooltip("ONなら、使える間、砲台の上にミントタブレットのケースを出す。")]
        [SkillLabel("ケースを出す")]
        public bool enabled = true;

        [Tooltip("ケースの高さ（m）。カメラが遠いので大きめ（砲台は約1.2m）。")]
        [SkillLabel("高さ（m）")]
        [Min(0.2f)]
        public float height = 2.2f;

        [Tooltip("ケースの幅（m）。")]
        [SkillLabel("幅（m）")]
        [Min(0.2f)]
        public float width = 1.45f;

        [Tooltip("砲台の中心から、画面の上方向へずらす距離（m）。どのプレイヤーの画面でも、ケースは画面の上向きに立つ。")]
        [SkillLabel("画面の上へずらす（m）")]
        public float screenUpOffset = 1.7f;

        [Tooltip("出てくるときの秒数（ポンと弾んで出る）。")]
        [SkillLabel("出てくる時間（秒）")]
        [Min(0.01f)]
        public float appearSeconds = 0.35f;

        [Tooltip("いつも小刻みに震える幅（m）。")]
        [SkillLabel("震える幅（m）")]
        [Min(0f)]
        public float trembleAmount = 0.035f;

        [Tooltip("「カシャカシャ」と大きく揺れる間隔の最短（秒）。")]
        [SkillLabel("カシャカシャの間隔・最短（秒）")]
        [Min(0.05f)]
        public float rattleIntervalMin = 0.45f;

        [Tooltip("「カシャカシャ」と大きく揺れる間隔の最長（秒）。")]
        [SkillLabel("カシャカシャの間隔・最長（秒）")]
        [Min(0.05f)]
        public float rattleIntervalMax = 1.05f;

        [Tooltip("「カシャカシャ」のときにケースが跳ねる高さ（m）と傾き（度）。")]
        [SkillLabel("カシャカシャの跳ね（m・度）")]
        public Vector2 rattleJump = new Vector2(0.16f, 9f);

        [Tooltip("ケースの窓から見えるタブレットの数。")]
        [SkillLabel("中のタブレットの数")]
        [Range(0, 10)]
        public int tabletsInside = 6;

        [Tooltip("ケースのまわりのキラキラの数（1秒あたり）。")]
        [SkillLabel("キラキラ（毎秒）")]
        [Min(0f)]
        public float sparkleRate = 10f;

        [Tooltip("ケースから浮かぶ回復の十字（＋）の数（1秒あたり）。")]
        [SkillLabel("回復の十字（毎秒）")]
        [Min(0f)]
        public float crossRate = 1.6f;

        [Tooltip("ケースの後ろの光の大きさ（ケースの高さに対する倍率）。")]
        [SkillLabel("後ろの光の大きさ（倍）")]
        [Min(0f)]
        public float auraScale = 1.9f;

        [Tooltip("発動したとき、蓋が開くまでの秒数。")]
        [SkillLabel("蓋が開く時間（秒）")]
        [Min(0.01f)]
        public float lidOpenSeconds = 0.18f;

        [Tooltip("中身を出し切った後、消えるまでの秒数。")]
        [SkillLabel("消える時間（秒）")]
        [Min(0.01f)]
        public float fadeOutSeconds = 0.45f;

        [Tooltip("描く順番（大きいほど手前）。")]
        [SkillLabel("描く順番")]
        public int sortingOrder = 140;
    }

    [Serializable]
    public sealed class GreenMintSatelliteFx
    {
        [Tooltip("コアの周りを回るタブレットの数。")]
        [SkillLabel("衛星の数")]
        [Range(1, 10)]
        public int count = 5;

        [Tooltip("衛星（タブレット）の直径（m）。")]
        [SkillLabel("衛星の大きさ（m）")]
        [Min(0.1f)]
        public float size = 1.3f;

        [Tooltip("コアの中心から衛星までの距離（m）。")]
        [SkillLabel("回る半径（m）")]
        [Min(0.2f)]
        public float orbitRadius = 2.9f;

        [Tooltip("衛星が回る速さ（度/秒）。")]
        [SkillLabel("回る速さ（度/秒）")]
        public float orbitDegreesPerSecond = 75f;

        [Tooltip("回る半径の、ふわふわ伸び縮みする幅（m）。")]
        [SkillLabel("ふわふわの幅（m）")]
        [Min(0f)]
        public float bobAmount = 0.18f;

        [Tooltip("撃った瞬間に衛星が弾む大きさ（倍率の増え方）。")]
        [SkillLabel("撃ったときの弾み")]
        [Min(0f)]
        public float shotPunch = 0.22f;

        [Tooltip("コアを包む光の膜の半径（m）。ジェルはこの膜に流れ込む。")]
        [SkillLabel("膜の半径（m）")]
        [Min(0.2f)]
        public float membraneRadius = 2.1f;

        [Tooltip("膜の濃さ（0〜1）。")]
        [SkillLabel("膜の濃さ")]
        [Range(0f, 1f)]
        public float membraneOpacity = 0.55f;

        [Tooltip("ONなら、守る範囲のふちに、うすいミント色の点線の輪を出す（ゆっくり回る）。")]
        [SkillLabel("守る範囲の輪を出す")]
        public bool showRangeRing = true;

        [Tooltip("守る範囲の輪の濃さ（0〜1）。")]
        [SkillLabel("範囲の輪の濃さ")]
        [Range(0f, 1f)]
        public float rangeRingOpacity = 0.4f;

        [Tooltip("描く順番（大きいほど手前）。コアの絵より大きくする。")]
        [SkillLabel("描く順番")]
        public int sortingOrder = 160;
    }

    [Serializable]
    public sealed class GreenMintLinkFx
    {
        [Tooltip("ジェルの太さ（m、握っていないとき）。")]
        [SkillLabel("太さ（m）")]
        [Min(0.02f)]
        public float width = 0.6f;

        [Tooltip("ジェルが横にうねる幅（m）。")]
        [SkillLabel("うねりの幅（m）")]
        [Min(0f)]
        public float wobbleAmount = 0.22f;

        [Tooltip("ジェル1本に入るうねりの数。")]
        [SkillLabel("うねりの数")]
        [Min(0f)]
        public float wobbleWaves = 2.2f;

        [Tooltip("ジェル全体が弓なりに曲がる大きさ（m）。")]
        [SkillLabel("弓なりの曲がり（m）")]
        public float bow = 0.8f;

        [Tooltip("ジェルが流れる速さ（m/秒、握っていないとき）。")]
        [SkillLabel("流れる速さ（m/秒）")]
        [Min(0f)]
        public float flowSpeed = 5f;

        [Tooltip("ジェルの中を運ばれていく小さなタブレットの数。")]
        [SkillLabel("運ばれるタブレットの数")]
        [Range(0, 24)]
        public int carriedTablets = 7;

        [Tooltip("ジェルの濃さ（0〜1）。")]
        [SkillLabel("濃さ")]
        [Range(0f, 1f)]
        public float opacity = 0.9f;

        [Tooltip("ジェルが伸びる／縮む秒数。")]
        [SkillLabel("伸びる時間（秒）")]
        [Min(0.01f)]
        public float extendSeconds = 0.4f;

        [Tooltip("ジェルの区切りの数（多いほどなめらか）。")]
        [SkillLabel("なめらかさ（区切り）")]
        [Range(8, 96)]
        public int segments = 40;

        [Tooltip("描く順番（大きいほど手前）。")]
        [SkillLabel("描く順番")]
        public int sortingOrder = 145;
    }

    [Serializable]
    public sealed class GreenMintShotFx
    {
        [Tooltip("錠剤の弾の直径（m）。")]
        [SkillLabel("弾の大きさ（m）")]
        [Min(0.05f)]
        public float bulletSize = 0.6f;

        [Tooltip("弾が回る速さ（度/秒）。")]
        [SkillLabel("弾が回る速さ（度/秒）")]
        public float bulletSpin = 540f;

        [Tooltip("弾の後ろに伸びる光の尾の長さ（秒ぶん）。")]
        [SkillLabel("尾の長さ（秒）")]
        [Min(0f)]
        public float trailSeconds = 0.16f;

        [Tooltip("光の尾の太さ（弾の大きさに対する倍率）。")]
        [SkillLabel("尾の太さ（倍）")]
        [Min(0f)]
        public float trailWidthScale = 0.75f;

        [Tooltip("当たったときに弾ける粉の数。")]
        [SkillLabel("粉の数")]
        [Range(0, 40)]
        public int powderCount = 9;

        [Tooltip("粉ひと粒の大きさ（m）。")]
        [SkillLabel("粉の大きさ（m）")]
        [Min(0.02f)]
        public float powderSize = 0.75f;

        [Tooltip("当たったときに舞う葉っぱの数。")]
        [SkillLabel("葉っぱの数")]
        [Range(0, 10)]
        public int leafCount = 2;

        [Tooltip("葉っぱの大きさ（m）。")]
        [SkillLabel("葉っぱの大きさ（m）")]
        [Min(0.05f)]
        public float leafSize = 0.6f;

        [Tooltip("当たったときのキラキラの数。")]
        [SkillLabel("キラキラの数")]
        [Range(0, 40)]
        public int sparkleCount = 7;

        [Tooltip("当たったときに広がる輪の大きさ（m）。0 で出さない。")]
        [SkillLabel("輪の大きさ（m）")]
        [Min(0f)]
        public float ringSize = 1.7f;

        [Tooltip("衛星から撃つ瞬間の光の大きさ（m）。")]
        [SkillLabel("撃つ瞬間の光（m）")]
        [Min(0f)]
        public float muzzleFlashSize = 1.4f;
    }

    [Serializable]
    public sealed class GreenMintBurstFx
    {
        [Tooltip("コアから広がるミントの波の、最後の大きさ（直径 m）。画面全体を通り抜けるくらい。")]
        [SkillLabel("波の大きさ（m）")]
        [Min(0f)]
        public float waveSize = 40f;

        [Tooltip("波が広がりきるまでの秒数。")]
        [SkillLabel("波の時間（秒）")]
        [Min(0.05f)]
        public float waveSeconds = 1.1f;

        [Tooltip("コアの光の大きさ（m）。")]
        [SkillLabel("コアの光の大きさ（m）")]
        [Min(0f)]
        public float flashSize = 11f;

        [Tooltip("画面のふちをミント色に光らせる濃さ（0〜1）。0 で光らせない。")]
        [SkillLabel("画面のふちの光の濃さ")]
        [Range(0f, 1f)]
        public float screenEdgeStrength = 0.55f;

        [Tooltip("画面のふちの光が消えるまでの秒数。")]
        [SkillLabel("画面のふちの光の時間（秒）")]
        [Min(0.05f)]
        public float screenEdgeSeconds = 1.1f;

        [Tooltip("コアから飛び散るキラキラの数。")]
        [SkillLabel("キラキラの数")]
        [Range(0, 300)]
        public int sparkleCount = 110;

        [Tooltip("コアから舞う葉っぱの数。")]
        [SkillLabel("葉っぱの数")]
        [Range(0, 60)]
        public int leafCount = 18;

        [Tooltip("コアから浮かぶ回復の十字（＋）の数。")]
        [SkillLabel("回復の十字の数")]
        [Range(0, 30)]
        public int crossCount = 10;

        [Tooltip("ONなら、コアの上を光の帯が斜めに通り抜ける（ピカッと磨かれたように見せる）。")]
        [SkillLabel("コアを磨く光の帯")]
        public bool shine = true;

        [Tooltip("じわじわ回復している間、コアから回復の十字（＋）が浮かぶ数（1秒あたり）。")]
        [SkillLabel("じわじわ中の十字（毎秒）")]
        [Min(0f)]
        public float regenCrossRate = 2.2f;
    }

    [Serializable]
    public sealed class GreenMintCameraShake
    {
        [Tooltip("ONなら、緑のプレイヤーの画面だけ揺らす（OFFなら全員の画面が揺れる）。")]
        [SkillLabel("緑の人の画面だけ揺らす")]
        public bool ownerViewOnly;

        [Tooltip("コアに着いて回復した瞬間の揺れ（軽め）。")]
        [SkillLabel("回復の瞬間の揺れ")]
        public SimpleShakeSettings burst = new SimpleShakeSettings { amplitude = 0.16f, rollAmplitude = 0.35f, frequency = 18f, duration = 0.35f };

        [Tooltip("回復の瞬間の揺れの強さ（0で揺らさない）。")]
        [SkillLabel("回復の瞬間の揺れの強さ")]
        [Min(0f)]
        public float burstStrength = 1f;
    }

    [Serializable]
    public sealed class GreenMintSounds
    {
        [Tooltip("使えるようになった瞬間（砲台の位置）。")]
        [SkillLabel("使えるようになった瞬間")]
        public FortressEffect onReady = new FortressEffect();

        [Tooltip("握って発動した瞬間（ケースの蓋が開く。ケースの位置）。")]
        [SkillLabel("発動（蓋が開く）")]
        public FortressEffect onActivate = new FortressEffect();

        [Tooltip("タブレットがコアに着いて回復した瞬間（コアの位置）。")]
        [SkillLabel("回復の瞬間")]
        public FortressEffect onBurst = new FortressEffect();

        [Tooltip("衛星が回っている間ずっと（コアについていく。VFX は Loop にしておく）。")]
        [SkillLabel("衛星が回っている間（ループ）")]
        public FortressEffect whileActive = new FortressEffect();

        [Tooltip("衛星が弾を撃つたび（撃った衛星の位置。数が多いので下の間隔で間引く）。")]
        [SkillLabel("弾を撃つ")]
        public FortressEffect onShot = new FortressEffect();

        [Tooltip("「弾を撃つ」を鳴らす最短の間隔（秒）。")]
        [SkillLabel("弾を撃つ音の最短間隔（秒）")]
        [Min(0f)]
        public float shotInterval = 0.08f;

        [Tooltip("弾が当たったとき（当たった場所。下の間隔で間引く）。")]
        [SkillLabel("弾が当たる")]
        public FortressEffect onHit = new FortressEffect();

        [Tooltip("「弾が当たる」を鳴らす最短の間隔（秒）。")]
        [SkillLabel("弾が当たる音の最短間隔（秒）")]
        [Min(0f)]
        public float hitInterval = 0.08f;

        [Tooltip("衛星がコアに吸い込まれて終わった瞬間（コアの位置）。")]
        [SkillLabel("おしまい")]
        public FortressEffect onEnd = new FortressEffect();
    }

    [Serializable]
    public sealed class GreenMintShaders
    {
        [Tooltip("光・輪のシェーダー（MS2026/RedSkill/Radial。赤と共通）。")]
        [SkillLabel("光・輪")]
        public Shader radial;

        [Tooltip("キラキラ・光の尾のシェーダー（MS2026/RedSkill/Particle。赤と共通）。")]
        [SkillLabel("キラキラ・光の尾")]
        public Shader particle;

        [Tooltip("ジェルのシェーダー（MS2026/GreenSkill/Gel）。")]
        [SkillLabel("ジェル")]
        public Shader gel;

        [Tooltip("粉・葉っぱ・十字のシェーダー（MS2026/GreenSkill/Sprite）。")]
        [SkillLabel("粉・葉っぱ")]
        public Shader sprite;
    }
}
