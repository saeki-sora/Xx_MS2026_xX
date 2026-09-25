using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 砲台の向きを示す細い線（照準線）。レーザーを出していない間だけ表示する。
    /// 長さ・太さ・濃さ・ON/OFFは LaserTuningConfig の「照準表示」で調整できる。
    /// 本番の見た目に差し替えるなら、このコンポーネントを外して別の表示に置き換えればよい。
    /// </summary>
    [RequireComponent(typeof(LaserTurret))]
    public sealed class TurretAimIndicator : MonoBehaviour
    {
        private LaserTurret _turret;
        private LineRenderer _line;
        private Material _material;

        private void Awake()
        {
            _turret = GetComponent<LaserTurret>();

            var child = new GameObject("AimIndicator") { hideFlags = HideFlags.DontSave };
            child.transform.SetParent(transform, false);

            _line = child.AddComponent<LineRenderer>();
            _line.positionCount = 2;
            _line.useWorldSpace = true;
            _line.numCapVertices = 4;
            _line.sortingOrder = -2;

            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _line.sharedMaterial = _material;
            }
        }

        private void LateUpdate()
        {
            var tuning = _turret.tuning;
            var visible = tuning != null
                          && tuning.showAimIndicator
                          && !_turret.IsFiring
                          && _turret.State != TurretState.Overheated;

            _line.enabled = visible;
            if (!visible)
            {
                return;
            }

            var origin = _turret.MuzzlePosition;
            var length = Mathf.Min(tuning.aimIndicatorLength, tuning.range);
            var end = origin + (Vector3)(_turret.AimDirection * length);

            var color = FortressColors.PlayerColor(_turret.playerIndex);
            var faded = new Color(color.r, color.g, color.b, tuning.aimIndicatorAlpha);
            var transparent = new Color(color.r, color.g, color.b, 0f);

            _line.startWidth = tuning.aimIndicatorWidth;
            _line.endWidth = tuning.aimIndicatorWidth;
            _line.startColor = faded;
            _line.endColor = transparent;
            _line.SetPosition(0, origin);
            _line.SetPosition(1, end);
        }

        private void OnDestroy()
        {
            if (_material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_material);
            }
            else
            {
                DestroyImmediate(_material);
            }
        }
    }
}
