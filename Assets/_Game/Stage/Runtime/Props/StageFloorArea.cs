using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】WASD 移動の「動ける範囲」とスマッシュボールの「漂う範囲」をステージから知らせる。
    /// ・役割が「床」で、名前が枠の名前（既定 stage_frame）の物があれば、その枠の形（<see cref="TurretMoveFrame"/>）。
    ///   枠のモデルの頂点から、枠の中心から見た向きごとに「いちばん近い頂点＝内側のふち」「いちばん遠い頂点＝外側のふち」を求める。
    ///   モデルの頂点が読めない（Read/Write がオフ）ときは、足元の範囲（Footprint）の輪郭で代わりにする（内側＝外側になるので少し広い）。
    /// ・枠が無ければ、役割が「床」の物の見た目の範囲（長方形）を全部。
    /// </summary>
    internal static class StageFloorArea
    {
        private const int Bins = 96;

        // 向きの区切りが細かいと、ふちの頂点が入らない区切りが出てギザギザになるので、両隣この数の区切りまで見る
        // （内側は一番近い値・外側は一番遠い値を使うので、どちらも枠からはみ出さない側にずれる）。
        private const int Window = 2;

        private static readonly Dictionary<StageProp, CachedFrame> Cache = new Dictionary<StageProp, CachedFrame>();
        private static readonly List<MeshFilter> Filters = new List<MeshFilter>();
        private static readonly HashSet<StageProp> Warned = new HashSet<StageProp>();
        private static readonly float[] MinRadius = new float[Bins];
        private static readonly float[] MaxRadius = new float[Bins];

        private struct CachedFrame
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public TurretMoveFrame Frame;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Cache.Clear();
            Warned.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            TurretMoveHooks.SetFloorAreaProvider(Collect);
        }

        private static void Collect(string frameName, TurretMoveRegion region)
        {
            var props = StageProp.All;
            if (!string.IsNullOrEmpty(frameName))
            {
                for (var i = 0; i < props.Count; i++)
                {
                    var prop = props[i];
                    if (IsFloor(prop) && prop.gameObject.name == frameName)
                    {
                        region.AddFrame(FrameOf(prop));
                    }
                }

                if (region.Frames.Count > 0)
                {
                    return;
                }
            }

            for (var i = 0; i < props.Count; i++)
            {
                var prop = props[i];
                if (!IsFloor(prop))
                {
                    continue;
                }

                var bounds = prop.VisualBounds;
                if (bounds.size.x > 0.01f && bounds.size.y > 0.01f)
                {
                    region.Rects.Add(Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y));
                }
            }
        }

        private static bool IsFloor(StageProp prop)
        {
            return prop != null && prop.role == StagePropRole.Floor && prop.isActiveAndEnabled;
        }

        // ───────── 枠の形 ─────────

        private static TurretMoveFrame FrameOf(StageProp prop)
        {
            var t = prop.transform;
            if (Cache.TryGetValue(prop, out var cached) && cached.Position == t.position && cached.Rotation == t.rotation && cached.Scale == t.lossyScale)
            {
                return cached.Frame;
            }

            // 中心 = 見た目の範囲の真ん中（輪の形なら輪の中心）。
            var bounds = prop.VisualBounds;
            var center = new Vector2(bounds.center.x, bounds.center.y);
            var frame = (FromMesh(prop, center) || FromFootprint(prop, center)) ? Build(center) : null;
            Cache[prop] = new CachedFrame { Position = t.position, Rotation = t.rotation, Scale = t.lossyScale, Frame = frame };
            return frame;
        }

        private static void ResetBins()
        {
            for (var b = 0; b < Bins; b++)
            {
                MinRadius[b] = float.PositiveInfinity;
                MaxRadius[b] = float.NegativeInfinity;
            }
        }

        private static void AddPoint(Vector2 point, Vector2 center)
        {
            var d = point - center;
            var r = d.magnitude;
            if (r < 1e-4f)
            {
                return;
            }

            var angle = Mathf.Atan2(d.y, d.x); // -π〜π
            var bin = Mathf.Clamp(Mathf.FloorToInt((angle + Mathf.PI) / (Mathf.PI * 2f) * Bins), 0, Bins - 1);
            MinRadius[bin] = Mathf.Min(MinRadius[bin], r);
            MaxRadius[bin] = Mathf.Max(MaxRadius[bin], r);
        }

        private static bool FromMesh(StageProp prop, Vector2 center)
        {
            var root = prop.visualRoot != null ? prop.visualRoot : prop.transform;
            root.GetComponentsInChildren(Filters);
            if (Filters.Count == 0)
            {
                return false;
            }

            ResetBins();
            var any = false;
            foreach (var filter in Filters)
            {
                var mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                if (!mesh.isReadable)
                {
                    WarnOnce(prop, $"枠「{prop.name}」のモデルの頂点が読めません（モデルの Import Settings の Read/Write がオフ）。足元の範囲の輪郭で代わりにします（少し広くなります）。");
                    return false;
                }

                var matrix = filter.transform.localToWorldMatrix;
                var vertices = mesh.vertices;
                for (var i = 0; i < vertices.Length; i++)
                {
                    var w = matrix.MultiplyPoint3x4(vertices[i]);
                    AddPoint(new Vector2(w.x, w.y), center);
                    any = true;
                }
            }

            return any;
        }

        // 足元の範囲（敵が通れない範囲の輪郭）を代わりに使う。床の役割では当たり判定はオフだが、形は残っている。
        private static bool FromFootprint(StageProp prop, Vector2 center)
        {
            var collider = prop.footprint != null ? prop.footprint.GetComponent<PolygonCollider2D>() : null;
            if (collider == null || collider.pathCount == 0)
            {
                return false;
            }

            ResetBins();
            var matrix = collider.transform.localToWorldMatrix;
            var any = false;
            for (var p = 0; p < collider.pathCount; p++)
            {
                foreach (var local in collider.GetPath(p))
                {
                    var w = matrix.MultiplyPoint3x4(local + collider.offset);
                    AddPoint(new Vector2(w.x, w.y), center);
                    any = true;
                }
            }

            if (any)
            {
                // 輪郭の線だけなので、内側のふちは分からない。外側と同じにする（動ける範囲が少し広くなる）。
                for (var b = 0; b < Bins; b++)
                {
                    MinRadius[b] = MaxRadius[b];
                }
            }

            return any;
        }

        private static TurretMoveFrame Build(Vector2 center)
        {
            FillEmptyBins(MinRadius, float.PositiveInfinity);
            FillEmptyBins(MaxRadius, float.NegativeInfinity);
            var inner = new float[Bins];
            var outer = new float[Bins];
            for (var b = 0; b < Bins; b++)
            {
                var lo = float.PositiveInfinity;
                var hi = 0f;
                for (var k = -Window; k <= Window; k++)
                {
                    var i = (b + k + Bins) % Bins;
                    lo = Mathf.Min(lo, MinRadius[i]);
                    hi = Mathf.Max(hi, MaxRadius[i]);
                }

                inner[b] = lo;
                outer[b] = Mathf.Max(hi, lo);
            }

            return new TurretMoveFrame(center, inner, outer);
        }

        // 頂点が1つも入らなかった向きは、両隣の入っている向きの値でつなぐ（内側は近い方、外側は遠い方）。
        private static void FillEmptyBins(float[] values, float empty)
        {
            var isMin = float.IsPositiveInfinity(empty);
            var copy = (float[])values.Clone();
            for (var b = 0; b < Bins; b++)
            {
                if (copy[b] != empty)
                {
                    continue;
                }

                var before = empty;
                var after = empty;
                for (var k = 1; k < Bins && (before == empty || after == empty); k++)
                {
                    if (before == empty)
                    {
                        before = copy[(b - k + Bins) % Bins];
                    }

                    if (after == empty)
                    {
                        after = copy[(b + k) % Bins];
                    }
                }

                values[b] = isMin ? Mathf.Min(before, after) : Mathf.Max(before, after);
            }
        }

        private static void WarnOnce(StageProp prop, string message)
        {
            if (Warned.Add(prop))
            {
                Debug.LogWarning("[実験 WASD 移動] " + message, prop);
            }
        }
    }
}
