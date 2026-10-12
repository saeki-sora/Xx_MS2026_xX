using System;
using System.Collections.Generic;
using System.Text;

namespace MS2026.UI.EditorTools
{
    /// <summary>Play 中の変更を、止めたあとにどこへ書き戻すか。</summary>
    public enum UiPlayDestination
    {
        /// <summary>一覧（UiScreenCatalog）の画面の Prefab ファイル。</summary>
        Prefab,

        /// <summary>シーンに置いた物（[UI] の下・シーンに置いた画面）。</summary>
        Scene
    }

    /// <summary>
    /// 変更された物の「住所」。Play が終わると Play 中の物は消えるので、物そのものではなく
    /// 書き戻し先（Prefab かシーン）＋目印（画面の名前か [UI]）＋そこからの名前の道のり＋部品の種類と番号で覚える。
    /// 同じ名前の兄弟がいるときは、同じ名前の中で何番目か（steps の index）で見分ける。
    /// </summary>
    [Serializable]
    public sealed class UiPlayTarget
    {
        public UiPlayDestination destination;

        /// <summary>Prefab のパス、またはシーンのパス。</summary>
        public string assetPath = "";

        /// <summary>シーンのとき: 画面の名前（screenId）。空なら [UI]（UiRoot）が目印。Prefab のときは空（Prefab のルートが目印）。</summary>
        public string anchorId = "";

        /// <summary>目印から下の、名前の道のり（目印そのものなら空）。</summary>
        public List<string> names = new List<string>();

        /// <summary>同じ名前の兄弟の中で何番目か（names と同じ数）。</summary>
        public List<int> indices = new List<int>();

        /// <summary>部品の型（AssemblyQualifiedName）。空なら GameObject そのもの（名前・表示など）。</summary>
        public string componentType = "";

        /// <summary>同じ型の部品の中で何番目か。</summary>
        public int componentIndex;

        /// <summary>表示用: 「LobbyTop › Panel › Title」。</summary>
        public string where = "";

        /// <summary>表示用: 部品の名前（「Text」「Rect Transform」…）。</summary>
        public string componentLabel = "";

        public bool IsGameObject => string.IsNullOrEmpty(componentType);

        /// <summary>書き戻し先（Prefab かシーン）が同じか。</summary>
        public string DestinationKey => $"{(int)destination}|{assetPath}";

        /// <summary>同じ目印（同じ画面の中・同じ [UI] の中）か。参照先を書き戻せるかの判定に使う。</summary>
        public string AnchorKey => $"{DestinationKey}|{anchorId}";

        /// <summary>同じ物の同じ部品かを見分ける鍵。</summary>
        public string Key
        {
            get
            {
                var builder = new StringBuilder(AnchorKey);
                for (var i = 0; i < names.Count; i++)
                {
                    builder.Append('/').Append(names[i]).Append('#').Append(i < indices.Count ? indices[i] : 0);
                }

                return builder.Append('|').Append(componentType).Append('#').Append(componentIndex).ToString();
            }
        }

        public UiPlayTarget Clone() => new UiPlayTarget
        {
            destination = destination,
            assetPath = assetPath,
            anchorId = anchorId,
            names = new List<string>(names),
            indices = new List<int>(indices),
            componentType = componentType,
            componentIndex = componentIndex,
            where = where,
            componentLabel = componentLabel
        };
    }

    /// <summary>Play 中に変えた値1つ（どの物の・どの欄を・何から何へ）。</summary>
    [Serializable]
    public sealed class UiPlayChange
    {
        public UiPlayTarget target = new UiPlayTarget();

        /// <summary>Unity の欄の道のり（例: m_AnchoredPosition.x / entries.Array.data[0].motion.delay）。</summary>
        public string propertyPath = "";

        /// <summary>新しい値（文字で。数・ON/OFF・文字・選択肢の番号）。</summary>
        public string value = "";

        /// <summary>新しい値が、プロジェクトのファイル（画像・フォント・効果音…）を指すとき、その物。</summary>
        public UnityEngine.Object objectValue;

        /// <summary>新しい値が、同じ画面（同じ [UI]）の中の物を指すとき true（その住所は reference）。</summary>
        public bool referencesLocal;

        public UiPlayTarget reference = new UiPlayTarget();

        /// <summary>表示用: 変える前の値。</summary>
        public string before = "";

        /// <summary>変える前の値（文字・参照）。Play 中に元に戻した（Ctrl+Z など）変更を見分けるのに使う。</summary>
        public string beforeValue = "";

        public UnityEngine.Object beforeObject;

        /// <summary>表示用: 新しい値。</summary>
        public string after = "";

        /// <summary>表示用: 欄の名前（「Anchored Position › X」）。</summary>
        public string label = "";

        /// <summary>残すかどうか（一覧のチェック）。</summary>
        public bool keep = true;

        /// <summary>残せない理由（空なら残せる）。</summary>
        public string problem = "";

        public bool CanApply => string.IsNullOrEmpty(problem);

        public string Key => target.Key + "\n" + propertyPath;

        public bool IsArraySize => UiPlayChangeList.IsArraySize(propertyPath);
    }
}
