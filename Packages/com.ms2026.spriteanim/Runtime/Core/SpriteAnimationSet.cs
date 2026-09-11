using System.Collections.Generic;
using UnityEngine;

namespace MS2026.SpriteAnim
{
    /// <summary>
    /// 1キャラクター（あるいは1エフェクト群）分のアニメーションをまとめたアセット。
    /// SpriteAnimator はこのセットの中から名前でアニメーションを検索して再生する。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/Sprite Anim/Sprite Animation Set", fileName = "NewSpriteAnimationSet")]
    public class SpriteAnimationSet : ScriptableObject
    {
        [SerializeField] private List<SpriteAnimation> animations = new List<SpriteAnimation>();

        [Tooltip("Play() でクロスフェード秒数を明示しなかった場合に使われるデフォルト値。")]
        [SerializeField] private float defaultCrossFadeSeconds = 0.1f;

        [Tooltip("特定の from→to の組み合わせだけクロスフェード時間を変えたい場合に使う。from/to は \"*\" で任意にマッチ。")]
        [SerializeField] private List<TransitionRule> transitionRules = new List<TransitionRule>();

        private Dictionary<string, SpriteAnimation> _lookup;

        public IReadOnlyList<SpriteAnimation> Animations => animations;
        public float DefaultCrossFadeSeconds => defaultCrossFadeSeconds;

        /// <summary>名前でアニメーションを検索する。見つからなければ null。</summary>
        public SpriteAnimation Find(string animationName)
        {
            if (string.IsNullOrEmpty(animationName)) return null;
            BuildLookupIfNeeded();
            return _lookup.TryGetValue(animationName, out var anim) ? anim : null;
        }

        /// <summary>from→to のクロスフェード秒数を、登録済みルール→デフォルト値の優先順で解決する。</summary>
        public float GetCrossFadeSeconds(string from, string to)
        {
            if (transitionRules != null)
            {
                foreach (var rule in transitionRules)
                {
                    bool fromMatch = rule.from == "*" || rule.from == from;
                    bool toMatch = rule.to == "*" || rule.to == to;
                    if (fromMatch && toMatch) return rule.crossFadeSeconds;
                }
            }
            return defaultCrossFadeSeconds;
        }

        private void BuildLookupIfNeeded()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, SpriteAnimation>();
            foreach (var a in animations)
            {
                if (a == null) continue;
                _lookup[a.AnimationName] = a;
            }
        }

        private void OnValidate() => _lookup = null;
        private void OnEnable() => _lookup = null;

#if UNITY_EDITOR
        // Sprite Anim Studio エディタからのみ呼び出す編集用API。ランタイムコードからは使用しないこと。
        public List<SpriteAnimation> EditorAnimationsList => animations;
        public List<TransitionRule> EditorTransitionRules => transitionRules;

        public void EditorAddAnimation(SpriteAnimation anim)
        {
            animations.Add(anim);
            _lookup = null;
        }

        public void EditorRemoveAnimation(SpriteAnimation anim)
        {
            animations.Remove(anim);
            _lookup = null;
        }

        public void EditorSetDefaultCrossFade(float value) => defaultCrossFadeSeconds = Mathf.Max(0f, value);
#endif
    }
}
