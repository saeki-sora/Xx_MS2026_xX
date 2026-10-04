using System;
using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>カメラの投影方式。</summary>
    public enum CameraProjection
    {
        /// <summary>正投影（2D）。映る範囲は Orthographic Size で決まる。</summary>
        Orthographic,

        /// <summary>透視投影。映る範囲は視野角(FOV)と距離で決まる。</summary>
        Perspective
    }

    /// <summary>
    /// 1人分の「視点」。見る場所(center)・画面の回転(roll)・映す広さ(ズーム)・投影方式を持つ値型。
    /// マップはXY平面(z=0)にあり、カメラは+Z方向を向いて見下ろす前提。
    /// 値型なので、補間(<see cref="Lerp"/>)や一時演出(ズーム・揺れ)で気軽にコピーして書き換えられる。
    /// </summary>
    [Serializable]
    public struct CameraViewSettings
    {
        public const float MinOrthographicSize = 0.1f;
        public const float MinFieldOfView = 1f;
        public const float MaxFieldOfView = 170f;
        public const float MinDistance = 0.1f;
        public const float MaxTiltDegrees = 80f;

        [Tooltip("画面の中心に来るワールド座標(XY平面上)。")]
        public Vector2 center;

        [Tooltip("画面の回転(度)。0で+Yが画面の上。反時計回りが正。90度単位にすると各プレイヤーの砲台を画面下に持ってこられる。")]
        public float rollDegrees;

        [Tooltip("投影方式。正投影(2D)が既定。")]
        public CameraProjection projection;

        [Tooltip("正投影時、画面の縦半分に映るワールドの長さ。小さいほどズームイン。")]
        public float orthographicSize;

        [Tooltip("透視投影時の縦の視野角(度)。小さいほどズームイン。")]
        public float fieldOfView;

        [Tooltip("カメラから見る場所(center)までの距離。透視投影では映る範囲にも影響する。")]
        public float distance;

        [Tooltip("見下ろしの傾き(度)。0で真上から、値を増やすと画面下側から覗き込む角度になる。主に透視投影用。")]
        public float tiltDegrees;

        public static CameraViewSettings Default => new CameraViewSettings
        {
            center = Vector2.zero,
            rollDegrees = 0f,
            projection = CameraProjection.Orthographic,
            orthographicSize = 14f,
            fieldOfView = 60f,
            distance = 10f,
            tiltDegrees = 0f
        };

        public bool IsOrthographic => projection == CameraProjection.Orthographic;

        /// <summary>
        /// 見る場所(center)の高さで、画面の縦半分に映るワールドの長さ。投影方式が違っても「映る広さ」を比べられる共通の尺度。
        /// </summary>
        public float VisibleHalfHeight => IsOrthographic
            ? orthographicSize
            : distance * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);

        /// <summary>映る広さ(縦半分)を指定値にした視点を返す。投影方式は変えない（透視投影ならFOVを変える）。</summary>
        public CameraViewSettings WithVisibleHalfHeight(float halfHeight)
        {
            var result = this;
            halfHeight = Mathf.Max(MinOrthographicSize, halfHeight);

            if (IsOrthographic)
            {
                result.orthographicSize = halfHeight;
            }
            else
            {
                var fov = 2f * Mathf.Atan(halfHeight / Mathf.Max(MinDistance, distance)) * Mathf.Rad2Deg;
                result.fieldOfView = Mathf.Clamp(fov, MinFieldOfView, MaxFieldOfView);
            }

            return result;
        }

        /// <summary>映る広さを保ったまま投影方式を切り替える（正投影⇔透視投影で画面の見え方が極力変わらないようにする）。</summary>
        public CameraViewSettings WithProjection(CameraProjection newProjection)
        {
            if (newProjection == projection)
            {
                return this;
            }

            var halfHeight = VisibleHalfHeight;
            var result = this;
            result.projection = newProjection;
            return result.WithVisibleHalfHeight(halfHeight);
        }

        /// <summary>範囲外の値(負のサイズ等)を丸めた視点を返す。ツールやアセットの手入力の後に通す。</summary>
        public CameraViewSettings Sanitized()
        {
            var result = this;
            result.orthographicSize = Mathf.Max(MinOrthographicSize, orthographicSize);
            result.fieldOfView = Mathf.Clamp(fieldOfView, MinFieldOfView, MaxFieldOfView);
            result.distance = Mathf.Max(MinDistance, distance);
            result.tiltDegrees = Mathf.Clamp(tiltDegrees, 0f, MaxTiltDegrees);
            result.rollDegrees = Mathf.Repeat(rollDegrees + 180f, 360f) - 180f;
            return result;
        }

        /// <summary>
        /// 2つの視点を補間する。回転は近い向きに回り、ズームは「映る広さ」で補間するので、
        /// 投影方式が異なる視点間でも破綻しない（投影方式自体は中間点で切り替わる）。
        /// </summary>
        public static CameraViewSettings Lerp(CameraViewSettings from, CameraViewSettings to, float t)
        {
            t = Mathf.Clamp01(t);

            var result = new CameraViewSettings
            {
                center = Vector2.Lerp(from.center, to.center, t),
                rollDegrees = Mathf.LerpAngle(from.rollDegrees, to.rollDegrees, t),
                projection = t < 0.5f ? from.projection : to.projection,
                orthographicSize = Mathf.Lerp(from.orthographicSize, to.orthographicSize, t),
                fieldOfView = Mathf.Lerp(from.fieldOfView, to.fieldOfView, t),
                distance = Mathf.Lerp(from.distance, to.distance, t),
                tiltDegrees = Mathf.Lerp(from.tiltDegrees, to.tiltDegrees, t)
            };

            if (from.projection != to.projection)
            {
                result = result.WithVisibleHalfHeight(Mathf.Lerp(from.VisibleHalfHeight, to.VisibleHalfHeight, t));
            }

            return result;
        }

        public bool Approximately(CameraViewSettings other, float epsilon = 1e-4f)
        {
            return projection == other.projection
                   && (center - other.center).sqrMagnitude <= epsilon * epsilon
                   && Mathf.Abs(Mathf.DeltaAngle(rollDegrees, other.rollDegrees)) <= epsilon
                   && Mathf.Abs(orthographicSize - other.orthographicSize) <= epsilon
                   && Mathf.Abs(fieldOfView - other.fieldOfView) <= epsilon
                   && Mathf.Abs(distance - other.distance) <= epsilon
                   && Mathf.Abs(tiltDegrees - other.tiltDegrees) <= epsilon;
        }
    }
}
