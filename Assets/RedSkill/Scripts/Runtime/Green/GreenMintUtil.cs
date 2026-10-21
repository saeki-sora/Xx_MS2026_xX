using MS2026.Fortress;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Skills.Green
{
    /// <summary>緑スキルの小道具（コアを探す・回復する・画面の上向き）。</summary>
    internal static class GreenMintUtil
    {
        private static CoreCrystalController _core;
        private static float _nextCoreSearch;

        /// <summary>シーンのコア（無ければ null。見つからないときは 1 秒おきに探し直す）。</summary>
        public static CoreCrystalController Core
        {
            get
            {
                if (_core != null)
                {
                    return _core;
                }

                if (Time.unscaledTime < _nextCoreSearch)
                {
                    return null;
                }

                _nextCoreSearch = Time.unscaledTime + 1f;
                _core = Object.FindFirstObjectByType<CoreCrystalController>();
                return _core;
            }
        }

        /// <summary>コアの位置（コアが無ければ fallback）。</summary>
        public static Vector3 CorePosition(Vector3 fallback)
        {
            var core = Core;
            return core != null ? core.transform.position : fallback;
        }

        /// <summary>
        /// コアを回復する（結果を決めるPC＝オフライン／Host だけ。Client には試合の同期でHPが届く）。
        /// コアの部品には「回復」の入口が無いので、Client 用の「HPをこの値にする」（<see cref="CoreCrystalController.ApplyReplicatedHealth"/>）を使う。
        /// 増えたときは HP の変化を知らせるだけで、ダメージの演出は鳴らない（汚れ・ひび・HUD は HP に合わせて戻る）。
        /// </summary>
        /// <returns>実際に増えた量。</returns>
        public static float Heal(float amount, bool stopWhenMatchEnded)
        {
            var core = Core;
            if (core == null || amount <= 0f || !FortressNet.HasSimulationAuthority)
            {
                return 0f;
            }

            if (core.CurrentHealth <= 0f || (stopWhenMatchEnded && FortressMatch.HasEnded))
            {
                return 0f;
            }

            var before = core.CurrentHealth;
            core.ApplyReplicatedHealth(Mathf.Min(core.maxHealth, before + amount));
            return core.CurrentHealth - before;
        }

        /// <summary>この PC の画面の上向き（地面の上で）。カメラが回っていても、絵をこの向きに立てれば画面の上を向く。</summary>
        public static Vector2 ScreenUp
        {
            get
            {
                var camera = Camera.main;
                if (camera == null)
                {
                    return Vector2.up;
                }

                var up = (Vector2)camera.transform.up;
                return up.sqrMagnitude > 0.0001f ? up.normalized : Vector2.up;
            }
        }

        /// <summary>上向き up に絵の上を合わせる回転（地面の上で回す）。</summary>
        public static Quaternion FacingUp(Vector2 up)
        {
            return Quaternion.Euler(0f, 0f, Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg - 90f);
        }

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        public static Vector2 RandomDirection()
        {
            var angle = Random.Range(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>毎秒の数×経過秒を整数の数にする（端数は次のフレームへ持ち越す）。</summary>
        public static int Accumulate(ref float carry, float amount)
        {
            carry += Mathf.Max(0f, amount);
            var count = Mathf.FloorToInt(carry);
            carry -= count;
            return Mathf.Min(count, 200);
        }

        public static float EaseOutBack(float t, float overshoot = 1.7f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
        }

        public static float EaseOutCubic(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }

        public static void DestroySafe(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
