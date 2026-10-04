using UnityEngine;

namespace MS2026.Fortress.Billboards
{
    /// <summary>
    /// これを付けたオブジェクト(と子)のSpriteRendererも立たせる。自動の対象(敵・コア・砲台・破壊可能物)以外を立たせたいときに使う。
    /// ビルボード自体がOFFのプリセットでは何もしない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BillboardSprite : MonoBehaviour
    {
    }
}
