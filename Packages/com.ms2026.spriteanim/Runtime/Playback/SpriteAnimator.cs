using System;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.SpriteAnim
{
    /// <summary>
    /// スプライトアニメーションの再生を担当するコンポーネント。
    /// 同じ GameObject の SpriteRenderer または UI Image を自動検出して描画する。
    ///
    /// 使い方:
    ///   animator.Play("Walk");                 // 再生（同じアニメーションを指定した場合は何もしない）
    ///   animator.Play("Attack", 0.1f);          // 0.1秒かけてクロスフェードしながら切り替え
    ///   animator.PlayOneShot("Hit", "Idle");    // Hit を一回再生し、終わったら自動で Idle に戻る
    ///   animator.Stop();
    /// </summary>
    [AddComponentMenu("MS2026/Sprite Anim/Sprite Animator")]
    [DefaultExecutionOrder(-1)]
    public class SpriteAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteAnimationSet animationSet;
        [SerializeField] private string defaultAnimation;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool useUnscaledTime;
        [Tooltip("再生開始時のフレームをランダムにする。同じアニメーションを再生する複数キャラの動きを完全同期させたくない場合に有効。")]
        [SerializeField] private bool randomizeStartFrame;
        [SerializeField, Range(0f, 5f)] private float speed = 1f;

        [Header("Target (未指定なら同じ GameObject の SpriteRenderer / Image を自動使用)")]
        [SerializeField] private SpriteRenderer targetSpriteRenderer;
        [SerializeField] private Image targetImage;

        [Header("Events")]
        public SpriteAnimationStringEvent onAnimationStarted;
        public SpriteAnimationStringEvent onAnimationCompleted;
        public SpriteAnimationStringEvent onAnimationLooped;
        public SpriteAnimationStringEvent onFrameEvent;

        /// <summary>アニメーションが Play() された瞬間に発火（引数はアニメーション名）。</summary>
        public event Action<string> AnimationStarted;

        /// <summary>Once/ClampForever のアニメーションが最終フレームまで再生し終えた瞬間に発火。</summary>
        public event Action<string> AnimationCompleted;

        /// <summary>Loop/PingPong が先頭（または端）に戻った瞬間に発火。</summary>
        public event Action<string> AnimationLooped;

        /// <summary>フレームに設定されたイベント名が表示された瞬間に発火。</summary>
        public event Action<string> FrameEvent;

        private ISpriteAnimationTarget _target;
        private ISpriteAnimationTarget _blendLayer;

        private SpriteAnimation _current;
        private int _frameIndex;
        private float _frameTimer;
        private bool _isPlaying;
        private bool _pingPongForward = true;

        private bool _isBlending;
        private float _blendTimer;
        private float _blendDuration;

        private string _pendingReturnAnimation;

        public bool IsPlaying => _isPlaying;
        public string CurrentAnimationName => _current != null ? _current.AnimationName : null;
        public SpriteAnimationSet AnimationSet { get => animationSet; set => animationSet = value; }
        public float Speed { get => speed; set => speed = Mathf.Max(0f, value); }

        /// <summary>現在のアニメーションの再生位置を 0〜1 で返す。</summary>
        public float NormalizedTime
        {
            get
            {
                if (_current == null || _current.TotalDuration <= 0f) return 0f;
                var frames = _current.Frames;
                float elapsed = 0f;
                for (int i = 0; i < _frameIndex && i < frames.Length; i++) elapsed += frames[i].duration;
                elapsed += _frameTimer;
                return Mathf.Clamp01(elapsed / _current.TotalDuration);
            }
        }

        private void Awake()
        {
            _target = ResolveTarget();
        }

        private ISpriteAnimationTarget ResolveTarget()
        {
            if (targetSpriteRenderer != null) return new SpriteRendererTarget(targetSpriteRenderer);
            if (targetImage != null) return new ImageTarget(targetImage);

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) return new SpriteRendererTarget(sr);

            var img = GetComponent<Image>();
            if (img != null) return new ImageTarget(img);

            Debug.LogWarning($"[SpriteAnimator] '{name}' に SpriteRenderer も Image も見つかりません。表示対象を Target 欄で指定してください。", this);
            return null;
        }

        private void OnEnable()
        {
            if (playOnEnable && !string.IsNullOrEmpty(defaultAnimation))
                Play(defaultAnimation);
        }

        private void OnDisable()
        {
            CancelBlend();
        }

        /// <summary>
        /// アニメーションを再生する。既に同じアニメーションを再生中の場合は何もしない
        /// （毎フレーム Play() を呼んでも再生が最初から巻き戻らない、安全な仕様）。
        /// </summary>
        /// <param name="animationName">SpriteAnimationSet 内のアニメーション名。</param>
        /// <param name="crossFadeSeconds">切替にかける秒数。負値ならセットのデフォルト/遷移ルールに従う。0 なら即時切替。</param>
        public void Play(string animationName, float crossFadeSeconds = -1f)
        {
            if (animationSet == null)
            {
                Debug.LogWarning($"[SpriteAnimator] '{name}' に Animation Set が設定されていません。", this);
                return;
            }

            var next = animationSet.Find(animationName);
            if (next == null)
            {
                Debug.LogWarning($"[SpriteAnimator] アニメーション '{animationName}' が見つかりません。", this);
                return;
            }

            if (next == _current && _isPlaying) return;
            if (_target == null) return;

            string previousName = _current != null ? _current.AnimationName : null;
            float duration = crossFadeSeconds >= 0f
                ? crossFadeSeconds
                : animationSet.GetCrossFadeSeconds(previousName, animationName);

            if (duration > 0f && _current != null)
                BeginBlend(duration);

            _current = next;
            _frameIndex = randomizeStartFrame && next.FrameCount > 0 ? UnityEngine.Random.Range(0, next.FrameCount) : 0;
            _frameTimer = 0f;
            _pingPongForward = true;
            _isPlaying = true;
            _pendingReturnAnimation = null;

            ApplyCurrentFrame();
            AnimationStarted?.Invoke(animationName);
            onAnimationStarted?.Invoke(animationName);
        }

        /// <summary>animationName を一回だけ再生し、終了したら自動的に returnToAnimation を再生する。攻撃やヒット演出などに便利。</summary>
        public void PlayOneShot(string animationName, string returnToAnimation, float crossFadeSeconds = -1f)
        {
            Play(animationName, crossFadeSeconds);
            _pendingReturnAnimation = returnToAnimation;
        }

        public void Stop()
        {
            _isPlaying = false;
            CancelBlend();
        }

        public void Pause() => _isPlaying = false;

        public void Resume()
        {
            if (_current != null) _isPlaying = true;
        }

        public bool HasAnimation(string animationName) => animationSet != null && animationSet.Find(animationName) != null;

        private void BeginBlend(float duration)
        {
            CancelBlend();
            _blendLayer = _target.CreateBlendLayer();
            _blendDuration = Mathf.Max(0.0001f, duration);
            _blendTimer = 0f;
            _isBlending = true;

            var c = _target.Color;
            c.a = 0f;
            _target.Color = c;
        }

        private void CancelBlend()
        {
            if (_isBlending && _blendLayer != null && _target != null)
                _target.DestroyBlendLayer(_blendLayer);

            _isBlending = false;
            _blendLayer = null;

            if (_target != null)
            {
                var c = _target.Color;
                c.a = 1f;
                _target.Color = c;
            }
        }

        private void Update()
        {
            if (_target == null) return;

            if (_isBlending) UpdateBlend();

            if (!_isPlaying || _current == null || _current.FrameCount == 0) return;

            _frameTimer += DeltaTime() * speed * _current.Speed;

            var frames = _current.Frames;
            float currentDuration = Mathf.Max(0.0001f, frames[_frameIndex].duration);

            int safety = 0;
            while (_frameTimer >= currentDuration && safety++ < 1000)
            {
                _frameTimer -= currentDuration;
                AdvanceFrame();
                if (!_isPlaying || _current == null) break;
                currentDuration = Mathf.Max(0.0001f, _current.Frames[_frameIndex].duration);
            }

            ApplyCurrentFrame();
        }

        private void UpdateBlend()
        {
            _blendTimer += DeltaTime();
            float t = Mathf.Clamp01(_blendTimer / _blendDuration);

            var mainColor = _target.Color;
            mainColor.a = t;
            _target.Color = mainColor;

            if (_blendLayer != null)
            {
                var blendColor = _blendLayer.Color;
                blendColor.a = 1f - t;
                _blendLayer.Color = blendColor;
            }

            if (t >= 1f) CancelBlend();
        }

        private void AdvanceFrame()
        {
            int previous = _frameIndex;
            var step = SpriteAnimationPlayback.Advance(_current.LoopMode, _current.FrameCount, _frameIndex, _pingPongForward);
            _frameIndex = step.frameIndex;
            _pingPongForward = step.forward;

            if (step.looped)
            {
                AnimationLooped?.Invoke(_current.AnimationName);
                onAnimationLooped?.Invoke(_current.AnimationName);
            }

            if (step.finished)
            {
                FinishPlayback();
                return;
            }

            if (_frameIndex != previous) FireFrameEventIfAny();
        }

        private void FinishPlayback()
        {
            string finished = _current.AnimationName;
            _isPlaying = false;

            AnimationCompleted?.Invoke(finished);
            onAnimationCompleted?.Invoke(finished);

            if (!string.IsNullOrEmpty(_pendingReturnAnimation))
            {
                string ret = _pendingReturnAnimation;
                _pendingReturnAnimation = null;
                Play(ret);
            }
        }

        private void FireFrameEventIfAny()
        {
            var frames = _current.Frames;
            if (_frameIndex < 0 || _frameIndex >= frames.Length) return;

            var evt = frames[_frameIndex].eventName;
            if (string.IsNullOrEmpty(evt)) return;

            FrameEvent?.Invoke(evt);
            onFrameEvent?.Invoke(evt);
        }

        private void ApplyCurrentFrame()
        {
            if (_current == null || _current.FrameCount == 0) return;
            int idx = Mathf.Clamp(_frameIndex, 0, _current.FrameCount - 1);
            _target.Sprite = _current.Frames[idx].sprite;
        }

        private float DeltaTime() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}
