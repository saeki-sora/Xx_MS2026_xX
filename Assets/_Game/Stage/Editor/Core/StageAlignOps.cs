using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>そろえる辺（小さい側・真ん中・大きい側）。</summary>
    public enum StageAlignEdge
    {
        Min,
        Center,
        Max
    }

    /// <summary>
    /// 「並べる」の計算（Unity に頼らない部分。テストあり）。1つの軸（X か Y）の上で、各物が占める範囲（min〜max）から、
    /// そろえる・等間隔にするために動かす量を出す。
    /// </summary>
    public static class StageAlignMath
    {
        /// <summary>辺をそろえるための、各物の移動量。小さい側＝いちばん小さい min、大きい側＝いちばん大きい max、真ん中＝全体の真ん中。</summary>
        public static float[] AlignDeltas(IReadOnlyList<Vector2> spans, StageAlignEdge edge)
        {
            var deltas = new float[spans.Count];
            if (spans.Count == 0)
            {
                return deltas;
            }

            var min = float.MaxValue;
            var max = float.MinValue;
            foreach (var s in spans)
            {
                min = Mathf.Min(min, s.x);
                max = Mathf.Max(max, s.y);
            }

            var target = edge == StageAlignEdge.Min ? min : edge == StageAlignEdge.Max ? max : (min + max) * 0.5f;
            for (var i = 0; i < spans.Count; i++)
            {
                var s = spans[i];
                var current = edge == StageAlignEdge.Min ? s.x : edge == StageAlignEdge.Max ? s.y : (s.x + s.y) * 0.5f;
                deltas[i] = target - current;
            }

            return deltas;
        }

        /// <summary>
        /// 端の2つは動かさず、間のすき間が全部同じになるように並べる移動量（3つ以上のとき。並び順は真ん中の位置の順）。
        /// 大きさが違っても「すき間」がそろう。
        /// </summary>
        public static float[] DistributeDeltas(IReadOnlyList<Vector2> spans)
        {
            var deltas = new float[spans.Count];
            if (spans.Count < 3)
            {
                return deltas;
            }

            var order = new List<int>();
            for (var i = 0; i < spans.Count; i++)
            {
                order.Add(i);
            }

            order.Sort((a, b) => ((spans[a].x + spans[a].y) * 0.5f).CompareTo((spans[b].x + spans[b].y) * 0.5f));
            var first = spans[order[0]];
            var last = spans[order[order.Count - 1]];
            var widths = 0f;
            foreach (var s in spans)
            {
                widths += s.y - s.x;
            }

            var gap = (last.y - first.x - widths) / (spans.Count - 1);
            var cursor = first.x;
            foreach (var i in order)
            {
                deltas[i] = cursor - spans[i].x;
                cursor += spans[i].y - spans[i].x + gap;
            }

            return deltas;
        }
    }

    /// <summary>選んだ背景をそろえる・等間隔に並べる（見た目の床の上の範囲で測る）。1回の Ctrl+Z で戻る。</summary>
    public static class StageAlignOps
    {
        /// <param name="axis">0 = X（横）、1 = Y（縦）。</param>
        public static void Align(IReadOnlyList<StageProp> props, int axis, StageAlignEdge edge) =>
            Apply(props, axis, "背景をそろえる", spans => StageAlignMath.AlignDeltas(spans, edge));

        /// <param name="axis">0 = X（横）、1 = Y（縦）。</param>
        public static void Distribute(IReadOnlyList<StageProp> props, int axis) =>
            Apply(props, axis, "背景を等間隔に並べる", StageAlignMath.DistributeDeltas);

        private static void Apply(IReadOnlyList<StageProp> props, int axis, string label, System.Func<IReadOnlyList<Vector2>, float[]> compute)
        {
            var list = new List<StageProp>();
            var spans = new List<Vector2>();
            foreach (var prop in props)
            {
                if (prop == null)
                {
                    continue;
                }

                var bounds = StagePropTransformOps.VisualBounds(prop);
                list.Add(prop);
                spans.Add(axis == 0 ? new Vector2(bounds.min.x, bounds.max.x) : new Vector2(bounds.min.y, bounds.max.y));
            }

            if (list.Count < 2)
            {
                return;
            }

            var deltas = compute(spans);
            StageUndoBurst.Before(label);
            for (var i = 0; i < list.Count; i++)
            {
                if (Mathf.Abs(deltas[i]) < 1e-5f)
                {
                    continue;
                }

                Undo.RecordObject(list[i].transform, label);
                StagePropTransformOps.Move(list[i], axis == 0 ? new Vector2(deltas[i], 0f) : new Vector2(0f, deltas[i]));
            }

            StageUndoBurst.After();
        }
    }
}
