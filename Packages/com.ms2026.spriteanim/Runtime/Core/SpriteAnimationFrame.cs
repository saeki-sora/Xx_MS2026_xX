using System;
using UnityEngine;

namespace MS2026.SpriteAnim
{
    /// <summary>1フレーム分のデータ。表示するスプライトと表示時間、任意のフレームイベント名を持つ。</summary>
    [Serializable]
    public struct SpriteAnimationFrame
    {
        public Sprite sprite;

        /// <summary>このフレームを表示し続ける秒数。</summary>
        public float duration;

        /// <summary>このフレームが表示された瞬間に SpriteAnimator が発火するイベント名（足音・攻撃判定などに使用）。空なら何も発火しない。</summary>
        public string eventName;
    }
}
