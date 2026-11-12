using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MS2026.PlayerFloatShadow
{
    /// <summary>
    /// プレイヤーが浮いて見える影の設定（見た目だけ）。Resources/PlayerFloatShadowSettings に置くと使われ、無ければ下の初期値で動く。
    /// メニュー「Tools/プレイヤーの浮いた影/設定を開く（無ければ作る）」で作れる。Play 中に変えるとすぐ効き、値は残る。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/ステージ/プレイヤーの浮いた影の設定", fileName = "PlayerFloatShadowSettings")]
    public sealed class PlayerFloatShadowSettings : ScriptableObject
    {
        public const string ResourcePath = "PlayerFloatShadowSettings";

        [Tooltip("OFFにすると、元の足もとの丸い影に戻す。")]
        public bool enabled = true;

        [Header("浮いている高さ")]
        [Tooltip("床から浮いている高さ（メートル）。高いほど影が体から離れる。")]
        [Range(0f, 5f)] public float hoverHeight = 1.2f;

        [Tooltip("ふわふわ上下する幅（メートル）。0で止まる。")]
        [Range(0f, 1f)] public float bobHeight = 0.22f;

        [Tooltip("ふわふわが1往復する秒数。")]
        [Range(0.3f, 8f)] public float bobSeconds = 2.4f;

        [Header("影の見た目")]
        [Tooltip("影の直径（体の大きさに対する倍率）。")]
        [Range(0.2f, 2f)] public float size = 0.8f;

        [Tooltip("影の濃さ（0〜1）。")]
        [Range(0f, 1f)] public float darkness = 0.42f;

        [Tooltip("高く上がったときに影が広がる量。")]
        [Range(0f, 1f)] public float spreadWhenHigh = 0.2f;

        [Tooltip("高く上がったときに影が薄くなる量。")]
        [Range(0f, 1f)] public float fadeWhenHigh = 0.3f;

        [Header("影の出る向き")]
        [Tooltip("ONなら、ステージの太陽と反対側へ影を出す。OFFなら下の向きへ出す。")]
        public bool followSun = true;

        [Tooltip("太陽を使わないとき、または太陽が真上のときの影の向き（床の上）。")]
        public Vector2 fallbackDirection = new Vector2(0.4f, -1f);

        [Tooltip("影が体から離れる距離の下限（メートル）。太陽が真上でも、この分は離れて見える。")]
        [Range(0f, 3f)] public float minimumOffset = 0.5f;

        [Tooltip("影が体から離れる距離の上限（メートル）。太陽が低くても、これより離れない。")]
        [Range(0.5f, 8f)] public float maximumOffset = 2.5f;

        [Tooltip("試合の開始演出の後、影が現れるまでの秒数。")]
        [Range(0f, 3f)] public float appearSeconds = 0.5f;

        private static PlayerFloatShadowSettings _fallback;

        /// <summary>置いてある設定。無ければ初期値の設定（Play 中だけのもの）。</summary>
        public static PlayerFloatShadowSettings Load()
        {
            var settings = Resources.Load<PlayerFloatShadowSettings>(ResourcePath);
            if (settings != null)
            {
                return settings;
            }

            if (_fallback == null)
            {
                _fallback = CreateInstance<PlayerFloatShadowSettings>();
                _fallback.hideFlags = HideFlags.DontSave;
            }

            return _fallback;
        }

#if UNITY_EDITOR
        private const string AssetFolder = "Assets/PlayerFloatShadow/Resources";
        private const string AssetPath = AssetFolder + "/PlayerFloatShadowSettings.asset";

        [MenuItem("Tools/プレイヤーの浮いた影/設定を開く（無ければ作る）")]
        private static void OpenSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PlayerFloatShadowSettings>(AssetPath);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder(AssetFolder))
                {
                    AssetDatabase.CreateFolder("Assets/PlayerFloatShadow", "Resources");
                }

                settings = CreateInstance<PlayerFloatShadowSettings>();
                AssetDatabase.CreateAsset(settings, AssetPath);
                AssetDatabase.SaveAssets();
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
#endif
    }
}
