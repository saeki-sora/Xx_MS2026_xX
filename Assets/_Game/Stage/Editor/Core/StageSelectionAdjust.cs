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

        /// <summary>大きさを倍にする（足元を中心に）。</summary>
        public static void Scale(IReadOnlyList<StageProp> props, float factor)
        {
            if (Mathf.Approximately(factor, 1f))
            {
                return;
            }

            Run(props, "大きさを変える", prop => StagePropTransformOps.ScaleModel(prop, factor));
        }

        /// <summary>床からの高さを足す（メートル。プラスで浮く）。</summary>
        public static void Lift(IReadOnlyList<StageProp> props, float delta)
        {
            Run(props, "高さを変える", prop => StagePropTransformOps.SetLift(prop, StagePropTransformOps.Lift(prop) + delta));
        }

        /// <summary>床からの高さをそろえる（0なら床に下ろす）。</summary>
        public static void SetLift(IReadOnlyList<StageProp> props, float lift)
        {
            Run(props, "高さを変える", prop => StagePropTransformOps.SetLift(prop, lift));
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
