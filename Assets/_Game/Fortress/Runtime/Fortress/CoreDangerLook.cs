using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MS2026.Fortress
{
    /// <summary>
    /// コアが危ないほど、画面全体が少しずつ赤く熱っぽくなり、ふちが赤く染まる（HP がかなり減るとふちが鼓動のように脈打つ）。
    /// 設定は <see cref="CoreCrystalController.dangerLook"/>。<see cref="CoreCrystalController"/> が起動時に自動で付ける。
    /// 画面効果（URP の Volume。色の調整＋ふちの暗さ）を、シーンに保存しない別オブジェクトとして作り、危なさに合わせて重みを変える。
    /// ステージの光の設定が作る Volume（まぶしさ・露出）とは別の項目しか触らないので、両方とも効く。
    /// ライト・ステージの設定・カメラの揺れには触れない。初めて危なくなったときに、ゲームのカメラのポストエフェクトをONにする
    /// （ステージの「まぶしさを抑える」と同じ。何も効果が無ければ見た目は変わらない）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CoreCrystalController))]
    public sealed class CoreDangerLook : MonoBehaviour
    {
        /// <summary>
        /// 要塞デザイナーの「テスト」タブ用。0以上なら、本当の HP の代わりにこの値（0〜1）で画面の色を決める。見た目だけ。-1 で解除。
        /// </summary>
        public static float PreviewHealth01 = -1f;

        private const string ObjectName = "[Core] Danger Look";

        private CoreCrystalController _core;
        private Volume _volume;
        private ColorAdjustments _color;
        private Vignette _vignette;
        private float _danger;
        private bool _postProcessingEnabled;

        private void Awake()
        {
            _core = GetComponent<CoreCrystalController>();
        }

        private void OnDisable()
        {
            if (_volume != null)
            {
                _volume.weight = 0f;
            }
        }

        private void OnDestroy()
        {
            if (_volume != null)
            {
                Destroy(_volume.sharedProfile);
                Destroy(_volume.gameObject);
            }
        }

        private void Update()
        {
            var settings = _core.dangerLook;
            var health01 = PreviewHealth01 >= 0f
                ? Mathf.Clamp01(PreviewHealth01)
                : _core.maxHealth > 0f ? Mathf.Clamp01(_core.CurrentHealth / _core.maxHealth) : 1f;
            var target = settings != null && settings.enabled ? CoreDangerMath.Danger(health01, settings.startHealth01) : 0f;
            var response = settings != null ? settings.response : 1.5f;
            _danger = Mathf.MoveTowards(_danger, target, response * Time.deltaTime);

            if (_danger <= 0.001f)
            {
                if (_volume != null)
                {
                    _volume.weight = 0f;
                }

                return;
            }

            EnsureVolume();
            EnablePostProcessingOnce();

            _color.colorFilter.Override(settings.tint);
            _color.saturation.Override(settings.saturation);
            var pulse = CoreDangerMath.Pulse(health01, settings.pulseBelowHealth01, settings.pulsePerSecond, Time.time);
            _vignette.color.Override(settings.edgeColor);
            _vignette.intensity.Override(Mathf.Clamp01(settings.edgeStrength * pulse));
            _volume.weight = _danger;
        }

        private void EnsureVolume()
        {
            if (_volume != null)
            {
                return;
            }

            // コアの子にはしない（コアが壊れて消えても、最後の赤さが急に消えないように）。
            // Play中のシーンの物として作るので、Playを止めれば必ず消える（DontSave にすると、赤い画面がPlay後も残ってしまう）。
            var go = new GameObject(ObjectName);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            // ステージの光の Volume（優先度10）より後に重ねる。触る項目が違うので、ステージ側の設定はそのまま効く。
            _volume.priority = 20f;
            _volume.weight = 0f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = ObjectName;
            profile.hideFlags = HideFlags.DontSave;
            _color = profile.Add<ColorAdjustments>();
            _vignette = profile.Add<Vignette>();
            _vignette.smoothness.Override(0.5f);
            _vignette.rounded.Override(false);
            _volume.sharedProfile = profile;
        }

        private void EnablePostProcessingOnce()
        {
            if (_postProcessingEnabled)
            {
                return;
            }

            _postProcessingEnabled = true;
            foreach (var camera in Camera.allCameras)
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            PreviewHealth01 = -1f;
        }
    }
}
