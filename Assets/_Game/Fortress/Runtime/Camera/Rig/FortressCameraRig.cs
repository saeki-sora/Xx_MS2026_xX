using System;
using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// ゲームのカメラを動かす親オブジェクト。「誰の視点か」を決め(<see cref="LocalViewerResolver"/>)、
    /// プリセットの視点をなめらかに補間し(<see cref="CameraViewBlend"/>)、一時演出を重ねて(<see cref="CameraModifierStack"/>)、
    /// 最後に自分のTransformとCameraへ書き込む。
    ///
    /// Cameraはこのオブジェクトの子に「位置・回転ゼロ」で置く。動かすのは常にこのリグ(親)の方なので、
    /// D-DriveのCameraFxがカメラの直上に揺れ用ノードを挟み込んでも、揺れとリグの動きが素直に足し合わされる。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class FortressCameraRig : MonoBehaviour
    {
        [Tooltip("使う視点セット。ゲーム中に SetPreset で切り替えることもできる。")]
        public CameraViewPreset preset;

        [Tooltip("動かすカメラ。未設定なら子オブジェクトから自動で探す。")]
        public Camera targetCamera;

        [Tooltip("通信・起動引数のどちらでもプレイヤーが決まらないときの視点。-1で全体視点、0-3でそのプレイヤー。")]
        [Range(-1, 3)]
        public int fallbackViewer = ViewerIndex.Overview;

        [Header("切り替え")]
        [Tooltip("視点の持ち主が変わった(デバッグ切替など)ときに補間する秒数。0で即座に切り替わる。")]
        [Min(0f)]
        public float viewerSwitchBlendSeconds = 0.35f;

        [Tooltip("補間の緩急。横=時間(0-1) 縦=進み具合(0-1)。")]
        public AnimationCurve blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("構図ガイド")]
        public CameraGuideSettings guides = new CameraGuideSettings();

        private readonly CameraViewBlend _blend = new CameraViewBlend();
        private readonly CameraModifierStack _modifiers = new CameraModifierStack();
        private LocalViewerResolver _viewerResolver;
        private bool _initialized;

        /// <summary>シーンで有効なリグ(1つだけ置く想定)。演出やデバッグ機能はここから辿る。</summary>
        public static FortressCameraRig Active { get; private set; }

        /// <summary>現在の視点の持ち主(<see cref="ViewerIndex"/>)。</summary>
        public int CurrentViewer { get; private set; } = ViewerIndex.Overview;

        /// <summary>現在の視点の持ち主を決めた情報源の名前。</summary>
        public string ViewerSourceLabel { get; private set; } = LocalViewerResolver.FallbackLabel;

        /// <summary>補間後・一時演出を重ねる前の視点。</summary>
        public CameraViewSettings BaseView { get; private set; } = CameraViewSettings.Default;

        /// <summary>実際にカメラへ書き込んだ視点(一時演出込み)。</summary>
        public CameraViewSettings CurrentView { get; private set; } = CameraViewSettings.Default;

        public bool IsBlending => _blend.IsBlending;
        public int ActiveModifierCount => _modifiers.Count;

        /// <summary>画面の縦横比。カメラが無ければ構図ガイドの基準解像度から求める。</summary>
        public float Aspect => targetCamera != null ? targetCamera.aspect : guides.ReferenceAspect;

        public LocalViewerResolver ViewerResolver => _viewerResolver ??= LocalViewerResolver.CreateDefault();

        public event Action<int> ViewerChanged;

        private void Awake()
        {
            EnsureCamera();
        }

        private void OnEnable()
        {
            Active = this;
        }

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        private void LateUpdate()
        {
            // HitStop等でtimeScaleが下がってもカメラの補間・演出は止めない(D-DriveのCameraFxと同じ方針)。
            Tick(Time.unscaledDeltaTime);
        }

        public void EnsureCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponentInChildren<Camera>();
            }
        }

        /// <summary>視点セットを切り替える。blendSecondsが0なら即座に切り替わる。</summary>
        public void SetPreset(CameraViewPreset next, float blendSeconds)
        {
            if (next == null || next == preset)
            {
                return;
            }

            if (_initialized)
            {
                _blend.Begin(BaseView, blendSeconds, blendCurve);
            }

            preset = next;
        }

        /// <summary>一時演出(ズーム・揺れ等)を重ねる。終われば自動で取り除かれる。</summary>
        public void AddModifier(ICameraViewModifier modifier) => _modifiers.Add(modifier);

        public void ClearModifiers() => _modifiers.Clear();

        /// <summary>補間・一時演出を打ち切り、今の持ち主の視点へ即座に合わせる。</summary>
        public void SnapToTarget()
        {
            _blend.Cancel();
            _modifiers.Clear();
            _initialized = false;
            Tick(0f);
        }

        /// <summary>
        /// 状態(補間・演出・持ち主)を変えずに、指定した視点をそのままカメラへ書き込む。
        /// エディタでPlay Mode外に視点を確認するためのもの。
        /// </summary>
        public void ApplyViewImmediate(CameraViewSettings view)
        {
            EnsureCamera();
            ApplyToCamera(view);
        }

        private void Tick(float deltaTime)
        {
            if (preset == null)
            {
                return;
            }

            EnsureCamera();

            var viewer = ViewerResolver.Resolve(fallbackViewer, out var sourceLabel);
            ViewerSourceLabel = sourceLabel;

            if (!_initialized)
            {
                CurrentViewer = viewer;
                BaseView = preset.GetView(viewer);
                _initialized = true;
            }
            else if (viewer != CurrentViewer)
            {
                _blend.Begin(BaseView, viewerSwitchBlendSeconds, blendCurve);
                CurrentViewer = viewer;
                ViewerChanged?.Invoke(viewer);
            }

            BaseView = _blend.Evaluate(preset.GetView(viewer), deltaTime);
            CurrentView = _modifiers.Apply(BaseView, deltaTime);
            ApplyToCamera(CurrentView);
        }

        private void ApplyToCamera(in CameraViewSettings view)
        {
            var nearClip = preset != null ? preset.nearClip : 0.3f;
            var farClip = preset != null ? preset.farClip : 1000f;
            CameraPoseApplier.Apply(transform, targetCamera, view, nearClip, farClip);

            // カメラが直接の子なら位置・回転ゼロに保つ(ずれているとリグの計算と見た目が食い違う)。
            // D-Driveの揺れ用ノードが間に挟まっている場合は、そのノードの管理に任せて触らない。
            if (targetCamera != null && targetCamera.transform.parent == transform)
            {
                targetCamera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
        }
    }
}
