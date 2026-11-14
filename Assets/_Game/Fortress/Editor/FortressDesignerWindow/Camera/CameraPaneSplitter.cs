using System;
using UnityEngine.UIElements;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 上下2つの領域の境目(横長のつまみ)。ドラッグした量を知らせるだけで、高さの決め方・保存は使う側が行う。
    /// ダブルクリックは「既定の高さに戻す」に使う。
    /// </summary>
    public sealed class CameraPaneSplitter : VisualElement
    {
        private bool _dragging;
        private float _startY;

        public event Action DragStarted;

        /// <summary>ドラッグを始めた所からの縦の移動量(下向きが正)。</summary>
        public event Action<float> Dragging;

        public event Action DragFinished;
        public event Action DoubleClicked;

        public CameraPaneSplitter(string tooltipText)
        {
            AddToClassList("fd-pane-splitter");
            tooltip = tooltipText;

            var grip = new VisualElement();
            grip.AddToClassList("fd-pane-splitter__grip");
            grip.pickingMode = PickingMode.Ignore;
            Add(grip);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => Finish());
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            evt.StopPropagation();
            if (evt.clickCount >= 2)
            {
                DoubleClicked?.Invoke();
                return;
            }

            _dragging = true;
            _startY = evt.position.y;
            this.CapturePointer(evt.pointerId);
            AddToClassList("fd-pane-splitter--dragging");
            DragStarted?.Invoke();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_dragging && this.HasPointerCapture(evt.pointerId))
            {
                Dragging?.Invoke(evt.position.y - _startY);
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (this.HasPointerCapture(evt.pointerId))
            {
                this.ReleasePointer(evt.pointerId);
            }

            Finish();
        }

        private void Finish()
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            RemoveFromClassList("fd-pane-splitter--dragging");
            DragFinished?.Invoke();
        }
    }
}
