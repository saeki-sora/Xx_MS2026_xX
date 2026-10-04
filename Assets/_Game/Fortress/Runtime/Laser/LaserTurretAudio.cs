using DDrive.Generated;
using DDrive.Runtime.Audio;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// LaserTurretがFiringに入った瞬間にSEを鳴らす。
    /// </summary>
    [RequireComponent(typeof(LaserTurret))]
    public sealed class LaserTurretAudio : MonoBehaviour
    {
        private LaserTurret _turret;

        private void Awake()
        {
            _turret = GetComponent<LaserTurret>();
        }

        private void OnEnable()
        {
            _turret.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            _turret.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(TurretState state)
        {
            if (state == TurretState.Firing)
            {
                Audio.PlaySe(SEID.Se);
            }
        }
    }
}
