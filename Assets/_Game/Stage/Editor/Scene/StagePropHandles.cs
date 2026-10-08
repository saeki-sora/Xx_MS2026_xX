using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 選んでいる背景のまわりに出す取っ手:
    /// ・床の輪（回す。Z軸だけ。吸着なら決めた角度ごと）
    /// ・輪の右下の四角（外へ引くと大きく、内へ押すと小さく。足元を中心に）
    /// ・「↕ 高さ」の札（上下に引くと床から浮かせる／沈める。床の近くでは床にくっつく）
    /// 複数選んでいるときは、いちばん最初に選んだ物に取っ手を出し、変えた分を全部に同じだけ掛ける。
    /// </summary>
    public sealed class StagePropHandles
    {
        private static readonly int ScaleHash = "StageScaleHandle".GetHashCode();
        private static readonly int LiftHash = "StageLiftHandle".GetHashCode();
        private static readonly Color RingColor = new Color(1f, 0.83f, 0.35f, 0.9f);

        private readonly List<float> _startLifts = new List<float>();
        private float _scaleStartRadius;
        private float _scaleApplied = 1f;
        private float _liftStartMouseY;
        private float _liftMetersPerPixel;
        private int _undoGroup;
        private GUIStyle _label;
        private GUIStyle _pill;

        private StageProp _primary;
        private Vector3 _pivot;
        private Bounds _bounds;
        private float _radius;
        private Vector3 _knob;
        private Rect _pillRect;
        private int _rotateGroup = -1;

        public bool IsDragging { get; private set; }

        /// <summary>取っ手の位置を決める（毎イベント、最初に呼ぶ。本体をつかむ判定より先に取っ手の場所が要るため）。</summary>
        public void Prepare(IReadOnlyList<StageProp> selection)
        {
            _primary = selection.Count > 0 ? selection[0] : null;
            if (_primary == null)
            {
                return;
            }

            _pivot = _primary.transform.position;
            _bounds = _primary.VisualBounds;
            _radius = Mathf.Max(0.4f, Mathf.Max(_bounds.extents.x, _bounds.extents.y) * 1.1f);
            _knob = _pivot + new Vector3(0.7071f, -0.7071f, 0f) * _radius;
            var top = HandleUtility.WorldToGUIPoint(new Vector3(_bounds.center.x, _bounds.center.y, _bounds.min.z));
            _pillRect = new Rect(top.x + 18f, top.y - 11f, 104f, 22f);
        }

        /// <summary>マウスが大きさの四角か高さの札の上にあるか（そこでは本体をつかまない）。</summary>
        public bool IsOverHandle(Vector2 mouse)
        {
            return _primary != null &&
                   (_pillRect.Contains(mouse) || Vector2.Distance(HandleUtility.WorldToGUIPoint(_knob), mouse) < 12f);
        }

        public void OnSceneGui(SceneView view, IReadOnlyList<StageProp> selection)
        {
            if (_primary == null)
            {
                return;
            }

            _label ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            RotateRing(selection, _primary, _pivot, _radius);
            ScaleKnob(selection, _pivot, _knob);
            LiftPill(selection, _primary, _bounds);
        }

        private void RotateRing(IReadOnlyList<StageProp> selection, StageProp primary, Vector3 pivot, float radius)
        {
            var before = primary.transform.rotation;
            Handles.color = RingColor;
            EditorGUI.BeginChangeCheck();
            var snap = StagePlaceSettings.SnapActive(Event.current) ? StagePlaceSettings.AngleStep : 0f;
            var after = Handles.Disc(before, pivot, Vector3.forward, radius, false, snap);
            if (EditorGUI.EndChangeCheck())
            {
                if (_rotateGroup < 0)
                {
                    _rotateGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("背景を回す");
                }

                var delta = Mathf.DeltaAngle(before.eulerAngles.z, after.eulerAngles.z);
                StagePropTransformOps.RecordMove(selection, "背景を回す");
                foreach (var prop in selection)
                {
                    if (prop != null)
                    {
                        StagePropTransformOps.SetYaw(prop, StagePropTransformOps.Yaw(prop) + delta);
                    }
                }

                StagePropTransformOps.Commit();
                StageGameCamera.MarkDirty();
            }

            // 輪を離したら、回した分を1回の「元に戻す」にまとめる。
            if (_rotateGroup >= 0 && GUIUtility.hotControl == 0)
            {
                Undo.CollapseUndoOperations(_rotateGroup);
                _rotateGroup = -1;
            }

            if (GUIUtility.hotControl != 0 && Event.current.type == EventType.Repaint && IsNear(pivot, radius))
            {
                Handles.Label(pivot + Vector3.down * (radius + 0.25f), $"向き {Mathf.Repeat(primary.transform.eulerAngles.z, 360f):0}°", _label);
            }
        }

        private void ScaleKnob(IReadOnlyList<StageProp> selection, Vector3 pivot, Vector3 position)
        {
            var e = Event.current;
            var id = GUIUtility.GetControlID(ScaleHash, FocusType.Passive);
            var size = HandleUtility.GetHandleSize(position) * 0.08f;

            switch (e.GetTypeForControl(id))
            {
                case EventType.Layout:
                case EventType.MouseMove:
                    HandleUtility.AddControl(id, HandleUtility.DistanceToRectangle(position, Quaternion.identity, size * 1.4f));
                    break;
                case EventType.MouseDown:
                    if (e.button == 0 && !e.alt && HandleUtility.nearestControl == id && FloorDistance(e, pivot, out var startRadius) && startRadius > 1e-3f)
                    {
                        GUIUtility.hotControl = id;
                        _scaleStartRadius = startRadius;
                        _scaleApplied = 1f;
                        BeginGesture("大きさを変える");
                        e.Use();
                    }

                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id && FloorDistance(e, pivot, out var current))
                    {
                        var total = Mathf.Max(StagePropTransformOps.MinScaleFactor, current / _scaleStartRadius);
                        if (StagePlaceSettings.SnapActive(e))
                        {
                            total = Mathf.Max(StagePropTransformOps.MinScaleFactor, StagePlaceMath.SnapValue(total, 0.05f));
                        }

                        var step = total / _scaleApplied;
                        if (!Mathf.Approximately(step, 1f))
                        {
                            foreach (var prop in selection)
                            {
                                if (prop != null)
                                {
                                    StagePropTransformOps.ScaleModel(prop, step);
                                }
                            }

                            _scaleApplied = total;
                            StageGameCamera.MarkDirty();
                        }

                        e.Use();
                    }

                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id && e.button == 0)
                    {
                        GUIUtility.hotControl = 0;
                        EndGesture();
                        e.Use();
                    }

                    break;
                case EventType.Repaint:
                    var hot = GUIUtility.hotControl == id || HandleUtility.nearestControl == id && GUIUtility.hotControl == 0;
                    Handles.color = hot ? Color.white : RingColor;
                    Handles.CubeHandleCap(id, position, Quaternion.identity, size * (hot ? 1.3f : 1f), EventType.Repaint);
                    if (GUIUtility.hotControl == id)
                    {
                        var s = StagePropTransformOps.Size(selection[0]);
                        Handles.Label(position + Vector3.down * size * 3f, $"×{_scaleApplied:0.00}（幅 {s.x:0.0} × 奥行 {s.y:0.0} × 高さ {s.z:0.0} m）", _label);
                    }

                    break;
            }
        }

        private void LiftPill(IReadOnlyList<StageProp> selection, StageProp primary, Bounds bounds)
        {
            var e = Event.current;
            var id = GUIUtility.GetControlID(LiftHash, FocusType.Passive);
            var top = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
            var lift = StagePropTransformOps.Lift(primary);
            var rect = _pillRect;

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && !e.alt && GUIUtility.hotControl == 0 && rect.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        _liftStartMouseY = e.mousePosition.y;
                        // 1ピクセルを何メートルとして扱うか: その場所の取っ手の大きさ（画面上でだいたい一定の長さ）から決める。
                        _liftMetersPerPixel = HandleUtility.GetHandleSize(top) / 120f;
                        _startLifts.Clear();
                        foreach (var prop in selection)
                        {
                            _startLifts.Add(prop != null ? StagePropTransformOps.Lift(prop) : 0f);
                        }

                        BeginGesture("高さを変える");
                        e.Use();
                    }

                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        var delta = (_liftStartMouseY - e.mousePosition.y) * _liftMetersPerPixel * (e.shift ? 0.2f : 1f);
                        for (var i = 0; i < selection.Count && i < _startLifts.Count; i++)
                        {
                            if (selection[i] == null)
                            {
                                continue;
                            }

                            var target = _startLifts[i] + delta;
                            if (StagePlaceSettings.SnapActive(e))
                            {
                                target = StagePlaceMath.SnapValue(target, 0.05f);
                            }
                            else if (Mathf.Abs(target) < 0.03f)
                            {
                                target = 0f; // 吸着を切っていても床にはくっつける
                            }

                            StagePropTransformOps.SetLift(selection[i], target);
                        }

                        StageGameCamera.MarkDirty();
                        e.Use();
                    }

                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id && e.button == 0)
                    {
                        GUIUtility.hotControl = 0;
                        EndGesture();
                        e.Use();
                    }

                    break;
                case EventType.Repaint:
                    _pill ??= new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                    Handles.BeginGUI();
                    var hot = GUIUtility.hotControl == id;
                    var old = GUI.backgroundColor;
                    GUI.backgroundColor = hot ? new Color(1f, 0.83f, 0.35f) : Color.white;
                    var text = Mathf.Abs(lift) < 0.005f ? "↕ 床の上" : lift > 0f ? $"↕ {lift:0.00}m 浮き" : $"↕ {-lift:0.00}m 沈み";
                    _pill.Draw(rect, new GUIContent(text), id, hot);
                    GUI.backgroundColor = old;
                    Handles.EndGUI();
                    EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);
                    break;
            }
        }

        private void BeginGesture(string label)
        {
            _undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);
            IsDragging = true;
        }

        private void EndGesture()
        {
            IsDragging = false;
            StageFootprintRefresher.Flush();
            StagePropTransformOps.Commit();
            Undo.CollapseUndoOperations(_undoGroup);
        }

        private static bool FloorDistance(Event e, Vector3 pivot, out float distance)
        {
            distance = 0f;
            if (!StageMoveSession.RayToPlane(HandleUtility.GUIPointToWorldRay(e.mousePosition), 0f, out var point))
            {
                return false;
            }

            distance = Vector2.Distance(point, pivot);
            return true;
        }

        private static bool IsNear(Vector3 pivot, float radius)
        {
            return StageMoveSession.RayToPlane(HandleUtility.GUIPointToWorldRay(Event.current.mousePosition), 0f, out var point) &&
                   Vector2.Distance(point, pivot) < radius * 1.5f;
        }
    }
}
