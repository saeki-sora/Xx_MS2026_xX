using System;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using UnityEngine;

namespace MS2026.Stages
{
    /// <summary>
    /// 試合の始まりの演出「ステージの物が上から落ちてくる」（<see cref="StageDropIntro"/>）の設定。全ステージ共通で、Resources に 1 つ置く。
    /// ステージごとに切るときは、そのステージの <see cref="StageDefinition.playDropIntro"/> を OFF にする。
    /// 1 つの物だけ落とさないときは、その物に <see cref="StageDropIntroSkip"/> を付ける。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/ステージ/開始の演出（落ちてくる）の設定", fileName = "StageDropIntroSettings")]
    public sealed class StageDropIntroSettings : ScriptableObject
    {
        public const string ResourcePath = "StageDropIntroSettings";

        [Tooltip("OFF にすると、どのステージでも演出をしない（物は最初から置いてある）。")]
        public bool enabled = true;

        [Header("始まり")]
        [Tooltip("幕が全部消えて盤面が見えてから、最初の物が落ち始めるまでの秒数。")]
        [Min(0f)]
        public float startDelay = 0.15f;

        [Tooltip("画面の上のふちから、さらにどれだけ外から落とすか（画面の高さに対する割合）。")]
        [Min(0f)]
        public float startAboveScreen = 0.15f;

        [Header("順番と間隔")]
        [Tooltip("障害物を落とし始めてから、最後の障害物が落ち始めるまでの秒数（数が多くても、この中に収める）。")]
        [Min(0f)]
        public float propsSpreadSeconds = 0.45f;

        [Tooltip("障害物どうしの間隔の上限（秒）。数が少ないときは、この間隔で落とす。")]
        [Min(0f)]
        public float propsMaxInterval = 0.12f;

        [Tooltip("最後の障害物が着地してから、コアが落ち始めるまでの秒数（マイナスなら着地より前に落ち始める）。")]
        public float coreGap = 0f;

        [Tooltip("コアが着地してから、1P が落ち始めるまでの秒数。")]
        public float playersGap = 0.15f;

        [Tooltip("プレイヤーどうしの間隔（秒）。1P → 2P → 3P → 4P の順に落ちる。")]
        [Min(0f)]
        public float playersInterval = 0.1f;

        [Header("落ち方（種類ごと）")]
        public StageDropKindSettings props = StageDropKindSettings.Props();

        public StageDropKindSettings core = StageDropKindSettings.Core();

        public StageDropKindSettings players = StageDropKindSettings.Players();

        [Header("終わり")]
        [Tooltip("全部が着地し終わった瞬間の演出（D-Drive の番号札。空なら鳴らさない）。")]
        public FortressEffect onFinished = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("ON なら、演出の間は HUD（コアのバー・プレイヤー欄など）を隠し、終わったらスッと出す。")]
        public bool hideHud = true;

        [Tooltip("HUD を出すときのフェードの秒数。")]
        [Min(0f)]
        public float hudFadeSeconds = 0.35f;

        [Tooltip("隠す HUD の画面の名前（UIスタジオの画面 ID）。")]
        public string[] hudScreens = { "Hud" };

        [Tooltip("ON なら、演出の間はレーザーを撃てない（砲台がまだ落ちてきていないため）。")]
        public bool lockTurrets = true;

        [Tooltip("幕が消えるのを待つ最長の秒数（幕が終わらないときの保険。過ぎたら演出なしで置く）。")]
        [Min(1f)]
        public float maxWaitSeconds = 20f;

        /// <summary>種類ごとの設定。</summary>
        public StageDropKindSettings Get(StageDropKind kind) => kind switch
        {
            StageDropKind.Core => core,
            StageDropKind.Player => players,
            _ => props
        };

        private static StageDropIntroSettings _loaded;
        private static StageDropIntroSettings _fallback;

        /// <summary>Resources の設定（無ければ既定値の設定）。</summary>
        public static StageDropIntroSettings Load()
        {
            if (_loaded == null)
            {
                _loaded = Resources.Load<StageDropIntroSettings>(ResourcePath);
            }

            if (_loaded != null)
            {
                return _loaded;
            }

            if (_fallback == null)
            {
                _fallback = CreateInstance<StageDropIntroSettings>();
                _fallback.hideFlags = HideFlags.DontSave;
            }

            return _fallback;
        }
    }

    /// <summary>落ちてくる物の種類。この順に落ちる。</summary>
    public enum StageDropKind
    {
        /// <summary>障害物（壁・飾り・壊せる物。床は落とさない）。</summary>
        Prop,

        /// <summary>コア（歯）。</summary>
        Core,

        /// <summary>プレイヤーの砲台（キャラの絵）。</summary>
        Player
    }

    /// <summary>1 種類の物の落ち方。</summary>
    [Serializable]
    public sealed class StageDropKindSettings
    {
        [Tooltip("落ち始めてから着地するまでの秒数（だんだん速く落ちる）。")]
        [Min(0.05f)]
        public float fallSeconds = 0.45f;

        [Tooltip("落ちている間に、落ちる向きへ伸びる量（0.1 なら 10%）。")]
        [Range(0f, 0.5f)]
        public float fallStretch = 0.08f;

        [Tooltip("着地でつぶれる量（0.2 なら横に 20% 広がり、縦に 20% 縮む）。")]
        [Range(0f, 0.6f)]
        public float squash = 0.22f;

        [Tooltip("つぶれてから元に戻るまでの秒数。")]
        [Min(0.01f)]
        public float squashSeconds = 0.24f;

        [Tooltip("着地の瞬間の演出（土ぼこり・ドスンの音など。D-Drive の番号札。空なら鳴らさない）。位置は着地した所。")]
        public FortressEffect onLand = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("着地の瞬間の画面の揺れ。揺らさないなら「揺れの大きさ」を 0 に。")]
        public SimpleShakeSettings landShake = new SimpleShakeSettings { amplitude = 0.08f, rollAmplitude = 0.15f, frequency = 20f, duration = 0.15f };

        /// <summary>着地してから元の形に戻るまでを含めた秒数。</summary>
        public float TotalSeconds => fallSeconds + squashSeconds;

        public static StageDropKindSettings Props() => new StageDropKindSettings();

        public static StageDropKindSettings Core() => new StageDropKindSettings
        {
            fallSeconds = 0.55f,
            squash = 0.32f,
            squashSeconds = 0.28f,
            landShake = new SimpleShakeSettings { amplitude = 0.35f, rollAmplitude = 0.6f, frequency = 18f, duration = 0.35f }
        };

        public static StageDropKindSettings Players() => new StageDropKindSettings
        {
            fallSeconds = 0.4f,
            squash = 0.18f,
            squashSeconds = 0.22f,
            onLand = new FortressEffect { tintWithPlayerColor = true },
            landShake = new SimpleShakeSettings { amplitude = 0.1f, rollAmplitude = 0.2f, frequency = 20f, duration = 0.15f }
        };
    }
}
