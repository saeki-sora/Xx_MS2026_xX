using MS2026.Fortress;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MS2026.CyberCleanLook
{
    /// <summary>
    /// 電脳クリーニングの見た目（世界の色・靄の色・WAVE の画面演出）の設定。見た目だけで、ゲームの進行には関わらない。
    /// Resources/CyberCleanSettings に置くと使われ、無ければ下の初期値で動く。
    /// メニュー「Tools/電脳クリーニングの見た目/設定を開く（無ければ作る）」で作れる。Play 中に変えた値は残る
    /// （世界の色と光は1秒ごとに入れ直し、画面演出は次に鳴るときから効く）。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/ステージ/電脳クリーニングの見た目の設定", fileName = "CyberCleanSettings")]
    public sealed class CyberCleanSettings : ScriptableObject
    {
        public const string ResourcePath = "CyberCleanSettings";

        [Tooltip("この見た目にするステージのシーン名。")]
        public string[] stageScenes = { "Stage1" };

        [Header("水色とラベンダーの世界")]
        [Tooltip("ONなら、床と床の外の色を変える（材質のファイルは書き換えず、Play 中だけの複製に色を入れる）。")]
        public bool recolorFloor = true;

        [Tooltip("丸い床の色。材質に模様の絵があるときは、その絵に掛ける色になる。")]
        public Color floorColor = new Color(0.64f, 0.93f, 0.98f);

        [Tooltip("床の外（いちばん大きい床）に掛ける色。")]
        public Color outerTint = new Color(0.82f, 0.74f, 1f);

        [Tooltip("ONなら、ステージの光（太陽・まわりの明るさ）をこの下の値にする。ステージ背景スタジオの「光」ページの値より優先する。")]
        public bool overrideLight = true;

        public Color sunColor = new Color(1f, 0.97f, 1f);
        [Range(0f, 4f)] public float sunIntensity = 0.85f;
        [Range(-180f, 180f)] public float sunDirection = -46f;
        [Range(5f, 90f)] public float sunElevation = 45f;
        [Range(0f, 1f)] public float shadowStrength = 0.8f;
        public Color skyColor = new Color(0.72f, 0.66f, 0.92f);
        public Color equatorColor = new Color(0.54f, 0.5f, 0.74f);
        public Color groundColor = new Color(0.36f, 0.3f, 0.5f);

        [Tooltip("ONなら、明るい所の白飛びを映画風に抑える（スタジオの「まぶしさを抑える: 映画風」と同じ）。")]
        public bool cinematicToneMapping = true;

        [Header("靄を電子ノイズの色に")]
        [Tooltip("ONなら、画面のふちの靄の色を変える（形や濃さは今の設定のまま）。")]
        public bool recolorMist = true;

        public Color mistColor = new Color(0.13f, 0.03f, 0.22f, 1f);
        public Color mistShadeTint = new Color(0.78f, 0.55f, 1f, 1f);
        public Color mistEdgeBandColor = new Color(1f, 0.35f, 0.85f, 1f);
        [Range(0f, 1f)] public float mistEdgeBandStrength = 0.24f;
        public Color mistEnemyRimColor = new Color(0.6f, 1.6f, 2.4f, 1f);

        [Header("WAVE 開始の走査線")]
        public bool scanOnWaveStart = true;
        [Tooltip("線が画面の上から下まで走る秒数。")]
        [Range(0.2f, 3f)] public float scanSeconds = 0.9f;
        public Color scanColor = new Color(0.6f, 0.97f, 1f, 0.45f);
        public Color scanGridColor = new Color(0.55f, 0.88f, 1f, 0.2f);
        public FortressEffect onScan = new FortressEffect();

        [Header("WAVE クリアでキラーン")]
        public bool kiraOnWaveClear = true;
        [Range(0.3f, 3f)] public float kiraSeconds = 1.1f;
        [Tooltip("光の強さ（0〜1）。展示会場では強すぎないようにする。")]
        [Range(0f, 1f)] public float kiraStrength = 0.8f;

        [Tooltip("光の大きさの倍率（1 で最初に作った大きさ）。光の線の長さ・輪・星・キラキラの広がりをまとめて変える。")]
        [Range(0.2f, 1.5f)] public float kiraScale = 0.6f;

        public Color kiraColor = new Color(0.45f, 0.95f, 1f, 1f);

        [Tooltip("キラーンの光の線の、もう1つの色（線が交互にこの色になる）。")]
        public Color kiraAccentColor = new Color(1f, 0.55f, 0.9f, 1f);

        [Tooltip("キラーンを描く順番。HUD（100）より大きいと HUD の上に出る。")]
        public int kiraSortingOrder = 105;
        public FortressEffect onKira = new FortressEffect();

        [Header("歯みがきタイムへの泡の切り替え")]
        [Tooltip("ONなら、WAVE クリアの後、泡が HUD ごと画面を覆い、その下で歯みがきタイムに切り替わってから、泡がはじけて歯みがきタイムの画面が出る（最後の WAVE では出さない）。")]
        public bool foamOnBreak = true;

        [Tooltip("泡が画面を覆うのにかける秒数。歯みがきタイムが始まる前に覆い終わるよう、出し始めを自動で決める（先に「WAVE CLEAR!」とキラーンを見せる）。")]
        [Range(0.2f, 1.5f)] public float foamCoverSeconds = 0.5f;

        [Tooltip("歯みがきタイムが始まってから、泡がはじけ始めるまでの秒数（切り替わりを泡の下に隠す）。")]
        [Range(0f, 1.5f)] public float foamHoldSeconds = 0.25f;

        [Tooltip("泡が全部はじけ終わるまでの秒数。")]
        [Range(0.2f, 2f)] public float foamPopSeconds = 0.75f;

        [Tooltip("歯みがきタイムが来ないとき（敵がなかなか逃げないなど）に、覆ったまま待つ最長の秒数。過ぎたらはじける。")]
        [Range(1f, 10f)] public float foamMaxHoldSeconds = 6f;

        [Tooltip("画面を埋める小さな泡の数。")]
        [Range(40, 300)] public int foamBubbleCount = 150;

        [Tooltip("上に浮かぶ、虹色に光る大きな泡の数。")]
        [Range(0, 60)] public int glossyBubbleCount = 22;

        [Tooltip("泡の色（白に近い色を交互に使う）。")]
        public Color[] foamColors =
        {
            new Color(1f, 1f, 1f),
            new Color(1f, 1f, 1f),
            new Color(0.91f, 0.99f, 1f),
            new Color(0.96f, 0.93f, 1f),
            new Color(1f, 0.95f, 0.99f),
        };

        [Tooltip("泡を描く順番。ゲーム中の HUD（100）より大きいと HUD ごと覆う（切り替えらしくなる）。")]
        public int foamSortingOrder = 120;

        [Tooltip("泡が画面を覆い始めるときに鳴らす。")]
        public FortressEffect onFoam = new FortressEffect();

        [Tooltip("泡がはじけ始めるときに鳴らす。")]
        public FortressEffect onFoamPop = new FortressEffect();

        [Header("画面演出の重なり順")]
        [Tooltip("走査線を描く順番。ゲーム中の HUD（100）より小さくすると、HUD の下に出る。")]
        public int canvasSortingOrder = 90;

        private static CyberCleanSettings _fallback;

        /// <summary>置いてある設定。無ければ初期値の設定（Play 中だけのもの）。</summary>
        public static CyberCleanSettings Load()
        {
            var settings = Resources.Load<CyberCleanSettings>(ResourcePath);
            if (settings != null)
            {
                return settings;
            }

            if (_fallback == null)
            {
                _fallback = CreateInstance<CyberCleanSettings>();
                _fallback.hideFlags = HideFlags.DontSave;
            }

            return _fallback;
        }

        public bool IsStage(string sceneName)
        {
            if (stageScenes == null)
            {
                return false;
            }

            foreach (var name in stageScenes)
            {
                if (name == sceneName)
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        private const string AssetFolder = "Assets/CyberCleanLook/Resources";
        private const string AssetPath = AssetFolder + "/CyberCleanSettings.asset";

        [MenuItem("Tools/電脳クリーニングの見た目/設定を開く（無ければ作る）")]
        private static void OpenSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CyberCleanSettings>(AssetPath);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder(AssetFolder))
                {
                    AssetDatabase.CreateFolder("Assets/CyberCleanLook", "Resources");
                }

                settings = CreateInstance<CyberCleanSettings>();
                AssetDatabase.CreateAsset(settings, AssetPath);
                AssetDatabase.SaveAssets();
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Tools/電脳クリーニングの見た目/試す: 走査線（Play中）")]
        private static void TestScan() => CyberCleanScreenFx.Test(0);

        [MenuItem("Tools/電脳クリーニングの見た目/試す: キラーン（Play中）")]
        private static void TestKira() => CyberCleanScreenFx.Test(1);

        [MenuItem("Tools/電脳クリーニングの見た目/試す: 泡で洗う（Play中）")]
        private static void TestFoam() => CyberCleanScreenFx.Test(2);

        [MenuItem("Tools/電脳クリーニングの見た目/試す: 走査線（Play中）", true)]
        [MenuItem("Tools/電脳クリーニングの見た目/試す: キラーン（Play中）", true)]
        [MenuItem("Tools/電脳クリーニングの見た目/試す: 泡で洗う（Play中）", true)]
        private static bool CanTest() => Application.isPlaying;
#endif
    }
}
