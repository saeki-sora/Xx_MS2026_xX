using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 経路探索の本体。範囲内をグリッド化し、シーン上の障害物（NavigationObstacle）を反映して、
    /// コアクリスタルなどの目的地までのフロー・フィールドを計算・保持する。
    /// 障害物が壊れる/再生する/動くたびに自動で再計算される。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NavigationField : MonoBehaviour, IEnemyNavigator
    {
        [Header("対象範囲（この範囲の外は経路計算の対象外）")]
        [Tooltip("経路計算するエリアの中心座標。")]
        public Vector2 areaCenter = Vector2.zero;

        [Tooltip("経路計算するエリアの幅・高さ。敵の湧き位置・砲台・障害物が全て収まる大きさにする。")]
        public Vector2 areaSize = new Vector2(32f, 32f);

        [Tooltip("グリッド1マスの大きさ。小さいほど細かく迂回するが、計算が重くなる。")]
        [Min(0.1f)]
        public float cellSize = 0.5f;

        [Header("経路の性格")]
        [Tooltip("敵の種類にNavigation Profileが設定されていない場合に使う既定の性格。未設定なら標準値。")]
        public NavigationProfile defaultProfile;

        [Tooltip("目的地の周囲、この半径のセルは障害物があっても通行可能として扱う。コアが障害物に囲まれていても到達できるようにするため。")]
        [Range(0, 6)]
        public int goalClearCells = 2;

        [Header("目的地")]
        [Tooltip("シーン内の全てのCoreCrystalControllerを目的地にする。")]
        public bool autoCollectCores = true;

        [Tooltip("コア以外の追加の目的地（拠点など）。")]
        public Transform[] extraGoals;

        [Header("再計算")]
        [Tooltip("障害物の変化で経路を再計算する最短間隔(秒)。短すぎると連続破壊時に重くなる。")]
        [Min(0f)]
        public float rebuildMinInterval = 0.1f;

        private readonly Dictionary<NavigationProfile, FlowFieldResult> _results =
            new Dictionary<NavigationProfile, FlowFieldResult>();

        private readonly List<Vector2> _goals = new List<Vector2>();
        private static readonly List<Collider2D> ColliderBuffer = new List<Collider2D>();

        private NavigationProfile _runtimeDefault;
        private bool _dirty = true;
        private float _lastBuildTime = float.NegativeInfinity;
        private int _lastSignature;

        public NavigationGridData Data { get; private set; }
        public IReadOnlyList<Vector2> CurrentGoals => _goals;

        /// <summary>再計算した回数。デバッグ・テスト用。</summary>
        public int RebuildCount { get; private set; }

        public void MarkDirty()
        {
            _dirty = true;
        }

        public void EnsureBuilt()
        {
            if (Data == null)
            {
                RebuildNow();
            }
        }

        /// <summary>編集中に呼ぶ用。障害物・目的地・設定に変化があった場合だけ再計算する。</summary>
        public void RebuildIfChanged()
        {
            var signature = ComputeSignature();
            if (Data == null || signature != _lastSignature)
            {
                RebuildNow();
            }
        }

        public void RebuildNow()
        {
            Physics2D.SyncTransforms();

            var data = new NavigationGridData(areaCenter, areaSize, cellSize);
            Rasterize(data);
            CollectGoals();

            var profiles = new List<NavigationProfile>();
            foreach (var key in _results.Keys)
            {
                if (key != null)
                {
                    profiles.Add(key);
                }
            }

            var defaultKey = ResolveProfile(null);
            if (!profiles.Contains(defaultKey))
            {
                profiles.Add(defaultKey);
            }

            Data = data;
            _results.Clear();
            foreach (var profile in profiles)
            {
                _results[profile] = FlowFieldSolver.Solve(data, profile.ToSettings(), _goals, goalClearCells);
            }

            _dirty = false;
            _lastBuildTime = Time.time;
            if (!Application.isPlaying)
            {
                // 変化検知(編集中の自動再計算)専用。Play中は障害物の変化通知で足りるので、走査コストを払わない。
                _lastSignature = ComputeSignature();
            }

            RebuildCount++;
        }

        /// <summary>指定プロファイル（nullなら既定）のフロー・フィールドを返す。未計算なら計算する。</summary>
        public FlowFieldResult GetResult(NavigationProfile profile)
        {
            EnsureBuilt();

            var key = ResolveProfile(profile);
            if (!_results.TryGetValue(key, out var result))
            {
                result = FlowFieldSolver.Solve(Data, key.ToSettings(), _goals, goalClearCells);
                _results[key] = result;
            }

            return result;
        }

        public Vector2 GetDirection(Vector2 position, NavigationProfile profile)
        {
            return GetResult(profile).SampleDirection(position);
        }

        public float GetSpeedMultiplier(Vector2 position)
        {
            if (Data == null)
            {
                return 1f;
            }

            var cell = Data.WorldToCellClamped(position);
            return Data.SpeedMultiplier[Data.Index(cell.x, cell.y)];
        }

        private NavigationProfile ResolveProfile(NavigationProfile profile)
        {
            if (profile != null)
            {
                return profile;
            }

            if (defaultProfile != null)
            {
                return defaultProfile;
            }

            if (_runtimeDefault == null)
            {
                _runtimeDefault = NavigationProfile.CreateRuntimeDefault();
            }

            return _runtimeDefault;
        }

        private void CollectGoals()
        {
            _goals.Clear();

            if (autoCollectCores)
            {
                foreach (var core in FindObjectsByType<CoreCrystalController>(FindObjectsSortMode.None))
                {
                    _goals.Add(core.transform.position);
                }
            }

            if (extraGoals == null)
            {
                return;
            }

            foreach (var goal in extraGoals)
            {
                if (goal != null)
                {
                    _goals.Add(goal.position);
                }
            }
        }

        private static void Rasterize(NavigationGridData data)
        {
            var quarter = data.CellSize * 0.3f;
            var samples = new[]
            {
                Vector2.zero,
                new Vector2(quarter, quarter),
                new Vector2(-quarter, quarter),
                new Vector2(quarter, -quarter),
                new Vector2(-quarter, -quarter)
            };

            foreach (var obstacle in NavigationObstacle.FindAll())
            {
                if (obstacle == null || !obstacle.isActiveAndEnabled)
                {
                    continue;
                }

                obstacle.CollectActiveColliders(ColliderBuffer);
                foreach (var collider in ColliderBuffer)
                {
                    Stamp(data, obstacle, collider, samples);
                }
            }
        }

        private static void Stamp(NavigationGridData data, NavigationObstacle obstacle, Collider2D collider, Vector2[] samples)
        {
            var bounds = collider.bounds;
            var min = data.WorldToCellClamped(bounds.min);
            var max = data.WorldToCellClamped(bounds.max);

            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    var center = data.CellCenter(x, y);
                    if (!OverlapsAny(collider, center, samples))
                    {
                        continue;
                    }

                    var index = data.Index(x, y);
                    if (obstacle.mode == NavigationObstacleMode.Block)
                    {
                        data.Blocked[index] = true;
                        continue;
                    }

                    data.ExtraCost[index] = Mathf.Max(data.ExtraCost[index], obstacle.costMultiplier - 1f);
                    data.SpeedMultiplier[index] = Mathf.Min(data.SpeedMultiplier[index], obstacle.speedMultiplier);
                }
            }
        }

        private static bool OverlapsAny(Collider2D collider, Vector2 center, Vector2[] samples)
        {
            foreach (var offset in samples)
            {
                if (collider.OverlapPoint(center + offset))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>設定・障害物・目的地の状態を表す値。変化検知（編集中の自動再計算）に使う。</summary>
        public int ComputeSignature()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + areaCenter.GetHashCode();
                hash = hash * 31 + areaSize.GetHashCode();
                hash = hash * 31 + cellSize.GetHashCode();
                hash = hash * 31 + goalClearCells;

                var settings = ResolveProfile(null).ToSettings();
                hash = hash * 31 + settings.hardInflateCells;
                hash = hash * 31 + settings.softClearanceCells;
                hash = hash * 31 + settings.softClearancePenalty.GetHashCode();

                foreach (var obstacle in NavigationObstacle.FindAll())
                {
                    if (obstacle == null || !obstacle.isActiveAndEnabled)
                    {
                        continue;
                    }

                    hash = hash * 31 + (int)obstacle.mode;
                    hash = hash * 31 + obstacle.costMultiplier.GetHashCode();
                    hash = hash * 31 + obstacle.speedMultiplier.GetHashCode();

                    obstacle.CollectActiveColliders(ColliderBuffer);
                    foreach (var collider in ColliderBuffer)
                    {
                        var bounds = collider.bounds;
                        hash = hash * 31 + bounds.center.GetHashCode();
                        hash = hash * 31 + bounds.size.GetHashCode();
                    }
                }

                if (autoCollectCores)
                {
                    foreach (var core in FindObjectsByType<CoreCrystalController>(FindObjectsSortMode.None))
                    {
                        hash = hash * 31 + core.transform.position.GetHashCode();
                    }
                }

                return hash;
            }
        }

        private void OnEnable()
        {
            EnemyNavigation.Current = this;
            NavigationObstacle.Changed += MarkDirty;
            MarkDirty();
        }

        private void OnDisable()
        {
            NavigationObstacle.Changed -= MarkDirty;
            if (ReferenceEquals(EnemyNavigation.Current, this))
            {
                EnemyNavigation.Current = null;
            }
        }

        private void OnDestroy()
        {
            if (_runtimeDefault == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_runtimeDefault);
            }
            else
            {
                DestroyImmediate(_runtimeDefault);
            }
        }

        private void Update()
        {
            if (_dirty && Time.time - _lastBuildTime >= rebuildMinInterval)
            {
                RebuildNow();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.9f, 0.8f);
            Gizmos.DrawWireCube(areaCenter, areaSize);
        }
    }
}
