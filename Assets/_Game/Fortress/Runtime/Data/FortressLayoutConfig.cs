using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>1基の砲台の配置データ。</summary>
    [Serializable]
    public struct TurretPlacementData
    {
        [Tooltip("0-3。GripInputBridgeのプレイヤー番号と対応する。")]
        public int playerIndex;

        public Vector2 position;

        [Tooltip("砲台の向き(Z軸回転、度)。0度で+Y方向（上）を向く。")]
        public float facingDegrees;
    }

    /// <summary>
    /// 4基の砲台の配置プリセット。地形案（時計仕掛け/二層構造/花弁シンメトリーなど）ごとに
    /// アセットとして保存しておき、シーン上のFortressLayoutControllerから読み書きする。
    /// </summary>
    [CreateAssetMenu(menuName = "Fortress/Fortress Layout Config", fileName = "New FortressLayoutConfig")]
    public sealed class FortressLayoutConfig : ScriptableObject
    {
        [Tooltip("地形プリセットの表示名（例: 巨大時計仕掛け要塞）。")]
        public string presetName = "New Layout";

        public TurretPlacementData[] turretPlacements = new TurretPlacementData[4];
    }
}
