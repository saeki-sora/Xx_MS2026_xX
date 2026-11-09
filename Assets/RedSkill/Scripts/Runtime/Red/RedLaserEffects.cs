using System;
using UnityEngine;

namespace MS2026.Skills.Red
{
    /// <summary>
    /// 極太レーザーの演出ごとの「付け外し」と「強さのメーター」（2026-11-09、スキル調整スタジオ用）。
    /// それぞれの演出の細かい値（色・数・速さ…）は各欄（稲妻・派手さ・光の奔流…）にあり、ここでは出す／出さないと強さの倍率だけを決める。
    /// 最初の状態はすべて ON・強さ 1（＝それまでの見た目に「光の奔流」の光と影を足した見た目）。
    /// </summary>
    [Serializable]
    public sealed class RedLaserEffects
    {
        [Tooltip("溜め中・撃っている間・当たった場所に出る、赤い稲妻（ビリビリ）。強さは稲妻の数に掛かる。")]
        [SkillLabel("ビリビリの赤い稲妻")]
        public SkillEffectDial lightning = new SkillEffectDial();

        [Tooltip("レーザーの中を流れる、斜めのしま模様。強さはしまの濃さに掛かる。")]
        [SkillLabel("斜めのしま模様")]
        public SkillEffectDial stripes = new SkillEffectDial();

        [Tooltip("発射口から出て、レーザーに沿って先へ飛ぶ光の輪。強さは輪の明るさに掛かる。")]
        [SkillLabel("レーザーを進む輪")]
        public SkillEffectDial beamRings = new SkillEffectDial();

        [Tooltip("根元から先へ走る明るい帯（光の波）。強さは帯の明るさに掛かる。")]
        [SkillLabel("光の波")]
        public SkillEffectDial lightWave = new SkillEffectDial();

        [Tooltip("レーザーのまわりのにじむ光（外側の光）。強さは明るさに掛かる。")]
        [SkillLabel("外側のにじむ光")]
        public SkillEffectDial halo = new SkillEffectDial();

        [Tooltip("レーザーの中を流れる細いスジ模様。強さはスジの濃さに掛かる。")]
        [SkillLabel("流れるスジ")]
        public SkillEffectDial streaks = new SkillEffectDial();

        [Tooltip("レーザーのふちのギザギザ（エネルギーが荒れる感じ）。強さはギザギザの大きさに掛かる。")]
        [SkillLabel("ふちのギザギザ")]
        public SkillEffectDial edgeJag = new SkillEffectDial();

        [Tooltip("発射口のトゲ付きの光と、その後ろで回る大きな光。強さは明るさに掛かる。")]
        [SkillLabel("発射口の光")]
        public SkillEffectDial muzzleFlare = new SkillEffectDial();

        [Tooltip("発射口から飛び散る火花・吹き出すトゲ状の光・レーザーに沿って舞う粒・走る光の線。強さは数に掛かる。")]
        [SkillLabel("発射口とレーザーの粒")]
        public SkillEffectDial muzzleParticles = new SkillEffectDial();

        [Tooltip("壁に当たった場所の光・横に広がる光・火花・衝撃の輪。強さは明るさと数に掛かる。")]
        [SkillLabel("当たった場所の光と火花")]
        public SkillEffectDial impact = new SkillEffectDial();

        [Tooltip("撃ち始め・撃っている間・当たっている間のカメラの揺れ。強さは揺れの強さに掛かる。")]
        [SkillLabel("カメラの揺れ")]
        public SkillEffectDial cameraShake = new SkillEffectDial();

        [Header("光の奔流（細かい値は「光の奔流」の欄）")]
        [Tooltip("レーザーの真ん中の、白く焼けた芯の光と、赤い光がゆらめく動き。強さは芯の明るさとゆらめきに掛かる。")]
        [SkillLabel("白く焼けた芯")]
        public SkillEffectDial hotCore = new SkillEffectDial();

        [Tooltip("レーザーのまわりの床を赤く照らす光。障害物の後ろは影になる（光は回り込まない）。強さは光の強さに掛かる。")]
        [SkillLabel("床を照らす光")]
        public SkillEffectDial floorLight = new SkillEffectDial();

        [Tooltip("レーザーのまわりの景色が熱でゆらぐ陽炎。強さはゆらぎの大きさに掛かる。")]
        [SkillLabel("陽炎")]
        public SkillEffectDial heatHaze = new SkillEffectDial();

        [Tooltip("障害物の後ろで途切れる所（影のふち）を、やわらかくぼかして明るいふちを付ける。強さはぼかす幅に掛かる。OFF でくっきり切れる。")]
        [SkillLabel("影のふちのやわらかさ")]
        public SkillEffectDial softCut = new SkillEffectDial();

        [Tooltip("当たった場所から、壁に沿って横へ流れる線の火花。強さは数に掛かる。")]
        [SkillLabel("壁に沿って流れる火花")]
        public SkillEffectDial wallSparks = new SkillEffectDial();
    }

    /// <summary>
    /// 「光の奔流」の見た目（見本ページの B、2026-11-09 にユーザーが選んだ）の細かい値。
    /// 白く焼けた芯から赤い光があふれ、床まで赤く照らす。障害物の後ろは影になり、影のふちは少しだけやわらかい。陽炎でまわりがゆらぐ。
    /// どれも障害物の裏側には描かない（床の光も、壁で止まった筋の先には届かない）。
    /// </summary>
    [Serializable]
    public sealed class RedLaserGlowFx
    {
        [Header("白く焼けた芯")]
        [Tooltip("真ん中に足す白い光の強さ（1 より大きいと強く光る）。")]
        [SkillLabel("芯の明るさ")]
        [Min(0f)]
        public float coreGlow = 0.7f;

        [Tooltip("芯の太さ（レーザーの太さに対する割合）。")]
        [SkillLabel("芯の太さ")]
        [Range(0.05f, 0.6f)]
        public float coreWidth = 0.22f;

        [Tooltip("赤い光が先へ流れながらゆらめく強さ（0でゆらめかない）。")]
        [SkillLabel("光のゆらめき")]
        [Range(0f, 1f)]
        public float flowNoise = 0.35f;

        [Tooltip("光のゆらめきが流れる速さ（メートル/秒）。")]
        [SkillLabel("ゆらめきの速さ")]
        public float flowSpeed = 9f;

        [Header("床を照らす光")]
        [Tooltip("床の光の色（HDR）。床の色に掛けて足すので、明るい床でも白く飛びにくい。")]
        [SkillLabel("光の色")]
        [ColorUsage(false, true)]
        public Color floorLightColor = new Color(1.6f, 0.35f, 0.4f, 1f);

        [Tooltip("床を赤く染める強さ（0〜1）。緑と青を少し暗くして、赤い光に照らされた色にする。")]
        [SkillLabel("赤く染める強さ")]
        [Range(0f, 1f)]
        public float floorTint = 0.45f;

        [Tooltip("床の光が届く幅（見た目のレーザーの太さに対する倍率）。")]
        [SkillLabel("光の幅（倍）")]
        [Range(1f, 5f)]
        public float floorLightWidth = 2.6f;

        [Tooltip("当たった場所のまわりの床が明るくなる強さ。")]
        [SkillLabel("当たった所の床の明るさ")]
        [Min(0f)]
        public float floorContactGlow = 1.2f;

        [Header("陽炎")]
        [Tooltip("ゆらぎの強さ（景色が明るく・暗くゆれる大きさ）。")]
        [SkillLabel("ゆらぎの強さ")]
        [Range(0f, 0.4f)]
        public float hazeStrength = 0.09f;

        [Tooltip("陽炎の幅（見た目のレーザーの太さに対する倍率）。")]
        [SkillLabel("陽炎の幅（倍）")]
        [Range(1f, 4f)]
        public float hazeWidth = 1.7f;

        [Tooltip("ゆらぎの細かさ（大きいほど細かく揺れる）。")]
        [SkillLabel("ゆらぎの細かさ")]
        public float hazeScale = 1.3f;

        [Tooltip("ゆらぎが動く速さ。")]
        [SkillLabel("ゆらぎの速さ")]
        public float hazeSpeed = 3f;

        [Header("影のふち")]
        [Tooltip("障害物の後ろで途切れる所をぼかす幅（メートル）。ぼかすのは途切れる側（障害物の後ろではない側）だけ。")]
        [SkillLabel("ぼかす幅（m）")]
        [Range(0f, 1f)]
        public float cutSoftness = 0.35f;

        [Tooltip("途切れる所に付ける明るいふちの強さ。")]
        [SkillLabel("明るいふち")]
        [Range(0f, 2f)]
        public float cutFringe = 0.9f;

        [Header("壁に沿って流れる火花")]
        [Tooltip("火花の数（毎秒・当たっている部分が全部のとき）。")]
        [SkillLabel("火花の数（毎秒）")]
        [Min(0f)]
        public float wallSparkRate = 260f;

        [Tooltip("火花の速さ（メートル/秒）。")]
        [SkillLabel("火花の速さ")]
        public float wallSparkSpeed = 20f;

        [Tooltip("火花の色。")]
        [SkillLabel("火花の色")]
        public Color wallSparkColor = new Color(1f, 0.62f, 0.45f, 1f);

        [Header("描く順番")]
        [Tooltip("床の光と陽炎を描く順番。敵（群衆）より下に描くため、敵の描く順番（0）以下にしておく。")]
        [SkillLabel("床の光・陽炎の描く順番")]
        public int floorSortingOrder = 0;
    }
}
