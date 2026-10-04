using UnityEngine;

namespace MS2026.Fortress.Billboards
{
    /// <summary>
    /// ビルボード用シェーダー(BillboardSprite / SwarmSprite)が読むグローバル値。
    /// 全て0にすると、どちらのシェーダーも従来と同じ「地面に寝た絵」になる。
    /// </summary>
    public static class BillboardShaderGlobals
    {
        public static readonly int StandId = Shader.PropertyToID("_FortressBillboardStand");
        public static readonly int SwarmStandId = Shader.PropertyToID("_FortressSwarmBillboardStand");
        public static readonly int FeetId = Shader.PropertyToID("_FortressBillboardFeet");
        public static readonly int CutoffId = Shader.PropertyToID("_FortressBillboardCutoff");

        public static void Apply(BillboardSettings settings)
        {
            if (settings == null || !settings.enabled)
            {
                Reset();
                return;
            }

            Shader.SetGlobalFloat(StandId, settings.standAmount);
            Shader.SetGlobalFloat(SwarmStandId, settings.Includes(BillboardTargets.Enemies) ? settings.standAmount : 0f);
            Shader.SetGlobalFloat(FeetId, settings.anchor == BillboardAnchor.Feet ? 1f : 0f);
            Shader.SetGlobalFloat(CutoffId, settings.alphaCutoff);
        }

        public static void Reset()
        {
            Shader.SetGlobalFloat(StandId, 0f);
            Shader.SetGlobalFloat(SwarmStandId, 0f);
            Shader.SetGlobalFloat(FeetId, 0f);
            Shader.SetGlobalFloat(CutoffId, 0.5f);
        }
    }
}
