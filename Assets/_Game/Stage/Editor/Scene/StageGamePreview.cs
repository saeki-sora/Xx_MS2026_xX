using System;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// ゲームのカメラと同じ位置・向き・遠近で、シーンを絵（RenderTexture）に描く（配置ページのゲーム画面）。
    /// シーンに保存されない隠しカメラを使い、本物のカメラ・リグには触らない（Play中でも使える）。
    /// </summary>
    public sealed class StageGamePreview : IDisposable
    {
        private GameObject _cameraObject;
        private Camera _camera;
        private RenderTexture _texture;

        public Texture Texture => _texture;

        /// <summary>最後に描いたときのカメラ（画面の点から視線を出す・ワールドの点を画面に写すのに使う）。描く前は null。</summary>
        public Camera Camera => _camera;

        /// <summary>その視点のゲームのカメラで、width×height ピクセルの絵を描く。</summary>
        public bool Render(int viewer, int width, int height)
        {
            var preset = StageGameCamera.Preset;
            if (preset == null)
            {
                return false;
            }

            width = Mathf.Clamp(width, 32, 2048);
            height = Mathf.Clamp(height, 32, 2048);
            var aspect = width / (float)height;
            EnsureCamera();
            EnsureTexture(width, height);

            var rig = StageGameCamera.Rig;
            var source = rig != null && rig.targetCamera != null ? rig.targetCamera : Camera.main;
            if (source != null)
            {
                _camera.CopyFrom(source);
            }
            else
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            }

            _camera.enabled = false;
            _camera.targetTexture = null;
            CameraPoseApplier.Apply(_cameraObject.transform, _camera, preset.GetView(viewer), preset.nearClip, preset.farClip);
            _camera.aspect = aspect;

            var request = new RenderPipeline.StandardRequest { destination = _texture };
            if (RenderPipeline.SupportsRenderRequest(_camera, request))
            {
                RenderPipeline.SubmitRenderRequest(_camera, request);
            }
            else
            {
                _camera.targetTexture = _texture;
                _camera.Render();
                _camera.targetTexture = null;
            }

            return true;
        }

        public void Dispose()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            if (_texture != null)
            {
                _texture.Release();
                Object.DestroyImmediate(_texture);
                _texture = null;
            }

            if (_cameraObject != null)
            {
                Object.DestroyImmediate(_cameraObject);
            }

            _cameraObject = null;
            _camera = null;
        }

        private void EnsureCamera()
        {
            if (_camera != null)
            {
                return;
            }

            _cameraObject = new GameObject("StageGamePreview") { hideFlags = HideFlags.HideAndDontSave };
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.enabled = false;

            // スクリプトの再読み込みで参照が失われると隠しオブジェクトが残り続けるため、その前に片付ける。
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        }

        private void EnsureTexture(int width, int height)
        {
            if (_texture != null && _texture.width == width && _texture.height == height)
            {
                return;
            }

            if (_texture != null)
            {
                _texture.Release();
                Object.DestroyImmediate(_texture);
            }

            _texture = new RenderTexture(width, height, 24) { hideFlags = HideFlags.HideAndDontSave, name = "StageGamePreview" };
            _texture.Create();
        }
    }
}
