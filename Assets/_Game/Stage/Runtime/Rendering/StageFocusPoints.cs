using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 「隠れてはいけない物」（4基の砲台・コア）の位置と透かす半径を毎フレーム集め、シェーダーへ渡す。
    /// シェーダーは「カメラとこの点を結ぶ線の近くにある背景の画素」を透かすので、どの視点のカメラでも自動で正しく透ける。
    /// 自分の砲台（今見ている視点のプレイヤー）は少し大きめに透かす。StageRoot が自動で付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class StageFocusPoints : MonoBehaviour
    {
        private const float RefreshInterval = 1f;

        private static readonly Vector4[] Points = new Vector4[StageShaderIds.MaxFocusPoints];
        private static int _count;

        private readonly List<LaserTurret> _turrets = new List<LaserTurret>();
        private readonly List<CoreCrystalController> _cores = new List<CoreCrystalController>();
        private FortressNetworkBootstrap _bootstrap;
        private float _nextRefresh;

        /// <summary>今フレームの点（xyz=位置、w=半径）。</summary>
        public static int Count => _count;

        public static Vector4 Get(int index) => Points[index];

        private void OnDisable()
        {
            _count = 0;
            Shader.SetGlobalInt(StageShaderIds.FocusCount, 0);
        }

        private void LateUpdate()
        {
            if (Time.realtimeSinceStartup >= _nextRefresh)
            {
                _nextRefresh = Time.realtimeSinceStartup + RefreshInterval;
                Refresh();
            }

            var profile = StageLookProfile.Current;
            var localPlayer = ResolveLocalPlayer();
            var lift = Vector3.back * profile.focusHeight;
            _count = 0;

            foreach (var core in _cores)
            {
                if (core != null && core.isActiveAndEnabled)
                {
                    Add(core.transform.position + lift, profile.coreHoleRadius);
                }
            }

            foreach (var turret in _turrets)
            {
                if (turret == null || !turret.isActiveAndEnabled)
                {
                    continue;
                }

                var radius = profile.turretHoleRadius;
                if (turret.playerIndex == localPlayer)
                {
                    radius *= profile.localTurretHoleScale;
                }

                Add(turret.transform.position + lift, radius);
            }

            for (var i = _count; i < Points.Length; i++)
            {
                Points[i] = Vector4.zero;
            }

            Shader.SetGlobalInt(StageShaderIds.FocusCount, _count);
            Shader.SetGlobalVectorArray(StageShaderIds.FocusPoints, Points);
            profile.ApplyGlobals();
        }

        /// <summary>砲台・コアが増減したときにすぐ反映したい場合に呼ぶ（普段は1秒ごとに自動で取り直す）。</summary>
        public void Refresh()
        {
            _turrets.Clear();
            _turrets.AddRange(FindObjectsByType<LaserTurret>(FindObjectsSortMode.None));
            _turrets.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));
            _cores.Clear();
            _cores.AddRange(FindObjectsByType<CoreCrystalController>(FindObjectsSortMode.None));
            _bootstrap = FindFirstObjectByType<FortressNetworkBootstrap>();
        }

        private static void Add(Vector3 position, float radius)
        {
            if (_count < Points.Length && radius > 0f)
            {
                Points[_count++] = new Vector4(position.x, position.y, position.z, radius);
            }
        }

        private int ResolveLocalPlayer()
        {
            var rig = FortressCameraRig.Active;
            if (rig != null && ViewerIndex.IsPlayer(rig.CurrentViewer))
            {
                return rig.CurrentViewer;
            }

            return _bootstrap != null ? _bootstrap.LocalPlayerIndex : -1;
        }
    }
}
