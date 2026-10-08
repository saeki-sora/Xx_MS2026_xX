using System.Collections.Generic;
using UnityEngine;

namespace MS2026.UI
{
    /// <summary>
    /// 画面（Prefab）の一覧。ここに入っている画面は、シーンに置いていなくても名前で開ける（初めて開くときに作られる）。
    /// UIスタジオで画面を作ると自動でここに入る。
    /// </summary>
    [CreateAssetMenu(menuName = "UI/Screen Catalog", fileName = "UiScreenCatalog")]
    public sealed class UiScreenCatalog : ScriptableObject
    {
        [Tooltip("画面のPrefab（ルートに UiScreen が付いている物）。")]
        public List<UiScreen> screens = new List<UiScreen>();

        public UiScreen Find(string screenId)
        {
            foreach (var screen in screens)
            {
                if (screen != null && screen.screenId == screenId)
                {
                    return screen;
                }
            }

            return null;
        }
    }
}
