using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Cpu.Experimental
{
    /// <summary>
    /// 【実験】カメラが砲台に付いていく（TurretMoveDirector の ICameraFollowSource）ときの、CPU の席の画面に映る範囲。
    /// 席のカメラの決まった視点を、砲台が席から離れた分（× 付いていく強さ <see cref="TurretMoveSettings.cameraFollow"/>）だけずらす。
    /// 人のカメラの遅れ・動いている向きの先読み・全速のときに引くのは見込まない（おおまかでよい）。
    /// </summary>
    internal sealed class CpuFollowViewSource : ICpuViewSource
    {
        private const int PlayerCount = 4;

        private readonly Vector2[] _firstSeen = new Vector2[PlayerCount];
        private readonly LaserTurret[] _firstSeenTurret = new LaserTurret[PlayerCount];

        public bool TryGetView(int player, out CpuView view)
        {
            if (!CpuDefaultView.TryGet(player, out view))
            {
                return false;
            }

            var settings = TurretMoveSettings.Load();
            var director = CpuDirector.Current;
            var turret = director != null ? director.GetPlayer(player)?.Context.Turret : null;
            if (settings == null || !settings.movementEnabled || turret == null || FortressSeats.IsMoving)
            {
                return true;
            }

            var position = (Vector2)turret.transform.position;
            if (!TurretMoveHooks.TryGetHome(player, out var home))
            {
                // 席の場所の知らせが無いステージ: 初めて見た場所を席とみなす。
                if (_firstSeenTurret[player] != turret)
                {
                    _firstSeenTurret[player] = turret;
                    _firstSeen[player] = position;
                }

                home = _firstSeen[player];
            }

            view = new CpuView(view.Center + (position - home) * settings.cameraFollow, view.HalfSize, view.Up);
            return true;
        }
    }
}
