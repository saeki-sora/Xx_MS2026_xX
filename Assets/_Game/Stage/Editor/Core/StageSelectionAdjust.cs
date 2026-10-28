using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 選んでいる背景（複数可）に同じだけの変更を掛ける操作。配置ページのホイール・キー・右の欄が使う。
    /// 連続した変更は <see cref="StageUndoBurst"/> で1回の「元に戻す」にまとまる。
    /// </summary>
    public static class StageSelectionAdjust
    {
        /// <summary>床の上でずらす（メートル）。</summary>
        public static void Nudge(IReadOnlyList<StageProp> props, Vector2 delta)
        {
            Run(props, "背景を動かす", prop =>
            {
                Undo.RecordObject(prop.transform, "背景を動かす");
                StagePropTransformOps.Move(prop, delta);
            });
        }

        /// <summary>それぞれの根元を中心に回す（度。反時計回りが正）。</summary>
        public static void Rotate(IReadOnlyList<StageProp> props, float degrees)
        {
            Run(props, "背景を回す", prop =>
            {
                Undo.RecordObject(prop.transform, "背景を回す");
                StagePropTransformOps.SetYaw(prop, StagePropTransformOps.Yaw(prop) + degrees);
            });
        }

        /// <summary>向きをそろえる（度）。</summary>
        public static void SetYaw(IReadOnlyList<StageProp> props, float degrees)
        {
            Run(props, "背景を回す", prop =>
            {
                Undo.RecordObject(prop.transform, "背景を回す");
                StagePropTransformOps.SetYaw(prop, degrees);
            });
        }

        /// <summary>見た目のモデルの傾きを足す（度。それぞれの根元の向きが基準）。</summary>
        public static void Tilt(IReadOnlyList<StageProp> props, Vector2 delta)
        {
            Run(props, "背景を傾ける", prop => StagePropTransformOps.SetTilt(prop, StagePropTransformOps.Tilt(prop) + delta));
        }

        /// <summary>傾きをそろえる（度。0, 0 でまっすぐに立てる）。</summary>
        public static void SetTilt(IReadOnlyList<StageProp> props, Vector2 tilt)
        {
            Run(props, "背景を傾ける", prop => StagePropTransformOps.SetTilt(prop, tilt));
        }

        /// <summary>X か Y の片方だけをそろえる（axis 0 = X、1 = Y）。</summary>
        public static void SetTiltAxis(IReadOnlyList<StageProp> props, int axis, float degrees)
        {
            Run(props, "背景を傾ける", prop =>
            {
                var tilt = StagePropTransformOps.Tilt(prop);
                tilt[axis] = degrees;
                StagePropTransformOps.SetTilt(prop, tilt);
            });
        }

        /// <summary>
        /// てっぺんを床の上の向き worldDirection（画面の上下左右など）へ degrees だけ倒す。
        /// 根元の向きに関係なく、見たとおりの向きに倒れる。
        /// </summary>
        public static void Lean(IReadOnlyList<StageProp> props, Vector2 worldDirection, float degrees)
        {
            if (worldDirection.sqrMagnitude < 1e-8f)
            {
                return;
            }

            Run(props, "背景を傾ける", prop =>
            {
                var up = StagePropTransformOps.TiltUpWorld(prop);
                var axis = Vector3.Cross(up, new Vector3(worldDirection.x, worldDirection.y, 0f));
                if (axis.sqrMagnitude < 1e-8f)
                {
                    return; // もうその向きに真横まで倒れている
                }

                var newUp = Quaternion.AngleAxis(degrees, axis.normalized) * up;
                StagePropTransformOps.SetTilt(prop, StagePlaceMath.TiltFromUp(Quaternion.Inverse(prop.transform.rotation) * newUp));
            });
        }

        /// <summary>大きさを倍にする（足元を中心に）。</summary>
        public static void Scale(IReadOnlyList<StageProp> props, float factor)
        {
            if (Mathf.Approximately(factor, 1f))
            {
                return;
            }

            Run(props, "大きさを変える", prop => StagePropTransformOps.ScaleModel(prop, factor));
        }

        /// <summary>
        /// 1方向だけ伸び縮みさせて、primary のその物自身の大きさ（sizeAxis: 0=幅、1=奥行、2=高さ。<see cref="StagePropTransformOps.OwnSize"/>）を
        /// size にする（形は変わる）。ほかの選んでいる物にも同じ倍率を掛ける。
        /// </summary>
        public static void StretchTo(IReadOnlyList<StageProp> props, StageProp primary, int sizeAxis, float size)
        {
            if (primary == null || size < 1e-3f)
            {
                return;
            }

            var current = StagePropTransformOps.OwnSize(primary)[sizeAxis];
            if (current < 1e-3f)
            {
                return;
            }

            var factor = size / current;
            Run(props, "大きさを変える", prop => StagePropTransformOps.StretchModel(prop, sizeAxis, factor));
        }

        /// <summary>床からの高さを足す（メートル。プラスで浮く）。</summary>
        public static void Lift(IReadOnlyList<StageProp> props, float delta)
        {
            Run(props, "高さを変える", prop => StagePropTransformOps.SetLift(prop, StagePropTransformOps.Lift(prop) + delta));
        }

        /// <summary>
        /// 床に下ろす: 真下にある「床」役の背景（選んでいる物は除く）の表面に、見た目のいちばん下をのせる。
        /// 真下に床役が無い物は、ゲームの床（根元の高さ。ふつうは0）が基準。複数のときは全員を同じだけ上下し、
        /// いちばん先に床に着く物が床にのる（皿に乗せた石けんは乗ったまま。どれも床に沈まない）。
        /// </summary>
        public static void DropToFloor(IReadOnlyList<StageProp> props)
        {
            if (props == null || props.Count == 0)
            {
                return;
            }

            var selected = new HashSet<StageProp>(props);
            var floors = new List<StageProp>();
            foreach (var prop in StageProp.All)
            {
                if (prop != null && prop.role == StagePropRole.Floor && !selected.Contains(prop))
                {
                    floors.Add(prop);
                }
            }

            var floorZ = new List<float>();
            var bottomZ = new List<float>();
            foreach (var prop in props)
            {
                if (prop == null || !StagePropShape.TryLowestZ(prop, out var bottom))
                {
                    continue;
                }

                floorZ.Add(StagePropShape.TryFloorBelow(prop, floors, out var z) ? z : prop.transform.position.z);
                bottomZ.Add(bottom);
            }

            if (floorZ.Count == 0)
            {
                return;
            }

            // 下へ動かす = 床からの高さが減る。
            Lift(props, -StagePlaceMath.GroupDropDistance(floorZ, bottomZ));
        }

        private static void Run(IReadOnlyList<StageProp> props, string label, System.Action<StageProp> action)
        {
            if (props == null || props.Count == 0)
            {
                return;
            }

            StageUndoBurst.Before(label);
            foreach (var prop in props)
            {
                if (prop != null)
                {
                    action(prop);
                }
            }

            StageUndoBurst.After();
        }
    }
}
