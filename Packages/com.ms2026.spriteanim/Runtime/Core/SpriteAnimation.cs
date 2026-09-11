using System;
using UnityEngine;

namespace MS2026.SpriteAnim
{
    /// <summary>1つのアニメーション（例: Walk, Attack）を表すデータアセット。フレーム列とループ方式を保持する。</summary>
    [CreateAssetMenu(menuName = "MS2026/Sprite Anim/Sprite Animation", fileName = "NewSpriteAnimation")]
    public class SpriteAnimation : ScriptableObject
    {
        [SerializeField] private string animationName;
        [SerializeField] private SpriteAnimationFrame[] frames = Array.Empty<SpriteAnimationFrame>();
        [SerializeField] private SpriteLoopMode loopMode = SpriteLoopMode.Loop;
        [SerializeField, Min(0.01f)] private float speed = 1f;

        /// <summary>Play() に渡す識別名。未設定ならアセット名を使う。</summary>
        public string AnimationName => string.IsNullOrEmpty(animationName) ? name : animationName;

        public SpriteAnimationFrame[] Frames => frames;
        public SpriteLoopMode LoopMode => loopMode;

        /// <summary>このアニメーション個別の再生速度倍率。SpriteAnimator.Speed と乗算される。</summary>
        public float Speed => speed;

        public int FrameCount => frames?.Length ?? 0;

        /// <summary>全フレームの表示時間の合計（秒）。</summary>
        public float TotalDuration
        {
            get
            {
                if (frames == null) return 0f;
                float total = 0f;
                foreach (var f in frames) total += Mathf.Max(0f, f.duration);
                return total;
            }
        }

#if UNITY_EDITOR
        // Sprite Anim Studio エディタからのみ呼び出す編集用API。ランタイムコードからは使用しないこと。
        public void EditorSetName(string value) => animationName = value;
        public void EditorSetFrames(SpriteAnimationFrame[] value) => frames = value ?? Array.Empty<SpriteAnimationFrame>();
        public void EditorSetLoopMode(SpriteLoopMode value) => loopMode = value;
        public void EditorSetSpeed(float value) => speed = Mathf.Max(0.01f, value);
#endif
    }
}
