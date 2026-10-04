using System;
using System.Collections.Generic;
using MS2026.Fortress.Billboards;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 各視点の見え方を、シーンに保存されない非表示のプレビュー用カメラでRenderTextureに描く。
    /// 本物のカメラ・リグには一切触らないので、Play中でも4人分を同時に確認できる。
    /// Play外では、プリセットの「絵を立たせる」設定を描画の間だけ一時的に適用し、描き終えたらすぐ元に戻す(シーンには残らない)。
    /// </summary>
    public sealed class CameraPreviewRenderer : IDisposable
    {
        private const int MinWidth = 64;
        private const int MaxWidth = 1024;

        private readonly Dictionary<int, RenderTexture> _textures = new Dictionary<int, RenderTexture>();
        private GameObject _cameraObject;
        private Camera _camera;
        private BillboardController _editModeBillboards;

        public Texture Get(int viewer) => _textures.TryGetValue(viewer, out var texture) ? texture : null;

        public void RenderAll(CameraTabContext context, int width)
        {
            var preset = context.Preset;
            if (preset == null)
            {
                return;
            }

            var aspect = context.Aspect;
            width = Mathf.Clamp(width, MinWidth, MaxWidth);
            var height = Mathf.Max(1, Mathf.RoundToInt(width / aspect));
            var source = context.Rig != null && context.Rig.targetCamera != null ? context.Rig.targetCamera : Camera.main;

            EnsureCamera();
            BeginEditModeBillboards(preset);
            try
            {
                foreach (var viewer in ViewerIndex.All)
                {
                    var texture = EnsureTexture(viewer, width, height);
                    CopySettings(source);
                    CameraPoseApplier.Apply(_cameraObject.transform, _camera, preset.GetView(viewer), preset.nearClip, preset.farClip);
                    _camera.aspect = aspect;
                    Render(texture);
                }
            }
            finally
            {
                EndEditModeBillboards();
            }
        }

        // Play中はBillboardDirectorが適用しているので何もしない。
        private void BeginEditModeBillboards(CameraViewPreset preset)
        {
            if (Application.isPlaying || preset.billboard == null || !preset.billboard.enabled)
            {
                return;
            }

            _editModeBillboards ??= new BillboardController();
            _editModeBillboards.Apply(preset.billboard, true);
        }

        private void EndEditModeBillboards()
        {
            if (!Application.isPlaying)
            {
                _editModeBillboards?.Restore();
            }
        }

        public void Dispose()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;

            _editModeBillboards?.Dispose();
            _editModeBillboards = null;

            foreach (var texture in _textures.Values)
            {
                if (texture != null)
                {
                    texture.Release();
                    Object.DestroyImmediate(texture);
                }
            }

            _textures.Clear();

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

            _cameraObject = new GameObject("CameraViewPreview") { hideFlags = HideFlags.HideAndDontSave };
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.enabled = false;

            // スクリプトの再読み込みで参照が失われると隠しオブジェクトが残り続けるため、その前に片付ける。
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        }

        private void CopySettings(Camera source)
        {
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
        }

        private RenderTexture EnsureTexture(int viewer, int width, int height)
        {
            if (_textures.TryGetValue(viewer, out var existing) && existing != null && existing.width == width && existing.height == height)
            {
                return existing;
            }

            if (existing != null)
            {
                existing.Release();
                Object.DestroyImmediate(existing);
            }

            var texture = new RenderTexture(width, height, 24) { hideFlags = HideFlags.HideAndDontSave, name = $"CameraViewPreview_{viewer}" };
            texture.Create();
            _textures[viewer] = texture;
            return texture;
        }

        private void Render(RenderTexture texture)
        {
            // URP等のSRPではRenderRequest経由で描く。未対応のパイプラインでは従来のCamera.Renderにフォールバックする。
            var request = new RenderPipeline.StandardRequest { destination = texture };
            if (RenderPipeline.SupportsRenderRequest(_camera, request))
            {
                RenderPipeline.SubmitRenderRequest(_camera, request);
                return;
            }

            _camera.targetTexture = texture;
            _camera.Render();
            _camera.targetTexture = null;
        }
    }
}
