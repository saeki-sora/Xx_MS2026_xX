using System.Linq;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// シーン内の4基の砲台をまとめて扱う。砲台の配置はTransformを直接動かすだけでよく
    /// （Unity標準の移動ツールで「好きな位置に設置」できる）、このコンポーネントは
    /// その配置をプリセットアセットに保存・復元する橋渡し役を担う。
    /// </summary>
    public sealed class FortressLayoutController : MonoBehaviour
    {
        [Tooltip("未設定なら子オブジェクトからLaserTurretを自動収集する。")]
        public LaserTurret[] turrets;

        private void Awake()
        {
            EnsureTurrets();
        }

        public void EnsureTurrets()
        {
            if (turrets == null || turrets.Length == 0)
            {
                turrets = GetComponentsInChildren<LaserTurret>();
            }
        }

        /// <summary>プリセットの配置をシーン上の砲台に適用する。</summary>
        public void ApplyLayout(FortressLayoutConfig config)
        {
            EnsureTurrets();

            if (config == null || turrets == null)
            {
                return;
            }

            foreach (var placement in config.turretPlacements)
            {
                var turret = turrets.FirstOrDefault(t => t != null && t.playerIndex == placement.playerIndex);
                if (turret == null)
                {
                    continue;
                }

                turret.transform.position = placement.position;
                turret.transform.rotation = Quaternion.Euler(0f, 0f, placement.facingDegrees);
            }
        }

        /// <summary>現在のシーン上の配置をプリセットアセットに書き込む。</summary>
        public void CaptureLayout(FortressLayoutConfig target)
        {
            EnsureTurrets();

            if (target == null || turrets == null)
            {
                return;
            }

            target.turretPlacements = turrets
                .Where(t => t != null)
                .Select(t => new TurretPlacementData
                {
                    playerIndex = t.playerIndex,
                    position = t.transform.position,
                    facingDegrees = t.transform.rotation.eulerAngles.z
                })
                .ToArray();
        }

        /// <summary>設定ミス検知用。2基以上が同じplayerIndexになっていないか。</summary>
        public bool HasDuplicatePlayerIndex()
        {
            EnsureTurrets();
            return turrets != null && turrets.Where(t => t != null).GroupBy(t => t.playerIndex).Any(g => g.Count() > 1);
        }
    }
}
