using UnityEngine;

namespace MS2026.Fortress.Billboards
{
    /// <summary>
    /// これを付けたオブジェクト(と子)のSpriteRendererは、自動の対象に含まれていても立たせない(地面に寝たまま)。
    /// 例: 地面に描かれた模様・影・範囲表示など。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BillboardIgnore : MonoBehaviour
    {
    }
}
