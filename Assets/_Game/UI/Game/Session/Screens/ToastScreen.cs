using UnityEngine;

namespace MS2026.UI.Game
{
    /// <summary>
    /// 画面の端に少しだけ出る通知（例:「たろう（P2）が入りました」）。開いてから決めた秒数で自分で閉じる。
    /// 文は値の部品（toast.text）で出す。出ている間に次の通知が来たら、時間を数え直す。
    /// </summary>
    [AddComponentMenu("UI Studio/ロビー/通知 (ToastScreen)")]
    [RequireComponent(typeof(UiScreen))]
    public sealed class ToastScreen : MonoBehaviour
    {
        [Tooltip("出しておく秒数。")]
        [Min(0.5f)]
        public float showSeconds = 2.5f;

        private UiScreen _screen;
        private float _closeAt = float.MaxValue;

        private void Awake()
        {
            _screen = GetComponent<UiScreen>();
            _screen.Shown += OnShown;
        }

        private void OnDestroy()
        {
            if (_screen != null)
            {
                _screen.Shown -= OnShown;
            }
        }

        /// <summary>手動の動きのうち、この名前の物を「次の通知が来た」ときに再生する。</summary>
        public const string BumpMotionName = "bump";

        /// <summary>出しておく時間を数え直し、中の部品の「bump」という名前の動き（手動）を再生する。</summary>
        public void Restart()
        {
            _closeAt = Time.unscaledTime + showSeconds;
            foreach (var motion in GetComponentsInChildren<UiElementMotion>())
            {
                if (motion.Has(UiMotionTrigger.Manual))
                {
                    motion.PlayNamed(BumpMotionName);
                }
            }
        }

        private void OnShown(UiScreen _) => _closeAt = Time.unscaledTime + showSeconds;

        private void Update()
        {
            if (Time.unscaledTime < _closeAt)
            {
                return;
            }

            _closeAt = float.MaxValue;
            var root = UiRoot.Active;
            if (root != null && root.IsOpen(_screen.screenId))
            {
                root.Close(_screen.screenId);
            }
        }
    }
}
