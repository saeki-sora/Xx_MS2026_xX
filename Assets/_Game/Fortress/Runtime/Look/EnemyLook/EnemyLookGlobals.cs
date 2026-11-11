using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 敵の質感の設定（<see cref="EnemyLookSettings"/>）を、群衆のシェーダー（SwarmSprite / SwarmSpriteCommon.hlsl）のグローバル値に入れる。
    /// null や OFF のときは全部0にして、今までの見た目に戻す。編集中もすぐ反映される（ステージの見え方の設定が変わるたびに呼ばれる）。
    /// </summary>
    public static class EnemyLookGlobals
    {
        private static readonly int LookId = Shader.PropertyToID("_FortressEnemyLook");
        private static readonly int EdgeId = Shader.PropertyToID("_FortressEnemyEdge");

        public static void Apply(EnemyLookSettings settings)
        {
            if (settings == null || !settings.enabled)
            {
                Shader.SetGlobalVector(LookId, Vector4.zero);
                Shader.SetGlobalVector(EdgeId, Vector4.zero);
                return;
            }

            Shader.SetGlobalVector(LookId, new Vector4(settings.jiggle, settings.jiggleFramesPerSecond, settings.hitSquash, settings.edgeDarken));
            var c = settings.edgeColor;
            Shader.SetGlobalVector(EdgeId, new Vector4(c.r, c.g, c.b, settings.edgeWidth));
        }
    }
}
