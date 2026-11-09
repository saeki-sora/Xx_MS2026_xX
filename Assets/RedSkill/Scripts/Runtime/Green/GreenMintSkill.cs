using System;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.Serialization;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// 緑キャラのスキルの設定ファイル（緑の守護霊）。値はすべてここで調整できる（Play 中に変えてもすぐ反映される。コードで描く絵の色だけは次の Play から）。
    /// 流れ（ユーザー決定 2026-11-05、デザイナー向け決定版ページ https://claude.ai/artifact/VdRM8zUcKys2KWpbMdLsAL ）:
    /// 1 溜め … 緑の砲台の横に歯みがき粉のチューブが転がり、口から煙が漏れ続ける。
    /// 2 カットイン … 作ってあるカットイン（この間、緑の砲台は止まる）。
    /// 3 降臨 … コアに光の柱、画面の上から巨大な守護霊が降りてきて「ドン」と着地（コアが回復）、葉の輪が広がる。一回り小さくなってコアの背後に収まる。
    /// 4 葉で守る … 8枚の葉がコアの周りを回り、輪が寄せては返す。葉に触れた敵にダメージ。コアはじわじわ回復。
    /// 5 終わり … 葉と守護霊が渦になってコアへ吸い込まれ、コアがきらっと光る。
    /// 飛び道具は無い（葉に触れた敵だけにダメージ）。実際に動かすのは <see cref="GreenMintSkillRunner"/>（スキル欄が自動で付ける）。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/スキル/グリーン：守護霊", fileName = "GreenMintSkill")]
    public sealed class GreenMintSkill : CharacterSkill
    {
        [Tooltip("降臨からおしまいまでの流れと長さ（秒は、カットインが終わって守護霊の場面が始まってから）。")]
        [SkillTab(SkillTab.Level)]
        [SkillLabel("タイミング")]
        public GreenGuardianTiming timing = new GreenGuardianTiming();

        [Tooltip("コアの回復量。")]
        [SkillTab(SkillTab.Level)]
        [SkillLabel("コアの回復")]
        public GreenMintHeal heal = new GreenMintHeal();

        [Tooltip("コアの周りを回る葉の輪（触れた敵にダメージ）。")]
        [SkillTab(SkillTab.Level)]
        [SkillLabel("葉の輪（攻撃）")]
        public GreenLeafRingSettings leaves = new GreenLeafRingSettings();

        [Tooltip("スキル中、緑のプレイヤーが握ったときのおまけ（輪が速く寄せては返し、葉が大きく光る）。")]
        [SkillTab(SkillTab.Level)]
        [SkillLabel("握ったときの強化")]
        public GreenGuardianBoost boost = new GreenGuardianBoost();

        [Tooltip("守護霊の大きさ・動き。")]
        [SkillTab(SkillTab.Look)]
        [SkillLabel("守護霊")]
        public GreenSpiritSettings spirit = new GreenSpiritSettings();

        [Tooltip("使える間、緑の砲台の横に転がる歯みがき粉のチューブ。")]
        [SkillTab(SkillTab.Look)]
        [SkillLabel("溜めの歯みがき粉チューブ")]
        public GreenTubeSettings tube = new GreenTubeSettings();

        [Tooltip("デザイナーの絵に差し替える欄。空の欄は、コードで描いた仮の絵を使う。")]
        [SkillTab(SkillTab.Look)]
        [SkillLabel("絵（差し替え）")]
        public GreenGuardianArtSlots art = new GreenGuardianArtSlots();

        [Tooltip("全体の色。コードで描く仮の絵もこの色から作る。")]
        [SkillTab(SkillTab.Look)]
        [SkillLabel("色")]
        public GreenGuardianPalette palette = new GreenGuardianPalette();

        [Tooltip("守護霊が着地する「ドン」と、おしまいの「きらっ」の演出（全員の画面に出る）。")]
        [SkillTab(SkillTab.Look)]
        [SkillLabel("ドンときらっの演出")]
        public GreenMintBurstFx burst = new GreenMintBurstFx();

        [Tooltip("カメラの揺れ。")]
        [SkillTab(SkillTab.Look)]
        [SkillLabel("カメラの揺れ")]
        public GreenMintCameraShake cameraShake = new GreenMintCameraShake();

        [Tooltip("効果音・声と、D-Drive の追加エフェクト（空なら何もしない）。")]
        [SkillTab(SkillTab.Look)]
        [SkillLabel("効果音・声・追加エフェクト（D-Drive）")]
        public GreenGuardianSounds sounds = new GreenGuardianSounds();

        [Tooltip("見た目に使うシェーダー（通常は触らない）。")]
        [SkillTab(SkillTab.Hidden)]
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
            displayName = "ミントの守護霊";
            description = "光の柱とともに巨大な守護霊が降りてきて、着地の衝撃でコアを大きく回復する。その後はコアの周りを8枚の葉が寄せては返しながら回り、触れた敵を倒す。コアもじわじわ回復する。";
            themeColor = new Color(0.25f, 0.8f, 0.5f);
        }

        /// <summary>守護霊の場面が始まってから終わるまでの合計秒数。</summary>
        public float TotalSeconds => timing.GuardEnd + timing.suckSeconds;
    }

    [Serializable]
    public sealed class GreenGuardianTiming
    {
        [Tooltip("光の柱が差してから、守護霊が降り始めるまでの秒数。")]
        [SkillLabel("降り始めるまで（秒）")]
        [Min(0f)]
        public float descendStart = 0.35f;

        [Tooltip("守護霊が着地して「ドン」と回復するまでの秒数（場面の始まりから）。ここから葉の輪が出て、攻撃が始まる。")]
        [SkillLabel("ドンまで（秒）")]
        [Min(0.1f)]
        public float landSeconds = 1.5f;

        [Tooltip("着地の後、葉の輪が広がりきるまでの秒数。")]
        [SkillLabel("葉の輪が広がる時間（秒）")]
        [Min(0.05f)]
        public float leafAppearSeconds = 0.8f;

        [Tooltip("守護霊が一回り小さくなり始める秒数（場面の始まりから）。")]
        [SkillLabel("縮み始める（秒）")]
        [Min(0f)]
        public float shrinkStart = 2.7f;

        [Tooltip("守護霊が小さくなり終わる秒数（場面の始まりから）。")]
        [SkillLabel("縮み終わる（秒）")]
        [Min(0f)]
        public float shrinkEnd = 3.5f;

        [Tooltip("降臨の始まりから、葉と守護霊がコアへ吸い込まれ始めるまでの秒数（葉で守る時間を含む）。")]
        [SkillLabel("守る時間（降臨から・秒）")]
        [Min(0.5f)]
        public float guardSeconds = 12f;

        [Tooltip("葉と守護霊がコアへ吸い込まれて、コアがきらっと光るまでの秒数。")]
        [SkillLabel("吸い込まれる時間（秒）")]
        [Min(0.1f)]
        public float suckSeconds = 1f;

        /// <summary>ドンの秒数（場面の始まりから）。</summary>
        public float Land => Mathf.Max(0.1f, landSeconds);

        /// <summary>吸い込みが始まる秒数（場面の始まりから。ドンより後）。</summary>
        public float GuardEnd => Mathf.Max(Land + 0.5f, guardSeconds);
    }

    [Serializable]
    public sealed class GreenMintHeal
    {
        [Tooltip("守護霊が着地した瞬間に回復する量（コアの最大体力に対する %）。")]
        [SkillLabel("ドンと回復（%）")]
        [Range(0f, 100f)]
        public float burstPercent = 20f;

        [Tooltip("葉で守っている間に、合わせてじわじわ回復する量（コアの最大体力に対する %）。")]
        [SkillLabel("じわじわ回復（合計 %）")]
        [Range(0f, 100f)]
        public float regenPercent = 15f;

        [Tooltip("ONなら、試合が終わった後（クリア・ゲームオーバー）は回復しない。")]
        [SkillLabel("試合が終わったら回復しない")]
        public bool stopWhenMatchEnded = true;
    }

    [Serializable]
    public sealed class GreenLeafRingSettings
    {
        [Tooltip("コアの周りを回る葉の数。")]
        [SkillLabel("葉の数")]
        [Range(1, 16)]
        public int count = 8;

        [Tooltip("輪が一番縮んだときの半径（m）。コアのすぐそば。")]
        [SkillLabel("一番小さい半径（m）")]
        [Min(0.3f)]
        public float minRadius = 2.6f;

        [Tooltip("輪が一番広がったときの半径（m）。画面の縦は約28m、砲台はコアから6m。")]
        [SkillLabel("一番大きい半径（m）")]
        [Min(0.3f)]
        public float maxRadius = 8.4f;

        [Tooltip("輪が縮んで広がって、元に戻るまでの秒数（寄せては返す1回ぶん）。")]
        [SkillLabel("寄せては返す間隔（秒）")]
        [Min(0.2f)]
        public float pulseSeconds = 3.2f;

        [Tooltip("1枚おきに、寄せては返すのを少し遅らせる（1回ぶんに対する割合）。波のように見える。")]
        [SkillLabel("1枚おきの遅れ（割合）")]
        [Range(0f, 0.5f)]
        public float alternateDelay = 0.07f;

        [Tooltip("葉が回る速さ（度/秒）。")]
        [SkillLabel("回る速さ（度/秒）")]
        public float spinDegreesPerSecond = 63f;

        [Tooltip("葉1枚の長さ（m、軸を含む）。")]
        [SkillLabel("葉の長さ（m）")]
        [Min(0.1f)]
        public float length = 1.75f;

        [Tooltip("葉がゆらゆら傾く大きさ（度）。")]
        [SkillLabel("ゆらゆらの傾き（度）")]
        [Min(0f)]
        public float wobbleDegrees = 12.6f;

        [Tooltip("葉に触れた敵に与えるダメージ（ふつうの敵の体力は 10 なので一撃。硬い敵は何度か当たると倒れる）。")]
        [SkillLabel("触れたときのダメージ")]
        [Min(0f)]
        public float damage = 14f;

        [Tooltip("当たり判定の半径（m、葉の中心から。ここに敵の大きさが足される）。")]
        [SkillLabel("当たり判定の半径（m）")]
        [Min(0.05f)]
        public float hitRadius = 0.9f;

        [Tooltip("同じ敵に、次に当たるまでの最短の秒数（硬い敵が1回の接触で溶けないように）。")]
        [SkillLabel("同じ敵に当たる間隔（秒）")]
        [Min(0.05f)]
        public float rehitSeconds = 0.4f;

        [Tooltip("1フレームに当てる最大数（群衆の当たり判定の枠は全員で1フレーム32本まで。余った敵は次のフレームに当たる）。")]
        [SkillLabel("1フレームに当てる最大数")]
        [Range(1, 16)]
        public int maxHitsPerFrame = 10;

        [Tooltip("ONなら、Actor の敵（1体ずつ動く敵）にも当たる。")]
        [SkillLabel("Actor の敵にも当たる")]
        public bool hitActors = true;

        [Tooltip("葉の後ろに伸びる光の尾の長さ（秒ぶん）。0 で出さない。")]
        [SkillLabel("光の尾の長さ（秒）")]
        [Min(0f)]
        public float trailSeconds = 0.26f;

        [Tooltip("光の尾の太さ（葉の長さに対する倍率）。")]
        [SkillLabel("光の尾の太さ（倍）")]
        [Min(0f)]
        public float trailWidth = 0.37f;

        [Tooltip("ONなら、葉の通り道にうすい光の輪を出す（輪といっしょに寄せては返す）。")]
        [SkillLabel("うすい光の輪を出す")]
        public bool showPathRing = true;

        [Tooltip("当たった所に出るミントの粉の数。")]
        [SkillLabel("当たったときの粉の数")]
        [Range(0, 30)]
        public int hitPowder = 5;

        [Tooltip("当たった所に出る光の粒の数。")]
        [SkillLabel("当たったときの光の粒の数")]
        [Range(0, 30)]
        public int hitSparkles = 7;

        [Tooltip("当たった所に出る白い線（斬ったような線）の長さ（m）。0 で出さない。")]
        [SkillLabel("当たったときの白い線（m）")]
        [Min(0f)]
        public float hitSlashSize = 3f;
    }

    [Serializable]
    public sealed class GreenGuardianBoost
    {
        [Tooltip("握りきったときの、寄せては返す間隔（秒）。")]
        [SkillLabel("握りきったときの間隔（秒）")]
        [Min(0.2f)]
        public float pulseSeconds = 2.2f;

        [Tooltip("握りきったときの、葉が回る速さ（度/秒）。")]
        [SkillLabel("握りきったときの回る速さ（度/秒）")]
        public float spinDegreesPerSecond = 109f;

        [Tooltip("握りきったときの葉の大きさ（倍）。当たり判定も同じだけ大きくなる。")]
        [SkillLabel("握りきったときの葉の大きさ（倍）")]
        [Min(1f)]
        public float sizeScale = 1.22f;

        [Tooltip("握る強さの変化をなめらかにする速さ（大きいほどすぐ反応）。")]
        [SkillLabel("反応の速さ")]
        [Min(0.5f)]
        public float response = 8f;
    }

    [Serializable]
    public sealed class GreenSpiritSettings
    {
        [Tooltip("降りてくるときの守護霊の高さ（m）。画面の縦は約28m なので、ほぼいっぱい。")]
        [SkillLabel("降りてくるときの高さ（m）")]
        [Min(1f)]
        public float giantHeight = 26f;

        [Tooltip("コアの背後に残っている間の高さ（m）。")]
        [SkillLabel("残っている間の高さ（m）")]
        [Min(1f)]
        public float stayHeight = 11f;

        [Tooltip("降り始める位置（コアから画面の上へ何 m か）。")]
        [SkillLabel("降り始める高さ（m）")]
        [Min(0f)]
        public float startAbove = 30f;

        [Tooltip("守護霊の体の濃さ（0〜1）。輪郭の線はいつも濃い。")]
        [SkillLabel("体の濃さ")]
        [Range(0.1f, 1f)]
        public float fillOpacity = 0.74f;

        [Tooltip("残っている間、ゆらゆら浮かぶ幅（m）。")]
        [SkillLabel("ゆらゆらの幅（m）")]
        [Min(0f)]
        public float bobAmount = 0.15f;

        [Tooltip("ゆらゆら浮かぶ速さ（1秒に何ラジアン）。")]
        [SkillLabel("ゆらゆらの速さ")]
        [Min(0f)]
        public float bobSpeed = 1.6f;

        [Tooltip("体のまわりの光の強さ（降りてくる間 / 着地の瞬間 / 残っている間）。")]
        [SkillLabel("まわりの光（降りる・着地・残る）")]
        public Vector3 glow = new Vector3(0.7f, 1f, 0.3f);

        [Tooltip("しっぽの煙の太さ（根元＝守護霊の高さに対する割合、先＝m）。")]
        [SkillLabel("しっぽの太さ（根元の割合・先 m）")]
        public Vector2 tailWidth = new Vector2(0.14f, 0.42f);

        [Tooltip("しっぽの煙の区切りの数（多いほどなめらか）。")]
        [SkillLabel("しっぽのなめらかさ")]
        [Range(8, 96)]
        public int tailSegments = 44;
    }

    [Serializable]
    public sealed class GreenTubeSettings
    {
        [Tooltip("ONなら、使える間、緑の砲台の横に歯みがき粉のチューブを出す。")]
        [SkillLabel("チューブを出す")]
        public bool enabled = true;

        [Tooltip("チューブの長さ（m）。")]
        [SkillLabel("長さ（m）")]
        [Min(0.2f)]
        public float length = 2.2f;

        [Tooltip("砲台の中心から、画面の右・上へずらす距離（m）。どのプレイヤーの画面でも、チューブは画面に対して同じ向きで転がる。")]
        [SkillLabel("置く場所（画面の右・上 m）")]
        public Vector2 screenOffset = new Vector2(1.25f, 0.35f);

        [Tooltip("チューブの傾き（度、画面で見て左回り）。口が右上を向く。")]
        [SkillLabel("傾き（度）")]
        public float tiltDegrees = 31.5f;

        [Tooltip("口から漏れる煙の数（1秒あたり）。")]
        [SkillLabel("漏れる煙（毎秒）")]
        [Min(0f)]
        public float smokeRate = 16f;

        [Tooltip("「ぼふっ」と大きな煙が出る間隔（秒）。")]
        [SkillLabel("ぼふっの間隔（秒）")]
        [Min(0.2f)]
        public float puffInterval = 1.5f;

        [Tooltip("「ぼふっ」で出る煙の数。")]
        [SkillLabel("ぼふっの煙の数")]
        [Range(0, 30)]
        public int puffSmoke = 7;

        [Tooltip("出てくるときの秒数（ポンと弾んで出る）。")]
        [SkillLabel("出てくる時間（秒）")]
        [Min(0.01f)]
        public float appearSeconds = 0.35f;
    }

    /// <summary>守護霊の絵の入れ方。</summary>
    public enum GreenSpiritArtMode
    {
        [InspectorName("ポーズごとの1枚絵")]
        PosePictures,

        [InspectorName("パーツ分け（体と両腕）")]
        Parts
    }

    [Serializable]
    public sealed class GreenGuardianArtSlots
    {
        [Tooltip("守護霊の絵の入れ方。ポーズごとの1枚絵なら、ポーズの間は絵を重ねて切り替える。パーツ分けなら、両腕を肩で回してポーズを作る。")]
        [SkillLabel("守護霊の絵の入れ方")]
        public GreenSpiritArtMode spiritMode = GreenSpiritArtMode.PosePictures;

        [Tooltip("1枚絵: 両手を上げて降りてくる姿。空ならコードで描いた仮の絵。絵の高さ＝守護霊の高さ、絵の中心（Pivot）がコアに重なる。")]
        [SkillLabel("1枚絵: 両手を上げて降りる")]
        public Sprite poseDescend;

        [Tooltip("1枚絵: 着地して両手を広げた姿。")]
        [SkillLabel("1枚絵: 両手を広げる")]
        public Sprite poseSpread;

        [Tooltip("1枚絵: コアを抱える姿（残っている間ずっと）。")]
        [SkillLabel("1枚絵: コアを抱える")]
        public Sprite poseHug;

        [Tooltip("パーツ: 腕の無い体（頭・髪・コート）。絵の高さ＝守護霊の高さ、絵の中心（Pivot）がコアに重なる。")]
        [SkillLabel("パーツ: 体")]
        public Sprite partBody;

        [Tooltip("パーツ: 左腕（画面で見て左）。絵の中心（Pivot）を肩に置くこと。")]
        [SkillLabel("パーツ: 左腕")]
        public Sprite partArmLeft;

        [Tooltip("パーツ: 右腕（画面で見て右）。絵の中心（Pivot）を肩に置くこと。")]
        [SkillLabel("パーツ: 右腕")]
        public Sprite partArmRight;

        [Tooltip("パーツ: 左右の肩の位置（守護霊の高さを 1 とした割合。右肩はこの左右を反転）。")]
        [SkillLabel("パーツ: 肩の位置")]
        public Vector2 shoulder = new Vector2(-0.11f, 0.27f);

        [Tooltip("パーツ: 左腕の角度（度。降りる / 広げる / 抱える）。右腕は左右反転。")]
        [SkillLabel("パーツ: 腕の角度（降りる・広げる・抱える）")]
        public Vector3 armDegrees = new Vector3(-40f, 10f, 75f);

        [Tooltip("しっぽの煙の付け根（守護霊の高さを 1 とした割合。絵の中心から）。")]
        [SkillLabel("しっぽの付け根")]
        public Vector2 tailRoot = new Vector2(0f, -0.17f);

        [Tooltip("葉っぱ1枚の絵（先を右に向けて描く）。空ならコードで描いた仮の絵。絵の横幅＝葉の長さ。")]
        [SkillLabel("葉っぱ")]
        public Sprite leaf;

        [Tooltip("歯みがき粉のチューブの絵（口を右に向けて描く）。空ならコードで描いた仮の絵。絵の横幅＝チューブの長さ。")]
        [SkillLabel("歯みがき粉のチューブ")]
        public Sprite tube;

        [Tooltip("チューブの口の位置（チューブの長さを 1 とした割合。絵の中心から）。")]
        [SkillLabel("チューブの口の位置")]
        public Vector2 tubeNozzle = new Vector2(0.52f, 0f);

        [Tooltip("煙の粒の絵（丸いもの）。空ならコードで描いた仮の絵。")]
        [SkillLabel("煙の粒")]
        public Texture2D smoke;
    }

    [Serializable]
    public sealed class GreenGuardianPalette
    {
        [Tooltip("守護霊の体の色（上・まん中・下のグラデーション）。")]
        [SkillLabel("体（上）")]
        public Color spiritTop = new Color(0.973f, 1f, 0.878f);

        [SkillLabel("体（まん中）")]
        public Color spiritMiddle = new Color(0.89f, 1f, 0.75f);

        [SkillLabel("体（下）")]
        public Color spiritBottom = new Color(0.776f, 0.965f, 0.635f);

        [Tooltip("守護霊の輪郭の線（濃く描く）。")]
        [SkillLabel("体の輪郭")]
        public Color spiritOutline = new Color(0.373f, 0.878f, 0.29f);

        [Tooltip("守護霊の服・髪の細い線。")]
        [SkillLabel("体の細い線")]
        public Color spiritLines = new Color(0.31f, 0.78f, 0.24f, 0.6f);

        [Tooltip("守護霊のまわりの光。")]
        [SkillLabel("体のまわりの光")]
        public Color spiritGlow = new Color(0.51f, 1f, 0.39f, 0.95f);

        [Tooltip("葉っぱの色（明るい側・濃い側・ふち・葉脈）。")]
        [SkillLabel("葉（明るい側）")]
        public Color leafLight = new Color(0.608f, 0.961f, 0.541f);

        [SkillLabel("葉（濃い側）")]
        public Color leafDark = new Color(0.169f, 0.682f, 0.322f);

        [SkillLabel("葉のふち")]
        public Color leafEdge = new Color(0.055f, 0.353f, 0.18f);

        [SkillLabel("葉脈")]
        public Color leafVein = new Color(0.9f, 1f, 0.84f, 0.85f);

        [Tooltip("煙の色（中・ふち）。")]
        [SkillLabel("煙")]
        public Color smoke = new Color(0.894f, 1f, 0.769f, 0.42f);

        [SkillLabel("煙のふち")]
        public Color smokeEdge = new Color(0.39f, 0.86f, 0.31f, 0.3f);

        [Tooltip("回復の十字（＋）。")]
        [SkillLabel("回復の十字")]
        public Color cross = new Color(0.357f, 0.918f, 0.494f);

        [Tooltip("ミントの粉。")]
        [SkillLabel("ミントの粉")]
        public Color powder = new Color(0.94f, 1f, 0.96f, 0.85f);

        [Tooltip("衝撃波・光の輪。")]
        [SkillLabel("衝撃波の輪")]
        public Color ring = new Color(0.59f, 1f, 0.67f, 1f);

        [Tooltip("光るところの色（HDR。加算で重ねる光・キラキラ）。")]
        [SkillLabel("光（HDR）")]
        [ColorUsage(false, true)]
        public Color glow = new Color(0.75f, 1.6f, 0.8f);

        [Tooltip("チューブの色（本体・緑の帯・輪郭）。")]
        [SkillLabel("チューブ（本体）")]
        public Color tubeBody = new Color(0.969f, 1f, 0.976f);

        [SkillLabel("チューブ（緑）")]
        public Color tubeGreen = new Color(0.239f, 0.859f, 0.416f);

        [SkillLabel("チューブ（輪郭）")]
        public Color tubeOutline = new Color(0.141f, 0.125f, 0.498f);
    }

    [Serializable]
    public sealed class GreenMintBurstFx
    {
        [Tooltip("ドン: コアから広がる衝撃波の、最後の大きさ（直径 m）。2本目は 0.7 倍で少し遅れて出る。")]
        [SkillLabel("ドン: 衝撃波の大きさ（m）")]
        [Min(0f)]
        public float waveSize = 40f;

        [Tooltip("ドン: 衝撃波が広がりきるまでの秒数。")]
        [SkillLabel("ドン: 衝撃波の時間（秒）")]
        [Min(0.05f)]
        public float waveSeconds = 1.1f;

        [Tooltip("ドン: コアの光の大きさ（直径 m）。")]
        [SkillLabel("ドン: コアの光の大きさ（m）")]
        [Min(0f)]
        public float flashSize = 12f;

        [Tooltip("ドン: 画面のふちを緑に光らせる濃さ（0〜1）。0 で光らせない。")]
        [SkillLabel("ドン: 画面のふちの光")]
        [Range(0f, 1f)]
        public float screenEdgeStrength = 0.5f;

        [Tooltip("ドン: 画面全体が白く光る濃さ（0〜1）。")]
        [SkillLabel("ドン: 画面の白い光")]
        [Range(0f, 1f)]
        public float screenFlash = 0.22f;

        [Tooltip("ドン: 画面のふちの光が消えるまでの秒数。")]
        [SkillLabel("ドン: ふちの光の時間（秒）")]
        [Min(0.05f)]
        public float screenEdgeSeconds = 1.1f;

        [Tooltip("ドン: コアから飛び散る光の粒の数。")]
        [SkillLabel("ドン: 光の粒の数")]
        [Range(0, 300)]
        public int sparkleCount = 70;

        [Tooltip("ドン: コアから舞う葉っぱの数。")]
        [SkillLabel("ドン: 葉っぱの数")]
        [Range(0, 60)]
        public int leafCount = 18;

        [Tooltip("ドン: コアから浮かぶ回復の十字（＋）の数。")]
        [SkillLabel("ドン: 回復の十字の数")]
        [Range(0, 30)]
        public int crossCount = 10;

        [Tooltip("ドン: 着地で舞い上がる煙の数。")]
        [SkillLabel("ドン: 煙の数")]
        [Range(0, 60)]
        public int smokeCount = 28;

        [Tooltip("葉で守っている間、コアから回復の十字（＋）が浮かぶ数（1秒あたり）。")]
        [SkillLabel("守っている間の十字（毎秒）")]
        [Min(0f)]
        public float regenCrossRate = 2.2f;

        [Tooltip("光の柱の太さ（m）と、出ている秒数。")]
        [SkillLabel("光の柱（太さ m・秒）")]
        public Vector2 pillar = new Vector2(3.2f, 1.9f);

        [Tooltip("きらっ: おしまいにコアが光る大きさ（直径 m）。")]
        [SkillLabel("きらっ: 光の大きさ（m）")]
        [Min(0f)]
        public float endFlashSize = 7f;
    }

    [Serializable]
    public sealed class GreenMintCameraShake
    {
        [Tooltip("ONなら、緑のプレイヤーの画面だけ揺らす（OFFなら全員の画面が揺れる）。")]
        [SkillLabel("緑の人の画面だけ揺らす")]
        public bool ownerViewOnly;

        [Tooltip("守護霊が着地した瞬間の揺れ。")]
        [SkillLabel("着地の揺れ")]
        [FormerlySerializedAs("burst")]
        public SimpleShakeSettings land = new SimpleShakeSettings { amplitude = 0.3f, rollAmplitude = 0.6f, frequency = 16f, duration = 0.6f };

        [Tooltip("着地の揺れの強さ（0で揺らさない）。")]
        [SkillLabel("着地の揺れの強さ")]
        [FormerlySerializedAs("burstStrength")]
        [Min(0f)]
        public float landStrength = 1f;
    }

    [Serializable]
    public sealed class GreenGuardianSounds
    {
        [Tooltip("1 溜め: 使えるようになった瞬間（チューブの位置）。")]
        [SkillLabel("溜まった")]
        public FortressEffect onReady = new FortressEffect();

        [Tooltip("1 溜め: チューブから「ぼふっ」と煙が出るたび（チューブの口）。")]
        [SkillLabel("ぼふっ（チューブの煙）")]
        public FortressEffect onTubePuff = new FortressEffect();

        [Tooltip("3 降臨: 光の柱が差した瞬間（コアの位置）。")]
        [SkillLabel("光の柱")]
        public FortressEffect onPillar = new FortressEffect();

        [Tooltip("3 降臨: 守護霊が着地した「ドン」（コアの位置）。")]
        [SkillLabel("ドン（着地）")]
        public FortressEffect onLand = new FortressEffect();

        [Tooltip("3 降臨: 着地のときの守護霊の声（コアの位置）。")]
        [SkillLabel("ドンの声")]
        public FortressEffect landVoice = new FortressEffect();

        [Tooltip("4 葉で守る: 守っている間ずっと（コアについていく。VFX は Loop にしておく）。")]
        [SkillLabel("守っている間（ループ）")]
        public FortressEffect whileGuarding = new FortressEffect();

        [Tooltip("4 葉で守る: 葉が敵に当たったとき（当たった場所。数が多いので下の間隔で間引く）。")]
        [SkillLabel("葉が当たる")]
        public FortressEffect onLeafHit = new FortressEffect();

        [Tooltip("「葉が当たる」を鳴らす最短の間隔（秒）。")]
        [SkillLabel("葉が当たる音の最短間隔（秒）")]
        [Min(0f)]
        public float leafHitInterval = 0.07f;

        [Tooltip("5 終わり: 葉と守護霊がコアへ吸い込まれ始めた瞬間（コアの位置）。")]
        [SkillLabel("吸い込まれる")]
        public FortressEffect onSuck = new FortressEffect();

        [Tooltip("5 終わり: コアがきらっと光った瞬間（コアの位置）。")]
        [SkillLabel("コアきらっ")]
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

        [Tooltip("守護霊のしっぽの煙のシェーダー（MS2026/GreenSkill/Tail）。")]
        [SkillLabel("しっぽの煙")]
        [FormerlySerializedAs("gel")]
        public Shader tail;

        [Tooltip("絵（守護霊・葉・粉・煙・十字）のシェーダー（MS2026/GreenSkill/Sprite）。")]
        [SkillLabel("絵")]
        public Shader sprite;
    }
}
