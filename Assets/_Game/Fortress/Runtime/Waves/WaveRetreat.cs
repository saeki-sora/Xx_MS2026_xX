using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ウェーブの目標を倒した後の「撤退」（残りの敵が全員の画面の外へ逃げる）。
    /// 逃がす・消すのは Host（オフラインでは自分）だけ。見た目（逃げる敵を薄くする）は全員のPCで。
    /// </summary>
    public static class WaveRetreat
    {
        /// <summary>撤退中か（Host）。</summary>
        public static bool IsRetreating { get; private set; }

        /// <summary>撤退中、まだだれかの画面に映っている敵の数（Host）。</summary>
        public static int VisibleCount { get; private set; }

        /// <summary>Host: 残りの敵を逃がし始める。</summary>
        public static void Begin(float speedMultiplier)
        {
            IsRetreating = true;
            VisibleCount = 0;
        }

        /// <summary>Host: 撤退を終える。まだ残っている敵は消す（撃破数には入らない）。</summary>
        public static void Finish()
        {
            IsRetreating = false;
            VisibleCount = 0;
            RemoveAllEnemies();
        }

        /// <summary>試合の外へ出たとき・Client になったとき。</summary>
        public static void Reset()
        {
            IsRetreating = false;
            VisibleCount = 0;
        }

        /// <summary>全員のPC: 逃げている敵の見た目にするか。</summary>
        public static void SetRetreatLook(bool on)
        {
        }

        private static void RemoveAllEnemies()
        {
            if (!Net.FortressNet.HasSimulationAuthority)
            {
                return;
            }

            SwarmSystem.Current?.ClearAll();
            for (var i = EnemyController.Active.Count - 1; i >= 0; i--)
            {
                var enemy = EnemyController.Active[i];
                if (enemy != null)
                {
                    Object.Destroy(enemy.gameObject);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Reset();
        }
    }
}
