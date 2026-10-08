using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// シーンビューで背景を「つかんで床の上を動かす」操作。中身は <see cref="StageMoveSession"/>（配置ページのゲーム画面と共通）。
    /// ここはシーンビューのマウス操作を受け取り、吸着の線（水色）を描くだけ。Esc でつかむ前に戻る。
    /// </summary>
    public sealed class StageBodyDrag
    {
        private static readonly int Hash = "StageBodyDrag".GetHashCode();

        private readonly StageMoveSession _session = new StageMoveSession();
        private readonly List<StageProp> _grabbed = new List<StageProp>();

        public bool IsDragging => _session.IsActive;

        public void OnSceneGui(SceneView view, IReadOnlyList<StageProp> selection, System.Func<Vector2, bool> isOverHandle)
        {
            var e = Event.current;
            var id = GUIUtility.GetControlID(Hash, FocusType.Passive);
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && !e.alt && GUIUtility.hotControl == 0 && !isOverHandle(e.mousePosition) && TryBegin(e, selection))
                    {
                        GUIUtility.hotControl = id;
                        e.Use();
                    }

                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        _session.Update(HandleUtility.GUIPointToWorldRay(e.mousePosition), StagePlaceSettings.SnapActive(e));
                        e.Use();
                    }

                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id && e.button == 0)
                    {
                        GUIUtility.hotControl = 0;
                        _session.End();
                        e.Use();
                    }

                    break;
                case EventType.KeyDown:
                    if (GUIUtility.hotControl == id && e.keyCode == KeyCode.Escape)
                    {
                        GUIUtility.hotControl = 0;
                        _session.Cancel();
                        e.Use();
                    }

                    break;
                case EventType.Repaint:
                    if (GUIUtility.hotControl == id)
                    {
                        DrawGuides();
                    }

                    break;
            }
        }

        private bool TryBegin(Event e, IReadOnlyList<StageProp> selection)
        {
            var picked = HandleUtility.PickGameObject(e.mousePosition, false);
            var prop = picked != null ? picked.GetComponentInParent<StageProp>() : null;
            if (prop == null)
            {
                return false; // 何も無い所: Unity の範囲選択に任せる
            }

            _grabbed.Clear();
            var alreadySelected = false;
            foreach (var p in selection)
            {
                alreadySelected |= p == prop;
            }

            if (alreadySelected)
            {
                _grabbed.AddRange(selection);
            }
            else if (e.shift)
            {
                _grabbed.AddRange(selection);
                _grabbed.Add(prop);
                SelectAll(_grabbed);
            }
            else
            {
                _grabbed.Add(prop);
                Selection.activeGameObject = prop.gameObject;
            }

            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var grab = picked.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null && StagePropShape.TryIntersect(ray, filter, out var hit, out _)
                ? hit
                : prop.VisualBounds.center;
            var context = StageSceneOverlay.Context;
            return _session.Begin(_grabbed, ray, grab, context != null ? context.Props : null);
        }

        private void DrawGuides()
        {
            var snap = _session.Snap;
            if (!snap.SnappedX && !snap.SnappedY)
            {
                return;
            }

            var rect = _session.CurrentRect();
            Handles.color = new Color(0.35f, 0.85f, 1f, 0.95f);
            const float extend = 3f;
            if (snap.SnappedX)
            {
                Handles.DrawAAPolyLine(2f, new Vector3(snap.GuideX, rect.yMin - extend, 0f), new Vector3(snap.GuideX, rect.yMax + extend, 0f));
            }

            if (snap.SnappedY)
            {
                Handles.DrawAAPolyLine(2f, new Vector3(rect.xMin - extend, snap.GuideY, 0f), new Vector3(rect.xMax + extend, snap.GuideY, 0f));
            }
        }

        private static void SelectAll(List<StageProp> props)
        {
            var objects = new List<Object>();
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
