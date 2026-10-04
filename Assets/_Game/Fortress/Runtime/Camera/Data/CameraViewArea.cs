using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// 視点がマップ(XY平面)のどの範囲を映すかの計算。「全員の画面に映る範囲」をゲーム側(スマッシュボールなど)が使う。
    /// 傾き(tilt)は無視する。回転(roll)している視点は、どの向きでも確実に映る内側の正方形で近似する(控えめに見積もる)。
    /// </summary>
    public static class CameraViewArea
    {
        /// <summary>1つの視点が映す範囲(XY平面の矩形)。</summary>
        public static Rect VisibleRect(CameraViewSettings view, float aspect)
        {
            var halfHeight = Mathf.Max(0f, view.VisibleHalfHeight);
            var halfWidth = halfHeight * Mathf.Max(0.01f, aspect);

            if (Mathf.Abs(Mathf.DeltaAngle(0f, view.rollDegrees)) > 0.5f)
            {
                // 回転した長方形に内接する円の、さらに内側の正方形。どの角度でも必ず画面内に収まる。
                var radius = Mathf.Min(halfWidth, halfHeight);
                halfWidth = halfHeight = radius * 0.70710677f;
            }

            return new Rect(view.center.x - halfWidth, view.center.y - halfHeight, halfWidth * 2f, halfHeight * 2f);
        }

        /// <summary>
        /// 4人全員の画面に共通して映る範囲。共通部分が無いとき(視点が全く重ならない)は全体視点の範囲を返す。
        /// </summary>
        public static Rect SharedPlayerRect(CameraViewPreset preset, float aspect)
        {
            var players = new CameraViewSettings[ViewerIndex.PlayerCount];
            for (var i = 0; i < players.Length; i++)
            {
                players[i] = preset.GetView(i);
            }

            return SharedPlayerRect(players, preset.overview, aspect);
        }

        public static Rect SharedPlayerRect(IReadOnlyList<CameraViewSettings> players, CameraViewSettings overview, float aspect)
        {
            if (players == null || players.Count == 0)
            {
                return VisibleRect(overview, aspect);
            }

            var shared = VisibleRect(players[0], aspect);
            for (var i = 1; i < players.Count; i++)
            {
                if (!TryIntersect(shared, VisibleRect(players[i], aspect), out shared))
                {
                    return VisibleRect(overview, aspect);
                }
            }

            return shared;
        }

        /// <summary>周囲をmarginだけ狭めた矩形。狭めすぎて無くなる場合は中心の点にする。</summary>
        public static Rect Inset(Rect rect, float margin)
        {
            var width = Mathf.Max(0f, rect.width - margin * 2f);
            var height = Mathf.Max(0f, rect.height - margin * 2f);
            return new Rect(rect.center.x - width * 0.5f, rect.center.y - height * 0.5f, width, height);
        }

        public static bool TryIntersect(Rect a, Rect b, out Rect result)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMax = Mathf.Min(a.yMax, b.yMax);
            if (xMax <= xMin || yMax <= yMin)
            {
                result = default;
                return false;
            }

            result = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }
    }
}
