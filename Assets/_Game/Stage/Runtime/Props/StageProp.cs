using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 背景オブジェクト（シャンプー・歯ブラシ・床など）1つの目印と設定。
    /// 子の構成:
    ///   Visual    … 揺れの支点（足元）。見た目はこの下に入れる
    ///     (Model) … 取り込んだ3Dモデル。差し替えるとここが入れ替わる
    ///   Footprint … 敵が通れない範囲（PolygonCollider2D）。モデルの形から自動で作る
    /// 役割（壁・床…）ごとの部品の付け外しはステージ背景スタジオが行う。ここは実行中に役割どおりの状態を保つだけ。
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [SelectionBase] // シーンでモデルをクリックしたら、中の部品ではなくこの根元が選ばれるように
    public sealed class StageProp : MonoBehaviour
    {
        private static readonly List<StageProp> Registry = new List<StageProp>();

        [Tooltip("このオブジェクトがゲームで果たす役割。敵の通り方・レーザーへの影響が変わる。")]
        public StagePropRole role = StagePropRole.Wall;

        [Tooltip("「壁」のとき、ONならレーザーはこのオブジェクトを素通りする（細い物・奥の物向け）。")]
        public bool laserPassesThrough;

        [Tooltip("「遅くなる地帯」のときの、敵の移動速度の倍率。0.5なら半分の速さ。")]
        [Range(0.05f, 1f)]
        public float slowZoneSpeed = 0.5f;

        [Tooltip("「遅くなる地帯」のときの、敵が避けたがる強さ。大きいほど遠回りしてでも避ける。")]
        [Min(1f)]
        public float slowZoneAvoidance = 3f;

        public StagePropLookSettings look = new StagePropLookSettings();

        [Tooltip("揺れの支点（足元）。見た目のモデルはこの下に入れる。")]
        public Transform visualRoot;

        [Tooltip("取り込んだ3Dモデルの実体。差し替えボタンはここを入れ替える。")]
        public Transform modelRoot;

        [Tooltip("差し替えの元になったモデルのアセット（FBXやPrefab）。")]
        public GameObject sourceModel;

        [Tooltip("取り込み元のモデルの中で、この物にあたる部分の場所（例: Shampoo や Shelf/Bottle）。空ならモデル全体。")]
        public string sourceNodePath;

        [Tooltip("敵が通れない範囲を持つ子（Footprint）。")]
        public StagePropFootprint footprint;

        [Tooltip("メモ（チーム内の申し送りなど。ゲームには影響しない）。")]
        [TextArea(1, 4)]
        public string memo;

        /// <summary>シーン内で有効な背景オブジェクトの一覧。</summary>
        public static IReadOnlyList<StageProp> All => Registry;

        /// <summary>レーザーを止めるか（壊せる壁は壊れていない間だけ止まる。その切り替えは破壊可能物の仕組みが行う）。</summary>
        public bool BlocksLaser => role == StagePropRole.DestructibleWall || (role == StagePropRole.Wall && !laserPassesThrough);

        /// <summary>見た目の中心（揺れ・透けの判定に使う）。モデルが無ければ自分の位置。</summary>
        public Bounds VisualBounds
        {
            get
            {
                var root = visualRoot != null ? visualRoot : transform;
                var has = false;
                var bounds = new Bounds(root.position, Vector3.zero);
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer)
                    {
                        continue;
                    }

                    if (!has)
                    {
                        bounds = r.bounds;
                        has = true;
                    }
                    else
                    {
                        bounds.Encapsulate(r.bounds);
                    }
                }

                return bounds;
            }
        }

        private void OnEnable()
        {
            Registry.Add(this);
            ApplyRoleState();
        }

        private void OnDisable()
        {
            Registry.Remove(this);
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled)
            {
                ApplyRoleState();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Registry.Clear();
        }

        /// <summary>
        /// 役割に合わせて、通路への影響（NavigationObstacle）とレーザーの当たり（トリガーかどうか）をそろえる。
        /// 壊せる壁は破壊可能物の仕組み（DestructibleCollision）が状態を管理するので触らない。
        /// </summary>
        public void ApplyRoleState()
        {
            if (role == StagePropRole.DestructibleWall)
            {
                return;
            }

            var navigation = GetComponent<NavigationObstacle>();
            var collider = footprint != null ? footprint.Collider : null;
            var needsFootprint = role.NeedsFootprint();

            if (collider != null)
            {
                collider.enabled = needsFootprint;
                collider.isTrigger = role == StagePropRole.SlowZone || (role == StagePropRole.Wall && laserPassesThrough);
            }

            if (navigation == null)
            {
                return;
            }

            var mode = role == StagePropRole.SlowZone ? NavigationObstacleMode.Cost : NavigationObstacleMode.Block;
            var changed = navigation.enabled != needsFootprint || navigation.mode != mode;
            navigation.enabled = needsFootprint;
            navigation.mode = mode;
            if (role == StagePropRole.SlowZone)
            {
                changed |= !Mathf.Approximately(navigation.speedMultiplier, slowZoneSpeed) ||
                           !Mathf.Approximately(navigation.costMultiplier, slowZoneAvoidance);
                navigation.speedMultiplier = slowZoneSpeed;
                navigation.costMultiplier = slowZoneAvoidance;
            }

            if (changed)
            {
                NavigationObstacle.NotifyChanged();
            }
        }
    }
}
