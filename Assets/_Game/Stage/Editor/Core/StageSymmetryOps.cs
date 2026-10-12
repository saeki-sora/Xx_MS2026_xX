using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 「4人ぶん同じ形に置く」の計算（Unity に頼らない部分。テストあり）。
    /// 4人がコアを囲んで遊ぶので、片側に置いた背景を、コアを中心に 180°（向かい側）や 90° ずつ回した位置にも置くと、どの席からも条件が同じになる。
    /// </summary>
    public static class StageSymmetryMath
    {
        /// <summary>何方向に置くか（2 = 向かい側、4 = 90°ずつ）から、コピーを置く角度（度）を出す。元の物の 0° は含まない。</summary>
        public static List<float> CopyAngles(int ways)
        {
            var angles = new List<float>();
            ways = Mathf.Max(1, ways);
            for (var i = 1; i < ways; i++)
            {
                angles.Add(360f * i / ways);
            }

            return angles;
        }

        /// <summary>床の上の点を、center を中心に degrees 度（反時計回りが正）回す。</summary>
        public static Vector2 RotateAround(Vector2 point, Vector2 center, float degrees)
        {
            var r = degrees * Mathf.Deg2Rad;
            var d = point - center;
            var c = Mathf.Cos(r);
            var s = Mathf.Sin(r);
            return center + new Vector2(d.x * c - d.y * s, d.x * s + d.y * c);
        }

        /// <summary>中心にとても近い物は回しても同じ所に重なるだけなので、コピーしない。</summary>
        public static bool IsAtCenter(Vector2 point, Vector2 center, float tolerance = 0.05f) => (point - center).sqrMagnitude <= tolerance * tolerance;
    }

    /// <summary>選んだ背景を、コアを中心に回した位置へコピーする（向かい側・4方向）。1回の Ctrl+Z で全部戻る。</summary>
    public static class StageSymmetryOps
    {
        /// <summary>回す中心: コアがあればコアの位置、無ければ原点。</summary>
        public static Vector2 Center(out string label)
        {
            var core = Object.FindFirstObjectByType<CoreCrystalController>();
            if (core != null)
            {
                label = "コア";
                return core.transform.position;
            }

            label = "原点（コアが無いため）";
            return Vector2.zero;
        }

        /// <summary>選んだ背景のコピーを、中心のまわりに ways 方向へ置く。作ったコピーを返す。</summary>
        public static List<StageProp> PlaceRotatedCopies(IReadOnlyList<StageProp> props, int ways)
        {
            var center = Center(out var centerLabel);
            var sources = props.Where(p => p != null && !StageSymmetryMath.IsAtCenter(p.transform.position, center)).ToList();
            var created = new List<StageProp>();
            if (sources.Count == 0)
            {
                EditorUtility.DisplayDialog("置けません", $"選んでいる物が{centerLabel}の真上にあるので、回しても同じ所に重なります。中心から離れた物を選んでください。", "OK");
                return created;
            }

            var angles = StageSymmetryMath.CopyAngles(ways);
            var what = ways == 2 ? "向かい側" : $"{ways}方向";
            if (!EditorUtility.DisplayDialog("4人ぶん同じ形に置く",
                    $"選んでいる {sources.Count} 個を、{centerLabel}を中心に回して{what}にコピーします（{angles.Count * sources.Count} 個できます）。\nCtrl+Z で全部戻せます。", "置く", "やめる"))
            {
                return created;
            }

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            foreach (var angle in angles)
            {
                Selection.objects = sources.Select(p => (Object)p.gameObject).ToArray();
                Unsupported.DuplicateGameObjectsUsingPasteboard();
                foreach (var go in Selection.gameObjects)
                {
                    var copy = go.GetComponent<StageProp>();
                    if (copy == null || sources.Contains(copy))
                    {
                        continue;
                    }

                    Undo.RecordObject(copy.transform, "4人ぶん同じ形に置く");
                    // 根元は Z 回転だけなので、位置を中心のまわりに回して、向きを同じだけ足せばよい（傾きは中のモデルについてくる）。
                    var position = copy.transform.position;
                    var rotated = StageSymmetryMath.RotateAround(position, center, angle);
                    copy.transform.position = new Vector3(rotated.x, rotated.y, position.z);
                    StagePropTransformOps.SetYaw(copy, StagePropTransformOps.Yaw(copy) + angle);
                    created.Add(copy);
                }
            }

            Undo.SetCurrentGroupName("4人ぶん同じ形に置く");
            Undo.CollapseUndoOperations(group);
            Selection.objects = sources.Concat(created).Select(p => (Object)p.gameObject).ToArray();
            StagePropTransformOps.Commit();
            StageGameCamera.MarkDirty();
            return created;
        }
    }
}
