using MS2026.Fortress.Billboards;
using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// リグが使っている視点プリセットのビルボード設定を、ゲーム中に適用し続ける。
    /// プリセットを切り替えれば立たせる/寝かせるも一緒に切り替わり、OFFのプリセットでは全て元の描画に戻る。
    /// このコンポーネントを外す(無効にする)だけでも、いつでも元の描画に戻せる。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1001)]
    public sealed class BillboardDirector : MonoBehaviour
    {
        [Tooltip("設定の読み取り元のリグ。未設定ならシーンで有効なリグを使う。")]
        public FortressCameraRig rig;

        [Tooltip("立たせる対象をシーンから集め直す間隔(秒)。後から出現した敵・破壊可能物もこの間隔で拾う。")]
        [Min(0.05f)]
        public float rescanInterval = 0.5f;

        private BillboardController _controller;
        private BillboardSettings _lastEnabledSettings;
        private float _nextRescanTime;

        public BillboardController Controller => _controller ??= new BillboardController();

        private FortressCameraRig Rig => rig != null ? rig : FortressCameraRig.Active;

        /// <summary>次のフレームで対象を集め直す(エディタから設定を変えた直後など)。</summary>
        public void RequestRescan() => _nextRescanTime = 0f;

        private void LateUpdate()
        {
            var settings = ResolveSettings();
            var rescan = Time.unscaledTime >= _nextRescanTime;
            if (rescan)
            {
                _nextRescanTime = Time.unscaledTime + rescanInterval;
            }

            Controller.Apply(settings, rescan);
        }

        private void OnDisable()
        {
            _controller?.Restore();
            _lastEnabledSettings = null;
        }

        private void OnDestroy()
        {
            _controller?.Dispose();
            _controller = null;
        }

        /// <summary>
        /// 立たせるプリセットから寝かせるプリセットへ切り替える途中は、カメラがまだ傾いているので、
        /// 遷移が終わるまで直前の設定を使い続ける(途中で絵が急に寝ないように)。
        /// 逆方向は、遷移の始めはカメラがほぼ真上なので、すぐ切り替えても見た目は変わらない。
        /// </summary>
        private BillboardSettings ResolveSettings()
        {
            var currentRig = Rig;
            var settings = currentRig != null && currentRig.preset != null ? currentRig.preset.billboard : null;

            if (settings != null && settings.enabled)
            {
                _lastEnabledSettings = settings;
                return settings;
            }

            if (_lastEnabledSettings != null && currentRig != null && currentRig.IsBlending)
            {
                return _lastEnabledSettings;
            }

            _lastEnabledSettings = null;
            return settings;
        }
    }
}
