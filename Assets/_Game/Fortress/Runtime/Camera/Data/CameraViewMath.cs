using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// 視点(<see cref="CameraViewSettings"/>)に関する計算をまとめた純関数群。Camera/Transformに依存しないので、
    /// ランタイムの適用・エディタのScene表示/プレビュー/自動フィット・テストが同じ計算を共有できる。
    /// 「地面」はマップのあるXY平面(z=0)。
    /// </summary>
    public static class CameraViewMath
    {
        /// <summary>カメラの位置と回転。見る場所(center)から、傾き(tilt)の分だけ画面下側に引いた位置に置く。</summary>
        public static void GetPose(in CameraViewSettings view, out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.AngleAxis(view.rollDegrees, Vector3.forward)
                       * Quaternion.AngleAxis(-view.tiltDegrees, Vector3.right);

            var forward = rotation * Vector3.forward;
            position = (Vector3)view.center - forward * Mathf.Max(CameraViewSettings.MinDistance, view.distance);
        }

        /// <summary>画面の「上」がワールドのどちらを向いているか(XY平面上の単位ベクトル)。</summary>
        public static Vector2 ScreenUp(in CameraViewSettings view) => Rotate(Vector2.up, view.rollDegrees);

        /// <summary>画面の「右」がワールドのどちらを向いているか(XY平面上の単位ベクトル)。</summary>
        public static Vector2 ScreenRight(in CameraViewSettings view) => Rotate(Vector2.right, view.rollDegrees);

        /// <summary>
        /// ワールド座標をビューポート座標(左下0,0 〜 右上1,1)に変換する。カメラの後ろにある点ならfalse。
        /// </summary>
        public static bool TryWorldToViewport(in CameraViewSettings view, float aspect, Vector3 worldPoint, out Vector2 viewport01)
        {
            GetPose(view, out var position, out var rotation);
            var local = Quaternion.Inverse(rotation) * (worldPoint - position);

            Vector2 ndc;
            if (view.IsOrthographic)
            {
                var halfHeight = Mathf.Max(CameraViewSettings.MinOrthographicSize, view.orthographicSize);
                ndc = new Vector2(local.x / (halfHeight * aspect), local.y / halfHeight);
            }
            else
            {
                if (local.z <= 1e-4f)
                {
                    viewport01 = default;
                    return false;
                }

                var tanHalf = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
                ndc = new Vector2(local.x / (local.z * tanHalf * aspect), local.y / (local.z * tanHalf));
            }

            viewport01 = ndc * 0.5f + new Vector2(0.5f, 0.5f);
            return true;
        }

        /// <summary>点が画面内(ビューポートの指定範囲内)に映るか。</summary>
        public static bool IsVisible(in CameraViewSettings view, float aspect, Vector3 worldPoint, Rect viewportRect)
        {
            return TryWorldToViewport(view, aspect, worldPoint, out var viewport01) && viewportRect.Contains(viewport01);
        }

        /// <summary>
        /// ビューポート上の点を通る視線が地面(z=0)と交わる位置。透視投影で水平線より上を指すなど、地面に届かないならfalse。
        /// </summary>
        public static bool TryViewportToGround(in CameraViewSettings view, float aspect, Vector2 viewport01, out Vector3 groundPoint)
        {
            GetPose(view, out var position, out var rotation);
            var ndc = viewport01 * 2f - Vector2.one;

            Vector3 origin;
            Vector3 direction;
            if (view.IsOrthographic)
            {
                var halfHeight = Mathf.Max(CameraViewSettings.MinOrthographicSize, view.orthographicSize);
                origin = position + rotation * new Vector3(ndc.x * halfHeight * aspect, ndc.y * halfHeight, 0f);
                direction = rotation * Vector3.forward;
            }
            else
            {
                var tanHalf = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
                origin = position;
                direction = rotation * new Vector3(ndc.x * tanHalf * aspect, ndc.y * tanHalf, 1f);
            }

            if (direction.z <= 1e-5f)
            {
                groundPoint = default;
                return false;
            }

            var t = -origin.z / direction.z;
            groundPoint = origin + direction * t;
            groundPoint.z = 0f;
            return t >= 0f;
        }

        /// <summary>
        /// ビューポート上の矩形(既定は画面全体)が地面に映る四隅を、左下→左上→右上→右下の順で書き込む。
        /// 地面に届かない隅があればfalse（その隅は描画用に遠方へ丸めた値が入る）。
        /// </summary>
        public static bool GetGroundQuad(in CameraViewSettings view, float aspect, Rect viewportRect, Vector3[] corners)
        {
            var allHit = true;
            allHit &= TryCorner(view, aspect, new Vector2(viewportRect.xMin, viewportRect.yMin), out corners[0]);
            allHit &= TryCorner(view, aspect, new Vector2(viewportRect.xMin, viewportRect.yMax), out corners[1]);
            allHit &= TryCorner(view, aspect, new Vector2(viewportRect.xMax, viewportRect.yMax), out corners[2]);
            allHit &= TryCorner(view, aspect, new Vector2(viewportRect.xMax, viewportRect.yMin), out corners[3]);
            return allHit;
        }

        public static bool GetGroundQuad(in CameraViewSettings view, float aspect, Vector3[] corners)
        {
            return GetGroundQuad(view, aspect, new Rect(0f, 0f, 1f, 1f), corners);
        }

        private static bool TryCorner(in CameraViewSettings view, float aspect, Vector2 viewport01, out Vector3 corner)
        {
            if (TryViewportToGround(view, aspect, viewport01, out corner))
            {
                return true;
            }

            // 水平線より上: 描画が破綻しないよう、画面上方向の遠方に置いておく。
            var far = view.VisibleHalfHeight * 20f;
            corner = (Vector3)(view.center + ScreenUp(view) * far + ScreenRight(view) * (viewport01.x - 0.5f) * 2f * far * aspect);
            return false;
        }

        /// <summary>
        /// 指定した点がすべて安全域に収まるよう、中心と映る広さを調整した視点を返す（回転・投影方式・傾きは維持）。
        /// 傾き(tilt)がある透視投影では近似になる。
        /// </summary>
        public static CameraViewSettings FitToPoints(CameraViewSettings view, float aspect, IReadOnlyList<Vector2> points, float safeAreaRatio, float padding)
        {
            if (points == null || points.Count == 0)
            {
                return view;
            }

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var point in points)
            {
                var screenAligned = Rotate(point, -view.rollDegrees);
                min = Vector2.Min(min, screenAligned);
                max = Vector2.Max(max, screenAligned);
            }

            var size = max - min;
            var halfHeight = Mathf.Max(size.y * 0.5f, size.x * 0.5f / Mathf.Max(0.01f, aspect));
            halfHeight = halfHeight / Mathf.Clamp(safeAreaRatio, 0.1f, 1f) + Mathf.Max(0f, padding);

            var result = view;
            result.center = Rotate((min + max) * 0.5f, view.rollDegrees);
            return result.WithVisibleHalfHeight(halfHeight);
        }

        /// <summary>中心から見て target が画面の真下に来る回転角(度)。snapDegreesが正ならその単位に丸める。</summary>
        public static float RollToPlaceAtBottom(Vector2 center, Vector2 target, float snapDegrees = 0f)
        {
            var direction = target - center;
            if (direction.sqrMagnitude < 1e-8f)
            {
                return 0f;
            }

            var roll = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
            if (snapDegrees > 0f)
            {
                roll = Mathf.Round(roll / snapDegrees) * snapDegrees;
            }

            return Mathf.Repeat(roll + 180f, 360f) - 180f;
        }

        /// <summary>原点まわりに回転(度、反時計回りが正)。</summary>
        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            var rad = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rad);
            var sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        /// <summary>pivotまわりに回転。</summary>
        public static Vector2 RotateAround(Vector2 v, Vector2 pivot, float degrees) => pivot + Rotate(v - pivot, degrees);
    }
}
