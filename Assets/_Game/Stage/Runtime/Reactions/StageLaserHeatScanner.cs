using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 全砲台のレーザーの着弾先を毎フレーム見て、背景オブジェクトに当たっていればその熱の部品へ知らせる。
    /// 着弾はどのPCでも同じように求まる（Clientもレーザーの線を自分で計算している）ので、通信は不要。
    /// StageRoot が自動で付ける。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageLaserHeatScanner : MonoBehaviour
    {
        // 砲台のレーザー計算（LaserBeamVisual.Update）の後に読む。
        private void LateUpdate()
        {
            var beams = LaserBeamVisual.Active;
            for (var i = 0; i < beams.Count; i++)
            {
                var beam = beams[i];
                if (beam == null || !beam.HasImpact || beam.ImpactCollider == null)
                {
                    continue;
                }

                var heat = beam.ImpactCollider.GetComponentInParent<StagePropHeat>();
                if (heat == null)
                {
                    continue;
                }

                var thickness = beam.Turret != null ? beam.Turret.CurrentThicknessMeters : 0.1f;
                var point = beam.ImpactPoint;
                point.z -= StageLookProfile.Current.heatHeightOffset;
                heat.ReceiveLaser(point, thickness, Time.deltaTime);
            }
        }
    }
}
