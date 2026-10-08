using System;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>モデル（FBX・Prefab）をドラッグ＆ドロップで受け取る場所。</summary>
    public sealed class ModelDropZone : VisualElement
    {
        private readonly Action<GameObject> _onDropped;
        private readonly Label _text;

        public ModelDropZone(Action<GameObject> onDropped)
        {
            _onDropped = onDropped;
            AddToClassList("sk-dropzone");
            tooltip = "プロジェクトウィンドウから、MayaのFBX（またはPrefab）をここへドラッグして離してください。";
            Add(StudioUi.Styled(new Label("⇩"), "sk-dropzone-icon"));
            _text = StudioUi.Styled(new Label("FBX をここにドラッグ＆ドロップ"), "sk-dropzone-text");
            Add(_text);

            RegisterCallback<DragEnterEvent>(_ => EnableInClassList("sk-dropzone--hover", Find() != null));
            RegisterCallback<DragLeaveEvent>(_ => RemoveFromClassList("sk-dropzone--hover"));
            RegisterCallback<DragUpdatedEvent>(_ =>
            {
                DragAndDrop.visualMode = Find() != null ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            });
            RegisterCallback<DragPerformEvent>(_ =>
            {
                RemoveFromClassList("sk-dropzone--hover");
                var model = Find();
                if (model != null)
                {
                    DragAndDrop.AcceptDrag();
                    _onDropped(model);
                }
            });
        }

        public void SetCaption(string caption) => _text.text = caption;

        private static GameObject Find()
        {
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is GameObject go && EditorUtility.IsPersistent(go))
                {
                    return go;
                }
            }

            return null;
        }
    }
}
