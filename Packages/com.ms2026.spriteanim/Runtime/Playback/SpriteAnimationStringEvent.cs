using System;
using UnityEngine.Events;

namespace MS2026.SpriteAnim
{
    /// <summary>Inspector 上でワイヤリングできるようにするための string 引数付き UnityEvent。</summary>
    [Serializable]
    public class SpriteAnimationStringEvent : UnityEvent<string>
    {
    }
}
