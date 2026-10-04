using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress.Billboards
{
    /// <summary>
    /// 立たせる対象のSpriteRendererをシーンから集める係。対象の種類を増やすときは、これを実装して
    /// <see cref="BillboardController.AddSource"/> で登録する(または <see cref="ComponentBillboardSource{T}"/> を使う)。
    /// </summary>
    public interface IBillboardTargetSource
    {
        /// <summary>どの対象種別のスイッチに従うか。Noneなら(ビルボードがONである限り)常に対象。</summary>
        BillboardTargets Category { get; }

        void Collect(List<SpriteRenderer> results);
    }

    /// <summary>指定したコンポーネントを持つオブジェクトのSpriteRendererを集める汎用の係。</summary>
    public sealed class ComponentBillboardSource<T> : IBillboardTargetSource where T : Component
    {
        private readonly bool _includeChildren;
        private readonly List<SpriteRenderer> _temp = new List<SpriteRenderer>();

        /// <param name="includeChildren">falseならそのオブジェクト自身の絵だけ(例: 砲台の本体だけで、子の砲身は含めない)。</param>
        public ComponentBillboardSource(BillboardTargets category, bool includeChildren)
        {
            Category = category;
            _includeChildren = includeChildren;
        }

        public BillboardTargets Category { get; }

        public void Collect(List<SpriteRenderer> results)
        {
            foreach (var owner in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (_includeChildren)
                {
                    owner.GetComponentsInChildren(false, _temp);
                    results.AddRange(_temp);
                }
                else if (owner.TryGetComponent<SpriteRenderer>(out var renderer))
                {
                    results.Add(renderer);
                }
            }
        }
    }

    /// <summary>既定の対象の集め方。</summary>
    public static class BillboardTargetSources
    {
        public static IBillboardTargetSource[] CreateDefault()
        {
            return new IBillboardTargetSource[]
            {
                // 群衆の敵はシェーダー側で立たせるので、ここで集めるのは個別に置いた敵だけ。
                new ComponentBillboardSource<EnemyController>(BillboardTargets.Enemies, true),
                new ComponentBillboardSource<CoreCrystalController>(BillboardTargets.Core, true),
                // 砲台は本体だけ。砲身・照準線・レーザーは向きが正確に分かるよう地面に寝たままにする。
                new ComponentBillboardSource<LaserTurret>(BillboardTargets.TurretBody, false),
                new ComponentBillboardSource<DestructibleObstacle>(BillboardTargets.Destructibles, true),
                new ComponentBillboardSource<BillboardSprite>(BillboardTargets.None, true)
            };
        }
    }
}
