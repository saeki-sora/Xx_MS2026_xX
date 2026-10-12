using System;
using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 配置ページの「ゲーム画面」1枚: 選んだ視点のゲームのカメラで映した絵の上で、背景を直接さわれる。
    /// ・クリックで選ぶ（Shift で追加・外す）、そのままドラッグで床の上を動かす（つかんだ所が付いてくる。吸着あり、Ctrl で逆）
    /// ・ホイールで回す／Shift+ホイールで大きさ／Alt+ホイールで高さ
    /// ・てっぺんのピンクの丸（傾ける丸）をつかんで引くと、その向きに傾く（5°ごと。Ctrl で自由に）
    /// ・矢印キーで少しずらす、Ctrl+矢印キーで画面の向きに傾ける、Q/E で回す、-/+ で大きさ、PageUp/PageDown で高さ、
    ///   F でシーンビューに映す、Esc でやめる
    /// 絵の上には、通れない範囲の輪郭（役割の色）・選んでいる物の縁取り・砲台などとの距離・吸着の線を重ねる。
    /// 絵の描き直しはシーンが変わったとき（StageGameCamera.Version）と大きさが変わったときだけ。Play中は1秒20回。
    /// </summary>
    public sealed class StagePreviewView : VisualElement
    {
        private static readonly int Hash = "StagePreviewView".GetHashCode();
        private static readonly Color SelectColor = new Color(1f, 0.48f, 0.24f);
        private static readonly Color GuideColor = new Color(0.35f, 0.85f, 1f, 0.95f);
        internal static readonly Color TiltColor = new Color(1f, 0.42f, 0.78f);
        private const float KnobRadius = 7f;

        private readonly StageStudioContext _context;
        private readonly Func<int> _viewer;
        private readonly IMGUIContainer _gui;
        private readonly StageGamePreview _preview = new StageGamePreview();
        private readonly StageMoveSession _move = new StageMoveSession();
        private readonly StageTiltSession _tilt = new StageTiltSession();
        private readonly List<Vector2> _scratch = new List<Vector2>(2048);
        private int _renderedVersion = -1;
        private int _renderedViewer = int.MinValue;
        private Vector2Int _renderedSize;
        private double _nextPlayRender;
        private bool _hasImage;
        private StageProp _hover;
        private bool _overKnob;
        private GUIStyle _label;
        private GUIStyle _badge;

        public StagePreviewView(StageStudioContext context, Func<int> viewer)
        {
            _context = context;
            _viewer = viewer;
            style.flexGrow = 1;
            style.marginBottom = 6;
            style.backgroundColor = new Color(0.06f, 0.06f, 0.08f);
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 6;
            style.overflow = Overflow.Hidden;

            _gui = new IMGUIContainer(OnGui) { focusable = true };
            _gui.style.flexGrow = 1;
            Add(_gui);

            RegisterCallback<GeometryChangedEvent>(_ => FitHeight());
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                EditorApplication.update += Tick;
                _context.SelectionChanged += _gui.MarkDirtyRepaint;
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                EditorApplication.update -= Tick;
                _context.SelectionChanged -= _gui.MarkDirtyRepaint;
                _move.Cancel();
                _tilt.Cancel();
                _preview.Dispose();
                _renderedVersion = -1;
            });
        }

        // 横幅に合わせて、Game ビューと同じ縦横比の高さにする。
        private void FitHeight()
        {
            var width = resolvedStyle.width;
            if (width > 1f)
            {
                var height = Mathf.Round(width / StageGameCamera.Aspect);
                if (Mathf.Abs(resolvedStyle.height - height) > 0.5f)
                {
                    style.height = height;
                }
            }
        }

        private void Tick()
        {
            var rect = _gui.contentRect;
            var ppp = EditorGUIUtility.pixelsPerPoint;
            var size = new Vector2Int(Mathf.RoundToInt(rect.width * ppp), Mathf.RoundToInt(rect.height * ppp));
            if (size.x < 32 || size.y < 32)
            {
                return;
            }

            var viewer = _viewer();
            var now = EditorApplication.timeSinceStartup;
            var playing = EditorApplication.isPlaying && !EditorApplication.isPaused && now >= _nextPlayRender;
            if (StageGameCamera.Version == _renderedVersion && viewer == _renderedViewer && size == _renderedSize && !playing)
            {
                return;
            }

            _nextPlayRender = now + 0.05;
            _renderedVersion = StageGameCamera.Version;
            _renderedViewer = viewer;
            _renderedSize = size;
            _hasImage = _preview.Render(viewer, size.x, size.y);
            _gui.MarkDirtyRepaint();
        }

        private void OnGui()
        {
            var rect = new Rect(0f, 0f, _gui.contentRect.width, _gui.contentRect.height);
            var e = Event.current;
            if (e.type == EventType.Repaint)
            {
                Draw(rect);
            }
            else
            {
                HandleInput(rect, e);
            }
        }

        // ── 描く ───────────────────────────
        private void Draw(Rect rect)
        {
            _label ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _badge ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };

            if (_hasImage && _preview.Texture != null)
            {
                GUI.DrawTexture(rect, _preview.Texture, ScaleMode.StretchToFill, false);
            }
            else
            {
                GUI.Label(rect, StageGameCamera.Preset == null ? "カメラの見え方プリセットがありません（見え方ページで選べます）" : "描いています…", _label);
                return;
            }

            if (_preview.Camera == null)
            {
                return;
            }

            if (StagePlaceSettings.PreviewOutlines)
            {
                DrawFootprints(rect);
            }

            if (_hover != null && !IsSelected(_hover))
            {
                DrawHull(rect, _hover, new Color(1f, 1f, 1f, 0.55f), 1.5f);
            }

            foreach (var prop in _context.SelectedProps)
            {
                if (prop != null)
                {
                    DrawHull(rect, prop, SelectColor, 2.5f);
                }
            }

            if (StagePlaceSettings.ShowDistances)
            {
                DrawDistances(rect);
            }

            DrawSnapGuides(rect);
            DrawTiltKnob(rect);
            DrawPrimaryLabel(rect);
            DrawViewerBadge(rect);
        }

        private void DrawFootprints(Rect rect)
        {
            foreach (var prop in _context.Props)
            {
                if (prop == null || !prop.isActiveAndEnabled || !prop.role.NeedsFootprint())
                {
                    continue;
                }

                var color = StageRoleStyle.Color(prop.role);
                Handles.color = new Color(color.r, color.g, color.b, IsSelected(prop) ? 1f : 0.7f);
                foreach (var path in StageGapFinder.WorldPaths(prop))
                {
                    DrawWorldLoop(rect, path, IsSelected(prop) ? 3f : 1.8f);
                }
            }
        }

        private void DrawHull(Rect rect, StageProp prop, Color color, float width)
        {
            _scratch.Clear();
            foreach (var v in StagePropShape.WorldVertices(prop))
            {
                if (ToGui(rect, v, out var p))
                {
                    _scratch.Add(p);
                }
            }

            if (_scratch.Count < 3)
            {
                return;
            }

            var hull = StagePlaceMath.ConvexHull(_scratch);
            var points = new Vector3[hull.Count + 1];
            for (var i = 0; i < hull.Count; i++)
            {
                points[i] = hull[i];
            }

            points[hull.Count] = points[0];
            Handles.color = color;
            Handles.DrawAAPolyLine(width, points);
        }

        private void DrawDistances(Rect rect)
        {
            foreach (var prop in _context.SelectedProps)
            {
                foreach (var reading in StageDistanceGuide.Measure(prop))
                {
                    var color = reading.TooClose ? StageDistanceGuide.NearColor : StageDistanceGuide.FarColor;
                    if (!ToGui(rect, reading.Target, out var b))
                    {
                        continue;
                    }

                    Handles.color = color;
                    if (!reading.Overlaps && ToGui(rect, reading.Closest, out var a))
                    {
                        Handles.DrawDottedLine(a, b, 3f);
                        Text(new Rect((a + b) * 0.5f - new Vector2(70f, 9f), new Vector2(140f, 18f)), reading.Text, color);
                    }
                    else
                    {
                        Text(new Rect(b - new Vector2(70f, 22f), new Vector2(140f, 18f)), reading.Text, color);
                    }
                }
            }
        }

        private void DrawSnapGuides(Rect rect)
        {
            if (!_move.IsActive)
            {
                return;
            }

            var snap = _move.Snap;
            var area = _move.CurrentRect();
            const float extend = 3f;
            Handles.color = GuideColor;
            if (snap.SnappedX)
            {
                DrawWorldLine(rect, new Vector2(snap.GuideX, area.yMin - extend), new Vector2(snap.GuideX, area.yMax + extend));
            }

            if (snap.SnappedY)
            {
                DrawWorldLine(rect, new Vector2(area.xMin - extend, snap.GuideY), new Vector2(area.xMax + extend, snap.GuideY));
            }
        }

        private void DrawTiltKnob(Rect rect)
        {
            if (!KnobAt(rect, out var pivot, out var top))
            {
                return;
            }

            var hot = _tilt.IsActive || _overKnob;
            Handles.color = new Color(TiltColor.r, TiltColor.g, TiltColor.b, hot ? 1f : 0.8f);
            Handles.DrawDottedLine(pivot, top, 3f);
            var r = KnobRadius * (hot ? 1.25f : 1f);
            Handles.DrawSolidDisc(top, Vector3.forward, r);
            Handles.color = Color.white;
            Handles.DrawWireDisc(top, Vector3.forward, r);

            if (hot)
            {
                var tilt = StagePropTransformOps.Tilt(_context.Primary);
                var text = _tilt.IsActive ? $"傾き X {tilt.x:0}°  Y {tilt.y:0}°" : "引いて傾ける";
                var size = _label.CalcSize(new GUIContent(text));
                var box = new Rect(top.x + 12f, top.y - 9f, size.x + 12f, 18f);
                EditorGUI.DrawRect(box, new Color(0f, 0f, 0f, 0.7f));
                Text(box, text, TiltColor);
            }
        }

        // 傾ける丸の場所（画面の座標）。最初に選んだ物だけに出す。
        private bool KnobAt(Rect rect, out Vector2 pivot, out Vector2 top)
        {
            pivot = top = default;
            return StagePlaceSettings.TiltKnob && _preview.Camera != null && !_move.IsActive &&
                   StageTiltSession.TryGetKnob(_context.Primary, out var pivotWorld, out var topWorld) &&
                   ToGui(rect, pivotWorld, out pivot) && ToGui(rect, topWorld, out top);
        }

        private bool IsOverKnob(Rect rect, Vector2 mouse) =>
            KnobAt(rect, out _, out var top) && Vector2.Distance(top, mouse) <= KnobRadius + 5f;

        private void DrawPrimaryLabel(Rect rect)
        {
            var prop = _context.Primary;
            if (prop == null)
            {
                Text(new Rect(rect.x, rect.yMax - 22f, rect.width, 18f), "背景をクリックで選ぶ・そのままドラッグで動かす", new Color(1f, 1f, 1f, 0.7f));
                return;
            }

            var bounds = prop.VisualBounds;
            if (!ToGui(rect, new Vector3(bounds.center.x, bounds.center.y, bounds.min.z), out var top))
            {
                return;
            }

            var lift = StagePropTransformOps.Lift(prop);
            var height = Mathf.Abs(lift) < 0.005f ? "" : lift > 0f ? $"・{lift:0.00}m 浮き" : $"・{-lift:0.00}m 沈み";
            var text = $"{prop.name}{height}";
            var size = _label.CalcSize(new GUIContent(text));
            var box = new Rect(top.x - size.x * 0.5f - 6f, top.y - 30f, size.x + 12f, 18f);
            EditorGUI.DrawRect(box, new Color(0f, 0f, 0f, 0.65f));
            Text(box, text, SelectColor);
        }

        private void DrawViewerBadge(Rect rect)
        {
            var viewer = _viewer();
            var box = new Rect(rect.x + 6f, rect.y + 6f, 46f, 20f);
            EditorGUI.DrawRect(box, new Color(0f, 0f, 0f, 0.6f));
            _badge.normal.textColor = ViewerIndex.Color(viewer);
            GUI.Label(box, ViewerIndex.Label(viewer), _badge);
        }

        private void DrawWorldLoop(Rect rect, Vector2[] path, float width)
        {
            var points = new List<Vector3>(path.Length + 1);
            foreach (var p in path)
            {
                if (!ToGui(rect, new Vector3(p.x, p.y, 0f), out var g))
                {
                    return;
                }

                points.Add(g);
            }

            if (points.Count > 1)
            {
                points.Add(points[0]);
                Handles.DrawAAPolyLine(width, points.ToArray());
            }
        }

        private void DrawWorldLine(Rect rect, Vector2 a, Vector2 b)
        {
            if (ToGui(rect, a, out var ga) && ToGui(rect, b, out var gb))
            {
                Handles.DrawAAPolyLine(2f, ga, gb);
            }
        }

        private void Text(Rect box, string text, Color color)
        {
            _label.normal.textColor = color;
            GUI.Label(box, text, _label);
        }

        private bool ToGui(Rect rect, Vector3 world, out Vector2 gui)
        {
            var vp = _preview.Camera.WorldToViewportPoint(world);
            gui = new Vector2(rect.x + vp.x * rect.width, rect.y + (1f - vp.y) * rect.height);
            return vp.z > 0f;
        }

        private bool ToGui(Rect rect, Vector2 floor, out Vector2 gui) => ToGui(rect, new Vector3(floor.x, floor.y, 0f), out gui);

        // ── さわる ───────────────────────────
        private void HandleInput(Rect rect, Event e)
        {
            var camera = _preview.Camera;
            if (camera == null || !_hasImage)
            {
                return;
            }

            var id = GUIUtility.GetControlID(Hash, FocusType.Keyboard);
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown when e.button == 0:
                    _gui.Focus();
                    GUIUtility.keyboardControl = id;
                    if (OnMouseDown(rect, e))
                    {
                        GUIUtility.hotControl = id;
                    }

                    e.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    if (_tilt.IsActive)
                    {
                        _tilt.Update(RayAt(rect, e.mousePosition), StagePlaceSettings.SnapActive(e));
                    }
                    else
                    {
                        _move.Update(RayAt(rect, e.mousePosition), StagePlaceSettings.SnapActive(e));
                    }

                    e.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    _move.End();
                    _tilt.End();
                    e.Use();
                    break;
                case EventType.MouseMove:
                    var overKnob = IsOverKnob(rect, e.mousePosition);
                    var hover = !overKnob && StagePropShape.Pick(RayAt(rect, e.mousePosition), _context.Props, out var over, out _) ? over : null;
                    if (hover != _hover || overKnob != _overKnob)
                    {
                        _hover = hover;
                        _overKnob = overKnob;
                        _gui.MarkDirtyRepaint();
                    }

                    break;
                case EventType.ScrollWheel:
                    if (OnWheel(e))
                    {
                        e.Use();
                    }

                    break;
                case EventType.KeyDown:
                    if (OnKey(e, id))
                    {
                        e.Use();
                    }

                    break;
                case EventType.ValidateCommand:
                case EventType.ExecuteCommand:
                    // Ctrl+D（複製）・Delete（消す）は、Unity から「コマンド」として届く。
                    if (GUIUtility.keyboardControl == id && _context.SelectedProps.Count > 0 && OnCommand(e.commandName, e.type == EventType.ExecuteCommand))
                    {
                        e.Use();
                    }

                    break;
            }

            EditorGUIUtility.AddCursorRect(rect, _tilt.IsActive || _overKnob ? MouseCursor.RotateArrow
                : _move.IsActive || _hover != null ? MouseCursor.MoveArrow : MouseCursor.Arrow);
        }

        private bool OnMouseDown(Rect rect, Event e)
        {
            if (!e.shift && IsOverKnob(rect, e.mousePosition))
            {
                return _tilt.Begin(_context.SelectedProps);
            }

            var ray = RayAt(rect, e.mousePosition);
            if (!StagePropShape.Pick(ray, _context.Props, out var prop, out var hit))
            {
                if (!e.shift)
                {
                    Selection.objects = Array.Empty<UnityEngine.Object>();
                }

                return false;
            }

            var selected = new List<StageProp>(_context.SelectedProps);
            if (e.shift)
            {
                if (selected.Contains(prop))
                {
                    selected.Remove(prop);
                    SelectAll(selected);
                    return false; // 外しただけ（つかまない）
                }

                selected.Add(prop);
                SelectAll(selected);
            }
            else if (!selected.Contains(prop))
            {
                selected.Clear();
                selected.Add(prop);
                SelectAll(selected);
            }

            return _move.Begin(selected, ray, hit, _context.Props);
        }

        private bool OnWheel(Event e)
        {
            var selection = _context.SelectedProps;
            if (selection.Count == 0)
            {
                return false; // 何も選んでいなければ、ページのスクロールに任せる
            }

            // Shift を押すと横スクロール扱いになる環境があるので、大きい方を使う。
            var amount = Mathf.Abs(e.delta.y) >= Mathf.Abs(e.delta.x) ? e.delta.y : e.delta.x;
            if (Mathf.Abs(amount) < 1e-3f)
            {
                return true;
            }

            var notch = amount < 0f ? 1f : -1f; // 上へ回すと「＋」
            if (e.shift)
            {
                StageSelectionAdjust.Scale(selection, Mathf.Pow(1.05f, notch));
            }
            else if (e.alt)
            {
                StageSelectionAdjust.Lift(selection, 0.05f * notch);
            }
            else
            {
                StageSelectionAdjust.Rotate(selection, notch * (StagePlaceSettings.SnapActive(e) ? StagePlaceSettings.AngleStep : 5f));
            }

            return true;
        }

        private bool OnCommand(string command, bool execute)
        {
            switch (command)
            {
                case "Duplicate":
                    if (execute)
                    {
                        EditorApplication.delayCall += () => StagePropDuplicateOps.Duplicate(_context.SelectedProps);
                    }

                    return true;
                case "Delete":
                case "SoftDelete":
                    if (execute)
                    {
                        EditorApplication.delayCall += () => StagePropDuplicateOps.Delete(_context.SelectedProps);
                    }

                    return true;
                default:
                    return false;
            }
        }

        private bool OnKey(Event e, int id)
        {
            if (GUIUtility.keyboardControl != id)
            {
                return false;
            }

            var selection = _context.SelectedProps;
            if (e.keyCode == KeyCode.Escape)
            {
                if (_move.IsActive || _tilt.IsActive)
                {
                    GUIUtility.hotControl = 0;
                    _move.Cancel();
                    _tilt.Cancel();
                }
                else
                {
                    Selection.objects = Array.Empty<UnityEngine.Object>();
                }

                return true;
            }

            if (selection.Count == 0)
            {
                return false;
            }

            if (e.control || e.command)
            {
                // Ctrl+矢印: 画面のその向きへ、てっぺんを倒す（Shift で 1°ずつ）。
                var degrees = e.shift ? 1f : StageTiltSession.SnapDegrees;
                switch (e.keyCode)
                {
                    case KeyCode.LeftArrow: StageSelectionAdjust.Lean(selection, ScreenDirection(Vector2.left), degrees); return true;
                    case KeyCode.RightArrow: StageSelectionAdjust.Lean(selection, ScreenDirection(Vector2.right), degrees); return true;
                    case KeyCode.UpArrow: StageSelectionAdjust.Lean(selection, ScreenDirection(Vector2.up), degrees); return true;
                    case KeyCode.DownArrow: StageSelectionAdjust.Lean(selection, ScreenDirection(Vector2.down), degrees); return true;
                }
            }

            var step = (StagePlaceSettings.Snap ? StagePlaceSettings.GridStep : 0.05f) * (e.shift ? 0.2f : 1f);
            switch (e.keyCode)
            {
                case KeyCode.LeftArrow: StageSelectionAdjust.Nudge(selection, ScreenDirection(Vector2.left) * step); return true;
                case KeyCode.RightArrow: StageSelectionAdjust.Nudge(selection, ScreenDirection(Vector2.right) * step); return true;
                case KeyCode.UpArrow: StageSelectionAdjust.Nudge(selection, ScreenDirection(Vector2.up) * step); return true;
                case KeyCode.DownArrow: StageSelectionAdjust.Nudge(selection, ScreenDirection(Vector2.down) * step); return true;
                case KeyCode.Q: StageSelectionAdjust.Rotate(selection, StagePlaceSettings.AngleStep); return true;
                case KeyCode.E: StageSelectionAdjust.Rotate(selection, -StagePlaceSettings.AngleStep); return true;
                case KeyCode.Minus:
                case KeyCode.KeypadMinus: StageSelectionAdjust.Scale(selection, 1f / 1.05f); return true;
                case KeyCode.Equals:
                case KeyCode.Plus:
                case KeyCode.KeypadPlus: StageSelectionAdjust.Scale(selection, 1.05f); return true;
                case KeyCode.PageUp: StageSelectionAdjust.Lift(selection, 0.05f); return true;
                case KeyCode.PageDown: StageSelectionAdjust.Lift(selection, -0.05f); return true;
                case KeyCode.F: StageStudioContext.Frame(_context.Primary); return true;
            }

            return false;
        }

        // 画面の上下左右が、床の上でどちらの向きか（視点ごとに画面が回っていても、矢印キーは画面どおりに動く）。
        private Vector2 ScreenDirection(Vector2 screen)
        {
            var t = _preview.Camera.transform;
            var right = new Vector2(t.right.x, t.right.y);
            var up = new Vector2(t.up.x, t.up.y);
            var dir = right.normalized * screen.x + (up.sqrMagnitude > 1e-6f ? up.normalized : Vector2.up) * screen.y;
            return dir.sqrMagnitude > 1e-6f ? dir.normalized : screen;
        }

        private Ray RayAt(Rect rect, Vector2 mouse)
        {
            var u = (mouse.x - rect.x) / Mathf.Max(1f, rect.width);
            var v = 1f - (mouse.y - rect.y) / Mathf.Max(1f, rect.height);
            return _preview.Camera.ViewportPointToRay(new Vector3(u, v, 0f));
        }

        private bool IsSelected(StageProp prop)
        {
            foreach (var p in _context.SelectedProps)
            {
                if (p == prop)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SelectAll(List<StageProp> props)
        {
            var objects = new List<UnityEngine.Object>();
            foreach (var p in props)
            {
                if (p != null)
                {
                    objects.Add(p.gameObject);
                }
            }

            Selection.objects = objects.ToArray();
        }
    }
}
