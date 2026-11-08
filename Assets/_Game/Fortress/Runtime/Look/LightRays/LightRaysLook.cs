using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MS2026.Fortress
{
    /// <summary>
    /// 画面に斜めに差し込む光の筋（見た目だけ）。設定は <see cref="Settings"/>（ステージの見た目の設定 StageLookProfile.lightRays を、
    /// ステージが開いたときに入れる）。Play 中だけ、ゲームのカメラ（<see cref="FortressCameraRig"/> のカメラ）が描くたびに
    /// <see cref="LightRaysPass"/> を差し込む。シーン・レンダラーの設定ファイルは触らない。通信しない。
    /// </summary>
    public static class LightRaysLook
    {
        private const string ShaderName = "Hidden/MS2026/Fortress/LightRays";

        private static Material _material;
        private static LightRaysPass _pass;
        private static bool _shaderMissingLogged;

        /// <summary>今の光の筋の設定。null なら出さない（ステージが無いシーン）。</summary>
        public static LightRaysSettings Settings { get; set; }

        private static Camera _editorPreviewCamera;

        /// <summary>
        /// 編集中のプレビュー（ステージ背景スタジオの「ゲームと同じ見え方で見る」）で、Play していなくても光の筋を描くカメラ。
        /// null なら今まで通り Play 中だけ。
        /// </summary>
        public static Camera EditorPreviewCamera
        {
            get => _editorPreviewCamera;
            set
            {
                _editorPreviewCamera = value;
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
                if (value != null || Application.isPlaying)
                {
                    RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            _editorPreviewCamera = null;
            Settings = null;
            _shaderMissingLogged = false;
            _pass = null;
            if (_material != null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            var settings = Settings;
            if (settings == null || !settings.enabled || settings.intensity <= 0f || !IsGameCamera(camera) || !EnsurePass())
            {
                return;
            }

            _pass.Setup(settings, camera);
            camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(_pass);
        }

        // ゲームの画面を映すカメラだけ（シーンビュー・重ねがけのカメラ・プレビュー用のカメラには出さない）。
        private static bool IsGameCamera(Camera camera)
        {
            if (camera.cameraType != CameraType.Game)
            {
                return false;
            }

            var preview = !Application.isPlaying && _editorPreviewCamera != null && _editorPreviewCamera == camera;
            var rig = FortressCameraRig.Active;
            if (!preview && (rig == null || rig.targetCamera != camera))
            {
                return false;
            }

            var data = camera.GetUniversalAdditionalCameraData();
            return data != null && data.renderType == CameraRenderType.Base && data.scriptableRenderer != null;
        }

        private static bool EnsurePass()
        {
            if (_pass != null)
            {
                return true;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                if (!_shaderMissingLogged)
                {
                    _shaderMissingLogged = true;
                    Debug.LogWarning($"[LightRays] シェーダー '{ShaderName}' が見つからないので、光の筋は出しません。");
                }

                return false;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new LightRaysPass(_material);
            return true;
        }
    }
}
