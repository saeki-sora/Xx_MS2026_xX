using System;
using System.Collections;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>敗北直後だけのズームと紫の画面切替。既存リザルトの中身は変更しない。</summary>
    [DefaultExecutionOrder(1100)]
    internal sealed class GameOverIntro : MonoBehaviour
    {
        private const float ZoomSeconds = 0.6f;
        private const float DecisionSeconds = 2.3f;
        private const float FrameRate = 30f;
        private FortressCameraRig _rig;
        private Camera _camera;
        private CameraViewSettings _original;
        private CameraViewSettings _closeup;
        private Vector3 _cameraPosition;
        private Vector3 _rigPosition;
        private Quaternion _cameraRotation, _rigRotation;
        private float _cameraSize;
        private float _cameraFov;
        private Vector3 _focus;
        private float _started;
        private float _effectsStarted;
        private bool _zooming;
        private RawImage _overlay;
        private Texture2D[] _frames;
        private Texture2D[] _startFrames;
        private Action _covered;
        private readonly GameOverRippleGraphic[] _ripples = new GameOverRippleGraphic[5];

        public static GameOverIntro Begin(Transform owner, Action covered, bool showCoreRipples)
        {
            var go = new GameObject("Game Over Intro", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(owner, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 850;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var intro = go.AddComponent<GameOverIntro>();
            intro._covered = covered;
            intro.Prepare(showCoreRipples);
            intro.StartCoroutine(intro.Play());
            return intro;
        }

        private void Prepare(bool showCoreRipples)
        {
            _rig = FortressCameraRig.Active;
            _camera = _rig != null ? _rig.targetCamera : Camera.main;
            var core = FindFirstObjectByType<CoreCrystalController>();
            _focus = core != null ? core.transform.position : Vector3.zero;
            var halfHeight = 2f;
            var model = core != null ? core.GetComponentInChildren<CoreToothModel>() : null;
            if (model != null)
            {
                var renderers = model.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    _focus = bounds.center;
                    halfHeight = Mathf.Max(1f, bounds.extents.magnitude * 1.35f);
                }
            }
            if (_camera != null)
            {
                _cameraPosition = _camera.transform.position;
                _cameraRotation = _camera.transform.rotation;
                _cameraSize = _camera.orthographicSize;
                _cameraFov = _camera.fieldOfView;
                if (_rig != null)
                {
                    _rigPosition = _rig.transform.position;
                    _rigRotation = _rig.transform.rotation;
                    _original = _rig.CurrentView;
                    _closeup = _original.WithVisibleHalfHeight(Mathf.Min(halfHeight, _original.VisibleHalfHeight));
                    _closeup.center = _focus;
                }
                _zooming = core != null;
            }
            _started = Time.unscaledTime;
            _effectsStarted = Time.time;
            CoreBreakSlowMotion.BeginDecisionSequence();
            for (var i = 0; i < _ripples.Length; i++)
            {
                if (!showCoreRipples) break;
                var ring = new GameObject("White ripple", typeof(RectTransform)).AddComponent<GameOverRippleGraphic>();
                ring.transform.SetParent(transform, false);
                ring.raycastTarget = false;
                ring.enabled = false;
                _ripples[i] = ring;
            }
            _frames = Resources.LoadAll<Texture2D>("GameOverIntro");
            Array.Sort(_frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            _startFrames = Resources.LoadAll<Texture2D>("GameOverIntroStart");
            Array.Sort(_startFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
            var rect = new GameObject("Purple transition", typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _overlay = rect.GetComponent<RawImage>();
            _overlay.raycastTarget = false;
            _overlay.enabled = false;
        }

        private void LateUpdate()
        {
            if (!_zooming || _camera == null) return;
            var t = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - _started) / ZoomSeconds);
            if (_rig != null)
                _rig.ApplyViewImmediate(CameraViewSettings.Lerp(_original, _closeup, t));
            else
            {
                var position = _focus - _camera.transform.forward * Vector3.Dot(_focus - _cameraPosition, _camera.transform.forward);
                _camera.transform.position = Vector3.Lerp(_cameraPosition, position, t);
                _camera.orthographicSize = Mathf.Lerp(_cameraSize, Mathf.Min(2f, _cameraSize), t);
                _camera.fieldOfView = Mathf.Lerp(_cameraFov, Mathf.Min(20f, _cameraFov), t);
            }
            var canvasRect = (RectTransform)transform;
            for (var i = 0; i < _ripples.Length; i++)
            {
                var ring = _ripples[i];
                if (ring == null) continue;
                // 全てコア中心から一度だけ発生。負の時刻をRepeatして大きい輪が突然出るのを避ける。
                var age = Time.time - _effectsStarted - i * 0.08f;
                var phase = Mathf.Clamp01(age / 1.1f);
                ring.enabled = age >= 0f && phase < 1f;
                if (!ring.enabled) continue;
                var screen = _camera.WorldToScreenPoint(_focus);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
                var radius = Mathf.Lerp(0.08f, 6f, 1f - (1f - phase) * (1f - phase));
                var edgeScreen = _camera.WorldToScreenPoint(_focus + _camera.transform.right * radius);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, edgeScreen, null, out var edge);
                ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                ring.rectTransform.anchoredPosition = local;
                ring.rectTransform.sizeDelta = Vector2.one * Vector2.Distance(local, edge) * 2f;
                ring.color = new Color(1f, 1f, 1f, (1f - phase) * 0.8f);
            }
        }

        private IEnumerator Play()
        {
            yield return new WaitForSecondsRealtime(DecisionSeconds);
            foreach (var ring in _ripples)
                if (ring != null) ring.gameObject.SetActive(false);
            if (_startFrames.Length > 0)
            {
                _overlay.enabled = true;
                yield return Animate(_startFrames, false);
            }
            else if (_frames.Length > 0)
            {
                _overlay.enabled = true;
                yield return Animate(_frames, true);
            }
            RestoreCamera();
            var covered = _covered;
            _covered = null;
            covered?.Invoke();
            // リザルトを描くフレームを挟んでから覆いを開く。
            yield return null;
            if (_frames.Length > 0) yield return Animate(_frames, false);
            Destroy(gameObject);
        }

        private IEnumerator Animate(Texture2D[] frames, bool reverse)
        {
            var start = Time.unscaledTime;
            var duration = frames.Length / FrameRate;
            while (Time.unscaledTime - start < duration)
            {
                var index = Mathf.Min(frames.Length - 1, Mathf.FloorToInt((Time.unscaledTime - start) * FrameRate));
                _overlay.texture = frames[reverse ? frames.Length - 1 - index : index];
                yield return null;
            }
            _overlay.texture = frames[reverse ? 0 : frames.Length - 1];
        }

        private void RestoreCamera()
        {
            if (!_zooming) return;
            _zooming = false;
            if (_rig != null) _rig.transform.SetPositionAndRotation(_rigPosition, _rigRotation);
            if (_camera != null)
            {
                _camera.transform.SetPositionAndRotation(_cameraPosition, _cameraRotation);
                _camera.orthographicSize = _cameraSize;
                _camera.fieldOfView = _cameraFov;
            }
        }

        private void OnDisable()
        {
            RestoreCamera();
            CoreBreakSlowMotion.EndDecisionSequence();
        }

        public void Cancel()
        {
            _covered = null;
            StopAllCoroutines();
            var canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = false;
            RestoreCamera();
            CoreBreakSlowMotion.EndDecisionSequence();
        }
    }

    /// <summary>動画の白い輪。画像を伸ばさず、輪郭をUIメッシュで描く。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class GameOverRippleGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            var inner = Mathf.Max(0f, radius - 2.5f);
            const int segments = 64;
            for (var i = 0; i < segments; i++)
            {
                var a = i * Mathf.PI * 2f / segments;
                var b = (i + 1) * Mathf.PI * 2f / segments;
                var start = vh.currentVertCount;
                var d0 = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var d1 = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                vh.AddVert(rect.center + d0 * inner, color, Vector2.zero);
                vh.AddVert(rect.center + d0 * radius, color, Vector2.zero);
                vh.AddVert(rect.center + d1 * radius, color, Vector2.zero);
                vh.AddVert(rect.center + d1 * inner, color, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
