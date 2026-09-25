using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 障害物が破壊された瞬間に再生する「ひびが限界に達して崩れ落ちる」演出。
    /// 複数の小さな破片をランダムな方向へ弾きつつ、重力で下に落としながら回転・フェードさせる。
    /// 何度も破壊が起きるとGameObjectの生成/破棄が積み重なって重くなりやすいため、
    /// 使い終わったインスタンスは破棄せずプールして使い回す。
    /// ObstacleVisualの定常表示(健在/ゴースト)とは責務を分けている。
    /// </summary>
    public sealed class ObstacleBreakEffect : MonoBehaviour
    {
        private const int MaxFragments = 12; // DestructibleObstacleTuning.breakFragmentCountの上限と合わせる

        private static readonly Stack<ObstacleBreakEffect> Pool = new Stack<ObstacleBreakEffect>();

        [Tooltip("破片が消えるまでの時間(秒)。")]
        [Min(0.05f)]
        public float duration = 0.6f;

        [Tooltip("破片が飛び散る速さ(ワールド単位/秒)。")]
        [Min(0f)]
        public float launchSpeed = 3f;

        [Tooltip("破片の回転速度(度/秒)。")]
        public float spinSpeedDegrees = 320f;

        [Tooltip("破片が落ちる重力加速度(ワールド単位/秒^2)。ワールド座標の下方向に働く。")]
        [Min(0f)]
        public float gravity = 6f;

        private Fragment[] _fragmentPool;
        private int _activeFragmentCount;
        private float _elapsed;

        private struct Fragment
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float SpinDirection;
        }

        /// <summary>
        /// 最初の破壊が起きた瞬間にGameObject生成のコストを払わずに済むよう、1体分プールを温めておく。
        /// 障害物の配置時に呼んでおく想定(ObstacleVisual.EnsureRenderers参照)。
        /// </summary>
        public static void Prewarm()
        {
            if (Pool.Count > 0)
            {
                return;
            }

            var effect = CreatePooledInstance();
            effect.gameObject.SetActive(false);
            Pool.Push(effect);
        }

        /// <summary>指定の位置・向き・サイズ・色で破片を弾け飛ばす。プールに使い回せるインスタンスがあればそれを使う。</summary>
        public static void Spawn(
            Vector3 position, Quaternion rotation, Vector2 size, Color color,
            int fragmentCount, float duration, float launchSpeed, float spinSpeedDegrees, float gravity)
        {
            ObstacleBreakEffect effect = null;
            while (Pool.Count > 0 && effect == null)
            {
                effect = Pool.Pop(); // ドメインリロード無効設定などでプール内が無効になっているケースへの保険。
            }

            effect ??= CreatePooledInstance();

            effect.gameObject.SetActive(true);
            effect.transform.SetPositionAndRotation(position, rotation);
            effect.duration = Mathf.Max(0.05f, duration);
            effect.launchSpeed = Mathf.Max(0f, launchSpeed);
            effect.spinSpeedDegrees = spinSpeedDegrees;
            effect.gravity = Mathf.Max(0f, gravity);
            effect._elapsed = 0f;
            effect.ActivateFragments(Mathf.Clamp(fragmentCount, 1, MaxFragments), size, color);
        }

        private static ObstacleBreakEffect CreatePooledInstance()
        {
            var go = new GameObject("ObstacleBreakEffect (Pooled)");
            var effect = go.AddComponent<ObstacleBreakEffect>();
            effect.BuildFragmentPool();
            return effect;
        }

        private void BuildFragmentPool()
        {
            _fragmentPool = new Fragment[MaxFragments];

            for (var i = 0; i < MaxFragments; i++)
            {
                var go = new GameObject($"Fragment{i}");
                go.transform.SetParent(transform, false);

                var renderer = go.AddComponent<SpriteRenderer>();
                _fragmentPool[i] = new Fragment { Transform = go.transform, Renderer = renderer };
            }
        }

        private void ActivateFragments(int fragmentCount, Vector2 size, Color color)
        {
            _activeFragmentCount = fragmentCount;
            var fragmentScale = Mathf.Max(0.1f, Mathf.Min(size.x, size.y)) * 0.5f;

            for (var i = 0; i < _fragmentPool.Length; i++)
            {
                var isActive = i < fragmentCount;
                var fragment = _fragmentPool[i];
                fragment.Transform.gameObject.SetActive(isActive);

                if (!isActive)
                {
                    continue;
                }

                // 破片ごとにランダムな位置・向きから飛び散らせ、square/triangleを混ぜて瓦礫っぽくする。
                var offset01 = new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f));
                fragment.Transform.localPosition = new Vector3(offset01.x * size.x, offset01.y * size.y, 0f);
                fragment.Transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                fragment.Transform.localScale = Vector3.one * fragmentScale * Random.Range(0.5f, 1f);

                fragment.Renderer.sprite = Random.value < 0.5f
                    ? PlaceholderSpriteFactory.CreateSquareSprite()
                    : PlaceholderSpriteFactory.CreateTriangleHalfSprite();
                fragment.Renderer.color = color;

                var burstDirection = (Vector2)(Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)) * Vector3.up);
                fragment.Velocity = burstDirection * launchSpeed * Random.Range(0.4f, 1f);
                fragment.SpinDirection = Random.value < 0.5f ? -1f : 1f;

                _fragmentPool[i] = fragment; // structなのでフィールド更新後に書き戻す。
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            var t01 = Mathf.Clamp01(_elapsed / duration);

            for (var i = 0; i < _activeFragmentCount; i++)
            {
                MoveFragment(ref _fragmentPool[i], t01);
            }

            if (_elapsed >= duration)
            {
                ReturnToPool();
            }
        }

        private void ReturnToPool()
        {
            gameObject.SetActive(false);
            Pool.Push(this);
        }

        private void MoveFragment(ref Fragment fragment, float t01)
        {
            // ワールド空間の下方向へ重力を効かせる(オブジェクトの回転に関係なく常に画面の下へ落ちる)。
            fragment.Velocity += Vector2.down * (gravity * Time.deltaTime);
            fragment.Transform.position += (Vector3)(fragment.Velocity * Time.deltaTime);
            fragment.Transform.Rotate(0f, 0f, spinSpeedDegrees * fragment.SpinDirection * Time.deltaTime);

            var color = fragment.Renderer.color;
            color.a = Mathf.Lerp(1f, 0f, t01);
            fragment.Renderer.color = color;
        }
    }
}
