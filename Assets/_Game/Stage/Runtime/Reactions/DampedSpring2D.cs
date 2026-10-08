using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// ばね＋ブレーキで「押されると傾き、手を離すとプルプル戻る」を作る純粋な計算（2軸）。
    /// stiffness が大きいほど速く震え、damping が大きいほど早く止まる。
    /// </summary>
    public struct DampedSpring2D
    {
        public Vector2 Value;
        public Vector2 Velocity;

        /// <summary>target へ向かって1ステップ進める（半陰的オイラー法。dtが大きいときは分割して安定させる）。</summary>
        public void Step(Vector2 target, float stiffness, float damping, float deltaTime)
        {
            const float maxStep = 1f / 120f;
            var remaining = Mathf.Max(0f, deltaTime);
            while (remaining > 0f)
            {
                var dt = Mathf.Min(maxStep, remaining);
                remaining -= dt;
                var acceleration = (target - Value) * stiffness - Velocity * damping;
                Velocity += acceleration * dt;
                Value += Velocity * dt;
            }
        }

        public void Kick(Vector2 impulse) => Velocity += impulse;

        public bool IsResting(float epsilon = 1e-4f) => Value.sqrMagnitude < epsilon * epsilon && Velocity.sqrMagnitude < epsilon * epsilon;
    }
}
