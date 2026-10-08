using System;
using DDrive.Runtime.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI
{
    /// <summary>
    /// 画面切り替えの幕を全画面に出す係（「画面切り替え」レイヤーに1つ）。UiRoot が自動で作る。
    /// 閉じる → onCovered（裏で画面やシーンを入れ替える）→ 準備ができたら開く → onDone。
    /// 幕が出ている間は、下の画面を押せない。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public sealed class UiTransitionOverlay : MonoBehaviour
    {
        private static UiScreenTransition _fallback;

        private readonly UiTransitionTimeline _timeline = new UiTransitionTimeline();
        private RawImage _image;
        private Material _material;
        private UiScreenTransition _transition;
        private Action _onCovered;
        private Func<bool> _isReady;
        private Action _onDone;
        private bool _revealSoundPlayed;

        public bool IsPlaying => _timeline.IsRunning;

        /// <summary>今覆っている割合（0〜1）。ツールの表示用。</summary>
        public float Progress => _timeline.Progress;

        public void Play(UiScreenTransition transition, Action onCovered, Func<bool> isReady = null, Action onDone = null)
        {
            if (transition == null)
            {
                if (_fallback == null)
                {
                    _fallback = UiScreenTransition.CreateDefault();
                }

                transition = _fallback;
            }

            if (IsPlaying)
            {
                // 前の切り替えの途中なら、前の分の「覆った」を先に済ませる（入れ替えが飛ばないように）。
                _onCovered?.Invoke();
                _onDone?.Invoke();
            }

            _transition = transition;
            _onCovered = onCovered;
            _isReady = isReady;
            _onDone = onDone;
            _revealSoundPlayed = false;
            _timeline.Start(transition.coverSeconds, transition.holdSeconds, transition.revealSeconds);
            EnsureImage();
            _image.enabled = true;
            _image.raycastTarget = true;
            if (transition.seCover.IsValid)
            {
                Audio.PlaySe(transition.seCover);
            }

            Draw(revealing: false);
        }

        private void Awake()
        {
            EnsureImage();
            _image.enabled = false;
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }

        private void Update()
        {
            if (!_timeline.IsRunning)
            {
                return;
            }

            var ready = _isReady == null || _isReady();
            var (coveredNow, finishedNow) = _timeline.Tick(Time.unscaledDeltaTime, ready);
            if (coveredNow)
            {
                var covered = _onCovered;
                _onCovered = null;
                covered?.Invoke();
            }

            var revealing = _timeline.Current == UiTransitionTimeline.Phase.Revealing || finishedNow;
            if (revealing && !_revealSoundPlayed)
            {
                _revealSoundPlayed = true;
                if (_transition.seReveal.IsValid)
                {
                    Audio.PlaySe(_transition.seReveal);
                }
            }

            Draw(revealing);
            if (finishedNow)
            {
                _image.enabled = false;
                var done = _onDone;
                _onDone = null;
                done?.Invoke();
            }
        }

        private void Draw(bool revealing)
        {
            var eased = _transition.ease != null && _transition.ease.length > 0 ? _transition.ease.Evaluate(_timeline.Progress) : _timeline.Progress;
            var rect = _image.rectTransform.rect;
            var aspect = rect.height > 0f ? rect.width / rect.height : 16f / 9f;
            _transition.ApplyTo(_material, eased, revealing, aspect);
        }

        private void EnsureImage()
        {
            if (_image == null)
            {
                _image = GetComponent<RawImage>();
            }

            if (_material == null)
            {
                var shader = Shader.Find(UiTransitionShaderIds.ShaderName);
                _material = shader != null ? new Material(shader) { hideFlags = HideFlags.DontSave } : new Material(Shader.Find("UI/Default"));
                _image.material = _material;
                _image.color = Color.white;
            }
        }
    }
}
