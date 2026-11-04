using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MS2026.Fortress
{
    /// <summary>
    /// 画面のふちの黒い靄（見た目だけ）。敵が出てくる「コアと反対側の画面の端」だけを暗い靄にして、その中を夜の見え方にする。
    /// 設定は <see cref="Settings"/>（ステージの見た目の設定 StageLookProfile.edgeMist を、ステージが開いたときに入れる）。
    /// Play 中だけ、ゲームのカメラ（<see cref="FortressCameraRig"/> のカメラ）が描くたびに <see cref="EdgeMistPass"/> を差し込む。
    /// シーン・レンダラーの設定ファイルは触らない。ネット対戦でも通信せず、各PCが自分のカメラで描く。
    /// 群衆の敵のシェーダー（SwarmSprite）も同じグローバル値を読んで、靄の中だけ縁の光を付ける。
    /// </summary>
    public static class EdgeMistLook
    {
        private const string ShaderName = "Hidden/MS2026/Fortress/EdgeMist";

        private static readonly int CoreId = Shader.PropertyToID("_FortressEdgeMistCore");
        private static readonly int ShapeId = Shader.PropertyToID("_FortressEdgeMistShape");
        private static readonly int FlowId = Shader.PropertyToID("_FortressEdgeMistFlow");
        private static readonly int RimColorId = Shader.PropertyToID("_FortressEdgeMistRimColor");
        private static readonly int RimWidthId = Shader.PropertyToID("_FortressEdgeMistRimWidth");

        private static Material _material;
        private static EdgeMistPass _pass;
        private static CoreCrystalController _core;
        private static Vector3 _coreHome;
        private static int _coreSearchFrame = -1;
        private static bool _shaderMissingLogged;
        private static Camera _smoothedCamera;
        private static Vector2 _smoothedCore;

        // 靄の中心が目標に追いつく速さ（1秒あたり）。カメラの切り替え・着地の揺れで靄がガクッと動かないように少しだけ遅らせる。
        private const float FollowSharpness = 8f;

        /// <summary>今の靄の設定。null なら靄を出さない（ステージが無いシーン）。</summary>
        public static EdgeMistSettings Settings { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Settings = null;
            _core = null;
            _coreSearchFrame = -1;
            _smoothedCamera = null;
            _shaderMissingLogged = false;
            _pass = null;
            if (_material != null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
            }

            Shader.SetGlobalVector(CoreId, Vector4.zero);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            CaptureCoreHome();
        }

        // ステージの読み込み直後（ステージの登場演出がコアを画面の外へ動かす前）に、コアの置かれている位置を覚える。
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => CaptureCoreHome();

        private static void CaptureCoreHome()
        {
            // 同じコアなら覚え直さない（登場演出の途中で別のシーンが読み込まれても、動かされた位置を覚えないように）。
            var core = Object.FindFirstObjectByType<CoreCrystalController>();
            if (core != null && core != _core)
            {
                _core = core;
                _coreHome = core.transform.position;
            }
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            var settings = Settings;
            if (settings == null || !settings.enabled || !IsGameCamera(camera) || !EnsurePass())
            {
                // 靄を描かないカメラでは、敵の縁の光も出さない。
                Shader.SetGlobalVector(CoreId, Vector4.zero);
                return;
            }

            var target = EdgeMistMath.CoreViewport(camera.WorldToViewportPoint(CorePosition()));
            if (_smoothedCamera != camera)
            {
                _smoothedCamera = camera;
                _smoothedCore = target;
            }
            else
            {
                _smoothedCore = EdgeMistMath.Follow(_smoothedCore, target, FollowSharpness, Time.unscaledDeltaTime);
            }

            var core = _smoothedCore;
            Shader.SetGlobalVector(CoreId, new Vector4(core.x, core.y, camera.aspect, 1f));
            Shader.SetGlobalVector(ShapeId, new Vector4(settings.start, settings.softness, settings.nearSideFade, settings.wobble));
            Shader.SetGlobalVector(FlowId, new Vector4(settings.wobbleScale, settings.flowSpeed, settings.cornerRoundness, 0f));
            Shader.SetGlobalColor(RimColorId, settings.enemyRimColor);
            Shader.SetGlobalFloat(RimWidthId, settings.enemyRimWidth);

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

            var rig = FortressCameraRig.Active;
            if (rig == null || rig.targetCamera != camera)
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
                    Debug.LogWarning($"[EdgeMist] シェーダー '{ShaderName}' が見つからないので、画面のふちの靄は出しません。");
                }

                return false;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new EdgeMistPass(_material);
            return true;
        }

        // コアの置かれている位置（ステージの読み込み直後に覚えた位置。登場演出で落ちてくる途中の位置は使わない）。
        // 見つからなければ地面の原点。探し直すのは1フレームに1回まで。
        private static Vector3 CorePosition()
        {
            if (_core == null && _coreSearchFrame != Time.frameCount)
            {
                _coreSearchFrame = Time.frameCount;
                CaptureCoreHome();
            }

            return _core != null ? _coreHome : Vector3.zero;
        }
    }
}
