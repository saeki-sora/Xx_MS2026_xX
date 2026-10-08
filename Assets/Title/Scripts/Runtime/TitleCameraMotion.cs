using UnityEngine;

namespace MS2026.Title
{
    /// <summary>
    /// タイトル用カメラの「揺れ」と「寄り」。カメラの今の位置・大きさに“上乗せ”するだけなので、
    /// 別のスクリプトやアニメーションでカメラを動かしても（今後のカメラワーク）、そのまま重ねて効く。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class TitleCameraMotion : MonoBehaviour
    {
        [Tooltip("揺れが収まるまでの秒数。")]
        public float shakeDuration = 0.35f;
        [Tooltip("揺れの細かさ（1秒あたりの揺れの回数）。")]
        public float shakeFrequency = 25f;
        [Tooltip("ブルブル（続く振動）の細かさ（1秒あたりの揺れの回数）。")]
        public float rumbleFrequency = 40f;

        private Camera cam;
        private Vector3 appliedOffset;
        private float appliedZoom;

        private float rumbleStrength;
        private float rumbleTime;
        private float shakeStrength;
        private float shakeTime = float.MaxValue;

        private float zoomFrom;
        private float zoomTo;
        private float zoomDuration;
        private float zoomTime = float.MaxValue;
        private TitleEaseType zoomEase;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        /// <summary>揺らす（<paramref name="strength"/> はワールドの単位での最大のずれ）。</summary>
        public void Shake(float strength)
        {
            if (strength <= 0f) return;
            shakeStrength = Mathf.Max(strength, IsShaking ? CurrentShake : 0f);
            shakeTime = 0f;
        }

        /// <summary>
        /// 画面を寄せる（<paramref name="amount"/> = 0.1 で10%寄る。マイナスなら引く）。<paramref name="seconds"/> 秒かけて寄り、そのまま保つ。
        /// </summary>
        public void ZoomTo(float amount, float seconds, TitleEaseType ease = TitleEaseType.OutCubic)
        {
            zoomFrom = CurrentZoom;
            zoomTo = amount;
            zoomDuration = Mathf.Max(0.0001f, seconds);
            zoomEase = ease;
            zoomTime = 0f;
        }

        /// <summary>揺れ・ブルブル・寄りをすべて止めて、カメラを元の位置・大きさに戻す（次のフレームで反映）。</summary>
        public void ResetMotion()
        {
            rumbleStrength = 0f;
            shakeTime = float.MaxValue;
            zoomFrom = 0f;
            zoomTo = 0f;
            zoomTime = float.MaxValue;
        }

        /// <summary>
        /// 止めるまで続く細かい振動（ブルブル）の強さを決める（ワールドの単位。0で止まる）。毎フレーム呼んで強さを変えてよい。
        /// </summary>
        public void SetRumble(float strength)
        {
            rumbleStrength = Mathf.Max(0f, strength);
        }

        private bool IsShaking => shakeTime < shakeDuration;
        private float CurrentShake => shakeStrength * (1f - Mathf.Clamp01(shakeTime / Mathf.Max(0.0001f, shakeDuration)));
        private float CurrentZoom => zoomTime >= zoomDuration
            ? zoomTo
            : Mathf.LerpUnclamped(zoomFrom, zoomTo, TitleEase.Evaluate(zoomEase, zoomTime / zoomDuration));

        private void LateUpdate()
        {
            // 前のフレームで上乗せした分を外してから、新しい分を乗せる。
            transform.localPosition -= appliedOffset;
            if (cam.orthographic) cam.orthographicSize /= 1f - appliedZoom;
            else cam.fieldOfView /= 1f - appliedZoom;

            var dt = Time.unscaledDeltaTime;
            shakeTime += dt;
            zoomTime += dt;

            appliedOffset = Vector3.zero;
            if (IsShaking)
            {
                var s = CurrentShake;
                var p = shakeTime * shakeFrequency;
                appliedOffset = new Vector3(
                    (Mathf.PerlinNoise(p, 0.3f) * 2f - 1f) * s,
                    (Mathf.PerlinNoise(0.7f, p) * 2f - 1f) * s,
                    0f);
            }

            if (rumbleStrength > 0f)
            {
                rumbleTime += dt;
                var q = rumbleTime * rumbleFrequency;
                appliedOffset += new Vector3(
                    (Mathf.PerlinNoise(q, 5.1f) * 2f - 1f) * rumbleStrength,
                    (Mathf.PerlinNoise(9.7f, q) * 2f - 1f) * rumbleStrength,
                    0f);
            }

            appliedZoom = Mathf.Clamp(CurrentZoom, -0.9f, 0.9f);

            transform.localPosition += appliedOffset;
            if (cam.orthographic) cam.orthographicSize *= 1f - appliedZoom;
            else cam.fieldOfView *= 1f - appliedZoom;
        }
    }
}
