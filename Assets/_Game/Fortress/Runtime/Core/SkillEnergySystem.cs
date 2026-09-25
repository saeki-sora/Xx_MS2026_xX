using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// Game 1 用の吸引・スキルエネルギー管理。ステージへエネルギーを散らし、
    /// 弱握り中の砲台へ敵とエネルギーを引き寄せる。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillEnergySystem : MonoBehaviour
    {
        public static SkillEnergySystem Current { get; private set; }

        [Header("エネルギー配置")]
        [Min(0)] public int pickupCount = 20;
        public Vector2 scatterHalfExtents = new Vector2(10f, 8f);
        [Tooltip("メインカメラの表示範囲全体にエネルギーを配置し、その範囲で漂わせる。")]
        public bool spreadAcrossScreen;
        [Min(0.01f)] public float pickupScale = 0.28f;
        [Min(0.1f)] public float energyPerPickup = 10f;
        [Min(1f)] public float maxGauge = 100f;

        [Header("エネルギーの漂流")]
        [Tooltip("吸引されていない水色の物体が漂う速さ。0で停止する。")]
        [Min(0f)] public float pickupDriftSpeed = 0.7f;

        [Header("吸引")]
        [Min(0.05f)] public float collectRadius = 0.65f;
        [Tooltip("水色のエネルギー物体の吸引速度倍率。敵の吸引速度とは別に調整する。")]
        [Range(0f, 1f)] public float pickupPullScale = 1f;
        [Range(0f, 1f)] public float enemyPullScale = 0.55f;

        private sealed class Pickup
        {
            public Transform transform;
            public Vector2 direction;
            public int lastSuctionFrame = -1;
        }

        private readonly List<Pickup> _pickups = new List<Pickup>();
        private readonly float[] _gauges = new float[4];

        public float GetGauge01(int playerIndex)
        {
            return IsValidPlayer(playerIndex) && maxGauge > 0f
                ? Mathf.Clamp01(_gauges[playerIndex] / maxGauge)
                : 0f;
        }

        private void OnEnable()
        {
            Current = this;
        }

        private void Start()
        {
            SpawnPickups();
        }

        private void LateUpdate()
        {
            var bounds = GetPickupBounds();
            for (var i = _pickups.Count - 1; i >= 0; i--)
            {
                var pickup = _pickups[i];
                if (pickup.transform == null)
                {
                    _pickups.RemoveAt(i);
                    continue;
                }

                // 吸引中は砲台へ向かう動きを優先する。
                if (pickup.lastSuctionFrame == Time.frameCount)
                {
                    continue;
                }

                var position = (Vector2)pickup.transform.position;
                var direction = pickup.direction;
                var next = position + direction * pickupDriftSpeed * Time.deltaTime;
                if ((next.x >= bounds.xMax && direction.x > 0f)
                    || (next.x <= bounds.xMin && direction.x < 0f))
                {
                    direction.x = -direction.x;
                }
                if ((next.y >= bounds.yMax && direction.y > 0f)
                    || (next.y <= bounds.yMin && direction.y < 0f))
                {
                    direction.y = -direction.y;
                }

                pickup.direction = direction;
                next = position + direction * pickupDriftSpeed * Time.deltaTime;
                pickup.transform.position = new Vector3(
                    Mathf.Clamp(next.x, bounds.xMin, bounds.xMax),
                    Mathf.Clamp(next.y, bounds.yMin, bounds.yMax), pickup.transform.position.z);
            }
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        public void ApplySuction(int playerIndex, Vector2 origin, float range, float speed, float deltaTime)
        {
            if (!IsValidPlayer(playerIndex) || range <= 0f || speed <= 0f)
            {
                return;
            }

            for (var i = _pickups.Count - 1; i >= 0; i--)
            {
                var pickupData = _pickups[i];
                var pickup = pickupData.transform;
                if (pickup == null)
                {
                    _pickups.RemoveAt(i);
                    continue;
                }

                var toOrigin = origin - (Vector2)pickup.position;
                if (toOrigin.sqrMagnitude > range * range)
                {
                    continue;
                }

                pickupData.lastSuctionFrame = Time.frameCount;
                pickup.position = Vector2.MoveTowards(pickup.position, origin, speed * pickupPullScale * deltaTime);
                if (((Vector2)pickup.position - origin).sqrMagnitude <= collectRadius * collectRadius)
                {
                    _gauges[playerIndex] = Mathf.Min(maxGauge, _gauges[playerIndex] + energyPerPickup);
                    _pickups.RemoveAt(i);
                    Destroy(pickup.gameObject);
                }
            }

            var enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            var enemySpeed = speed * enemyPullScale;
            SwarmSystem.Current?.QueueSuction(origin, range, enemySpeed);
            foreach (var enemy in enemies)
            {
                if (enemy == null)
                {
                    continue;
                }

                var enemyPosition = (Vector2)enemy.transform.position;
                if ((enemyPosition - origin).sqrMagnitude <= range * range)
                {
                    enemy.ApplySuction(origin, enemySpeed, deltaTime);
                }
            }
        }

        private Rect GetPickupBounds()
        {
            var center = Vector2.zero;
            var halfExtents = scatterHalfExtents;
            var camera = spreadAcrossScreen ? Camera.main : null;
            if (camera != null && camera.orthographic)
            {
                center = camera.transform.position;
                halfExtents = new Vector2(camera.orthographicSize * camera.aspect, camera.orthographicSize);
            }

            // 物体の半径分だけ内側で折り返し、画面端でも全体が見えるようにする。
            var margin = pickupScale * 0.5f;
            halfExtents = new Vector2(Mathf.Max(0.01f, halfExtents.x - margin),
                Mathf.Max(0.01f, halfExtents.y - margin));
            return new Rect(center - halfExtents, halfExtents * 2f);
        }

        private void SpawnPickups()
        {
            var previousState = Random.state;
            Random.InitState(20260925);
            var bounds = GetPickupBounds();
            var columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(pickupCount * bounds.width / bounds.height)));
            var rows = Mathf.Max(1, Mathf.CeilToInt((float)pickupCount / columns));

            for (var i = 0; i < pickupCount; i++)
            {
                var pickup = new GameObject($"SkillEnergy_{i + 1}");
                pickup.transform.SetParent(transform, false);
                // 画面を区切って配置し、中央だけに偏らず端や四隅にも広げる。
                var rowColumns = Mathf.Min(columns, pickupCount - i / columns * columns);
                var x = spreadAcrossScreen ? (i % columns + Random.Range(0.1f, 0.9f)) / rowColumns : Random.value;
                var y = spreadAcrossScreen ? (i / columns + Random.Range(0.1f, 0.9f)) / rows : Random.value;
                pickup.transform.position = new Vector3(
                    Mathf.Lerp(bounds.xMin, bounds.xMax, x),
                    Mathf.Lerp(bounds.yMin, bounds.yMax, y), 0f);
                pickup.transform.localScale = Vector3.one * pickupScale;

                var renderer = pickup.AddComponent<SpriteRenderer>();
                renderer.sprite = PlaceholderSpriteFactory.CreateCircleSprite();
                renderer.color = Color.Lerp(new Color(0.15f, 0.9f, 1f), Color.white, i % 3 * 0.2f);
                renderer.sortingOrder = 20;
                var angle = i * 2.399963f;
                _pickups.Add(new Pickup
                {
                    transform = pickup.transform,
                    direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                });
            }

            Random.state = previousState;
        }

        private void OnGUI()
        {
            const float width = 180f;
            const float height = 20f;
            const float gap = 8f;
            var startX = (Screen.width - (width * 4f + gap * 3f)) * 0.5f;

            for (var i = 0; i < _gauges.Length; i++)
            {
                var rect = new Rect(startX + i * (width + gap), 18f, width, height);
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = FortressColors.PlayerColor(i);
                GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * GetGauge01(i), rect.height - 4f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(rect, $"  P{i + 1} SKILL  {Mathf.RoundToInt(_gauges[i])}/{Mathf.RoundToInt(maxGauge)}");
            }

            GUI.color = Color.white;
        }

        private static bool IsValidPlayer(int playerIndex)
        {
            return playerIndex >= 0 && playerIndex < 4;
        }
    }
}
