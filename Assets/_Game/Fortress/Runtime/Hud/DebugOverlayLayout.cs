using DDrive.Runtime.Net;
using UnityEngine;

namespace MS2026.Fortress.Hud
{
    /// <summary>左上に縦に並べるデバッグ表示。上から順に、表示中の物だけを詰めて並べる。</summary>
    public enum DebugOverlaySlot
    {
        /// <summary>D-Driveの通信状態(NetDebugOverlay。位置はD-Drive側で固定)。</summary>
        DDriveNetStatus,

        /// <summary>群衆の計測表示(SwarmHud)。</summary>
        SwarmHud,

        /// <summary>カメラの視点表示(CameraGuideOverlay、F7)。</summary>
        CameraViewerLabel,

        Count
    }

    /// <summary>
    /// 画面に直接描くデバッグ表示(OnGUI)が重ならないようにする配置係。画面の使い方:
    /// 左上=デバッグ表示の縦積み(ここ) / 右上=接続画面(FortressConnectUI) / 下中央=自分のゲージ / 左下・右下=空き。
    /// D-Driveの表示(NetDebugOverlay)は位置を変えられないので、その下から並べる。
    /// 各表示は描くたびに自分の大きさを伝え、表示していない物(2フレーム以上描かれていない物)の分は詰める。
    /// </summary>
    public static class DebugOverlayLayout
    {
        public const float Margin = 8f;
        public const float Gap = 6f;

        // D-Drive NetDebugOverlay の固定位置(Rect(8, 8, 260, 168))。D-Drive側を変えられないため値を写しておく。
        private const float DDriveNetStatusHeight = 168f;
        private const float DDriveLookupInterval = 1f;

        private static readonly float[] Heights = new float[(int)DebugOverlaySlot.Count];
        private static readonly int[] LastDrawnFrame = new int[(int)DebugOverlaySlot.Count];
        private static NetDebugOverlay _ddriveOverlay;
        private static float _nextDDriveLookup;

        /// <summary>左上の縦積みで、この表示を置く場所を返す(OnGUIの中で毎回呼ぶ)。</summary>
        public static Rect TopLeft(DebugOverlaySlot slot, float width, float height)
        {
            var index = (int)slot;
            Heights[index] = height;
            LastDrawnFrame[index] = Time.frameCount;

            var y = Margin;
            for (var i = 0; i < index; i++)
            {
                if (IsShown((DebugOverlaySlot)i))
                {
                    y += HeightOf((DebugOverlaySlot)i) + Gap;
                }
            }

            return new Rect(Margin, y, width, height);
        }

        private static bool IsShown(DebugOverlaySlot slot)
        {
            if (slot == DebugOverlaySlot.DDriveNetStatus)
            {
                return IsDDriveNetStatusShown();
            }

            return Time.frameCount - LastDrawnFrame[(int)slot] <= 2;
        }

        private static float HeightOf(DebugOverlaySlot slot)
        {
            return slot == DebugOverlaySlot.DDriveNetStatus ? DDriveNetStatusHeight : Heights[(int)slot];
        }

        private static bool IsDDriveNetStatusShown()
        {
            if (_ddriveOverlay == null && Time.unscaledTime >= _nextDDriveLookup)
            {
                _nextDDriveLookup = Time.unscaledTime + DDriveLookupInterval;
                _ddriveOverlay = Object.FindFirstObjectByType<NetDebugOverlay>();
            }

            return _ddriveOverlay != null && _ddriveOverlay.isActiveAndEnabled && _ddriveOverlay.Visible;
        }
    }
}
