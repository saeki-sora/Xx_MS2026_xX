using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>群衆の演出（光の粒・閃光・衝撃波）の計算部分（Unityの部品に触らない純粋な計算。テストあり）。</summary>
    public static class SwarmFxMath
    {
        /// <summary>
        /// 光の粒の速度を1ステップ進める。散ってから <paramref name="homingDelay"/> 秒までは減速しながら広がり、その後は目標へ向かう。
        /// 目標が無い（倒した人が分からない）ときは、減速し続けるだけ。
        /// </summary>
        public static Vector2 StepSparkVelocity(
            Vector2 velocity, Vector2 position, Vector2? target, float age, float homingDelay, float homingSpeed, float deltaTime)
        {
            if (age < homingDelay || !target.HasValue)
            {
                return velocity * Mathf.Exp(-5f * deltaTime);
            }

            var toTarget = target.Value - position;
            var distance = toTarget.magnitude;
            if (distance < 1e-4f)
            {
                return velocity;
            }

            // 向かい始めてからの時間が長いほど、強く曲がる（取りこぼして周回し続けないように）。
            var desired = toTarget / distance * homingSpeed;
            var steer = homingSpeed * (4f + 6f * (age - homingDelay));
            return Vector2.MoveTowards(velocity, desired, steer * deltaTime);
        }

        /// <summary>閃光の大きさの倍率（広がりながら消える）。t は 0〜1。</summary>
        public static float FlashScale(float t)
        {
            var clamped = Mathf.Clamp01(t);
            return 0.6f + 0.8f * (1f - (1f - clamped) * (1f - clamped));
        }

        /// <summary>閃光の濃さ（すぐ消えていく）。t は 0〜1。</summary>
        public static float FlashAlpha(float t)
        {
            var rest = 1f - Mathf.Clamp01(t);
            return rest * rest;
        }

        /// <summary>光の粒の濃さ。出始めにすっと現れ、寿命の最後で消える。</summary>
        public static float SparkAlpha(float age, float life)
        {
            if (life <= 0f)
            {
                return 0f;
            }

            var fadeIn = Mathf.Clamp01(age / 0.05f);
            var fadeOut = Mathf.Clamp01((life - age) / 0.3f);
            return fadeIn * fadeOut;
        }
    }
}
