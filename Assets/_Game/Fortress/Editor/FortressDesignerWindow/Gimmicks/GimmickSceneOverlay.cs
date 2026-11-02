using System.Collections.Generic;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// Scene ビューに仕掛けを重ねて描く（2026-10-29）。
    /// ・歯車: 輪と歯の印・回る向きの矢印・名前と回り方。「止まっては回る」歯車は、次に回った後の腕の形を点線で出す
    /// ・動く障害物: 止まる所を結ぶ点線と番号つきの丸・各所での形（点線の四角）。選んだ動く障害物は丸をドラッグして止まる所を動かせる
    /// </summary>
    [InitializeOnLoad]
    public static class GimmickSceneOverlay
    {
        private const string PrefsPrefix = "MS2026.FortressDesigner.Gimmicks.";

        private static readonly Color GearColor = new Color(1f, 0.78f, 0.3f, 0.95f);
        private static readonly Color GhostColor = new Color(1f, 0.55f, 0.2f, 0.9f);
        private static readonly Color MoverColor = new Color(0.4f, 0.85f, 1f, 0.95f);
        private static readonly List<GimmickGearArm> ArmBuffer = new List<GimmickGearArm>();
        private static readonly Vector3[] Corners = new Vector3[5];

        private static bool? _show;
        private static bool? _ghosts;

        public static bool Show
        {
            get => _show ??= EditorPrefs.GetBool(PrefsPrefix + "Show", true);
            set
            {
                _show = value;
                EditorPrefs.SetBool(PrefsPrefix + "Show", value);
                SceneView.RepaintAll();
            }
        }

        /// <summary>次の形（歯車が回った後・止まる所での形）を点線で出すか。</summary>
        public static bool ShowGhosts
        {
            get => _ghosts ??= EditorPrefs.GetBool(PrefsPrefix + "Ghosts", true);
            set
            {
                _ghosts = value;
                EditorPrefs.SetBool(PrefsPrefix + "Ghosts", value);
                SceneView.RepaintAll();
            }
        }

        static GimmickSceneOverlay()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static void OnSceneGui(SceneView view)
        {
            if (!Show)
            {
                return;
            }

            var gimmicks = GimmickClock.FindAllInScenes();
            if (gimmicks.Count == 0)
            {
                return;
            }

            var now = GimmickClock.Now;
            foreach (var gimmick in gimmicks)
            {
                switch (gimmick)
                {
                    case GimmickGear gear:
                        DrawGear(gear, now);
                        break;
                    case GimmickMover mover:
                        DrawMover(mover, now, IsSelected(mover));
                        break;
                }
            }
        }

        private static bool IsSelected(Component component)
        {
            var active = Selection.activeGameObject;
            return active != null && active == component.gameObject;
        }

        // ───────── 歯車 ─────────

        private static void DrawGear(GimmickGear gear, float now)
        {
            var center = gear.transform.position;
            var radius = gear.radius;
            var angle = gear.Rotor != null ? gear.Rotor.eulerAngles.z : gear.transform.eulerAngles.z;
            Handles.color = GearColor;
            Handles.DrawWireDisc(center, Vector3.forward, radius, 2f);

            // 歯の印（回ると一緒に回る）
            var teeth = Mathf.Clamp(gear.teeth, 3, 64);
            for (var i = 0; i < teeth; i++)
            {
                var a = (angle + 360f * i / teeth) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Handles.DrawLine(center + dir * radius, center + dir * (radius + 0.25f), 2f);
            }

            // 回る向き
            if (gear.motion == GearMotion.Step || gear.motion == GearMotion.Continuous)
            {
                var sign = gear.clockwise ? -1f : 1f;
                var from = Quaternion.Euler(0f, 0f, 60f) * Vector3.right;
                Handles.DrawWireArc(center, Vector3.forward, from, sign * 50f, radius + 0.55f, 2f);
                var tipAngle = (60f + sign * 50f) * Mathf.Deg2Rad;
                var tip = center + new Vector3(Mathf.Cos(tipAngle), Mathf.Sin(tipAngle), 0f) * (radius + 0.55f);
                var tangent = new Vector3(-Mathf.Sin(tipAngle), Mathf.Cos(tipAngle), 0f) * sign;
                var side = Vector3.Cross(tangent, Vector3.forward);
                Handles.DrawAAConvexPolygon(tip + tangent * 0.35f, tip + side * 0.18f, tip - side * 0.18f);
            }

            gear.CollectArms(ArmBuffer);
            if (ShowGhosts && gear.motion == GearMotion.Step && ArmBuffer.Count > 0)
            {
                // 次に回った後の腕の形（点線）。
                var next = NextStepDelta(gear, now);
                if (Mathf.Abs(next) > 0.01f)
                {
                    Handles.color = GhostColor;
                    foreach (var arm in ArmBuffer)
                    {
                        if (arm != null)
                        {
                            DrawRotatedBox(arm.transform, center, next);
                        }
                    }
                }
            }

            var label = $"⚙ {gear.name}\n{gear.Describe()}";
            Handles.Label(center + new Vector3(-radius, -radius - 0.4f, 0f), label, EditorStyles.whiteMiniLabel);
        }

        /// <summary>今の形から、次に回り終えたときまでに回る角度（度）。</summary>
        private static float NextStepDelta(GimmickGear gear, float now)
        {
            var current = gear.AngleAt(now);
            var period = Mathf.Max(GimmickMath.MinSeconds, gear.turnSeconds) + Mathf.Max(0f, gear.waitSeconds);
            var probe = now < gear.firstTurnAt ? gear.firstTurnAt + gear.turnSeconds + 0.01f : now + period;
            // 回っている途中なら、その回転が終わった所を出す。
            GimmickMath.StepAngle(now, gear.firstTurnAt, gear.waitSeconds, gear.turnSeconds, gear.stepDegrees, out var turning, out var index);
            if (turning)
            {
                probe = gear.firstTurnAt + index * period + gear.turnSeconds + 0.01f;
            }

            return GimmickMath.DeltaAngle(current, gear.AngleAt(probe));
        }

        /// <summary>1×1 の箱（Transform の大きさで伸びる）を、center のまわりに delta 度回した所に点線で描く。</summary>
        private static void DrawRotatedBox(Transform box, Vector3 center, float delta)
        {
            var rotation = Quaternion.Euler(0f, 0f, delta);
            var local = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) };
            for (var i = 0; i < 4; i++)
            {
                var world = box.TransformPoint(local[i]);
                Corners[i] = center + rotation * (world - center);
            }

            Corners[4] = Corners[0];
            Handles.DrawDottedLines(new[] { Corners[0], Corners[1], Corners[1], Corners[2], Corners[2], Corners[3], Corners[3], Corners[0] }, 4f);
        }

        // ───────── 動く障害物 ─────────

        private static void DrawMover(GimmickMover mover, float now, bool selected)
        {
            var stops = mover.stops;
            if (stops == null || stops.Count == 0)
            {
                return;
            }

            Handles.color = MoverColor;
            var size = BoxSize(mover);
            for (var i = 0; i < stops.Count; i++)
            {
                var position = mover.StopWorldPosition(i);
                if (i > 0)
                {
                    Handles.DrawDottedLine(mover.StopWorldPosition(i - 1), position, 4f);
                }

                if (ShowGhosts && size.HasValue)
                {
                    DrawGhostRect(position, size.Value, mover.StopWorldAngle(i));
                }

                var discSize = HandleUtility.GetHandleSize(position) * 0.12f;
                if (selected && i > 0 && !Application.isPlaying)
                {
                    EditorGUI.BeginChangeCheck();
                    var moved = Handles.FreeMoveHandle(position, discSize * 1.4f, Vector3.zero, Handles.CircleHandleCap);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(mover, "止まる所を動かす");
                        var offset = mover.WorldToOffset(moved);
                        stops[i].offset = new Vector2(Snap(offset.x), Snap(offset.y));
                        EditorUtility.SetDirty(mover);
                        mover.MarkChanged();
                        if (GimmickPreview.Active)
                        {
                            GimmickPreview.SetTime(GimmickPreview.Time);
                        }
                    }
                }
                else
                {
                    Handles.DrawSolidDisc(position, Vector3.forward, discSize);
                }

                var text = i == 0 ? "出発点" : GimmickMoverPanel.Circled(i);
                Handles.Label(position + new Vector3(0.2f, 0.45f, 0f), text, EditorStyles.whiteBoldLabel);
            }

            if (stops.Count > 1 && mover.loop == MoverLoop.Loop)
            {
                Handles.DrawDottedLine(mover.StopWorldPosition(stops.Count - 1), mover.StopWorldPosition(0), 2f);
            }

            var pose = mover.PoseAt(now);
            Handles.Label(mover.transform.position + new Vector3(0.2f, -0.4f, 0f),
                $"↔ {mover.name}\n{mover.Describe()}{(pose.Moving ? "（動いている）" : "")}", EditorStyles.whiteMiniLabel);
        }

        private static float Snap(float value)
        {
            // 0.25m 単位にそろえる（Ctrl を押しながらだとそろえない）。
            return Event.current != null && Event.current.control ? value : Mathf.Round(value * 4f) / 4f;
        }

        /// <summary>当たり判定の大きさ（ローカルの箱の大きさ × Transform の大きさ）。無ければ null。</summary>
        private static Vector2? BoxSize(GimmickMover mover)
        {
            var box = mover.GetComponent<BoxCollider2D>();
            var scale = mover.transform.lossyScale;
            if (box != null)
            {
                return new Vector2(box.size.x * scale.x, box.size.y * scale.y);
            }

            var any = mover.GetComponentInChildren<Collider2D>();
            if (any == null)
            {
                return null;
            }

            var size = any.bounds.size;
            return new Vector2(size.x, size.y);
        }

        private static void DrawGhostRect(Vector3 center, Vector2 size, float angle)
        {
            var rotation = Quaternion.Euler(0f, 0f, angle);
            var hx = size.x * 0.5f;
            var hy = size.y * 0.5f;
            var a = center + rotation * new Vector3(-hx, -hy);
            var b = center + rotation * new Vector3(hx, -hy);
            var c = center + rotation * new Vector3(hx, hy);
            var d = center + rotation * new Vector3(-hx, hy);
            Handles.DrawDottedLines(new[] { a, b, b, c, c, d, d, a }, 3f);
        }
    }
}
