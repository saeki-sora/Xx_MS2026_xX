using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>破壊可能物の「種類」（木箱・岩・鉄壁など）を保存しておくアセット。配置時に選ぶだけで同じ設定の物を量産できる。</summary>
    [CreateAssetMenu(menuName = "Fortress/Destructible Preset", fileName = "New DestructiblePreset")]
    public sealed class DestructiblePreset : ScriptableObject
    {
        [Tooltip("ツール上に表示する名前。")]
        public string displayName = "破壊可能物";

        [Tooltip("配置するときの既定の大きさ(ワールド単位)。")]
        public Vector2 defaultSize = new Vector2(2f, 2f);

        public DestructibleSettings settings = new DestructibleSettings();
    }
}
