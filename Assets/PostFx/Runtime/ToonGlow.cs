using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MS2026.PostFx
{
    /// <summary>
    /// 試し：エフェクトだけを光らせる Bloom（光のにじみ）を、対戦の画面（Game シーン）に自動でかける（2026-10-29）。
    /// シーンのファイルは触らず、Play を始めたときに Volume を作ってカメラの後処理を ON にするだけ。
    /// 光る強さなどは Resources/ToonGlowProfile をインスペクターで変える（Play 中に変えても残る）。
    /// Threshold を 1 より上にして、キャラやステージの色（1 以下）はにじませず、HDR の色や加算合成の重なりだけを光らせる。
    /// Play 中に F9 で ON/OFF を切り替えて見比べられる。メニュー「Tools/光のにじみ（Bloom）/試しにかける」で最初から止めることもできる。
    /// </summary>
    public sealed class ToonGlow : MonoBehaviour
    {
        public const string ObjectName = "[PostFx] Toon Glow Volume";
        private const string ProfilePath = "ToonGlowProfile";
        private const string EditorPrefKey = "MS2026.PostFx.ToonGlow.Enabled";

        // カメラがあるシーン。ここが読み込まれているときだけかける（タイトルやキャラ選択の見た目は変えない）。
        private static readonly string[] TargetScenes = { "Game", "redskill_testscene" };

        private static ToonGlow _instance;
        private Volume _volume;

        /// <summary>メニューで止めていなければ ON（ビルドでは常に ON）。</summary>
        public static bool EnabledByDefault
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetBool(EditorPrefKey, true);
#else
                return true;
#endif
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Refresh();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Refresh();

        private static void Refresh()
        {
            if (!IsTargetSceneLoaded())
            {
                if (_instance != null)
                {
                    _instance._volume.enabled = false;
                }

                return;
            }

            if (_instance == null)
            {
                var profile = Resources.Load<VolumeProfile>(ProfilePath);
                if (profile == null)
                {
                    Debug.LogWarning($"[ToonGlow] Resources/{ProfilePath} が見つからないので Bloom はかけません。");
                    return;
                }

                var go = new GameObject(ObjectName);
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<ToonGlow>();
                _instance._volume = go.AddComponent<Volume>();
                _instance._volume.isGlobal = true;
                // ステージの光（優先度10）やコアのピンチ演出（20）より下。触る項目が違うので、どちらの設定もそのまま効く。
                _instance._volume.priority = 5f;
                _instance._volume.sharedProfile = profile;
                _instance._volume.enabled = EnabledByDefault;
            }
            else
            {
                _instance._volume.enabled = EnabledByDefault;
            }

            EnablePostProcessing();
        }

        private static bool IsTargetSceneLoaded()
        {
            foreach (var name in TargetScenes)
            {
                if (SceneManager.GetSceneByName(name).isLoaded)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>カメラの後処理を ON にする（OFF のままだと Volume が効かない）。</summary>
        private static void EnablePostProcessing()
        {
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera.cameraType != CameraType.Game)
                {
                    continue;
                }

                var data = camera.GetUniversalAdditionalCameraData();
                if (data != null && data.renderType == CameraRenderType.Base && !data.renderPostProcessing)
                {
                    data.renderPostProcessing = true;
                }
            }
        }

        // F9 の切り替えは見比べるための試し機能なので、エディタと開発用ビルドだけ（展示のビルドで誤って押しても変わらないように）。
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void HandleToggleKey()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame && IsTargetSceneLoaded())
            {
                _volume.enabled = !_volume.enabled;
                Debug.Log($"[ToonGlow] Bloom {(_volume.enabled ? "ON" : "OFF")}");
            }
        }

        private void Update()
        {
            HandleToggleKey();
        }

#if UNITY_EDITOR
        private const string MenuPath = "Tools/光のにじみ（Bloom）/試しにかける";

        [MenuItem(MenuPath)]
        private static void ToggleMenu()
        {
            EditorPrefs.SetBool(EditorPrefKey, !EnabledByDefault);
            if (_instance != null && IsTargetSceneLoaded())
            {
                _instance._volume.enabled = EnabledByDefault;
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleMenuValidate()
        {
            Menu.SetChecked(MenuPath, EnabledByDefault);
            return true;
        }
#endif
    }
}
